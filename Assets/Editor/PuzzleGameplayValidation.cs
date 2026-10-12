using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using PuzzleOnline.Gameplay.Competitive;
using PuzzleOnline.Network;
using PuzzleOnline.Utility;

public static class PuzzleGameplayValidation
{
    private static readonly List<string> Report = new List<string>();
    private static readonly string Output = Path.Combine(Path.GetTempPath(), "puzzle-gameplay-qa");
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Puzzle Online/Validate Gameplay Changes")]
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        try
        {
            DumpGroundCandidates();
            TestInventory();
            TestGround();
            TestTargeting();
            TestVictimEffects();
            TestPrefabs();
            TestLobby();
            TestOutline();
            TestLayouts();
            Report.Add("PASS: all gameplay regression checks completed");
            File.WriteAllLines(Path.Combine(Output, "report.txt"), Report);
            Debug.Log(string.Join("\n", Report));
        }
        catch (Exception error)
        {
            Report.Add("FAIL: " + error);
            File.WriteAllLines(Path.Combine(Output, "report.txt"), Report);
            throw;
        }
    }

    public static void SetupAndRun()
    {
        PuzzleGameplaySetup.Apply();
        Run();
    }

    public static void FullValidation()
    {
        SetupAndRun();
        PuzzleVisualValidation.Run();
    }

    private static void Check(bool passed, string message)
    {
        if (!passed) throw new InvalidOperationException(message);
        Report.Add("PASS: " + message);
    }

    private static NetMessage Message(string type, params string[] fields)
    {
        var data = new Dictionary<string, string>();
        for (int i = 0; i < fields.Length; i += 2) data[fields[i]] = fields[i + 1];
        return new NetMessage(type, data);
    }

    private static void TestInventory()
    {
        var sync = new NetworkGameplaySyncService(null);
        Action<NetMessage> receive = m => sync.HandleMessage(m, "alice", null, null);
        int starts = 0;
        sync.OnGameStarted += () => starts++;
        receive(Message("GAME_SNAPSHOT", "instanceId", "one", "heldItems", "alice\u001fFLASHLIGHT"));
        Check(sync.GetHeldItem("ALICE") == "FLASHLIGHT", "snapshot restores inventory case-insensitively");
        receive(Message("ITEM_USED", "by", "bob"));
        Check(sync.GetHeldItem("alice") == "FLASHLIGHT", "other player's use cannot consume local inventory");
        receive(Message("ITEM_USED", "by", "alice"));
        Check(sync.GetHeldItem("alice") == "", "confirmed use clears the owner's item");
        receive(Message("ITEM_PICKED_UP", "itemId", "gift", "by", "alice", "type", "MAGNET"));
        Check(sync.GetHeldItem("alice") == "MAGNET", "confirmed pickup grants the actual random item");
        receive(Message("GAME_SNAPSHOT", "instanceId", "two", "heldItems", ""));
        Check(starts == 2 && sync.GetHeldItem("alice") == "", "restart resets inventory and emits a new-game event");
        receive(Message("GAME_OVER", "result", "BUILDERS_WIN", "winnerTeam", "BUILDER"));
        Check(sync.HasRoundEnded && sync.GetHeldItem("alice") == "", "game over clears item models and targeting state");
    }

    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Private).SetValue(instance, value);
    private static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, Private).Invoke(instance, args);

    private static void TestGround()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.transform.localScale = new Vector3(10, 0.2f, 10);
        floor.layer = LayerMask.NameToLayer("Ground");
        var player = new GameObject("Ground test");
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2;
        controller.radius = 0.4f;
        controller.center = Vector3.up;
        var movement = player.AddComponent<PlayerController.PlayerController>();
        Set(movement, "controller", controller);
        Set(movement, "groundMask", 1 << floor.layer);
        Physics.SyncTransforms();
        Call(movement, "HandleGroundCheck");
        Check(movement.IsGrounded(), "Ground collider supports player");
        floor.layer = 0;
        Physics.SyncTransforms();
        Call(movement, "HandleGroundCheck");
        Check(!movement.IsGrounded(), "Default collider is not accepted as Ground");
        floor.layer = LayerMask.NameToLayer("Ground");
        floor.GetComponent<BoxCollider>().isTrigger = true;
        Call(movement, "HandleGroundCheck");
        Check(!movement.IsGrounded(), "Ground triggers do not support player");
        floor.GetComponent<BoxCollider>().isTrigger = false;
        Set(movement, "velocity", new Vector3(0, 2, 0));
        Call(movement, "HandleGroundCheck");
        Check(!movement.IsGrounded(), "upward jump cannot become grounded again");
        Set(movement, "velocity", Vector3.zero);
        player.transform.localScale = new Vector3(1.2f, 1.5f, 0.8f);
        Physics.SyncTransforms();
        Call(movement, "HandleGroundCheck");
        Check(movement.IsGrounded(), "ground probe respects non-uniform player scale and controller center");
        floor.transform.position = new Vector3(0.6f, 1, 0);
        floor.transform.localScale = new Vector3(0.1f, 3, 10);
        Physics.SyncTransforms();
        Call(movement, "HandleGroundCheck");
        Check(!movement.IsGrounded(), "vertical wall is not a supporting floor");

        // Test the actual resting contact gap created by Move, rather than placing feet exactly at y=0.
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.transform.localScale = new Vector3(10, 0.2f, 10);
        controller.center = Vector3.zero;
        controller.radius = 0.5f;
        controller.skinWidth = 0.08f;
        foreach (Vector3 scale in new[] { Vector3.one * 0.5f, new Vector3(1.2f, 1.5f, 0.8f) })
        {
            controller.enabled = false;
            player.transform.localScale = scale;
            player.transform.position = Vector3.up * 3;
            Set(movement, "velocity", Vector3.zero);
            controller.enabled = true;
            Physics.SyncTransforms();
            for (int frame = 0; frame < 240; frame++) StepMovement(movement, controller, false);
            Call(movement, "HandleGroundCheck");
            Check(movement.IsGrounded(), "Move landing recognizes the skin contact gap at scale " + scale);

            float startY = player.transform.position.y;
            StepMovement(movement, controller, true);
            Check(movement.GetVelocity().y > 0, "jump input starts ascent at scale " + scale);
            float peakY = player.transform.position.y;
            bool airJumpRejected = false;
            for (int frame = 0; frame < 300; frame++)
            {
                float before = movement.GetVelocity().y;
                StepMovement(movement, controller, frame == 3);
                if (frame == 3) airJumpRejected = movement.GetVelocity().y < before;
                peakY = Mathf.Max(peakY, player.transform.position.y);
            }
            Call(movement, "HandleGroundCheck");
            Check(peakY - startY > 1.5f && airJumpRejected && movement.IsGrounded(),
                "full jump lands without an air jump at scale " + scale);
            StepMovement(movement, controller, true);
            Check(movement.GetVelocity().y > 0, "player can jump again after landing at scale " + scale);
        }
    }

    private static void StepMovement(PlayerController.PlayerController movement, CharacterController controller, bool jump)
    {
        const float delta = 1f / 60;
        Call(movement, "HandleGroundCheck");
        Call(movement, "HandleGravityAndJump", jump, delta);
        controller.Move(movement.GetVelocity() * delta);
        Physics.SyncTransforms();
    }

    private static void TestPrefabs()
    {
        foreach (string path in new[] { "Assets/Player.prefab", "Assets/Resources/Player.prefab" })
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visuals = prefab.GetComponent<PlayerHeldItemVisuals>();
                Check(visuals != null, path + " has item visual references");
                var serialized = new SerializedObject(visuals);
                string[] fields = { "flashlight", "ink", "glove", "magnet", "crowbar" };
                string[] types = { "FLASHLIGHT", "INK", "SLAP", "MAGNET", "SHOVEL" };
                var items = fields.Select(f => (GameObject)serialized.FindProperty(f).objectReferenceValue).ToArray();
                Check(items.All(i => i != null && !i.activeSelf), "all five authored models are assigned and initially inactive");
                for (int i = 0; i < types.Length; i++)
                {
                    visuals.SetItem(types[i]);
                    Check(items.Where((item, j) => item.activeSelf != (i == j)).Count() == 0, types[i] + " enables exactly its matching child");
                }
                visuals.SetItem("");
                Check(items.All(i => !i.activeSelf), "reset hides all item models");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }

    private static void TestLobby()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lobby = new GameObject("Lobby test").AddComponent<PuzzleOnline.UI.LobbyRoom3DView>();
        var stand = new GameObject("Stand").transform;
        stand.position = Vector3.up * 0.2f;
        GameObject avatar = (GameObject)Call(lobby, "SpawnCleanAvatar", stand, 1);
        var body = avatar.GetComponentsInChildren<Renderer>().Where(r => r.GetComponent<TextMesh>() == null).ToArray();
        Check(body.Min(r => r.bounds.min.y) >= stand.position.y + 0.02f, "lobby avatar feet stay above the puzzle surface");
        Type slotType = typeof(PuzzleOnline.UI.LobbyRoom3DView).GetNestedType("PieceSlot", BindingFlags.NonPublic);
        object slot = Activator.CreateInstance(slotType, true);
        slotType.GetField("index").SetValue(slot, 1);
        slotType.GetField("currentAvatar").SetValue(slot, avatar);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        slotType.GetField("pieceMaterial").SetValue(slot, material);
        var player = new PlayerInfo { Username = "alice", Ready = false, IsHost = true };
        Call(lobby, "SetupPlayerSlot", slot, player);
        Check(material.color == Color.black, "unready host puzzle is black without an exception");
        player.Ready = true;
        Call(lobby, "SetupPlayerSlot", slot, player);
        Check(material.color == body.First(r => !r.name.StartsWith("Eye")).sharedMaterial.color, "ready puzzle matches that player's avatar colour");
        UnityEngine.Object.DestroyImmediate(material);
    }

    private static void TestOutline()
    {
        Shader outline = Resources.Load<Shader>("Rendering/TargetOutline");
        Shader mask = Resources.Load<Shader>("Rendering/TargetOutlineMask");
        Check(outline != null && mask != null && !ShaderUtil.ShaderHasError(outline) && !ShaderUtil.ShaderHasError(mask), "URP outline shaders import without errors");
    }

    private static void TestTargeting()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Camera").AddComponent<Camera>();
        var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        target.transform.position = Vector3.forward * 5;
        target.GetComponent<Collider>().isTrigger = true;
        target.AddComponent<PuzzleSystem.Gameplay.RemotePlayerAvatar>().username = "bob";
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.transform.position = Vector3.forward * 8;
        Physics.SyncTransforms();
        Ray ray = new Ray(Vector3.zero, Vector3.forward);
        Check(InteractionTargetQuery.TryHit(camera, ray, 25, out var hit) && hit.collider.gameObject == target,
            "remote trigger collider can be targeted");
        Check(InteractionTargetQuery.TryHit(camera, ray, 25, out hit, includePlayers: false) && hit.collider.gameObject == piece,
            "player targeting collider does not block ordinary puzzle queries");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = Vector3.forward * 3;
        Physics.SyncTransforms();
        Check(InteractionTargetQuery.TryHit(camera, ray, 25, out hit) && hit.collider.gameObject == wall,
            "solid obstacle blocks targeting through walls");
        Check(!InteractionTargetQuery.TryHit(camera, ray, 1, out hit), "targets outside range are ignored");
    }

    private static void TestVictimEffects()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var net = new GameObject("Victim network state").AddComponent<PuzzleNetworkManager>();
        typeof(PuzzleNetworkManager).GetProperty("Instance").SetValue(null, net);
        var auth = new NetworkAuthService(null);
        typeof(NetworkAuthService).GetProperty("Username").SetValue(auth, "alice");
        typeof(PuzzleNetworkManager).GetProperty("AuthService").SetValue(net, auth);
        var interactor = new GameObject("Victim interactor").AddComponent<PuzzleSystem.Gameplay.PuzzleRaycastInteractor>();
        new GameObject("Test audio without a catalog").AddComponent<PuzzleOnline.Audio.AudioManager>();
        var effects = net.gameObject.AddComponent<SabotageEffectsManager>();
        Call(effects, "HandleSabotageTriggered", "FLASHLIGHT", "bob", "saboteur", 3.5f);
        Check((float)effects.GetType().GetField("flashAlpha", Private).GetValue(effects) == 0, "flashlight effect ignores a different victim");
        Call(effects, "HandleSabotageTriggered", "FLASHLIGHT", "alice", "saboteur", 3.5f);
        Check((float)effects.GetType().GetField("flashAlpha", Private).GetValue(effects) == 1, "flashlight activates on the targeted client");
        Call(effects, "HandleSabotageTriggered", "SLAP", "alice", "saboteur", 3.5f);
        Check(interactor.IsStunned, "slap stuns the targeted client's puzzle interactor");
        Call(effects, "HandleSabotageTriggered", "MAGNET", "alice", "saboteur", 3.5f);
        Check((float)interactor.GetType().GetField("magnetDebuffEndTime", Private).GetValue(interactor) > Time.time,
            "magnet applies its debuff on the targeted client");
        UnityEngine.Object.DestroyImmediate(net.gameObject);
    }

    private static void TestLayouts()
    {
        foreach (string scene in new[] { "SCN_Menu", "SCN_Lobby", "SCN_Room" })
        {
            var loaded = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
            Canvas[] canvases = loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas).ToArray();
            foreach (Canvas canvas in canvases)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                Check(scaler != null && scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, scene + " keeps full design area at laptop aspect ratios");
            }
            foreach (var lobby in loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PuzzleOnline.UI.LobbyUIController>(true)))
            {
                var data = new SerializedObject(lobby);
                var friends = data.FindProperty("friendsPanel").objectReferenceValue as PuzzleOnline.UI.LobbyFriendsPanel;
                var toggle = data.FindProperty("navFriendsButton").objectReferenceValue as Button;
                Check(toggle == null || friends == null || !toggle.transform.IsChildOf(friends.transform),
                    scene + " does not reuse the add-friend button as a panel toggle");
            }
            int missing = loaded.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            Check(missing == 0, scene + " has no missing script references");
        }
    }

    private static void DumpGroundCandidates()
    {
        var lines = new List<string>();
        foreach (string scene in new[] { "SCN_Nature", "SCN_Classroom", "SCN_Sakura", "Test" })
        {
            var loaded = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
            if (scene != "Test") Check(loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Any(c => c.gameObject.layer == LayerMask.NameToLayer("Ground") && !c.isTrigger), scene + " has a physical Ground floor assigned");
            foreach (var collider in loaded.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
            {
                Bounds b = collider.bounds;
                if (b.size.x > 4 && b.size.z > 4)
                    lines.Add(scene + " | " + AnimationUtility.CalculateTransformPath(collider.transform, null) + " | layer=" + collider.gameObject.layer + " | " + b);
            }
        }
        File.WriteAllLines(Path.Combine(Output, "floor-candidates.txt"), lines);
    }
}
