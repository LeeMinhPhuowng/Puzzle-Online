using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PuzzleOnline.UI;
using PuzzleOnline.Network;

public static class GameplayUIValidation
{
    private static readonly string Output = Path.Combine(Path.GetTempPath(), "puzzle-hud-qa");
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException(message);
        Debug.Log("PASS: " + message);
    }

    public static void ApplyAndRun() { GameplayUISetup.Apply(); Run(); }

    public static void Run()
    {
        Directory.CreateDirectory(Output);
        TestRoomItem();
        TestLobbyInputs();
        RenderHUD();
        ValidateGameplayScenes();
        Check(typeof(UIInGameView).GetMethod("DrawHUD") == null &&
            typeof(PuzzleOnline.Gameplay.Competitive.SaboteurInventoryHUD).GetMethod("OnGUI", Private) == null &&
            typeof(PuzzleOnline.Gameplay.Competitive.SaboteurTargetingSystem).GetMethod("OnGUI", Private) == null,
            "old gameplay HUD, inventory and targeting IMGUI are removed");
    }

    private static void ValidateGameplayScenes()
    {
        foreach (string name in new[] { "SCN_Nature", "SCN_Classroom", "SCN_Sakura", "Test" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            Check(UnityEngine.Object.FindObjectsByType<GameplayHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                name + " contains exactly one serialized Canvas HUD");
            Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                name + " contains exactly one EventSystem");
            int missing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
            Check(missing == 0, name + " has no missing scripts");
        }
    }

    private static void TestRoomItem()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Resources/RoomItem.prefab");
        try
        {
            var card = root.GetComponent<UIRoomItem>();
            card.Bind(new RoomInfo { Id = "ABC123", Name = "Test", Host = "Alice", Count = 2, MaxPlayers = 4, State = "WAITING" }, _ => { });
            var data = new SerializedObject(card);
            Check(((TMP_Text)data.FindProperty("roomIdText").objectReferenceValue).text == "Mã: ABC123", "room card displays the real ID");
            Check(((TMP_Text)data.FindProperty("statusText").objectReferenceValue).text == "Đang chờ 2/4", "status contains actual occupancy");
            Check(root.transform.Find("Occupancy") == null, "separate occupancy object is removed");
            var room = new RoomInfo { Count = 2, MaxPlayers = 4, State = "PLAYING", GameMode = "COMPETITIVE" };
            card.Bind(room, _ => { });
            Check(!((Button)data.FindProperty("joinButton").objectReferenceValue).interactable, "active competitive room cannot be joined from its card");
            room.GameMode = "CLASSIC"; card.Bind(room, _ => { });
            Check(((Button)data.FindProperty("joinButton").objectReferenceValue).interactable, "active classic room remains joinable");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void TestLobbyInputs()
    {
        foreach (string name in new[] { "SCN_Room", "SCN_Lobby" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var lobby = UnityEngine.Object.FindFirstObjectByType<LobbyUIController>();
            typeof(LobbyUIController).GetMethod("InitViews", Private).Invoke(lobby, null);
            lobby.SyncRoomDisplay();
            var panel = UnityEngine.Object.FindFirstObjectByType<LobbyRoomsPanel>();
            var data = new SerializedObject(panel);
            var input = data.FindProperty("roomQuickJoinInput").objectReferenceValue as TMP_InputField;
            Check(input != null && input.interactable && !input.readOnly && input.textComponent != null && input.textViewport != null,
                name + " quick join input has editable, complete TMP references");
            var canvas = input.GetComponentInParent<Canvas>();
            var camera = WorldCanvas(canvas, 1920, 1080);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            typeof(BaseRaycaster).GetMethod("OnEnable", Private).Invoke(canvas.GetComponent<GraphicRaycaster>(), null);
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>() ?? new GameObject("QA events").AddComponent<EventSystem>();
            var pointer = new PointerEventData(eventSystem) { position = camera.WorldToScreenPoint(input.transform.TransformPoint(input.GetComponent<RectTransform>().rect.center)) };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Debug.Log(name + " pointer=" + pointer.position + " input rect=" + input.GetComponent<RectTransform>().rect +
                " raycasts=" + string.Join(",", hits.Select(h => h.gameObject.name)));
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<TMP_InputField>() == input,
                name + " quick join is the topmost raycast target at its center");
            var lobbyData = new SerializedObject(lobby);
            Check(lobbyData.FindProperty("inviteButton").objectReferenceValue != null, name + " InviteBtn is wired");
            Check(UnityEngine.Object.FindFirstObjectByType<RoomJoinDialogs>() != null, name + " password/invite Canvas dialog is wired");
        }
    }

    private static void RenderHUD()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/UI/GameplayHUD.prefab"));
        var hud = root.GetComponent<GameplayHUD>();
        var data = new SerializedObject(hud);
        var iterator = data.GetIterator();
        while (iterator.NextVisible(true))
            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                Check(iterator.objectReferenceValue != null, "HUD reference " + iterator.propertyPath);
        var history = data.FindProperty("chatMessages").objectReferenceValue as TMP_Text;
        history.text = "Alice: Xin chào 😊👍🎉🔥❤️👏\nBob: <sprite=999> là nội dung chat";
        var chat = data.FindProperty("chatPanel").objectReferenceValue as GameObject; chat.SetActive(true);
        (data.FindProperty("chatToggle").objectReferenceValue as Button).gameObject.SetActive(false);
        history.ForceMeshUpdate(true);
        uint[] emojis = { 0x1F60A, 0x1F44D, 0x1F389, 0x1F525, 0x2764, 0x1F44F };
        foreach (uint emoji in emojis)
            Check(history.textInfo.characterInfo.Take(history.textInfo.characterCount).Any(c => c.textElement != null && c.textElement.unicode == emoji),
                "TMP renders emoji U+" + emoji.ToString("X"));
        Check(!history.richText, "chat treats rich text tags as literal user text");
        var canvas = root.GetComponent<Canvas>();
        foreach (var size in new[] { new Vector2Int(1366,768), new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1920,1200) })
        {
            var camera = WorldCanvas(canvas, size.x, size.y);
            Canvas.ForceUpdateCanvases();
            var bounds = ((RectTransform)canvas.transform).rect;
            foreach (Button button in canvas.GetComponentsInChildren<Button>())
            {
                Vector3[] corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Check(corners.All(c => bounds.Contains((Vector2)canvas.transform.InverseTransformPoint(c))),
                    size + " keeps button " + button.name + " on screen");
            }
            Save(camera, size, "HUD-" + size.x + "x" + size.y + ".png");
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }

    private static Camera WorldCanvas(Canvas canvas, int width, int height)
    {
        float scale = Mathf.Min(width / 1920f, height / 1080f);
        canvas.GetComponent<CanvasScaler>().enabled = false;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); canvas.transform.localScale = Vector3.one;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(width / scale, height / scale);
        var camera = new GameObject("Canvas QA camera").AddComponent<Camera>();
        camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographicSize = height / scale * .5f; camera.aspect = (float)width / height;
        camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .09f, .12f);
        canvas.worldCamera = camera;
        return camera;
    }

    private static void Save(Camera camera, Vector2Int size, string name)
    {
        var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
        var pixels = image.GetPixels32(); var background = pixels[0];
        Check(pixels.Count(p => p.r != background.r || p.g != background.g || p.b != background.b) > 10000,
            name + " contains visible HUD graphics, not just the camera background");
        File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
    }
}
