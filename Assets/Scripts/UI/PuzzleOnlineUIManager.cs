using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Utility;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều phối viên giao diện người dùng (UI Coordinator) cho Puzzle Online.
    /// Quản lý phân giải ảo 1920x1080, nhận sự kiện mạng và ủy quyền hiển thị cho các View chuyên trách.
    /// Triển khai IUIService để hỗ trợ Dependency Injection.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class PuzzleOnlineUIManager : MonoBehaviour, IUIService
    {
        public static PuzzleOnlineUIManager Instance { get; private set; }

        private readonly UIStyles _styles = new UIStyles();
        private readonly UIAuthView _authView = new UIAuthView();
        private readonly UILobbyView _lobbyView = new UILobbyView();
        private readonly UIRoomView _roomView = new UIRoomView();
        private readonly UIInGameView _inGameView = new UIInGameView();

        /// <summary>
        /// Cho phép bật / tắt vẽ form Auth mặc định bằng IMGUI (tắt khi scene dùng UI Canvas riêng như SCN_Menu).
        /// </summary>
        public bool EnableAuthViewDrawing { get; set; } = true;

        // Chat & Messages
        private readonly List<string> _chatHistory = new List<string>();
        private string _toastMessage = "";
        private float _toastTimer = 0f;
        private string _lastToastMessage = "";
        private float _lastToastTime = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register<IUIService>(this);

            if (PuzzleNetworkManager.Instance == null)
            {
                var sceneNet = FindFirstObjectByType<PuzzleNetworkManager>(FindObjectsInactive.Include);
                if (sceneNet == null)
                {
                    var netObj = new GameObject("[PuzzleNetworkManager]");
                    netObj.AddComponent<PuzzleNetworkManager>();
                }
            }

            RegisterNetworkEvents();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
                ServiceLocator.Unregister<IUIService>();
                Instance = null;
            }
        }

        bool IUIService.IsModalOrChatOpen => IsModalOrChatOpen;

        public static bool IsModalOrChatOpen
        {
            get
            {
                if (!CursorManager.IsGameplayScene) return true;
                if (Instance == null) return false;

                var net = PuzzleNetworkManager.Instance;
                if (net == null || !net.IsGameActive)
                {
                    // Direct gameplay-scene testing remains controllable without an online match.
                    return false;
                }
                return Instance._inGameView.IsModalOrChatOpen;
            }
        }

        private void RegisterNetworkEvents()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnError += msg =>
            {
                ShowToast(msg);
                _inGameView.SetReconnectPrompt(null);
                if (PuzzleNetworkManager.Instance == null || !PuzzleNetworkManager.Instance.IsGameActive)
                {
                    CursorManager.Unlock();
                    string curScene = SceneManager.GetActiveScene().name;
                    // Nếu đang ở scene menu hoặc SCN_Room thì tuyệt đối không chuyển cảnh
                    if (curScene != "SCN_Menu" && curScene != "SCN_MainMenu" && curScene != "SCN_Room")
                    {
                        SceneManager.LoadScene("SCN_Room");
                    }
                }
            };
            net.OnPlayerEvent += msg => AddChatMessage("[Hệ thống] " + msg);
            net.OnChatEvent += (from, text) => AddChatMessage(from + ": " + text);
            net.OnEmojiEvent += (from, emoji) => AddChatMessage(from + ": " + emoji);
            net.OnGameCompleteEvent += (winner, bonus) =>
            {
                _inGameView.SetVictory(winner, bonus);
                CursorManager.Unlock();
            };
            net.OnInviteReceived += (from, roomId, roomName) =>
            {
                ShowToast($"Nhận lời mời từ {from} vào phòng {roomName} ({roomId})");
            };
            net.OnReconnectPromptReceived += info =>
            {
                _inGameView.SetReconnectPrompt(info);
            };
            net.OnRoomUpdated += () =>
            {
                _inGameView.ClearPasswordModal();
            };
            net.OnPasswordRequired += (roomId, roomName) =>
            {
                _inGameView.SetPasswordRequired(roomId, roomName);
            };
            net.OnGameTimeout += () =>
            {
                _inGameView.SetTimeout(true);
                CursorManager.Unlock();
            };
            net.OnGameContinued += () =>
            {
                _inGameView.SetTimeout(false);
                if (!IsModalOrChatOpen)
                {
                    CursorManager.Lock();
                }
            };
            net.OnGameStarted += () =>
            {
                _inGameView.SetTimeout(false);
            };
            net.OnRoomLeft += () =>
            {
                _inGameView.ResetRoomState();
                CursorManager.Unlock();
            };
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (CursorManager.IsGameplayScene) return;
            _inGameView.ResetRoomState();
            CursorManager.Unlock();
        }

        private void Update()
        {
            if (_toastTimer > 0f) _toastTimer -= Time.unscaledDeltaTime;

            // Voice toggle: Press 'V'
            if (PuzzleNetworkManager.Instance != null && PuzzleNetworkManager.Instance.Voice != null)
            {
                if (Input.GetKeyDown(KeyCode.V) && GUIUtility.keyboardControl == 0)
                {
                    bool nextState = !PuzzleNetworkManager.Instance.Voice.IsTransmitting;
                    PuzzleNetworkManager.Instance.Voice.SetTransmitting(nextState);
                }
            }

            // In-game chat toggle: 'T'
            if (PuzzleNetworkManager.Instance != null && PuzzleNetworkManager.Instance.IsGameActive)
            {
                if (Input.GetKeyDown(KeyCode.T) && GUIUtility.keyboardControl == 0)
                {
                    _inGameView.ShowInGameChat = !_inGameView.ShowInGameChat;
                    if (_inGameView.ShowInGameChat)
                    {
                        CursorManager.Unlock();
                    }
                    else
                    {
                        CursorManager.Lock();
                    }
                }
                else if (_inGameView.ShowInGameChat && Input.GetKeyDown(KeyCode.Escape))
                {
                    _inGameView.ShowInGameChat = false;
                    CursorManager.Lock();
                }
                else if (Input.GetKeyDown(KeyCode.Escape))
                {
                    var overhead = FindFirstObjectByType<PuzzleSystem.Gameplay.PuzzleOverheadViewController>();
                    if (overhead != null && overhead.IsOverheadToggled)
                    {
                        overhead.SetOverhead(false);
                    }
                    else
                    {
                        _inGameView.ShowInGameMenu = !_inGameView.ShowInGameMenu;
                        CursorManager.SetLocked(!_inGameView.ShowInGameMenu);
                    }
                }
            }
        }

        public void ShowToast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (message == _lastToastMessage && Time.unscaledTime - _lastToastTime < 1.5f) return;
            _lastToastMessage = message;
            _lastToastTime = Time.unscaledTime;
            _toastMessage = message;
            _toastTimer = 3.5f;
        }

        public void AddChatMessage(string message)
        {
            _chatHistory.Add(message);
            if (_chatHistory.Count > 100) _chatHistory.RemoveAt(0);
            _roomView.ResetScroll();
            _inGameView.ResetChatScroll();
        }

        private void OnGUI()
        {
            _styles.EnsureResources();

            using var viewport = new ResponsiveGUI(fixedLayout: true);

            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            string curScene = SceneManager.GetActiveScene().name;
            bool isEnvironmentScene = curScene.StartsWith("SCN_Nature") || curScene.StartsWith("SCN_Classroom") ||
                                     curScene.StartsWith("SCN_Sakura") || curScene.Equals("Test", StringComparison.OrdinalIgnoreCase);

            if (curScene == "SCN_Menu" || curScene == "SCN_Lobby" || curScene == "SCN_Room" || curScene == "SCN_MainMenu")
            {
                // Cả SCN_Menu, SCN_Lobby và SCN_Room đều có hệ thống UI Canvas riêng chính thức
                // Tuyệt đối không vẽ đè giao diện IMGUI cũ
                DrawToast();
                return;
            }

            if (string.IsNullOrEmpty(net.Username))
            {
                // Nếu đang ở các scene Environment (Nature, Classroom, Sakura...) để test trực tiếp thì KHÔNG vẽ login
                if (isEnvironmentScene)
                {
                    DrawToast();
                    return;
                }

                if (EnableAuthViewDrawing)
                {
                    _authView.Draw(_styles);
                }
            }
            else if (net.CurrentRoom == null)
            {
                // Không ở phòng nào: Không vẽ gì (nhường cho SCN_Lobby)
            }
            else if (net.CurrentRoom.State == "WAITING")
            {
                _roomView.Draw(_styles, _chatHistory);
            }
            else if (net.IsGameActive)
            {
                _inGameView.DrawHUD(_styles, _chatHistory);
            }

            DrawToast();
            _inGameView.DrawModals(_styles);
        }

        private void DrawToast()
        {
            if (_toastTimer <= 0f || string.IsNullOrEmpty(_toastMessage)) return;
            Rect toastRect = new Rect(UIStyles.VirtualWidth * 0.5f - 300, 950, 600, 54);
            _styles.DrawSolidRect(toastRect, new Color(0.12f, 0.14f, 0.20f, 0.96f));
            GUI.Label(toastRect, _toastMessage, _styles.BadgeStyle);
        }
    }
}
