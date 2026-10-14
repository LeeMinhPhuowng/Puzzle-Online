using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using PuzzleOnline.Network;
using PuzzleOnline.UI;
using PuzzleOnline.Utility;

[InitializeOnLoad]
public static class GameplayHUDPlayValidation
{
    private const string Key = "PuzzleHUDValidationPending";
    static GameplayHUDPlayValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            var scene = SceneManager.CreateScene("SCN_Nature"); SceneManager.SetActiveScene(scene);
            UnityEngine.Object.Instantiate(Resources.Load<GameObject>("UI/GameplayHUD"));
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("QA Events", typeof(EventSystem), typeof(StandaloneInputModule));
            new GameObject("HUD Play Test").AddComponent<GameplayHUDPlayRunner>();
        };
    }

    public static void Run()
    {
        GameplayUIValidation.Run();
        PuzzleGameplayValidation.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var network = new GameObject("Offline test network").AddComponent<PuzzleNetworkManager>();
        network.enabled = false; // Awake initializes services; Start never opens a real socket.
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }
}

public sealed class GameplayHUDPlayRunner : MonoBehaviour
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<string> report = new List<string>();
    private PuzzleNetworkManager network;
    private GameplayHUD hud;
    private PuzzleOnlineUIManager ui;
    private T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Private).GetValue(obj);
    private void Check(bool ok, string text)
    {
        if (!ok) throw new InvalidOperationException(text);
        report.Add("PASS: " + text); Debug.Log(report[report.Count - 1]);
    }
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        yield return null;
        network = PuzzleNetworkManager.Instance; hud = GameplayHUD.Instance; ui = PuzzleOnlineUIManager.Instance;
        try
        {
            network.AuthService.HandleMessage(new NetMessage("AUTH_OK", new Dictionary<string,string> { ["username"] = "Alice" }));
            network.RoomService.CurrentRoom = new RoomInfo { Id="TEST",Name="HUD test",GameMode="CLASSIC",MaxPlayers=4 };
            network.RoomPlayers.Add(new PlayerInfo { Username="Alice",Team="SABOTEUR",Connected=true,Score=5 });
            Snapshot("one");
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return null;
        try
        {
            Check(!Field<TMP_Text>(hud,"timer").gameObject.activeSelf, "CLASSIC hides the timer");
            Check(!Field<GameObject>(hud,"rolePanel").activeSelf, "CLASSIC hides the team announcement");
            ui.AddChatMessage("Alice: 😊👍🎉🔥❤️👏");
            hud.SetChatOpen(true);
            Check(!Field<UnityEngine.UI.Button>(hud,"chatToggle").gameObject.activeSelf, "open chat hides its launch button so Send stays clickable");
            Field<TMP_InputField>(hud,"chatInput").text="draft";
            Snapshot("one");
            Check(ui.ChatHistory.Count==1 && Field<TMP_InputField>(hud,"chatInput").text=="draft", "same-round snapshots preserve chat and draft");
            hud.SetChatOpen(false); hud.SetChatOpen(true);
            Check(ui.ChatHistory.Count==1,"closing/reopening chat preserves history");
            Check(PuzzleOnlineUIManager.IsModalOrChatOpen && InteractionTargetQuery.IsBlocked,"open chat blocks gameplay targeting");
            network.RoomService.CurrentRoom.GameMode="COMPETITIVE";
            Snapshot("two");
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return null;
        try
        {
            Check(ui.ChatHistory.Count==0 && Field<TMP_InputField>(hud,"chatInput").text=="", "new round clears history and input");
            Check(!hud.IsChatOpen, "new round closes the previous chat input");
            Check(Field<TMP_Text>(hud,"timer").gameObject.activeSelf,"COMPETITIVE displays its countdown");
            Check(Field<GameObject>(hud,"rolePanel").activeSelf,"COMPETITIVE shows a temporary team announcement");
            typeof(GameplayHUD).GetField("roleExpires",Private).SetValue(hud,Time.unscaledTime-1);
            hud.SetChatOpen(true);
            var input=Field<TMP_InputField>(hud,"chatInput");
            EventSystem.current.SetSelectedGameObject(input.gameObject); input.ActivateInputField();
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return null;
        try
        {
            Check(!Field<GameObject>(hud,"rolePanel").activeSelf,"team announcement disappears after its display interval");
            Check(PuzzleOnlineUIManager.IsTextInputFocused,"TMP chat focus is detected by gameplay input guards");
            FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive=true;
            hud.SetChatOpen(false);
            Check(Cursor.lockState==CursorLockMode.None && Cursor.visible,"closing chat preserves the overhead cursor");
            FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive=false;
            hud.SetChatOpen(true); hud.SetChatOpen(false);
            Check(!Cursor.visible,"closing chat returns the FPS cursor to gameplay");
            Field<UnityEngine.UI.Button>(hud,"menuButton").onClick.Invoke();
            Check(ui.IsPauseOrDialogOpen && Cursor.visible,"Canvas menu button opens the pause menu and cursor");
            ui.TogglePauseMenu();
            ui.AddChatMessage("old game"); Snapshot("three");
            Check(ui.ChatHistory.Count==0,"restart clears chat once more");
            network.RoomService.CurrentRoom = null;
            network.GameplaySyncService.ResetGameState();
            SceneManager.LoadScene("SCN_Room");
        }
        catch(Exception error) { Fail(error); yield break; }
        yield return null;
        yield return null;
        try { TestLobby(); Finish(); }
        catch(Exception error) { Fail(error); }
    }

    private void TestLobby()
    {
        var rooms = FindFirstObjectByType<LobbyRoomsPanel>();
        var input = Field<TMP_InputField>(rooms,"roomQuickJoinInput");
        var join = Field<UnityEngine.UI.Button>(rooms,"quickJoinButton");
        Canvas.ForceUpdateCanvases();
        var position = RectTransformUtility.WorldToScreenPoint(null,input.transform.TransformPoint(input.GetComponent<RectTransform>().rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position=position };
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<TMP_InputField>()==input,"lobby code field receives pointer input in Play Mode");
        // Exercise real send serialization without opening a socket in this UI fixture.
        typeof(RealtimeClient).GetField("<IsConnecting>k__BackingField",Private).SetValue(network.Client,true);
        var pending=Field<Queue<string>>(network.Client,"_pendingSend");
        int before=pending.Count; input.text="   ";join.onClick.Invoke();
        Check(pending.Count==before,"blank room code cannot join an arbitrary room");
        input.text=" abc123 ";join.onClick.Invoke();
        Check(WireProtocol.Decode(pending.ToArray()[pending.Count-1]).Get("roomId")=="abc123","join button sends the trimmed room code");
        var dialogs=FindFirstObjectByType<RoomJoinDialogs>();
        network.RoomService.HandleMessage(new NetMessage("ERROR",new Dictionary<string,string>{{"code","PASSWORD_REQUIRED"},{"roomId","ABC123"},{"roomName","Private"}}),network.Friends);
        Check(Field<GameObject>(dialogs,"passwordPanel").activeSelf,"password-required response opens the Canvas dialog");
        Field<TMP_InputField>(dialogs,"passwordInput").text="secret";
        Field<UnityEngine.UI.Button>(dialogs,"passwordSubmit").onClick.Invoke();
        Check(WireProtocol.Decode(pending.ToArray()[pending.Count-1]).Get("password")=="secret","password dialog sends the supplied password");
        Field<UnityEngine.UI.Button>(dialogs,"passwordCancel").onClick.Invoke();
        network.SocialService.HandleMessage(new NetMessage("INVITE",new Dictionary<string,string>{{"from","Bob"},{"roomId","ABC123"},{"roomName","Invite test"}}));
        Check(Field<GameObject>(dialogs,"invitePanel").activeSelf,"received invite opens its Canvas dialog");
        Field<UnityEngine.UI.Button>(dialogs,"inviteAccept").onClick.Invoke();
        var accepted=WireProtocol.Decode(pending.ToArray()[pending.Count-1]);
        Check(accepted.Type=="INVITE_RESPONSE" && accepted.GetBool("accept"),"invite accept button sends its decision");
        network.RoomService.HandleMessage(new NetMessage("ROOM_SNAPSHOT",new Dictionary<string,string>{{"roomId","ABC123"},{"name","Invite test"},{"max","4"},{"count","1"},{"state","WAITING"},{"host","Alice"}}),network.Friends);
        var lobby=FindFirstObjectByType<LobbyUIController>();
        var invite=Field<UnityEngine.UI.Button>(lobby,"inviteButton");
        var friends=Field<LobbyFriendsPanel>(lobby,"friendsPanel");
        Check(invite.gameObject.activeSelf && !friends.gameObject.activeSelf,"waiting room shows InviteBtn and previews initially");
        invite.onClick.Invoke();
        Check(friends.gameObject.activeSelf,"InviteBtn enables the friends list");
        var parent=friends.transform.parent;
        Check(friends.transform.GetSiblingIndex()>parent.Find("Map Preview").GetSiblingIndex() && friends.transform.GetSiblingIndex()>parent.Find("Puzzle Preview").GetSiblingIndex(),"friends overlay renders above both previews");
        invite.onClick.Invoke();
        Check(!friends.gameObject.activeSelf,"InviteBtn closes the overlay again");
        network.RoomService.HandleMessage(new NetMessage("ROOM_LEFT"),network.Friends);
        Check(friends.gameObject.activeSelf && !invite.gameObject.activeSelf,"leaving room restores lobby friends and hides InviteBtn");
        Check(Cursor.visible && Cursor.lockState==CursorLockMode.None,"leaving room preserves a visible unlocked lobby cursor");
    }

    private void Finish()
    {
        File.WriteAllLines(Path.Combine(Path.GetTempPath(),"puzzle-hud-qa","play-report.txt"),report);
        EditorApplication.Exit(0);
    }

    private void Snapshot(string id) => network.GameplaySyncService.HandleMessage(new NetMessage("GAME_SNAPSHOT",
        new Dictionary<string,string> { ["instanceId"]=id,["puzzleId"]="sunset_3x3",["themeId"]="SCN_Nature",["total"]="9",["placed"]="1",["remaining"]="180" }),"Alice",null,null);
    private void Fail(Exception error)
    {
        Debug.LogException(error); report.Add("FAIL: "+error);
        File.WriteAllLines(Path.Combine(Path.GetTempPath(),"puzzle-hud-qa","play-report.txt"),report);
        EditorApplication.Exit(1);
    }
}
