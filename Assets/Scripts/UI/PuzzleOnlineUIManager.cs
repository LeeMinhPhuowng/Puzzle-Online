using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;

namespace PuzzleOnline.UI
{
    [DefaultExecutionOrder(-10)]
    public sealed class PuzzleOnlineUIManager : MonoBehaviour
    {
        public static PuzzleOnlineUIManager Instance { get; private set; }

        private const float VirtualWidth = 1920f;
        private const float VirtualHeight = 1080f;

        private enum MainTab { Rooms, Friends, Shop, Profile }
        private MainTab _currentTab = MainTab.Rooms;

        // Auth Fields
        private string _serverHost = "127.0.0.1";
        private string _serverPort = "7777";
        private string _username = "";
        private string _password = "";

        // Lobby Create Room Fields
        private string _createRoomName = "Phòng thư giãn";
        private int _createMaxPlayers = 4;
        private bool _createIsPrivate = false;
        private string _createPassword = "";
        private string _selectedPuzzleId = "sunset_3x3";
        private string _selectedThemeId = "SCN_Nature";
        private int _createTargetTime = 300;
        private string _joinCodeInput = "";
        private string _addFriendInput = "";

        // Join Password Modal
        private RoomInfo _joiningPasswordRoom = null;
        private string _joiningPasswordRoomId = "";
        private string _joinPasswordInput = "";

        // Chat & Messages
        private string _chatInput = "";
        private readonly List<string> _chatHistory = new List<string>();
        private bool _showInGameChat = false;
        private bool _showInGameMenu = false;
        private string _toastMessage = "";
        private float _toastTimer = 0f;

        // Completion Popup
        private bool _showVictoryModal = false;
        private string _victoryWinner = "";
        private int _victoryBonus = 0;

        // Timeout Modal
        private bool _showTimeoutModal = false;

        // Reconnection Prompt Modal
        private ReconnectPromptInfo _reconnectPrompt = null;

        // Scroll Positions
        private Vector2 _roomListScroll;
        private Vector2 _friendListScroll;
        private Vector2 _shopScroll;
        private Vector2 _waitingRoomChatScroll;
        private Vector2 _inGameChatScroll;

        // UI Styling (Fixed Modern Dark Palette for all users)
        private Color _bgColor = new Color(0.08f, 0.09f, 0.12f, 1f);
        private Color _cardColor = new Color(0.13f, 0.15f, 0.20f, 0.95f);
        private Color _cardHoverColor = new Color(0.18f, 0.20f, 0.27f, 0.95f);
        private Color _accentColor = new Color(0.22f, 0.62f, 0.95f, 1f);
        private Color _accentHoverColor = new Color(0.35f, 0.72f, 1f, 1f);
        private Color _successColor = new Color(0.24f, 0.75f, 0.45f, 1f);
        private Color _dangerColor = new Color(0.90f, 0.28f, 0.28f, 1f);
        private Color _goldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private Color _textColor = new Color(0.94f, 0.95f, 0.97f, 1f);
        private Color _mutedColor = new Color(0.58f, 0.62f, 0.70f, 1f);

        private Texture2D _pixelTex;
        private GUIStyle _titleStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _mutedStyle;
        private GUIStyle _boldStyle;
        private GUIStyle _cardStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _primaryBtnStyle;
        private GUIStyle _goldBtnStyle;
        private GUIStyle _dangerBtnStyle;
        private GUIStyle _badgeStyle;
        private bool _stylesInitialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindFirstObjectByType<PuzzleOnlineUIManager>() != null) return;
            var go = new GameObject("[PuzzleOnlineUIManager]");
            go.AddComponent<PuzzleOnlineUIManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (PuzzleNetworkManager.Instance == null)
            {
                var netObj = new GameObject("[PuzzleNetworkManager]");
                netObj.AddComponent<PuzzleNetworkManager>();
            }

            if (PuzzleAudioService.Instance == null)
            {
                var audioObj = new GameObject("[PuzzleAudioService]");
                audioObj.AddComponent<PuzzleAudioService>();
            }

