using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleSystem.Gameplay;

namespace PuzzleOnline.Network
{
    [DefaultExecutionOrder(-50)]
    public sealed class PuzzleNetworkManager : MonoBehaviour
    {
        public static PuzzleNetworkManager Instance { get; private set; }

        public RealtimeClient Client => _client;
        public VoiceChatController Voice => _voice;

        [Header("Connection")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;

        public string Username { get; private set; } = string.Empty;
        public int Coins { get; private set; }
        public string SessionToken { get; private set; } = string.Empty;

        public RoomInfo CurrentRoom { get; private set; }
        public bool IsHost => CurrentRoom != null && CurrentRoom.Host == Username;
        public List<PlayerInfo> RoomPlayers { get; } = new List<PlayerInfo>();
        public List<RoomInfo> LobbyRooms { get; } = new List<RoomInfo>();
        public List<FriendInfo> Friends { get; } = new List<FriendInfo>();
        public List<FriendRequestItem> PendingFriendRequests { get; } = new List<FriendRequestItem>();
        public List<PuzzleDefinition> ShopPuzzles { get; } = new List<PuzzleDefinition>();
        public List<ThemeDefinition> ShopThemes { get; } = new List<ThemeDefinition>();

        // In-game state
        public string CurrentPuzzleId { get; private set; } = "sunset_3x3";
        public string CurrentThemeId { get; private set; } = "SCN_Nature";
        public int RemainingSeconds { get; private set; }
        public int PlacedPieces { get; private set; }
        public int TotalPieces { get; private set; }
        public bool IsGameActive { get; private set; }
        public Dictionary<int, PieceInfo> ActivePieces { get; } = new Dictionary<int, PieceInfo>();

        // Profile Stats
        public int TotalScore { get; private set; }
        public HashSet<string> OwnedPuzzles { get; } = new HashSet<string>();
        public HashSet<string> OwnedThemes { get; } = new HashSet<string>();
        public int UniquePuzzlesCompleted { get; private set; }

        // Events
        public event Action<string> OnError;
        public event Action<string> OnPlayerEvent;
        public event Action OnAuthSuccess;
        public event Action OnLobbyUpdated;
        public event Action OnFriendRequestsUpdated;
        public event Action<string, long> OnFriendRequestReceived;
        public event Action OnRoomUpdated;
        public event Action OnRoomLeft;
        public event Action OnGameStarted;
        public event Action OnGameUpdated;
        public event Action<int, string> OnPieceLockedEvent;
        public event Action<int, float, float, int> OnPieceMovedEvent;
        public event Action<int, float, float, int, string> OnPiecePlacedEvent;
        public event Action<int, float, float, int> OnPieceReleasedEvent;
        public event Action<string, int> OnGameCompleteEvent;
        public event Action<string, string> OnChatEvent;
        public event Action<string, string> OnEmojiEvent;
        public event Action<string, string, string> OnInviteReceived;
        public event Action<string, float, float> OnPlayerLookReceived;
        public event Action<ReconnectPromptInfo> OnReconnectPromptReceived;
        public event Action<string, string> OnPasswordRequired;
        public event Action OnGameTimeout;
        public event Action OnGameContinued;
        public bool IsTimedOut { get; set; } = false;
        public ReconnectPromptInfo PendingReconnect { get; set; }

        private RealtimeClient _client;
        private VoiceChatController _voice;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _client = gameObject.AddComponent<RealtimeClient>();
            _voice = gameObject.AddComponent<VoiceChatController>();
            _voice.Initialize(_client);

            _client.MessageReceived += HandleMessage;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        /// <summary>
        /// Every playable environment needs a board.  SCN_Nature used to work only
        /// because a PuzzleBoard prefab had been placed in that scene by hand;
        /// the other environments did not have one.  Create the common runtime
        /// board after loading a game scene so spawning and puzzle loading follow
        /// the same path for every theme.
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsGameActive || scene.name != CurrentThemeId) return;

