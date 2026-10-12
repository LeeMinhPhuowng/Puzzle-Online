using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleSystem.Gameplay;
using PuzzleOnline.Utility;
using PuzzleOnline.Core;

namespace PuzzleOnline.Network
{
    /// <summary>
    /// Điều phối viên kết nối mạng chính (Central Network Orchestrator & Router).
    /// Chịu trách nhiệm khởi tạo kết nối socket RealtimeClient, VoiceChatController và điều phối gói tin
    /// đến 4 Service chuyên trách:
    /// - NetworkAuthService: Xác thực và thông tin tài khoản
    /// - NetworkRoomService: Quản lý sảnh chờ, phòng chơi, cửa hàng và kết nối lại
    /// - NetworkSocialService: Bạn bè, lời mời, chat và emoji
    /// - NetworkGameplaySyncService: Đồng bộ hóa quá trình ghép hình và chuyển động người chơi
    /// Triển khai INetworkService để hỗ trợ Dependency Injection.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class PuzzleNetworkManager : MonoBehaviour, INetworkService
    {
        public static PuzzleNetworkManager Instance { get; private set; }

        public RealtimeClient Client => _client;
        public VoiceChatController Voice => _voice;

        [Header("Connection")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 7778;

        public string ServerHost
        {
            get => serverHost;
            set => serverHost = value;
        }

        public int ServerPort
        {
            get => serverPort;
            set => serverPort = value;
        }

        public void ConfigureConnection(string host, int port)
        {
            bool changed = false;
            if (!string.IsNullOrEmpty(host) && !serverHost.Equals(host.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                serverHost = host.Trim();
                changed = true;
            }
            if (port > 0 && serverPort != port)
            {
                serverPort = port;
                changed = true;
            }
            if (changed && _client != null)
            {
                _client.Disconnect();
                _client.Connect(serverHost, serverPort);
            }
        }

        private RealtimeClient _client;
        private VoiceChatController _voice;

        // Sub-services
        public NetworkAuthService AuthService { get; private set; }
        public NetworkRoomService RoomService { get; private set; }
        public NetworkSocialService SocialService { get; private set; }
        public NetworkGameplaySyncService GameplaySyncService { get; private set; }

        // =========================================================================
        // PROPERTIES FORWARDING (Đảm bảo 100% code UI/Gameplay gọi trực tiếp không đổi)
        // =========================================================================
        public string Username => AuthService?.Username ?? string.Empty;
        public int Coins => AuthService?.Coins ?? 0;
        public string SessionToken => AuthService?.SessionToken ?? string.Empty;
        public int TotalScore => AuthService?.TotalScore ?? 0;
        public int UniquePuzzlesCompleted => AuthService?.UniquePuzzlesCompleted ?? 0;

        public RoomInfo CurrentRoom => RoomService?.CurrentRoom;
        public bool IsHost => CurrentRoom != null && CurrentRoom.Host == Username;
        public List<PlayerInfo> RoomPlayers => RoomService != null ? RoomService.RoomPlayers : null;
        public List<RoomInfo> LobbyRooms => RoomService != null ? RoomService.LobbyRooms : null;
        public List<PuzzleDefinition> ShopPuzzles => RoomService != null ? RoomService.ShopPuzzles : null;
        public List<ThemeDefinition> ShopThemes => RoomService != null ? RoomService.ShopThemes : null;
        public HashSet<string> OwnedPuzzles => RoomService != null ? RoomService.OwnedPuzzles : null;
        public HashSet<string> OwnedThemes => RoomService != null ? RoomService.OwnedThemes : null;
        public bool IsTimedOut
        {
            get => RoomService != null && RoomService.IsTimedOut;
            set { if (RoomService != null) RoomService.IsTimedOut = value; }
        }
        public ReconnectPromptInfo PendingReconnect
        {
            get => RoomService?.PendingReconnect;
            set { if (RoomService != null) RoomService.PendingReconnect = value; }
        }

        public List<FriendInfo> Friends => SocialService != null ? SocialService.Friends : null;
        public List<FriendRequestItem> PendingFriendRequests => SocialService != null ? SocialService.PendingFriendRequests : null;
        public List<FriendRequestItem> SentFriendRequests => SocialService != null ? SocialService.SentFriendRequests : null;

        public string CurrentPuzzleId => GameplaySyncService?.CurrentPuzzleId ?? "sunset_3x3";
        public string CurrentThemeId => GameplaySyncService?.CurrentThemeId ?? "SCN_Nature";
        public int RemainingSeconds => GameplaySyncService?.RemainingSeconds ?? 0;
        public int PlacedPieces => GameplaySyncService?.PlacedPieces ?? 0;
        public int TotalPieces => GameplaySyncService?.TotalPieces ?? 0;
        public bool IsGameActive => GameplaySyncService != null && GameplaySyncService.IsGameActive;
        public Dictionary<int, PieceInfo> ActivePieces => GameplaySyncService?.ActivePieces;
        public List<WorldItemInfo> ActiveWorldItems => GameplaySyncService?.ActiveWorldItems;

        public string CurrentGameMode => CurrentRoom?.GameMode ?? "CLASSIC";
        public bool IsCompetitive => CurrentGameMode == "COMPETITIVE";
        public string LocalPlayerTeam
        {
            get
            {
                if (RoomPlayers == null || string.IsNullOrEmpty(Username)) return "BUILDER";
                var p = RoomPlayers.Find(x => string.Equals(x.Username, Username, StringComparison.OrdinalIgnoreCase));
                return p != null && !string.IsNullOrEmpty(p.Team) ? p.Team : "BUILDER";
            }
        }
        public bool IsSaboteur => LocalPlayerTeam == "SABOTEUR";
        public bool IsBuilder => !IsSaboteur;

        public string GetPuzzleName(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return string.Empty;
            var sp = ShopPuzzles?.Find(p => string.Equals(p.Id, puzzleId, StringComparison.OrdinalIgnoreCase));
            if (sp != null && !string.IsNullOrEmpty(sp.Name)) return sp.Name;
            var pData = ContentRegistry.Instance?.GetPuzzleData(puzzleId);
            if (pData != null && !string.IsNullOrEmpty(pData.displayName)) return pData.displayName;
            return puzzleId;
        }

        public string GetThemeName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return string.Empty;
            var st = ShopThemes?.Find(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));
            if (st != null && !string.IsNullOrEmpty(st.Name)) return st.Name;
            var tData = ContentRegistry.Instance?.GetTheme(themeId);
            if (tData != null && !string.IsNullOrEmpty(tData.displayName)) return tData.displayName;
            return themeId;
        }

        public string GetThemeDescription(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return string.Empty;
            var st = ShopThemes?.Find(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));
            if (st != null && !string.IsNullOrEmpty(st.Description)) return st.Description;
            var tData = ContentRegistry.Instance?.GetTheme(themeId);
            if (tData != null && !string.IsNullOrEmpty(tData.description)) return tData.description;
            return string.Empty;
        }