            RegisterNetworkEvents();
        }

        private void RegisterNetworkEvents()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnError += msg =>
            {
                ShowToast(msg);
                _reconnectPrompt = null;
                if (PuzzleNetworkManager.Instance == null || !PuzzleNetworkManager.Instance.IsGameActive)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    if (SceneManager.GetActiveScene().name != "SCN_MainMenu")
                    {
                        SceneManager.LoadScene("SCN_MainMenu");
                    }
                }
            };
            net.OnPlayerEvent += msg => AddChatMessage("[Hệ thống] " + msg);
            net.OnChatEvent += (from, text) => AddChatMessage(from + ": " + text);
            net.OnEmojiEvent += (from, emoji) => AddChatMessage(from + ": " + emoji);
            net.OnGameCompleteEvent += (winner, bonus) =>
            {
                _showVictoryModal = true;
                _victoryWinner = winner;
                _victoryBonus = bonus;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            };
            net.OnInviteReceived += (from, roomId, roomName) =>
            {
                ShowToast($"Nhận lời mời từ {from} vào phòng {roomName} ({roomId})");
            };
            net.OnReconnectPromptReceived += info =>
            {
                _reconnectPrompt = info;
            };
            net.OnRoomUpdated += () =>
            {
                _joiningPasswordRoom = null;
                _joiningPasswordRoomId = "";
                _joinPasswordInput = "";
            };
            net.OnPasswordRequired += (roomId, roomName) =>
            {
                _joiningPasswordRoomId = roomId;
                _joinPasswordInput = "";
            };
            net.OnGameTimeout += () =>
            {
                _showTimeoutModal = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            };
            net.OnGameContinued += () =>
            {
                _showTimeoutModal = false;
                if (!IsModalOrChatOpen)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            };
            net.OnGameStarted += () =>
            {
                _showTimeoutModal = false;
            };
            net.OnRoomLeft += () =>
            {
                _showTimeoutModal = false;
            };
        }

        public static bool IsModalOrChatOpen
        {
            get
            {
                if (Instance == null) return false;
                var net = PuzzleNetworkManager.Instance;
                if (net == null || !net.IsGameActive) return true;
                if (Instance._reconnectPrompt != null) return true;
                if (Instance._joiningPasswordRoom != null || !string.IsNullOrEmpty(Instance._joiningPasswordRoomId)) return true;
                if (Instance._showVictoryModal) return true;
                if (Instance._showTimeoutModal) return true;
                if (Instance._showInGameChat) return true;
                if (Instance._showInGameMenu) return true;
                return false;
            }
        }

        private void Update()
        {
            if (_toastTimer > 0f) _toastTimer -= Time.unscaledDeltaTime;

            // Voice push-to-talk: Hold 'V'
            if (PuzzleNetworkManager.Instance != null && PuzzleNetworkManager.Instance.Voice != null)
            {
                bool isTalking = Input.GetKey(KeyCode.V);
                PuzzleNetworkManager.Instance.Voice.SetTransmitting(isTalking);
            }

            // In-game chat toggle: 'T'
            if (PuzzleNetworkManager.Instance != null && PuzzleNetworkManager.Instance.IsGameActive)
            {
                if (Input.GetKeyDown(KeyCode.T) && GUIUtility.keyboardControl == 0)
                {
                    _showInGameChat = !_showInGameChat;
                    if (_showInGameChat)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                }
                else if (_showInGameChat && Input.GetKeyDown(KeyCode.Escape))
                {
                    _showInGameChat = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    _showInGameMenu = !_showInGameMenu;
                    Cursor.lockState = _showInGameMenu ? CursorLockMode.None : CursorLockMode.Locked;
                    Cursor.visible = _showInGameMenu;
                }
            }
        }

        private string _lastToastMessage = "";
        private float _lastToastTime = -10f;

        private void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message == _lastToastMessage && Time.unscaledTime - _lastToastTime < 1.5f) return;
            _lastToastMessage = message;
            _lastToastTime = Time.unscaledTime;
            _toastMessage = message;
            _toastTimer = 3.5f;
        }

        private void AddChatMessage(string message)
        {
            _chatHistory.Add(message);
            if (_chatHistory.Count > 100) _chatHistory.RemoveAt(0);
            _waitingRoomChatScroll.y = float.MaxValue;
            _inGameChatScroll.y = float.MaxValue;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor }
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor }
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _textColor }
            };

            _boldStyle = new GUIStyle(_bodyStyle) { fontStyle = FontStyle.Bold };

            _mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _mutedColor }
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _cardStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeSolidTex(_cardColor) }
            };

            _inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                normal = { background = MakeSolidTex(new Color(0.09f, 0.10f, 0.14f, 1f)), textColor = _textColor }
            };

            _btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeSolidTex(new Color(0.18f, 0.22f, 0.30f, 1f)), textColor = _textColor },
                hover = { background = MakeSolidTex(_cardHoverColor), textColor = Color.white }
            };

            _primaryBtnStyle = new GUIStyle(_btnStyle)
            {
                normal = { background = MakeSolidTex(_accentColor), textColor = Color.white },
                hover = { background = MakeSolidTex(_accentHoverColor), textColor = Color.white }
            };

            _goldBtnStyle = new GUIStyle(_btnStyle)
            {
                normal = { background = MakeSolidTex(_goldColor), textColor = Color.black },
                hover = { background = MakeSolidTex(new Color(1f, 0.88f, 0.35f, 1f)), textColor = Color.black }
            };

            _dangerBtnStyle = new GUIStyle(_btnStyle)
            {
                normal = { background = MakeSolidTex(_dangerColor), textColor = Color.white },
                hover = { background = MakeSolidTex(new Color(1f, 0.35f, 0.35f, 1f)), textColor = Color.white }
            };

            _stylesInitialized = true;
        }

        private void EnsureResources()
        {
            if (_pixelTex == null)
            {
                _pixelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _pixelTex.hideFlags = HideFlags.DontSave;
                _pixelTex.SetPixel(0, 0, Color.white);
                _pixelTex.Apply();
                _stylesInitialized = false;
            }
            if (!_stylesInitialized)
            {
                InitStyles();
            }
        }

        private Texture2D MakeSolidTex(Color col)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.DontSave;
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        private void DrawSolidRect(Rect rect, Color col)
        {
            EnsureResources();
            var oldCol = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(rect, _pixelTex);
            GUI.color = oldCol;
        }

        private void DrawBorder(Rect rect, float thickness, Color col)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), col);
            DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), col);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), col);
            DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), col);
        }

        private bool DrawButton(Rect rect, string text, bool isPrimary, bool isGold = false, bool isDanger = false, int fontSize = 16)
        {
            EnsureResources();
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color bgColor;
            Color textColor;

            if (isPrimary)
            {
                bgColor = hovered ? _accentHoverColor : _accentColor;
                textColor = Color.white;
            }
            else if (isGold)
            {
                bgColor = hovered ? new Color(1f, 0.88f, 0.35f, 1f) : _goldColor;
                textColor = Color.black;
            }
            else if (isDanger)
            {
                bgColor = hovered ? new Color(1f, 0.35f, 0.35f, 1f) : _dangerColor;
                textColor = Color.white;
            }
            else
            {
                bgColor = hovered ? _cardHoverColor : new Color(0.18f, 0.22f, 0.30f, 1f);
                textColor = _textColor;
            }

            DrawSolidRect(rect, bgColor);

            if (isPrimary)
            {
                DrawBorder(rect, 2f, new Color(0.65f, 0.88f, 1f, 1f));
            }
            else
            {
                DrawBorder(rect, 1f, new Color(0.28f, 0.33f, 0.44f, 0.7f));
            }

            var style = isPrimary ? _boldStyle : _bodyStyle;
            var prevAlign = style.alignment;
            var prevSize = style.fontSize;
            var prevColor = style.normal.textColor;

            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = fontSize;
            style.normal.textColor = textColor;

            GUI.Label(rect, text, style);

            style.alignment = prevAlign;
            style.fontSize = prevSize;
            style.normal.textColor = prevColor;

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private void OnGUI()
        {
            EnsureResources();

            // Set virtual resolution matrix (1920x1080)
            float scaleX = Screen.width / VirtualWidth;
            float scaleY = Screen.height / VirtualHeight;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scaleX, scaleY, 1f));

            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            if (string.IsNullOrEmpty(net.Username))
            {
                DrawAuthScreen();
            }
            else if (net.CurrentRoom == null)
            {
                DrawLobbyScreen();
            }
            else if (net.CurrentRoom.State == "WAITING")
            {
                DrawWaitingRoomScreen();
            }
            else if (net.IsGameActive)
            {
                DrawInGameHUD();
            }

            DrawToast();
            DrawVictoryModal();
            DrawTimeoutModal();
            DrawReconnectModal();
            DrawPasswordModal();
        }

        #region 1. Auth Screen
        private void DrawAuthScreen()
        {
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), _bgColor);

            // Left Hero banner
            GUI.Box(new Rect(80, 80, 850, 920), GUIContent.none, _cardStyle);
            GUI.Label(new Rect(130, 130, 750, 60), "PUZZLE ONLINE", _titleStyle);
            GUI.Label(new Rect(130, 195, 750, 40), "Cùng nhau ghép tranh thời gian thực trong không gian 3D", _mutedStyle);

            DrawAuthFeature(new Rect(130, 270, 750, 140), "01", "3D Multiplayer Thời Gian Thực",
                "Đồng bộ kéo thả, xoay và hút ghép mảnh ghép tức thì giữa tất cả người chơi qua Java Server.");
            DrawAuthFeature(new Rect(130, 430, 750, 140), "02", "3 Không Gian Phòng Chơi (3D Environments)",
                "Tự do lựa chọn bối cảnh: Khu vườn thiên nhiên, Phòng học cổ điển, hay Vườn hoa anh đào!");
            DrawAuthFeature(new Rect(130, 590, 750, 140), "03", "Trò Chuyện & Voice Chat (Nhấn giữ 'V')",
                "Giao tiếp trực tiếp với bạn bè qua chat chữ, biểu cảm cảm xúc và voice chat micro.");
            DrawAuthFeature(new Rect(130, 750, 750, 140), "04", "Hệ Thống Xu & Bộ Sưu Tập",
                "Ghép hoàn thành tranh để nhận xu thưởng, mở khóa thêm nhiều bộ tranh và không gian 3D mới.");

            // Right Form box
            Rect formBox = new Rect(990, 150, 850, 780);
            GUI.Box(formBox, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(1050, 200, 730, 50), "Đăng Nhập / Đăng Ký", _headerStyle);
            GUI.Label(new Rect(1050, 255, 730, 30), "Tạo tài khoản mới được tặng ngay 300 xu", _mutedStyle);

            // Host & Port row
            GUI.Label(new Rect(1050, 310, 200, 25), "Máy chủ (Host)", _mutedStyle);
            _serverHost = GUI.TextField(new Rect(1050, 340, 480, 46), _serverHost, _inputStyle);
            GUI.Label(new Rect(1550, 310, 150, 25), "Cổng (Port)", _mutedStyle);
            _serverPort = GUI.TextField(new Rect(1550, 340, 230, 46), _serverPort, _inputStyle);

            GUI.Label(new Rect(1050, 410, 730, 25), "Tên tài khoản", _mutedStyle);
            _username = GUI.TextField(new Rect(1050, 440, 730, 50), _username, _inputStyle);

            GUI.Label(new Rect(1050, 515, 730, 25), "Mật khẩu", _mutedStyle);
            _password = GUI.PasswordField(new Rect(1050, 545, 730, 50), _password, '•', _inputStyle);

            if (GUI.Button(new Rect(1050, 640, 350, 60), "Đăng ký mới", _btnStyle))
            {
                PuzzleAudioService.Instance?.PlayClick();
                if (int.TryParse(_serverPort, out var p))
                {
                    PuzzleNetworkManager.Instance.ConnectToServer(_serverHost, p);
                    PuzzleNetworkManager.Instance.Register(_username.Trim(), _password);
                }
            }

            if (GUI.Button(new Rect(1430, 640, 350, 60), "Đăng nhập", _primaryBtnStyle))
            {
                PuzzleAudioService.Instance?.PlayClick();
                if (int.TryParse(_serverPort, out var p))
                {
                    PuzzleNetworkManager.Instance.ConnectToServer(_serverHost, p);
                    PuzzleNetworkManager.Instance.Login(_username.Trim(), _password);
                }
            }

            // Connection indicator
            Color connCol = PuzzleNetworkManager.Instance.Client.IsConnected ? _successColor : _mutedColor;
            DrawSolidRect(new Rect(1052, 740, 14, 14), connCol);
            GUI.Label(new Rect(1075, 735, 700, 30), PuzzleNetworkManager.Instance.Client.ConnectionLabel, _mutedStyle);
        }

        private void DrawAuthFeature(Rect rect, string num, string title, string desc)
        {
            DrawSolidRect(rect, new Color(0.10f, 0.11f, 0.16f, 0.8f));
            DrawSolidRect(new Rect(rect.x + 20, rect.y + 25, 45, 30), _accentColor);
            GUI.Label(new Rect(rect.x + 20, rect.y + 25, 45, 30), num, _badgeStyle);
            GUI.Label(new Rect(rect.x + 80, rect.y + 22, rect.width - 100, 32), title, _boldStyle);
            GUI.Label(new Rect(rect.x + 80, rect.y + 58, rect.width - 100, 65), desc, _mutedStyle);
        }
        #endregion

        #region 2. Lobby Screen
        private void DrawLobbyScreen()
        {
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), _bgColor);

            // Top Header Bar
            DrawTopBar("Sảnh Chờ Trực Tuyến");

            // Navigation Tabs
            Rect tabBar = new Rect(80, 115, 1760, 60);
            DrawSolidRect(tabBar, _cardColor);

            var net = PuzzleNetworkManager.Instance;
            string friendsTabLabel = net != null && net.PendingFriendRequests.Count > 0
                ? $"BẠN BÈ ({net.PendingFriendRequests.Count})"
                : "BẠN BÈ";

            if (DrawTabBtn(new Rect(90, 122, 280, 46), "PHÒNG CHƠI", _currentTab == MainTab.Rooms)) _currentTab = MainTab.Rooms;
            if (DrawTabBtn(new Rect(380, 122, 280, 46), friendsTabLabel, _currentTab == MainTab.Friends))
            {
                _currentTab = MainTab.Friends;
                net?.SendGetFriendRequests();
            }
            if (DrawTabBtn(new Rect(670, 122, 280, 46), "CỬA HÀNG", _currentTab == MainTab.Shop)) _currentTab = MainTab.Shop;
            if (DrawTabBtn(new Rect(960, 122, 280, 46), "HỒ SƠ CÁ NHÂN", _currentTab == MainTab.Profile)) _currentTab = MainTab.Profile;

            // Content based on selected tab
            switch (_currentTab)
            {
                case MainTab.Rooms: DrawLobbyRoomsTab(); break;
                case MainTab.Friends: DrawLobbyFriendsTab(); break;
                case MainTab.Shop: DrawLobbyShopTab(); break;
                case MainTab.Profile: DrawLobbyProfileTab(); break;
            }
        }

        private bool DrawTabBtn(Rect rect, string text, bool active)
        {
            if (DrawButton(rect, text, active))
            {
                PuzzleAudioService.Instance?.PlayClick();
                return true;
            }
            return false;
        }

        private void DrawLobbyRoomsTab()
        {
            var net = PuzzleNetworkManager.Instance;

            // Left: Create Room & Join by Code (Width 600)
            Rect leftPanel = new Rect(80, 195, 600, 810);
            GUI.Box(leftPanel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(110, 215, 540, 35), "Tạo Phòng Mới", _headerStyle);

            GUI.Label(new Rect(110, 255, 540, 20), "Tên phòng", _mutedStyle);
            _createRoomName = GUI.TextField(new Rect(110, 278, 540, 38), _createRoomName, _inputStyle);

            GUI.Label(new Rect(110, 322, 540, 20), "Số lượng người chơi", _mutedStyle);
            for (int i = 2; i <= 4; i++)
            {
                bool sel = _createMaxPlayers == i;
                if (DrawButton(new Rect(110 + (i - 2) * 185, 344, 170, 36), $"{i} Người", sel))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _createMaxPlayers = i;
                }
            }

            GUI.Label(new Rect(110, 386, 540, 20), "Chế độ phòng", _mutedStyle);
            if (DrawButton(new Rect(110, 408, 265, 36), "🌐 Công khai", !_createIsPrivate))
            {
                PuzzleAudioService.Instance?.PlayClick();
                _createIsPrivate = false;
            }
            if (DrawButton(new Rect(385, 408, 265, 36), "🔒 Có mật khẩu", _createIsPrivate))
            {
                PuzzleAudioService.Instance?.PlayClick();
                _createIsPrivate = true;
            }

            float currentY = 450f;
            if (_createIsPrivate)
            {
                GUI.Label(new Rect(110, currentY, 540, 20), "Mật khẩu phòng", _mutedStyle);
                currentY += 22;
                _createPassword = GUI.PasswordField(new Rect(110, currentY, 540, 36), _createPassword, '•', _inputStyle);
                currentY += 44;
            }

            GUI.Label(new Rect(110, currentY, 540, 20), "Thời gian mục tiêu", _mutedStyle);
            currentY += 22;
            int[] times = { 180, 300, 600, 0 };
            string[] timeLabels = { "3 Phút", "5 Phút", "10 Phút", "Vô hạn" };
            for (int t = 0; t < 4; t++)
            {
                bool selTime = _createTargetTime == times[t];
                if (DrawButton(new Rect(110 + t * 138, currentY, 126, 34), timeLabels[t], selTime))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _createTargetTime = times[t];
                }
            }
            currentY += 42;

            GUI.Label(new Rect(110, currentY, 540, 20), "Chọn bộ xếp hình", _mutedStyle);
            currentY += 22;
            DrawPuzzleSelector(new Rect(110, currentY, 540, 56));
            currentY += 64;

            GUI.Label(new Rect(110, currentY, 540, 20), "Chọn không gian 3D", _mutedStyle);
            currentY += 22;
            DrawThemeSelector(new Rect(110, currentY, 540, 56));
            currentY += 66;

            if (DrawButton(new Rect(110, currentY, 540, 48), "TẠO PHÒNG NGAY", true))
            {
                PuzzleAudioService.Instance?.PlayClick();
                string pwd = _createIsPrivate ? _createPassword.Trim() : "";
                if (_createIsPrivate && string.IsNullOrEmpty(pwd))
                {
                    ShowToast("Vui lòng nhập mật khẩu cho phòng.");
                }
                else
                {
                    net.CreateRoom(_createRoomName.Trim(), _createMaxPlayers, _selectedPuzzleId, _selectedThemeId, pwd, _createTargetTime);
                }
            }
            currentY += 56;

            // Quick Join by Code
            GUI.Label(new Rect(110, currentY, 540, 20), "Tham gia nhanh bằng mã phòng", _mutedStyle);
            currentY += 22;
            _joinCodeInput = GUI.TextField(new Rect(110, currentY, 360, 44), _joinCodeInput.ToUpper(), _inputStyle);
            if (DrawButton(new Rect(485, currentY, 165, 44), "Vào phòng", false))
            {
                PuzzleAudioService.Instance?.PlayClick();
                if (!string.IsNullOrWhiteSpace(_joinCodeInput))
                {
                    string targetCode = _joinCodeInput.Trim().ToUpper();
                    var found = net.LobbyRooms.Find(r => r.Id.Equals(targetCode, StringComparison.OrdinalIgnoreCase));
                    if (found != null && found.HasPassword)
                    {
                        _joiningPasswordRoom = found;
                        _joiningPasswordRoomId = found.Id;
                        _joinPasswordInput = "";
                    }
                    else
                    {
                        net.JoinRoom(targetCode, "");
                    }
                }
            }

            // Right: Public Rooms List (Width 1130)
            Rect rightPanel = new Rect(710, 195, 1130, 810);
            GUI.Box(rightPanel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(740, 220, 800, 40), "Danh Sách Phòng Đang Chờ", _headerStyle);

            if (DrawButton(new Rect(1680, 220, 130, 40), "Làm mới", false))
            {
                PuzzleAudioService.Instance?.PlayClick();
                net.RefreshLobby();
            }

            Rect listArea = new Rect(740, 280, 1070, 700);
            Rect viewArea = new Rect(0, 0, 1040, Mathf.Max(690, net.LobbyRooms.Count * 110));
            _roomListScroll = GUI.BeginScrollView(listArea, _roomListScroll, viewArea);

            if (net.LobbyRooms.Count == 0)
            {
                GUI.Label(new Rect(20, 20, 1000, 40), "Hiện tại chưa có phòng nào. Hãy tạo phòng mới bên trái!", _mutedStyle);
            }
            else
            {
                for (int i = 0; i < net.LobbyRooms.Count; i++)
                {
                    var room = net.LobbyRooms[i];
                    Rect rCard = new Rect(0, i * 110, 1040, 96);
                    DrawSolidRect(rCard, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                    GUI.Label(new Rect(20, rCard.y + 14, 300, 30), room.Name, _boldStyle);

                    // Badge: Public or Password
                    Rect badgeRect = new Rect(330, rCard.y + 17, 120, 24);
                    if (room.HasPassword)
                    {
                        DrawSolidRect(badgeRect, new Color(0.9f, 0.28f, 0.28f, 0.25f));
                        GUI.Label(badgeRect, "🔒 Có mật khẩu", _badgeStyle);
                    }
                    else
                    {
                        DrawSolidRect(badgeRect, new Color(0.24f, 0.75f, 0.45f, 0.25f));
                        GUI.Label(badgeRect, "🌐 Công khai", _badgeStyle);
                    }

                    GUI.Label(new Rect(20, rCard.y + 50, 400, 25), $"Chủ phòng: {room.Host}  •  Mã: {room.Id}", _mutedStyle);

                    GUI.Label(new Rect(480, rCard.y + 14, 240, 26), $"Bộ tranh: {GetPuzzleName(room.PuzzleId)}", _bodyStyle);
                    GUI.Label(new Rect(480, rCard.y + 40, 240, 24), $"Không gian: {GetThemeName(room.ThemeId)}", _mutedStyle);
                    GUI.Label(new Rect(480, rCard.y + 64, 240, 24), $"Mục tiêu: {FormatTimeLimit(room.TargetTimeSeconds)}", _mutedStyle);

                    GUI.Label(new Rect(730, rCard.y + 32, 130, 30), $"{room.Count}/{room.MaxPlayers} Người", _boldStyle);

                    string joinBtnText = room.HasPassword ? "Nhập Khóa" : "Tham Gia";
                    if (DrawButton(new Rect(880, rCard.y + 24, 140, 48), joinBtnText, true))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        if (room.HasPassword)
                        {
                            _joiningPasswordRoom = room;
                            _joiningPasswordRoomId = room.Id;
                            _joinPasswordInput = "";
                        }
                        else
                        {
                            net.JoinRoom(room.Id, "");
                        }
                    }
                }
            }
            GUI.EndScrollView();
        }

        private void DrawPuzzleSelector(Rect rect)
        {
            string[] ids = { "sunset_3x3", "forest_4x3", "ocean_4x4" };
            string[] names = { "Hoàng hôn\n(3×3)", "Rừng xanh\n(4×3)", "Đại dương\n(4×4)" };
            float h = rect.height > 0 ? rect.height : 70f;
            for (int i = 0; i < 3; i++)
            {
                bool owned = PuzzleNetworkManager.Instance.OwnedPuzzles.Contains(ids[i]);
                bool sel = _selectedPuzzleId == ids[i];
                if (DrawButton(new Rect(rect.x + i * 185, rect.y, 170, h), names[i], sel))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    if (owned) _selectedPuzzleId = ids[i];
                    else ShowToast("Bạn chưa sở hữu bộ tranh này. Hãy mua tại Cửa hàng!");
                }
            }
        }

        private void DrawThemeSelector(Rect rect)
        {
            string[] ids = { "SCN_Nature", "SCN_Classroom", "SCN_Sakura" };
            string[] names = { "Thiên nhiên\n(SCN_Nature)", "Phòng học\n(SCN_Classroom)", "Hoa anh đào\n(SCN_Sakura)" };
            float h = rect.height > 0 ? rect.height : 70f;
            for (int i = 0; i < 3; i++)
            {
                bool owned = PuzzleNetworkManager.Instance.OwnedThemes.Contains(ids[i]);
                bool sel = _selectedThemeId == ids[i];
                if (DrawButton(new Rect(rect.x + i * 185, rect.y, 170, h), names[i], sel))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    if (owned) _selectedThemeId = ids[i];
                    else ShowToast("Bạn chưa mở khóa Không gian 3D này. Hãy vào Cửa hàng!");
                }
            }
        }

        private void DrawLobbyFriendsTab()
        {
            var net = PuzzleNetworkManager.Instance;
            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, _cardStyle);

            GUI.Label(new Rect(120, 225, 600, 40), "Danh Sách Bạn Bè & Lời Mời", _headerStyle);

            // Add friend row
            GUI.Label(new Rect(1100, 230, 200, 30), "Thêm bạn mới:", _mutedStyle);
            _addFriendInput = GUI.TextField(new Rect(1250, 225, 340, 44), _addFriendInput, _inputStyle);
            if (GUI.Button(new Rect(1610, 225, 190, 44), "Gửi lời mời", _primaryBtnStyle))
            {
                PuzzleAudioService.Instance?.PlayClick();
                if (!string.IsNullOrWhiteSpace(_addFriendInput))
                {
                    net.SendAddFriend(_addFriendInput.Trim());
                    _addFriendInput = "";
                }
            }

            int pendingCount = net.PendingFriendRequests.Count;
            int friendsCount = net.Friends.Count;

            int pendingHeight = pendingCount > 0 ? (50 + pendingCount * 85 + 20) : 0;
            int friendsHeight = 50 + Mathf.Max(1, friendsCount) * 85;
            int totalContentHeight = pendingHeight + friendsHeight + 40;

            Rect listArea = new Rect(120, 295, 1680, 680);
            Rect viewArea = new Rect(0, 0, 1650, Mathf.Max(670, totalContentHeight));
            _friendListScroll = GUI.BeginScrollView(listArea, _friendListScroll, viewArea);

            float currentY = 10;

            // --- 1. LỜI MỜI ĐANG CHỜ DUYỆT ---
            if (pendingCount > 0)
            {
                GUI.Label(new Rect(20, currentY, 800, 35), $"Lời Mời Kết Bạn Đang Chờ Duyệt ({pendingCount})", _boldStyle);
                currentY += 40;

                for (int i = 0; i < pendingCount; i++)
                {
                    var req = net.PendingFriendRequests[i];
                    Rect r = new Rect(0, currentY, 1650, 76);
                    DrawSolidRect(r, new Color(0.20f, 0.23f, 0.32f, 0.95f));

                    // Avatar icon dot (yellow pending)
                    DrawSolidRect(new Rect(20, r.y + 30, 16, 16), new Color(0.95f, 0.77f, 0.05f));

                    GUI.Label(new Rect(50, r.y + 16, 400, 30), req.SenderUsername, _boldStyle);
                    GUI.Label(new Rect(50, r.y + 44, 500, 25), $"Đã gửi lúc: {req.CreatedAt}", _mutedStyle);

                    // Accept Button
                    if (GUI.Button(new Rect(1310, r.y + 16, 150, 44), "Chấp nhận", _primaryBtnStyle))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SendAcceptFriendRequest(req.RequestId);
                    }

                    // Reject Button
                    if (GUI.Button(new Rect(1480, r.y + 16, 150, 44), "Từ chối", _btnStyle))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SendRejectFriendRequest(req.RequestId);
                    }

                    currentY += 85;
                }

                currentY += 20;
            }

            // --- 2. DANH SÁCH BẠN BÈ HIỆN TẠI ---
            GUI.Label(new Rect(20, currentY, 800, 35), $"Bạn Bè Hiện Tại ({friendsCount})", _boldStyle);
            currentY += 40;

            if (friendsCount == 0)
            {
                GUI.Label(new Rect(20, currentY + 10, 1000, 35), "Chưa có bạn bè nào. Hãy nhập tên tài khoản ở góc trên bên phải để gửi lời mời!", _mutedStyle);
            }
            else
            {
                for (int i = 0; i < friendsCount; i++)
                {
                    var f = net.Friends[i];
                    Rect r = new Rect(0, currentY, 1650, 76);
                    DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                    Color statusCol = f.Online ? _successColor : _mutedColor;
                    DrawSolidRect(new Rect(20, r.y + 30, 16, 16), statusCol);

                    GUI.Label(new Rect(50, r.y + 20, 400, 35), f.Username, _boldStyle);
                    GUI.Label(new Rect(500, r.y + 24, 400, 30), f.Online ? $"Trực tuyến ({f.Status})" : "Ngoại tuyến", _mutedStyle);

                    if (net.CurrentRoom != null)
                    {
                        GUI.enabled = f.Online && f.Status == "IDLE";
                        if (GUI.Button(new Rect(1480, r.y + 16, 150, 44), "Mời vào phòng", _btnStyle))
                        {
                            PuzzleAudioService.Instance?.PlayClick();
                            net.SendInvite(f.Username);
                        }
                        GUI.enabled = true;
                    }

                    currentY += 85;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawLobbyShopTab()
        {
            var net = PuzzleNetworkManager.Instance;
            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, _cardStyle);

            GUI.Label(new Rect(120, 225, 800, 40), "Cửa Hàng Trò Chơi", _headerStyle);
            GUI.Label(new Rect(1450, 230, 350, 35), $"Số xu hiện có: {net.Coins} xu", _boldStyle);

            Rect listArea = new Rect(120, 280, 1680, 700);
            Rect viewArea = new Rect(0, 0, 1650, 800);
            _shopScroll = GUI.BeginScrollView(listArea, _shopScroll, viewArea);

            // Puzzles Section
            GUI.Label(new Rect(0, 10, 600, 35), "1. BỘ TRANH GHÉP (PUZZLES)", _boldStyle);
            for (int i = 0; i < net.ShopPuzzles.Count; i++)
            {
                var p = net.ShopPuzzles[i];
                Rect r = new Rect(0, 50 + i * 95, 1650, 82);
                DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                GUI.Label(new Rect(25, r.y + 18, 400, 30), p.Name, _boldStyle);
                GUI.Label(new Rect(25, r.y + 48, 400, 25), $"Kích thước: {p.Rows}x{p.Columns} mảnh  •  Thời gian: {p.Seconds}s", _mutedStyle);

                if (p.Owned)
                {
                    GUI.Label(new Rect(1450, r.y + 26, 180, 35), "✓ ĐÃ SỞ HỮU", _boldStyle);
                }
                else
                {
                    GUI.Label(new Rect(1250, r.y + 26, 180, 35), $"{p.Price} xu", _boldStyle);
                    GUI.enabled = net.Coins >= p.Price;
                    if (DrawButton(new Rect(1450, r.y + 18, 180, 48), "MUA NGAY", false, true))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SendBuy("puzzle", p.Id);
                    }
                    GUI.enabled = true;
                }
            }

            // 3D Environments Section
            float themeY = 60 + net.ShopPuzzles.Count * 95 + 20;
            GUI.Label(new Rect(0, themeY, 800, 35), "2. KHÔNG GIAN PHÒNG CHƠI 3D (3D ENVIRONMENTS)", _boldStyle);
            for (int i = 0; i < net.ShopThemes.Count; i++)
            {
                var t = net.ShopThemes[i];
                Rect r = new Rect(0, themeY + 40 + i * 95, 1650, 82);
                DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                GUI.Label(new Rect(25, r.y + 18, 400, 30), t.Name, _boldStyle);
                GUI.Label(new Rect(25, r.y + 48, 400, 25), $"Scene: {t.Id}  •  Không gian trải nghiệm 3D độc quyền", _mutedStyle);

                if (t.Owned)
                {
                    GUI.Label(new Rect(1450, r.y + 26, 180, 35), "✓ ĐÃ SỞ HỮU", _boldStyle);
                }
                else
                {
                    GUI.Label(new Rect(1250, r.y + 26, 180, 35), $"{t.Price} xu", _boldStyle);
                    GUI.enabled = net.Coins >= t.Price;
                    if (DrawButton(new Rect(1450, r.y + 18, 180, 48), "MỞ KHÓA", false, true))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SendBuy("theme", t.Id);
                    }
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawLobbyProfileTab()
        {
            var net = PuzzleNetworkManager.Instance;
            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, _cardStyle);

            GUI.Label(new Rect(120, 225, 800, 40), "Hồ Sơ & Thống Kê Người Chơi", _headerStyle);

            // 4 Stats Cards
            DrawStatCard(new Rect(120, 300, 380, 180), "TỔNG ĐIỂM", net.TotalScore.ToString(), "Điểm cộng dồn từ việc ghép đúng mảnh (+10/mảnh)");
            DrawStatCard(new Rect(540, 300, 380, 180), "BỘ TRANH SỞ HỮU", $"{net.OwnedPuzzles.Count}/3", "Các bộ tranh Hoàng hôn, Rừng xanh, Đại dương");
            DrawStatCard(new Rect(960, 300, 380, 180), "KHÔNG GIAN 3D", $"{net.OwnedThemes.Count}/3", "Các môi trường: Thiên nhiên, Phòng học, Sakura");
            DrawStatCard(new Rect(1380, 300, 380, 180), "BỘ TRANH HOÀN THÀNH", net.UniquePuzzlesCompleted.ToString(), "Số ván chơi đã ghép trọn vẹn bức tranh cùng đồng đội");

            // Additional details card
            Rect infoCard = new Rect(120, 520, 1640, 440);
            DrawSolidRect(infoCard, new Color(0.16f, 0.19f, 0.26f, 0.9f));
            GUI.Label(new Rect(160, 550, 800, 35), "Thông Tin Tài Khoản", _boldStyle);
            GUI.Label(new Rect(160, 600, 500, 30), $"Tên tài khoản: {net.Username}", _bodyStyle);
            GUI.Label(new Rect(160, 640, 500, 30), $"Số xu tích lũy: {net.Coins} xu", _bodyStyle);
            GUI.Label(new Rect(160, 680, 500, 30), $"Trạng thái: Trực tuyến (Sẵn sàng)", _bodyStyle);
            GUI.Label(new Rect(160, 720, 800, 30), $"Phiên kết nối: {net.SessionToken}", _mutedStyle);
        }

        private void DrawStatCard(Rect rect, string title, string val, string sub)
        {
            DrawSolidRect(rect, new Color(0.16f, 0.19f, 0.26f, 0.9f));
            GUI.Label(new Rect(rect.x + 25, rect.y + 20, rect.width - 50, 25), title, _mutedStyle);
            GUI.Label(new Rect(rect.x + 25, rect.y + 55, rect.width - 50, 50), val, _titleStyle);
            GUI.Label(new Rect(rect.x + 25, rect.y + 115, rect.width - 50, 50), sub, _mutedStyle);
        }
        #endregion

        #region 3. Waiting Room Screen
        private void DrawWaitingRoomScreen()
        {
            var net = PuzzleNetworkManager.Instance;
            var room = net.CurrentRoom;
            if (room == null) return;

            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), _bgColor);
            DrawTopBar($"Phòng Chờ: {room.Name} (Mã: {room.Id})");

            bool isHost = room.Host == net.Username;

            // Left: Room Settings & Host Controls (Width 520)
            Rect leftPanel = new Rect(80, 115, 520, 890);
            GUI.Box(leftPanel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(110, 140, 460, 35), "Thông Tin Ván Chơi", _headerStyle);
            GUI.Label(new Rect(110, 175, 460, 20), $"Chủ phòng: {room.Host}  {(isHost ? "(Bạn là Host)" : "")}", _mutedStyle);
            GUI.Label(new Rect(110, 196, 460, 20), $"Chế độ: {(room.HasPassword ? "🔒 Riêng tư (Có mật khẩu)" : "🌐 Công khai")}", _mutedStyle);
            GUI.Label(new Rect(110, 217, 460, 20), $"Thời gian mục tiêu: {FormatTimeLimit(room.TargetTimeSeconds)}", _mutedStyle);

            if (isHost)
            {
                int[] times = { 180, 300, 600, 0 };
                string[] timeLabels = { "3 Phút", "5 Phút", "10 Phút", "Vô hạn" };
                for (int t = 0; t < 4; t++)
                {
                    bool selTime = room.TargetTimeSeconds == times[t];
                    if (DrawButton(new Rect(110 + t * 118, 238, 106, 30), timeLabels[t], selTime))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SelectTimeLimit(times[t]);
                    }
                }
            }

            float puzzleY = isHost ? 275f : 242f;
            GUI.Label(new Rect(110, puzzleY, 460, 20), "Bộ xếp hình đang chọn:", _mutedStyle);
            GUI.Label(new Rect(110, puzzleY + 20, 460, 26), GetPuzzleName(room.PuzzleId), _boldStyle);

            if (isHost)
            {
                string[] pIds = { "sunset_3x3", "forest_4x3", "ocean_4x4" };
                for (int i = 0; i < 3; i++)
                {
                    bool owned = net.OwnedPuzzles.Contains(pIds[i]);
                    bool active = room.PuzzleId == pIds[i];
                    GUI.enabled = owned;
                    if (DrawButton(new Rect(110, puzzleY + 48 + i * 46, 460, 40), GetPuzzleName(pIds[i]) + (owned ? "" : " (Chưa mở)"), active))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SelectPuzzle(pIds[i]);
                    }
                    GUI.enabled = true;
                }
            }

            // Environment selection
            float envY = isHost ? 472f : 295f;
            GUI.Label(new Rect(110, envY, 460, 20), "Không gian 3D (3D Environment):", _mutedStyle);
            GUI.Label(new Rect(110, envY + 20, 460, 26), GetThemeName(room.ThemeId), _boldStyle);

            if (isHost)
            {
                string[] tIds = { "SCN_Nature", "SCN_Classroom", "SCN_Sakura" };
                for (int i = 0; i < 3; i++)
                {
                    bool owned = net.OwnedThemes.Contains(tIds[i]);
                    bool active = room.ThemeId == tIds[i];
                    GUI.enabled = owned;
                    if (DrawButton(new Rect(110, envY + 48 + i * 46, 460, 40), GetThemeName(tIds[i]) + (owned ? "" : " (Chưa mở)"), active))
                    {
                        PuzzleAudioService.Instance?.PlayClick();
                        net.SelectTheme(tIds[i]);
                    }
                    GUI.enabled = true;
                }
            }

            // Ready & Start Game buttons
            var me = net.RoomPlayers.Find(p => p.Username == net.Username);
            bool ready = me != null && me.Ready;
            if (DrawButton(new Rect(110, 800, 460, 56), ready ? "✓ ĐÃ SẴN SÀNG" : "SẴN SÀNG", ready))
            {
                PuzzleAudioService.Instance?.PlayClick();
                net.SetReady(!ready);
            }

            if (isHost)
            {
                if (DrawButton(new Rect(110, 870, 460, 60), "BẮT ĐẦU VÁN CHƠI", true))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    net.StartGame();
                }
            }

            if (DrawButton(new Rect(110, 945, 460, 44), "Rời phòng", false, false, true))
            {
                PuzzleAudioService.Instance?.PlayClick();
                net.LeaveRoom();
            }

            // Middle: 4 Chairs / Players (Width 640)
            Rect midPanel = new Rect(630, 115, 640, 890);
            GUI.Box(midPanel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(660, 140, 580, 35), $"Thành Viên ({net.RoomPlayers.Count}/{room.MaxPlayers})", _headerStyle);

            for (int i = 0; i < 4; i++)
            {
                Rect slotRect = new Rect(660, 200 + i * 140, 580, 120);
                DrawSolidRect(slotRect, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                if (i < net.RoomPlayers.Count)
                {
                    var p = net.RoomPlayers[i];
                    Color statCol = p.Connected ? (p.Ready ? _successColor : _goldColor) : _dangerColor;
                    DrawSolidRect(new Rect(685, slotRect.y + 35, 16, 16), statCol);

                    GUI.Label(new Rect(720, slotRect.y + 25, 300, 35), p.Username + (p.IsHost ? " ★ (Chủ phòng)" : ""), _boldStyle);
                    GUI.Label(new Rect(720, slotRect.y + 65, 300, 25), p.Connected ? (p.Ready ? "Đã sẵn sàng" : "Đang chờ sẵn sàng") : "Mất kết nối", _mutedStyle);

                    GUI.Label(new Rect(1080, slotRect.y + 40, 140, 35), p.Ready ? "SẴN SÀNG" : "CHỜ", p.Ready ? _badgeStyle : _mutedStyle);
                }
                else
                {
                    GUI.Label(new Rect(720, slotRect.y + 45, 300, 35), "Ghế trống (Đang chờ người chơi...)", _mutedStyle);
                }
            }

            // Right: Chat & Friends (Width 530)
            Rect rightPanel = new Rect(1300, 115, 540, 890);
            GUI.Box(rightPanel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(1330, 140, 480, 35), "Trò Chuyện & Bạn Bè", _headerStyle);
            GUI.Label(new Rect(1330, 180, 480, 25), "Nhấn giữ phím 'V' để nói Voice Chat", _mutedStyle);

            DrawChatBox(new Rect(1330, 220, 480, 680), ref _waitingRoomChatScroll);
        }

        private void DrawChatBox(Rect rect, ref Vector2 scroll)
        {
            Rect logRect = new Rect(rect.x, rect.y, rect.width, rect.height - 80);
            DrawSolidRect(logRect, new Color(0.10f, 0.11f, 0.16f, 0.9f));

            Rect viewRect = new Rect(0, 0, rect.width - 30, Mathf.Max(logRect.height, _chatHistory.Count * 28));
            scroll = GUI.BeginScrollView(logRect, scroll, viewRect);
            for (int i = 0; i < _chatHistory.Count; i++)
            {
                GUI.Label(new Rect(10, i * 28 + 4, rect.width - 40, 25), _chatHistory[i], _bodyStyle);
            }
            GUI.EndScrollView();

            // Emoji Bar
            string[] emojis = { "😊", "👍", "🎉", "🔥", "❤️", "👏" };
            for (int e = 0; e < emojis.Length; e++)
            {
                if (GUI.Button(new Rect(rect.x + e * 42, rect.y + rect.height - 75, 38, 30), emojis[e], _btnStyle))
                {
                    PuzzleNetworkManager.Instance?.SendEmoji(emojis[e]);
                }
            }

            // Input Row
            _chatInput = GUI.TextField(new Rect(rect.x, rect.y + rect.height - 38, rect.width - 90, 38), _chatInput, _inputStyle);
            if (GUI.Button(new Rect(rect.x + rect.width - 80, rect.y + rect.height - 38, 80, 38), "Gửi", _primaryBtnStyle) ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUIUtility.keyboardControl != 0))
            {
                if (!string.IsNullOrWhiteSpace(_chatInput))
                {
                    PuzzleNetworkManager.Instance?.SendChat(_chatInput.Trim());
                    _chatInput = "";
                }
            }
        }
        #endregion

        #region 4. In-Game HUD
        private void DrawInGameHUD()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            // Top Bar HUD
            Rect topHud = new Rect(40, 20, 1840, 75);
            DrawSolidRect(topHud, new Color(0.10f, 0.12f, 0.18f, 0.88f));

            // Room Name & Environment
            GUI.Label(new Rect(70, 28, 450, 30), $"Phòng: {net.CurrentRoom?.Name}", _boldStyle);
            GUI.Label(new Rect(70, 58, 450, 25), $"Không gian: {GetThemeName(net.CurrentThemeId)}", _mutedStyle);

            // Progress Bar
            float pct = net.TotalPieces <= 0 ? 0f : (float)net.PlacedPieces / net.TotalPieces;
            Rect progBg = new Rect(550, 35, 750, 26);
            DrawSolidRect(progBg, new Color(0.18f, 0.22f, 0.30f, 1f));
            DrawSolidRect(new Rect(550, 35, 750 * pct, 26), _accentColor);
            GUI.Label(new Rect(550, 35, 750, 26), $"{net.PlacedPieces}/{net.TotalPieces} Mảnh ({Mathf.RoundToInt(pct * 100)}%)", _badgeStyle);

            // Timer
            if (net.RemainingSeconds < 0)
            {
                GUI.Label(new Rect(1330, 30, 160, 45), "∞ Vô hạn", _titleStyle);
            }
            else
            {
                int m = net.RemainingSeconds / 60;
                int s = net.RemainingSeconds % 60;
                GUI.Label(new Rect(1330, 30, 160, 45), $"{m:00}:{s:00}", _titleStyle);
            }

            // Voice Status Indicator
            if (net.Voice != null && net.Voice.IsTransmitting)
            {
                DrawSolidRect(new Rect(1510, 38, 14, 14), _successColor);
                GUI.Label(new Rect(1535, 34, 160, 30), "MIC ĐANG BẬT (V)", _boldStyle);
            }
            else
            {
                GUI.Label(new Rect(1510, 34, 160, 30), "Giữ [V] để nói", _mutedStyle);
            }

            // Quick Menu button (Leave / Save)
            if (DrawButton(new Rect(1690, 32, 160, 44), "Rời phòng", false, false, true))
            {
                PuzzleAudioService.Instance?.PlayClick();
                net.LeaveRoom();
            }

            // Player Scoreboard Panel (Top Left)
            Rect scorePanel = new Rect(40, 110, 320, 50 + net.RoomPlayers.Count * 45);
            DrawSolidRect(scorePanel, new Color(0.10f, 0.12f, 0.18f, 0.85f));
            GUI.Label(new Rect(55, 120, 290, 28), "BẢNG ĐIỂM", _boldStyle);
            for (int i = 0; i < net.RoomPlayers.Count; i++)
            {
                var p = net.RoomPlayers[i];
                GUI.Label(new Rect(55, 155 + i * 45, 190, 30), p.Username + (p.IsHost ? " ★" : ""), _bodyStyle);
                GUI.Label(new Rect(250, 155 + i * 45, 90, 30), $"{p.Score} đ", _boldStyle);
            }

            // Bottom Right Chat Drawer
            if (_showInGameChat)
            {
                Rect chatPanel = new Rect(1360, 600, 520, 440);
                GUI.Box(chatPanel, GUIContent.none, _cardStyle);
                GUI.Label(new Rect(1380, 615, 300, 30), "Trò chuyện trong ván [T]", _boldStyle);
                DrawChatBox(new Rect(1380, 650, 480, 370), ref _inGameChatScroll);
            }

            else
            {
                if (DrawButton(new Rect(1720, 1010, 160, 44), "Chat [T]", false))
                {
                    _showInGameChat = true;
                }
            }

            if (_showInGameMenu)
            {
                DrawInGamePauseMenu(net);
            }

            if (!_showInGameMenu)
            {
                // Screen Center Reticle / White Dot
                float cx = VirtualWidth * 0.5f;
                float cy = VirtualHeight * 0.5f;
                DrawSolidRect(new Rect(cx - 5, cy - 5, 10, 10), new Color(0f, 0f, 0f, 0.7f));
                DrawSolidRect(new Rect(cx - 3, cy - 3, 6, 6), Color.white);

                // Controls Helper Bar (Bottom Center)
                Rect ctrlBar = new Rect(VirtualWidth * 0.5f - 460, 1015, 920, 40);
                DrawSolidRect(ctrlBar, new Color(0.10f, 0.12f, 0.18f, 0.85f));
                GUI.Label(ctrlBar, "⚡ [Z] Giữ xem từ trên cao (Top-down)  •  [R / Cuộn chuột] Xoay 90°  •  [Chuột trái] Cầm / Thả  •  [V] Voice Chat", _badgeStyle);
            }
        }

        private void DrawInGamePauseMenu(PuzzleNetworkManager net)
        {
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), new Color(0f, 0f, 0f, 0.55f));

            Rect panel = new Rect(VirtualWidth * 0.5f - 240f, VirtualHeight * 0.5f - 145f, 480f, 290f);
            GUI.Box(panel, GUIContent.none, _cardStyle);
            GUI.Label(new Rect(panel.x + 40, panel.y + 32, panel.width - 80, 42), "TẠM DỪNG", _headerStyle);
            GUI.Label(new Rect(panel.x + 40, panel.y + 80, panel.width - 80, 28), "Nhấn Esc để tiếp tục chơi", _mutedStyle);

            if (DrawButton(new Rect(panel.x + 40, panel.y + 125, panel.width - 80, 48), "TIẾP TỤC", true))
            {
                _showInGameMenu = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (DrawButton(new Rect(panel.x + 40, panel.y + 185, panel.width - 80, 48), "RỜI PHÒNG", false, false, true))
            {
                _showInGameMenu = false;
                PuzzleAudioService.Instance?.PlayClick();
                net.LeaveRoom();
            }
        }
        #endregion

        #region Helper Renderers
        private void DrawTopBar(string title)
        {
            var net = PuzzleNetworkManager.Instance;
            Rect topRect = new Rect(80, 30, 1760, 65);
            DrawSolidRect(topRect, _cardColor);

            GUI.Label(new Rect(110, 42, 600, 40), title, _headerStyle);

            // User coins & name
            GUI.Label(new Rect(1180, 45, 400, 35), $"Tài khoản: {net.Username}  |  {net.Coins} xu", _boldStyle);

            if (DrawButton(new Rect(1650, 40, 160, 45), "Đăng xuất", false, false, true))
            {
                PuzzleAudioService.Instance?.PlayClick();
                net.Logout();
            }
        }

        private void DrawToast()
        {
            if (_toastTimer <= 0f || string.IsNullOrEmpty(_toastMessage)) return;
            Rect toastRect = new Rect(VirtualWidth * 0.5f - 300, 950, 600, 54);
            DrawSolidRect(toastRect, new Color(0.12f, 0.14f, 0.20f, 0.96f));
            GUI.Label(toastRect, _toastMessage, _badgeStyle);
        }

        private void DrawVictoryModal()
        {
            if (!_showVictoryModal) return;
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), new Color(0f, 0f, 0f, 0.75f));

            Rect modal = new Rect(VirtualWidth * 0.5f - 400, VirtualHeight * 0.5f - 240, 800, 480);
            GUI.Box(modal, GUIContent.none, _cardStyle);

            GUI.Label(new Rect(modal.x + 50, modal.y + 50, 700, 60), "🎉 HOÀN THÀNH XUẤT SẮC! 🎉", _titleStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 130, 700, 40), "Bức tranh puzzle đã được ghép trọn vẹn!", _headerStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 190, 700, 40), $"Người chiến thắng / đóng góp nhiều nhất: {_victoryWinner}", _boldStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 250, 700, 40), $"Thưởng hoàn thành: +{_victoryBonus} xu vào tài khoản!", _goldColor == Color.yellow ? _bodyStyle : _boldStyle);

            if (GUI.Button(new Rect(modal.x + 250, modal.y + 350, 300, 65), "QUAY VỀ SẢNH", _primaryBtnStyle))
            {
                _showVictoryModal = false;
                PuzzleNetworkManager.Instance?.LeaveRoom();
            }
        }

        private void DrawReconnectModal()
        {
            if (_reconnectPrompt == null) return;

            // Nền tối mờ phủ toàn màn hình
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), new Color(0f, 0f, 0f, 0.82f));

            // Hộp thoại BoxMessage căn giữa màn hình (Rộng 720, Cao 430)
            Rect cardRect = new Rect((VirtualWidth - 720) / 2f, (VirtualHeight - 430) / 2f, 720, 430);
            DrawSolidRect(cardRect, _cardColor);
            DrawBorder(cardRect, 2f, _accentColor);

            // Tiêu đề hộp thoại
            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 35, 640, 40), "THÔNG BÁO KẾT NỐI LẠI", _headerStyle);
            DrawSolidRect(new Rect(cardRect.x + 40, cardRect.y + 85, 640, 2), new Color(0.3f, 0.35f, 0.45f, 0.6f));

            // Nội dung chi tiết phòng chơi dở dang
            string stateText = _reconnectPrompt.State == "PLAYING" ? "Đang diễn ra ván chơi" : "Đang chờ trong phòng";
            string msg = $"Hệ thống phát hiện bạn có một phòng chơi trước đó chưa hoàn thành:\n\n" +
                         $"  • Tên phòng: {_reconnectPrompt.RoomName}\n" +
                         $"  • Thành viên: {_reconnectPrompt.Count}/{_reconnectPrompt.Max}\n" +
                         $"  • Trạng thái: {stateText}\n\n" +
                         $"Bạn có muốn quay lại phòng này để tiếp tục không?";
            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 110, 640, 190), msg, _bodyStyle);

            // Nút 1: Đồng ý quay lại phòng
            if (DrawButton(new Rect(cardRect.x + 40, cardRect.y + 330, 300, 58), "QUAY LẠI PHÒNG", true, false, false, 18))
            {
                PuzzleAudioService.Instance?.PlayClick();
                string rid = _reconnectPrompt.RoomId;
                _reconnectPrompt = null;
                PuzzleNetworkManager.Instance?.SendReconnectDecision(true, rid);
            }

            // Nút 2: Từ chối / Ở lại trang chủ
            if (DrawButton(new Rect(cardRect.x + 380, cardRect.y + 330, 300, 58), "Ở LẠI TRANG CHỦ", false, false, true, 18))
            {
                PuzzleAudioService.Instance?.PlayClick();
                string rid = _reconnectPrompt.RoomId;
                _reconnectPrompt = null;
                PuzzleNetworkManager.Instance?.SendReconnectDecision(false, rid);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (SceneManager.GetActiveScene().name != "SCN_MainMenu")
                {
                    SceneManager.LoadScene("SCN_MainMenu");
                }
            }
        }

        private void DrawTimeoutModal()
        {
            if (!_showTimeoutModal) return;

            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive) return;

            // Nền mờ tối phủ toàn màn hình
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), new Color(0f, 0f, 0f, 0.85f));

            // Hộp thoại căn giữa (Rộng 760, Cao 460)
            Rect cardRect = new Rect((VirtualWidth - 760) / 2f, (VirtualHeight - 460) / 2f, 760, 460);
            GUI.Box(cardRect, GUIContent.none, _cardStyle);
            DrawBorder(cardRect, 2f, _goldColor);

            // Tiêu đề
            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 35, 660, 45), "⏰ HẾT THỜI GIAN MỤC TIÊU ⏰", _titleStyle);
            DrawSolidRect(new Rect(cardRect.x + 50, cardRect.y + 88, 660, 2), new Color(0.35f, 0.4f, 0.5f, 0.6f));

            // Thông báo và tiến độ hoàn thành
            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 105, 660, 32), "Thời gian đặt ra cho màn chơi đã kết thúc!", _headerStyle);

            float percent = net.TotalPieces > 0 ? (float)net.PlacedPieces / net.TotalPieces * 100f : 0f;
            string statsText = $"  • Tiến độ hiện tại: {net.PlacedPieces}/{net.TotalPieces} mảnh ghép ({percent:F0}%)\n" +
                               $"  • Bộ tranh đang ghép: {GetPuzzleName(net.CurrentPuzzleId)}\n" +
                               $"  • Bối cảnh: {GetThemeName(net.CurrentThemeId)}";
            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 145, 660, 80), statsText, _bodyStyle);

            if (net.IsHost)
            {
                GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 235, 660, 45),
                    "Bạn là chủ phòng. Hãy lựa chọn tiếp tục chơi (chuyển sang chế độ thư giãn không giới hạn thời gian) hoặc khởi đầu lại ván chơi mới:",
                    _mutedStyle);

                // Nút 1: Tiếp tục chơi (Primary / Xanh)
                if (DrawButton(new Rect(cardRect.x + 50, cardRect.y + 295, 315, 58), "TIẾP TỤC CHƠI (THƯ GIÃN)", true, false, false, 16))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _showTimeoutModal = false;
                    net.SendContinueGame();
                }

                // Nút 2: Khởi đầu lại (Gold / Vàng)
                if (DrawButton(new Rect(cardRect.x + 395, cardRect.y + 295, 315, 58), "KHỞI ĐẦU LẠI", false, true, false, 17))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _showTimeoutModal = false;
                    net.SendRestartGame();
                }

                // Nút 3: Rời phòng (Danger / Đỏ)
                if (DrawButton(new Rect(cardRect.x + 220, cardRect.y + 375, 320, 50), "RỜI PHÒNG", false, false, true, 16))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _showTimeoutModal = false;
                    net.LeaveRoom();
                }
            }
            else
            {
                GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 250, 660, 50),
                    "Đang chờ chủ phòng lựa chọn tiếp tục chơi hoặc khởi đầu lại ván chơi mới...",
                    _mutedStyle);

                // Nút: Rời phòng (Danger / Đỏ)
                if (DrawButton(new Rect(cardRect.x + 220, cardRect.y + 340, 320, 55), "RỜI PHÒNG", false, false, true, 17))
                {
                    PuzzleAudioService.Instance?.PlayClick();
                    _showTimeoutModal = false;
                    net.LeaveRoom();
                }
            }
        }

        private void DrawPasswordModal()
        {
            if (_joiningPasswordRoom == null && string.IsNullOrEmpty(_joiningPasswordRoomId)) return;

            // Semi-transparent overlay
            DrawSolidRect(new Rect(0, 0, VirtualWidth, VirtualHeight), new Color(0.04f, 0.05f, 0.08f, 0.85f));

            Rect cardRect = new Rect((VirtualWidth - 620) / 2f, (VirtualHeight - 340) / 2f, 620, 340);
            GUI.Box(cardRect, GUIContent.none, _cardStyle);
            DrawBorder(cardRect, 2, _accentColor);

            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 30, 540, 35), "PHÒNG CÓ MẬT KHẨU", _headerStyle);
            DrawSolidRect(new Rect(cardRect.x + 40, cardRect.y + 75, 540, 2), new Color(0.3f, 0.35f, 0.45f, 0.6f));

            string roomName = _joiningPasswordRoom != null ? _joiningPasswordRoom.Name : ("Phòng #" + _joiningPasswordRoomId);
            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 95, 540, 25), $"Vui lòng nhập mật khẩu để tham gia: {roomName}", _bodyStyle);

            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 135, 540, 22), "Mật khẩu phòng:", _mutedStyle);
            _joinPasswordInput = GUI.PasswordField(new Rect(cardRect.x + 40, cardRect.y + 162, 540, 46), _joinPasswordInput, '•', _inputStyle);

            // Nút 1: Vào phòng
            if (DrawButton(new Rect(cardRect.x + 40, cardRect.y + 245, 255, 54), "VÀO PHÒNG", true, false, false, 16))
            {
                PuzzleAudioService.Instance?.PlayClick();
                string rid = _joiningPasswordRoom != null ? _joiningPasswordRoom.Id : _joiningPasswordRoomId;
                string pwd = _joinPasswordInput;
                _joiningPasswordRoom = null;
                _joiningPasswordRoomId = "";
                _joinPasswordInput = "";
                PuzzleNetworkManager.Instance?.JoinRoom(rid, pwd);
            }

            // Nút 2: Hủy
            if (DrawButton(new Rect(cardRect.x + 325, cardRect.y + 245, 255, 54), "HỦY BỎ", false, false, true, 16))
            {
                PuzzleAudioService.Instance?.PlayClick();
                _joiningPasswordRoom = null;
                _joiningPasswordRoomId = "";
                _joinPasswordInput = "";
            }
        }

        private static string GetPuzzleName(string puzzleId)
        {
            switch (puzzleId)
            {
                case "sunset_3x3": return "Hoàng hôn (3×3)";
                case "forest_4x3": return "Rừng xanh (4×3)";
                case "ocean_4x4": return "Đại dương (4×4)";
                default: return puzzleId ?? "";
            }
        }

        private static string GetThemeName(string themeId)
        {
            switch (themeId)
            {
                case "SCN_Nature": return "Thiên nhiên xanh mát (SCN_Nature)";
                case "SCN_Classroom": return "Phòng học cổ điển (SCN_Classroom)";
                case "SCN_Sakura": return "Vườn hoa anh đào (SCN_Sakura)";
                default: return themeId ?? "";
            }
        }

        private static string FormatTimeLimit(int seconds)
        {
            if (seconds <= 0) return "Không giới hạn (Thư giãn)";
            int m = seconds / 60;
            return $"{m} phút ({seconds}s)";
        }
        #endregion
    }
}