            if (FindFirstObjectByType<PuzzleBoard>() == null)
            {
                var boardObject = new GameObject("[PuzzleBoard]");
                boardObject.AddComponent<PuzzleBoard>();
            }

            StartCoroutine(PlaceLocalPlayerAfterSceneLoad(scene.name));
        }

        private IEnumerator PlaceLocalPlayerAfterSceneLoad(string sceneName)
        {
            // Let all scene-prefab Awake/Start methods finish first, including
            // a Player manually placed in the environment scene.
            yield return null;

            if (!IsGameActive || CurrentThemeId != sceneName ||
                SceneManager.GetActiveScene().name != sceneName)
            {
                yield break;
            }

            var board = FindFirstObjectByType<PuzzleBoard>();
            if (board != null)
            {
                board.PositionLocalPlayer(true);
                board.SyncRemotePlayerAvatars();
            }
        }

        private void Start()
        {
            ConnectToServer();
        }

        public void ConnectToServer(string host = null, int? port = null)
        {
            if (host != null) serverHost = host;
            if (port != null) serverPort = port.Value;
            _client.Connect(serverHost, serverPort);
        }

        public void Register(string user, string pass)
        {
            _client.Send("REGISTER", ("username", user), ("password", pass));
        }

        public void Login(string user, string pass)
        {
            _client.Send("LOGIN", ("username", user), ("password", pass));
        }

        public void Logout()
        {
            _client.Send("LOGOUT");
            SessionToken = string.Empty;
            Username = string.Empty;
            CurrentRoom = null;
            IsGameActive = false;
        }

        public void RefreshLobby()
        {
            _client.Send("LOBBY_GET");
            _client.Send("SHOP_LIST");
        }

        public void CreateRoom(string name, int max, string puzzleId, string themeId, string password = "", int targetTime = 300)
        {
            _client.Send("CREATE_ROOM", ("name", name), ("max", max), ("puzzleId", puzzleId), ("themeId", themeId), ("password", password ?? string.Empty), ("targetTime", targetTime));
        }

        public void SelectTimeLimit(int seconds)
        {
            _client.Send("SELECT_TIME_LIMIT", ("seconds", seconds));
        }

        public void JoinRoom(string roomId, string password = "")
        {
            _client.Send("JOIN_ROOM", ("roomId", roomId), ("password", password ?? string.Empty));
        }

        public void LeaveRoom()
        {
            _client.Send("LEAVE_ROOM");
        }

        public void SetReady(bool ready)
        {
            _client.Send("READY", ("ready", ready ? "true" : "false"));
        }

        public void SelectPuzzle(string puzzleId)
        {
            _client.Send("SELECT_PUZZLE", ("puzzleId", puzzleId));
        }

        public void SelectTheme(string themeId)
        {
            _client.Send("SELECT_THEME", ("themeId", themeId));
        }

        public void StartGame()
        {
            _client.Send("START_GAME");
        }

        public void SendLockPiece(int pieceId)
        {
            _client.Send("LOCK_PIECE", ("pieceId", pieceId), ("id", pieceId));
        }