        // =========================================================================
        // EVENTS FORWARDING
        // =========================================================================
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
        public event Action<string, Vector3, float, float> OnPlayerPoseReceived;
        public event Action<ReconnectPromptInfo> OnReconnectPromptReceived;
        public event Action<string, string> OnPasswordRequired;
        public event Action OnGameTimeout;
        public event Action OnGameContinued;
        public event Action<WorldItemInfo> OnItemSpawned;
        public event Action<string, string, string> OnItemPickedUp;
        public event Action<string, string, string, float> OnSabotageTriggered;
        public event Action<List<int>, float, string> OnInkSplash;
        public event Action<int, float, float, int, string> OnPieceUnfit;
        public event Action<string, string> OnGameOverResult;

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
                if (Instance.gameObject.name == "[PuzzleNetworkManager]")
                {
                    Destroy(Instance.gameObject);
                }
                else
                {
                    Destroy(gameObject);
                    return;
                }
            }

            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register<INetworkService>(this);

            _client = gameObject.AddComponent<RealtimeClient>();
            _voice = gameObject.AddComponent<VoiceChatController>();
            _voice.Initialize(_client);

            AuthService = new NetworkAuthService(_client);
            RoomService = new NetworkRoomService(_client);
            SocialService = new NetworkSocialService(_client);
            GameplaySyncService = new NetworkGameplaySyncService(_client);

            if (GetComponent<PuzzleOnline.Gameplay.Competitive.SabotageEffectsManager>() == null)
            {
                gameObject.AddComponent<PuzzleOnline.Gameplay.Competitive.SabotageEffectsManager>();
            }
            if (GetComponent<PuzzleOnline.Gameplay.Competitive.SaboteurInventoryHUD>() == null)
            {
                gameObject.AddComponent<PuzzleOnline.Gameplay.Competitive.SaboteurInventoryHUD>();
            }
            if (GetComponent<PuzzleOnline.Gameplay.Competitive.SaboteurTargetingSystem>() == null)
            {
                gameObject.AddComponent<PuzzleOnline.Gameplay.Competitive.SaboteurTargetingSystem>();
            }

            BindServiceEvents();

            _client.MessageReceived += HandleMessage;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this)
            {
                ServiceLocator.Unregister<INetworkService>();
                Instance = null;
            }
        }

        private void BindServiceEvents()
        {
            AuthService.OnAuthSuccess += () => {
                RefreshLobby();
                SendGetFriendRequests();
                OnAuthSuccess?.Invoke();
            };
            AuthService.OnError += err => OnError?.Invoke(err);

            RoomService.OnLobbyUpdated += () => OnLobbyUpdated?.Invoke();
            RoomService.OnRoomUpdated += () => {
                if (RoomService.CurrentRoom != null)
                {
                    GameplaySyncService.CurrentPuzzleId = RoomService.CurrentRoom.PuzzleId;
                    GameplaySyncService.CurrentThemeId = RoomService.CurrentRoom.ThemeId;
                }
                OnRoomUpdated?.Invoke();
            };
            RoomService.OnRoomLeft += () => {
                GameplaySyncService.ResetGameState();
                OnRoomLeft?.Invoke();
                ReturnToLobbyIfNeeded();
            };
            RoomService.OnGameTimeout += () => OnGameTimeout?.Invoke();
            RoomService.OnGameContinued += () => OnGameContinued?.Invoke();
            RoomService.OnReconnectPromptReceived += prompt => OnReconnectPromptReceived?.Invoke(prompt);
            RoomService.OnPasswordRequired += (rid, rname) => OnPasswordRequired?.Invoke(rid, rname);
            RoomService.OnCoinsUpdated += coins => { if (AuthService != null) AuthService.Coins = coins; };

            SocialService.OnFriendRequestsUpdated += () => OnFriendRequestsUpdated?.Invoke();
            SocialService.OnFriendRequestReceived += (sender, reqId) => OnFriendRequestReceived?.Invoke(sender, reqId);
            SocialService.OnChatEvent += (from, text) => OnChatEvent?.Invoke(from, text);
            SocialService.OnEmojiEvent += (from, emoji) => OnEmojiEvent?.Invoke(from, emoji);
            SocialService.OnInviteReceived += (from, rid, rname) => OnInviteReceived?.Invoke(from, rid, rname);
            SocialService.OnPlayerEvent += msg => OnPlayerEvent?.Invoke(msg);
            SocialService.OnLobbyRefreshNeeded += () => RefreshLobby();

            GameplaySyncService.OnGameStarted += () => OnGameStarted?.Invoke();
            GameplaySyncService.OnGameUpdated += () => OnGameUpdated?.Invoke();
            GameplaySyncService.OnPieceLockedEvent += (id, by) => OnPieceLockedEvent?.Invoke(id, by);
            GameplaySyncService.OnPieceMovedEvent += (id, x, y, rot) => OnPieceMovedEvent?.Invoke(id, x, y, rot);
            GameplaySyncService.OnPiecePlacedEvent += (id, x, y, rot, by) => OnPiecePlacedEvent?.Invoke(id, x, y, rot, by);
            GameplaySyncService.OnPieceReleasedEvent += (id, x, y, rot) => OnPieceReleasedEvent?.Invoke(id, x, y, rot);
            GameplaySyncService.OnGameCompleteEvent += (winner, bonus) => {
                if (AuthService != null) AuthService.Coins += bonus;
                OnGameCompleteEvent?.Invoke(winner, bonus);
            };
            GameplaySyncService.OnPlayerLookReceived += (u, y, p) => OnPlayerLookReceived?.Invoke(u, y, p);
            GameplaySyncService.OnPlayerPoseReceived += (u, pos, y, p) => OnPlayerPoseReceived?.Invoke(u, pos, y, p);

            GameplaySyncService.OnItemSpawned += item => OnItemSpawned?.Invoke(item);
            GameplaySyncService.OnItemPickedUp += (id, type, by) => OnItemPickedUp?.Invoke(id, type, by);
            GameplaySyncService.OnSabotageTriggered += (type, target, by, dur) => OnSabotageTriggered?.Invoke(type, target, by, dur);
            GameplaySyncService.OnInkSplash += (ids, dur, by) => OnInkSplash?.Invoke(ids, dur, by);
            GameplaySyncService.OnPieceUnfit += (id, x, y, rot, by) => OnPieceUnfit?.Invoke(id, x, y, rot, by);
            GameplaySyncService.OnGameOverResult += (res, team) => OnGameOverResult?.Invoke(res, team);
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

        private void HandleMessage(NetMessage message)
        {
            switch (message.Type)
            {
                case "VOICE":
                    _voice.Receive(message.Get("pcm"));
                    return;

                case "DISCONNECTED":
                case "CONNECTION_FAILED":
                    RoomService.ClearRoom();
                    GameplaySyncService.ResetGameState();
                    CursorManager.Unlock();
                    ReturnToLobbyIfNeeded();
                    return;

                case "GAME_SNAPSHOT":
                    if (PendingReconnect != null) return;
                    RoomPlayers.Clear();
                    RoomPlayers.AddRange(ModelParser.Players(message.Get("players")));
                    bool handled = GameplaySyncService.HandleMessage(message, Username, score => AuthService.TotalScore += score, () => AuthService.UniquePuzzlesCompleted++);
                    if (handled)
                    {
                        // Chuyển Scene nếu cần
                        string activeScene = SceneManager.GetActiveScene().name;
                        if (!string.IsNullOrEmpty(CurrentThemeId) && activeScene != CurrentThemeId)
                        {
                            SceneManager.LoadScene(CurrentThemeId);
                        }
                    }
                    return;
            }

            if (AuthService.HandleMessage(message)) return;
            if (RoomService.HandleMessage(message, SocialService.Friends)) return;
            if (SocialService.HandleMessage(message)) return;
            GameplaySyncService.HandleMessage(message, Username, score => AuthService.TotalScore += score, () => AuthService.UniquePuzzlesCompleted++);
        }

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
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!IsGameActive || CurrentThemeId != sceneName || SceneManager.GetActiveScene().name != sceneName)
            {
                yield break;
            }

            var board = FindFirstObjectByType<PuzzleBoard>();
            if (board != null)
            {
                board.PositionLocalPlayer(true);
                board.SyncRemotePlayerAvatars();
                if (!string.IsNullOrEmpty(CurrentPuzzleId))
                {
                    board.LoadPuzzle(CurrentPuzzleId, true);
                    board.SyncPiecesFromServer();
                }
            }
        }

        private void ReturnToLobbyIfNeeded()
        {
            string curScene = SceneManager.GetActiveScene().name;
            // Nếu người chơi đang ở trong SCN_Room, SCN_Menu hoặc SCN_MainMenu thì TUYỆT ĐỐI KHÔNG reload scene,
            // tránh gây khựng/lag và hiện lại scene Menu không mong muốn
            if (curScene == "SCN_Room" || curScene == "SCN_Menu" || curScene == "SCN_MainMenu")
            {
                return;
            }

            // Nếu đang ở màn chơi 3D (Forest, Ocean, Rei, Sunset...), chuyển về SCN_Room
            SceneManager.LoadScene("SCN_Room");
        }

        // =========================================================================
        // PUBLIC API FORWARDERS (Tương thích 100% với code hiện có)
        // =========================================================================
        public void Register(string user, string pass) => AuthService.Register(user, pass);
        public void Login(string user, string pass) => AuthService.Login(user, pass);
        public void Logout()
        {
            AuthService.Logout();
            RoomService.ClearRoom();
            GameplaySyncService.ResetGameState();
        }

        public void RefreshLobby() => RoomService.RefreshLobby();
        public void CreateRoom(string name, int maxPlayers, string puzzleId, string themeId, string password, int targetTimeSeconds)
            => RoomService.CreateRoom(name, maxPlayers, puzzleId, themeId, password, targetTimeSeconds, "CLASSIC");

        public void CreateRoom(string name, int maxPlayers, string puzzleId, string themeId, string password = "", int targetTimeSeconds = 300, string gameMode = "CLASSIC")
            => RoomService.CreateRoom(name, maxPlayers, puzzleId, themeId, password, targetTimeSeconds, gameMode);
        public void SelectTimeLimit(int seconds) => RoomService.SelectTimeLimit(seconds);
        public void SelectGameMode(string gameMode) => RoomService.SelectGameMode(gameMode);
        public void SwitchTeam(string team = "") => RoomService.SwitchTeam(team);
        public void PickupItem(string itemId) => RoomService.PickupItem(itemId);
        public void UseSabotage(string itemType, string targetUser = "", int pieceId = -1) => RoomService.UseSabotage(itemType, targetUser, pieceId);
        public void JoinRoom(string roomId, string password = "") => RoomService.JoinRoom(roomId, password);
        public void LeaveRoom() => RoomService.LeaveRoom();
        public void SetReady(bool ready) => RoomService.SetReady(ready);
        public void SelectPuzzle(string puzzleId) => RoomService.SelectPuzzle(puzzleId);
        public void SelectTheme(string themeId) => RoomService.SelectTheme(themeId);
        public void StartGame() => RoomService.StartGame();
        public void SendBuy(string kind, string id) => RoomService.SendBuy(kind, id);
        public void SendEquipTheme(string id) => RoomService.SendEquipTheme(id);
        public void SendReconnectDecision(bool accept, string roomId)
        {
            RoomService.SendReconnectDecision(accept, roomId);
            if (!accept)
            {
                RoomService.ClearRoom();
                GameplaySyncService.ResetGameState();
                CursorManager.Unlock();
                ReturnToLobbyIfNeeded();
            }
        }

        public void SendAddFriend(string username) => SocialService.SendAddFriend(username);
        public void SendGetFriendRequests() => SocialService.SendGetFriendRequests();
        public void SendAcceptFriendRequest(long reqId) => SocialService.SendAcceptFriendRequest(reqId);
        public void SendRejectFriendRequest(long reqId) => SocialService.SendRejectFriendRequest(reqId);
        public void SendCancelFriendRequest(long reqId) => SocialService.SendCancelFriendRequest(reqId);
        public void SendInvite(string username) => SocialService.SendInvite(username);
        public void SendInviteResponse(string roomId, bool accept) => SocialService.SendInviteResponse(roomId, accept);
        public void SendChat(string text) => SocialService.SendChat(text);
        public void SendEmoji(string emoji) => SocialService.SendEmoji(emoji);

        public void SendLockPiece(int pieceId) => GameplaySyncService.SendLockPiece(pieceId);
        public void SendMovePiece(int pieceId, float x, float y, int rotation) => GameplaySyncService.SendMovePiece(pieceId, x, y, rotation);
        public void SendRotatePiece(int pieceId, int delta) => GameplaySyncService.SendRotatePiece(pieceId, delta);
        public void SendPlacePiece(int pieceId, float x, float y, int rotation) => GameplaySyncService.SendPlacePiece(pieceId, x, y, rotation);
        public void SendReleasePiece(int pieceId, float x = 0f, float y = 0f, int rotation = 0) => GameplaySyncService.SendReleasePiece(pieceId, x, y, rotation);
        public void SendSaveGame(string label = "Ván chơi đã lưu") => GameplaySyncService.SendSaveGame(label);
        public void SendLoadGame() => GameplaySyncService.SendLoadGame();
        public void SendRestartGame()
        {
            IsTimedOut = false;
            GameplaySyncService.SendRestartGame();
        }
        public void SendContinueGame()
        {
            IsTimedOut = false;
            GameplaySyncService.SendContinueGame();
        }

        public void ConnectToServer(string host, int port) => ConnectToServer(host, (int?)port);
        public void LockPiece(int pieceId) => SendLockPiece(pieceId);
        public void MovePiece(int pieceId, float x, float y, int rotation) => SendMovePiece(pieceId, x, y, rotation);
        public void PlacePiece(int pieceId, float x, float y, int rotation) => SendPlacePiece(pieceId, x, y, rotation);
        public void ReleasePiece(int pieceId, float x = 0f, float y = 0f, int rotation = 0) => SendReleasePiece(pieceId, x, y, rotation);

        public void SendLook(float yaw, float pitch, Vector3? position = null) => GameplaySyncService.SendLook(yaw, pitch, position);

        private void OnApplicationQuit()
        {
            if (RoomService != null && RoomService.CurrentRoom != null)
            {
                RoomService.LeaveRoom();
            }
            _client?.Disconnect();
        }
    }
}