        public void SendMovePiece(int pieceId, float x, float y, int rotation)
        {
            _client.Send("MOVE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation));
        }

        public void SendRotatePiece(int pieceId, int angleDelta)
        {
            _client.Send("ROTATE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("degrees", angleDelta), ("delta", angleDelta));
        }

        public void SendPlacePiece(int pieceId, float x, float y, int rotation)
        {
            _client.Send("PLACE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation), ("placed", "true"));
        }

        public void SendReleasePiece(int pieceId, float x = 0f, float y = 0f, int rotation = 0)
        {
            _client.Send("RELEASE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation));
        }

        public void SendSaveGame(string label = "Ván chơi đã lưu")
        {
            _client.Send("SAVE_GAME", ("label", label));
        }

        public void SendLoadGame()
        {
            _client.Send("LOAD_GAME");
        }

        public void SendRestartGame()
        {
            IsTimedOut = false;
            _client.Send("RESTART_GAME");
        }

        public void SendContinueGame()
        {
            IsTimedOut = false;
            _client.Send("CONTINUE_GAME");
        }

        public void SendBuy(string kind, string id)
        {
            _client.Send("BUY", ("kind", kind), ("id", id));
        }

        public void SendEquipTheme(string id)
        {
            _client.Send("EQUIP_THEME", ("id", id));
        }

        public void SendAddFriend(string username)
        {
            _client.Send("FRIEND_ADD", ("username", username));
        }

        public void SendGetFriendRequests()
        {
            _client.Send("FRIEND_REQUEST_LIST");
        }

        public void SendAcceptFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_ACCEPT", ("requestId", requestId.ToString()));
        }

        public void SendRejectFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_REJECT", ("requestId", requestId.ToString()));
        }

        public void SendCancelFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_CANCEL", ("requestId", requestId.ToString()));
        }

        public void SendInvite(string username)
        {
            _client.Send("INVITE", ("username", username));
        }

        public void SendInviteResponse(string roomId, bool accept)
        {
            _client.Send("INVITE_RESPONSE", ("roomId", roomId), ("accept", accept ? "true" : "false"));
        }

        public void SendChat(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                _client.Send("CHAT", ("text", text));
        }

        public void SendEmoji(string emoji)
        {
            if (!string.IsNullOrWhiteSpace(emoji))
                _client.Send("EMOJI", ("emoji", emoji));
        }

        private void HandleMessage(NetMessage message)
        {
            switch (message.Type)
            {
                case "AUTH_OK":
                    Username = message.Get("username");
                    Coins = message.GetInt("coins", 0);
                    SessionToken = message.Get("token");
                    RefreshLobby();
                    SendGetFriendRequests();
                    OnAuthSuccess?.Invoke();
                    break;

                case "ERROR":
                    string errCode = message.Get("code");
                    if (errCode == "ROOM_FULL" || errCode == "ROOM_NOT_FOUND")
                    {
                        PendingReconnect = null;
                    }
                    if (errCode == "PASSWORD_REQUIRED")
                    {
                        OnPasswordRequired?.Invoke(message.Get("roomId", ""), message.Get("roomName", ""));
                    }
                    OnError?.Invoke(message.Get("message", "Lỗi không xác định"));
                    break;

                case "PLAYER_EVENT":
                    OnPlayerEvent?.Invoke(message.Get("message"));
                    break;

                case "LOBBY_SNAPSHOT":
                    Coins = message.GetInt("coins", Coins);
                    LobbyRooms.Clear();
                    LobbyRooms.AddRange(ModelParser.Rooms(message.Get("rooms")));
                    Friends.Clear();
                    Friends.AddRange(ModelParser.Friends(message.Get("friends")));
                    OnLobbyUpdated?.Invoke();
                    break;

                case "FRIEND_REQUEST_LIST":
                    PendingFriendRequests.Clear();
                    PendingFriendRequests.AddRange(ModelParser.FriendRequests(message.Get("requests")));
                    OnFriendRequestsUpdated?.Invoke();
                    break;

                case "FRIEND_REQUEST_RECEIVED":
                    string frSender = message.Get("sender");
                    long frReqId = message.GetLong("requestId", 0);
                    OnPlayerEvent?.Invoke($"Bạn vừa nhận được lời mời kết bạn từ {frSender}!");
                    OnFriendRequestReceived?.Invoke(frSender, frReqId);
                    SendGetFriendRequests();
                    break;

                case "FRIEND_REQUEST_SENT":
                    OnPlayerEvent?.Invoke($"Đã gửi lời mời kết bạn tới {message.Get("receiver")}!");
                    break;

                case "FRIEND_REQUEST_AUTO_ACCEPTED":
                    OnPlayerEvent?.Invoke("Hai bạn đã trở thành bạn bè!");
                    RefreshLobby();
                    SendGetFriendRequests();
                    break;

                case "FRIEND_REQUEST_ACCEPTED":
                    OnPlayerEvent?.Invoke("Đã chấp nhận lời mời kết bạn!");
                    RefreshLobby();
                    SendGetFriendRequests();
                    break;

                case "FRIEND_REQUEST_REJECTED":
                    OnPlayerEvent?.Invoke("Đã từ chối lời mời kết bạn.");
                    SendGetFriendRequests();
                    break;

                case "FRIEND_REQUEST_CANCELLED":
                    OnPlayerEvent?.Invoke("Đã hủy lời mời kết bạn.");
                    SendGetFriendRequests();
                    break;

                case "SHOP_LIST":
                    Coins = message.GetInt("coins", Coins);
                    ShopPuzzles.Clear();
                    ShopPuzzles.AddRange(ModelParser.Puzzles(message.Get("puzzles")));
                    ShopThemes.Clear();
                    ShopThemes.AddRange(ModelParser.Themes(message.Get("themes")));
                    OwnedPuzzles.Clear();
                    foreach (var p in ShopPuzzles) if (p.Owned) OwnedPuzzles.Add(p.Id);
                    OwnedThemes.Clear();
                    foreach (var t in ShopThemes) if (t.Owned) OwnedThemes.Add(t.Id);
                    OnLobbyUpdated?.Invoke();
                    break;

                case "ROOM_SNAPSHOT":
                    if (PendingReconnect != null)
                    {
                        // Đang chờ người chơi đưa ra quyết định trên BoxMessage kết nối lại, bỏ qua snapshot phòng cũ
                        return;
                    }

                    var room = new RoomInfo
                    {
                        Id = message.Get("roomId"),
                        Name = message.Get("name"),
                        Host = message.Get("host"),
                        Count = message.GetInt("count"),
                        MaxPlayers = message.GetInt("max"),
                        State = message.Get("state"),
                        PuzzleId = message.Get("puzzleId"),
                        ThemeId = message.Get("themeId", "SCN_Nature"),
                        HasSave = message.GetBool("hasSave"),
                        HasPassword = message.GetBool("hasPassword"),
                        TargetTimeSeconds = message.GetInt("targetTime", 300)
                    };
                    CurrentRoom = room;
                    CurrentPuzzleId = room.PuzzleId;
                    CurrentThemeId = room.ThemeId;

                    RoomPlayers.Clear();
                    RoomPlayers.AddRange(ModelParser.Players(message.Get("players")));
                    OnRoomUpdated?.Invoke();
                    break;

                case "ROOM_LEFT":
                    CurrentRoom = null;
                    IsGameActive = false;
                    IsTimedOut = false;
                    ActivePieces.Clear();
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    OnRoomLeft?.Invoke();
                    if (SceneManager.GetActiveScene().name != "SCN_MainMenu")
                    {
                        SceneManager.LoadScene("SCN_MainMenu");
                    }
                    break;

                case "DISCONNECTED":
                case "CONNECTION_FAILED":
                    CurrentRoom = null;
                    IsGameActive = false;
                    IsTimedOut = false;
                    ActivePieces.Clear();
                    PendingReconnect = null;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    if (SceneManager.GetActiveScene().name != "SCN_MainMenu")
                    {
                        SceneManager.LoadScene("SCN_MainMenu");
                    }
                    break;

                case "GAME_TIMEOUT":
                    IsTimedOut = true;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    OnGameTimeout?.Invoke();
                    break;

                case "GAME_CONTINUED":
                    IsTimedOut = false;
                    if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    OnGameContinued?.Invoke();
                    break;

                case "GAME_SNAPSHOT":
                    if (PendingReconnect != null)
                    {
                        // Đang chờ người chơi đưa ra quyết định trên BoxMessage kết nối lại, tuyệt đối không tự chuyển scene
                        return;
                    }

                    bool wasTimedOut = IsTimedOut;
                    IsTimedOut = message.GetBool("timedOut", false);
                    if (IsTimedOut && !wasTimedOut)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        OnGameTimeout?.Invoke();
                    }
                    else if (!IsTimedOut && wasTimedOut)
                    {
                        OnGameContinued?.Invoke();
                    }

                    bool isInitialStart = !IsGameActive;
                    IsGameActive = true;
                    CurrentPuzzleId = message.Get("puzzleId", CurrentPuzzleId);
                    CurrentThemeId = message.Get("themeId", CurrentThemeId);
                    RemainingSeconds = message.GetInt("remaining");
                    PlacedPieces = message.GetInt("placed");
                    TotalPieces = message.GetInt("total");

                    var pieces = ModelParser.Pieces(message.Get("pieces"));
                    ActivePieces.Clear();
                    foreach (var p in pieces) ActivePieces[p.Id] = p;

                    RoomPlayers.Clear();
                    RoomPlayers.AddRange(ModelParser.Players(message.Get("players")));

                    // Switch scene to 3D environment scene if needed
                    string activeScene = SceneManager.GetActiveScene().name;
                    if (!string.IsNullOrEmpty(CurrentThemeId) && activeScene != CurrentThemeId)
                    {
                        SceneManager.LoadScene(CurrentThemeId);
                    }
                    else if (isInitialStart)
                    {
                        // Scene đã mở sẵn (ví dụ bấm Play thẳng từ SCN_Nature trong Unity Editor)
                        if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
                        {
                            Cursor.lockState = CursorLockMode.Locked;
                            Cursor.visible = false;
                        }
                        var board = FindFirstObjectByType<PuzzleSystem.Gameplay.PuzzleBoard>();
                        if (board != null)
                        {
                            board.PositionLocalPlayer(false);
                            board.SyncRemotePlayerAvatars();
                            board.LoadPuzzle(CurrentPuzzleId, true);
                            board.SyncPiecesFromServer();
                        }
                    }

                    if (isInitialStart)
                    {
                        OnGameStarted?.Invoke();
                    }
                    OnGameUpdated?.Invoke();
                    break;

                case "PIECE_LOCKED":
                    int lockId = message.GetInt("pieceId", message.GetInt("id"));
                    string lockedBy = message.Get("by");
                    if (ActivePieces.TryGetValue(lockId, out var lockPiece))
                        lockPiece.LockedBy = lockedBy;
                    OnPieceLockedEvent?.Invoke(lockId, lockedBy);
                    break;

                case "PIECE_MOVED":
                    int moveId = message.GetInt("pieceId", message.GetInt("id"));
                    float mx = message.GetFloat("x");
                    float my = message.GetFloat("y");
                    int mrot = message.GetInt("rot");
                    if (ActivePieces.TryGetValue(moveId, out var movePiece))
                    {
                        movePiece.X = mx;
                        movePiece.Y = my;
                        movePiece.Rotation = mrot;
                    }
                    OnPieceMovedEvent?.Invoke(moveId, mx, my, mrot);
                    break;

                case "PIECE_PLACED":
                    int placeId = message.GetInt("pieceId", message.GetInt("id"));
                    float px = message.GetFloat("x");
                    float py = message.GetFloat("y");
                    int prot = message.GetInt("rot");
                    string placedBy = message.Get("by");
                    PlacedPieces = message.GetInt("placed", PlacedPieces + 1);
                    TotalPieces = message.GetInt("total", TotalPieces);
                    if (ActivePieces.TryGetValue(placeId, out var placedPiece))
                    {
                        placedPiece.Placed = true;
                        placedPiece.LockedBy = string.Empty;
                        placedPiece.X = px;
                        placedPiece.Y = py;
                        placedPiece.Rotation = prot;
                    }
                    if (placedBy == Username) TotalScore += 10;
                    OnPiecePlacedEvent?.Invoke(placeId, px, py, prot, placedBy);
                    break;

                case "PIECE_RELEASED":
                    int relId = message.GetInt("pieceId", message.GetInt("id"));
                    float rx = message.GetFloat("x");
                    float ry = message.GetFloat("y");
                    int rrot = message.GetInt("rot");
                    if (ActivePieces.TryGetValue(relId, out var relPiece))
                    {
                        relPiece.LockedBy = string.Empty;
                        if (message.Get("x") != null)
                        {
                            relPiece.X = rx;
                            relPiece.Y = ry;
                            relPiece.Rotation = rrot;
                        }
                    }
                    OnPieceReleasedEvent?.Invoke(relId, rx, ry, rrot);
                    break;

                case "GAME_COMPLETE":
                    string winner = message.Get("winner");
                    int bonus = message.GetInt("bonus", 50);
                    Coins = message.GetInt("coins", Coins);
                    UniquePuzzlesCompleted++;
                    OnGameCompleteEvent?.Invoke(winner, bonus);
                    break;

                case "CHAT":
                    OnChatEvent?.Invoke(message.Get("from"), message.Get("text"));
                    break;

                case "EMOJI":
                    OnEmojiEvent?.Invoke(message.Get("from"), message.Get("emoji"));
                    break;

                case "VOICE":
                    _voice.Receive(message.Get("pcm"));
                    break;

                case "INVITE":
                    OnInviteReceived?.Invoke(message.Get("from"), message.Get("roomId"), message.Get("roomName"));
                    break;

                case "PURCHASED":
                    Coins = message.GetInt("coins", Coins);
                    RefreshLobby();
                    break;

                case "THEME_EQUIPPED":
                    RefreshLobby();
                    break;

                case "PLAYER_LOOK":
                    string lookUser = message.Get("username");
                    float lookYaw = message.GetFloat("yaw");
                    float lookPitch = message.GetFloat("pitch");
                    OnPlayerLookReceived?.Invoke(lookUser, lookYaw, lookPitch);
                    break;

                case "RECONNECT_PROMPT":
                    PendingReconnect = new ReconnectPromptInfo
                    {
                        RoomId = message.Get("roomId"),
                        RoomName = message.Get("roomName"),
                        Count = message.GetInt("count"),
                        Max = message.GetInt("max"),
                        State = message.Get("state"),
                        PuzzleId = message.Get("puzzleId")
                    };
                    OnReconnectPromptReceived?.Invoke(PendingReconnect);
                    break;
            }
        }

        public void SendReconnectDecision(bool accept, string roomId)
        {
            if (_client == null || !_client.IsConnected) return;
            PendingReconnect = null;
            _client.Send("RECONNECT_DECISION", ("accept", accept ? "true" : "false"), ("roomId", roomId));
            if (!accept)
            {
                CurrentRoom = null;
                IsGameActive = false;
                ActivePieces.Clear();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (SceneManager.GetActiveScene().name != "SCN_MainMenu")
                {
                    SceneManager.LoadScene("SCN_MainMenu");
                }
            }
        }

        private float _lastSentLookYaw = -9999f;
        private float _lastSentLookPitch = -9999f;
        private float _lastLookSendTime = 0f;

        public void SendLook(float yaw, float pitch)
        {
            if (_client == null || !_client.IsConnected || !IsGameActive) return;

            // Giới hạn tần suất gửi tối đa ~15 lần/giây và chỉ gửi khi thay đổi > 0.3 độ
            if (Time.time - _lastLookSendTime < 0.066f) return;
            if (Mathf.Abs(yaw - _lastSentLookYaw) < 0.3f && Mathf.Abs(pitch - _lastSentLookPitch) < 0.3f) return;

            _lastSentLookYaw = yaw;
            _lastSentLookPitch = pitch;
            _lastLookSendTime = Time.time;

            _client.Send("LOOK", ("yaw", yaw), ("pitch", pitch));
        }
    }
}
