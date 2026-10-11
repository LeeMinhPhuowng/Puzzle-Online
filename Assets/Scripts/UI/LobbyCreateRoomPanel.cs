using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Quản lý form Tạo phòng (Lobby Mode) & Bảng Điều Khiển Phòng (Room Mode):
    /// - Khi ở Sảnh: Nhập tên phòng, mật khẩu, chọn bộ tranh, chọn map, nút TẠO PHÒNG
    /// - Khi ở trong Phòng: Nút tạo phòng chuyển thành RỜI PHÒNG, thêm nút BẮT ĐẦU / SẴN SÀNG
    /// - Cho phép Chủ phòng (Host) tiếp tục đổi Bộ Xếp Hình & Không Gian bất kỳ lúc nào
    /// - Mặc định thời gian chơi là Vô hạn (Relax mode, targetTime = 0)
    /// </summary>
    public class LobbyCreateRoomPanel : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private TMP_InputField puzzleInput;
        [SerializeField] private TMP_InputField environmentInput;

        [Header("Privacy Buttons")]
        [SerializeField] private Button publicButton;
        [SerializeField] private Button privateButton;

        [Header("Primary Action Buttons")]
        [SerializeField] private Button createButton;
        [SerializeField] private TextMeshProUGUI createButtonText;

        [Header("Room Action Buttons")]
        [SerializeField] private Button leaveRoomButton;

        [Header("Pickers")]
        [SerializeField] private Button choosePuzzleButton;
        [SerializeField] private Button chooseEnvironmentButton;
        [SerializeField] private LobbyChooseModalUI choosePuzzleModal;
        [SerializeField] private LobbyChooseModalUI chooseEnvironmentModal;

        [Header("Privacy Visuals")]
        [SerializeField] private Sprite activeGoldSprite;
        [SerializeField] private Sprite inactiveDarkSprite;

        [Header("Game Mode Buttons (Giải trí vs Hỗn loạn)")]
        [SerializeField] private Button peacefulButton; // Giải trí (CLASSIC)
        [SerializeField] private Button mayhemButton;   // Hỗn loạn (COMPETITIVE)

        private bool _isPrivateRoom = false;
        private bool _isInRoom = false;
        private string _selectedGameMode = "CLASSIC";
        private string _selectedPuzzleId = "sunset_3x3";
        private string _selectedThemeId = "SCN_Nature";

        private void Awake()
        {
            if (publicButton != null) publicButton.onClick.AddListener(() => SetPrivacy(false));
            if (privateButton != null) privateButton.onClick.AddListener(() => SetPrivacy(true));

            if (createButton == null)
            {
                var t = transform.Find("Create / Start / Ready") ?? transform.Find("CreateButton");
                if (t != null) createButton = t.GetComponent<Button>();
            }

            if (createButton != null)
            {
                createButton.onClick.RemoveAllListeners();
                createButton.onClick.AddListener(OnMainActionClicked);
                if (createButtonText == null) createButtonText = createButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (leaveRoomButton == null)
            {
                var t = transform.Find("Leave") ?? transform.Find("LeaveButton");
                if (t != null) leaveRoomButton = t.GetComponent<Button>();
            }

            if (leaveRoomButton != null)
            {
                leaveRoomButton.onClick.RemoveAllListeners();
                leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
            }

            if (peacefulButton == null)
            {
                var t = transform.Find("Peaceful") ?? transform.Find("PeacefulBtn") ?? transform.Find("GiaiTri");
                if (t != null) peacefulButton = t.GetComponent<Button>();
            }
            if (mayhemButton == null)
            {
                var t = transform.Find("Mayhem") ?? transform.Find("MayhemBtn") ?? transform.Find("HonLoan");
                if (t != null) mayhemButton = t.GetComponent<Button>();
            }

            if (peacefulButton != null)
            {
                peacefulButton.onClick.RemoveAllListeners();
                peacefulButton.onClick.AddListener(() => OnSelectGameMode("CLASSIC"));
            }
            if (mayhemButton != null)
            {
                mayhemButton.onClick.RemoveAllListeners();
                mayhemButton.onClick.AddListener(() => OnSelectGameMode("COMPETITIVE"));
            }

            if (choosePuzzleButton == null)
            {
                var t = transform.Find("ChoosePuzzleBtn") ?? transform.Find("ChoosePuzzle");
                if (t != null) choosePuzzleButton = t.GetComponent<Button>();
            }

            if (chooseEnvironmentButton == null)
            {
                var t = transform.Find("ChooseEnvironmentBtn") ?? transform.Find("ChooseEnvironment");
                if (t != null) chooseEnvironmentButton = t.GetComponent<Button>();
            }

            if (choosePuzzleButton != null)
            {
                choosePuzzleButton.onClick.AddListener(OnChoosePuzzleClicked);
            }

            if (chooseEnvironmentButton != null)
            {
                chooseEnvironmentButton.onClick.AddListener(OnChooseEnvironmentClicked);
            }

            SetPrivacy(false);
            InitDefaultValues();
        }

        private void Start()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnRoomUpdated += OnRoomUpdated;
                net.OnRoomLeft += OnRoomLeft;
                net.OnLobbyUpdated += OnLobbyUpdated;
            }

            SyncRoomState();
        }

        private void OnDestroy()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnRoomUpdated -= OnRoomUpdated;
                net.OnRoomLeft -= OnRoomLeft;
                net.OnLobbyUpdated -= OnLobbyUpdated;
            }

            if (publicButton != null) publicButton.onClick.RemoveAllListeners();
            if (privateButton != null) privateButton.onClick.RemoveAllListeners();
            if (createButton != null) createButton.onClick.RemoveAllListeners();
            if (leaveRoomButton != null) leaveRoomButton.onClick.RemoveAllListeners();
            if (choosePuzzleButton != null) choosePuzzleButton.onClick.RemoveAllListeners();
            if (chooseEnvironmentButton != null) chooseEnvironmentButton.onClick.RemoveAllListeners();
        }

        private void OnLobbyUpdated()
        {
            if (!_isInRoom)
            {
                if (puzzleInput != null) puzzleInput.text = GetPuzzleDisplayName(_selectedPuzzleId);
                if (environmentInput != null) environmentInput.text = GetThemeDisplayName(_selectedThemeId);
            }
        }

        private void OnRoomUpdated()
        {
            SyncRoomState();
        }

        private void OnRoomLeft()
        {
            SyncRoomState();
        }

        public void SyncRoomState()
        {
            var net = PuzzleNetworkManager.Instance;
            bool inRoom = net != null && net.CurrentRoom != null;
            _isInRoom = inRoom;

            if (inRoom)
            {
                var room = net.CurrentRoom;

                // 1. Cập nhật thông tin phòng
                if (roomNameInput != null)
                {
                    roomNameInput.text = room.Name;
                    roomNameInput.interactable = false; // Khóa tên phòng
                }

                if (passwordInput != null)
                {
                    passwordInput.gameObject.SetActive(false); // Ẩn ô mật khẩu
                }

                if (publicButton != null) publicButton.gameObject.SetActive(false);
                if (privateButton != null) privateButton.gameObject.SetActive(false);

                // 2. Cập nhật Puzzle & Environment (hiển thị tên tiếng Việt thân thiện thay vì ID)
                _selectedPuzzleId = room.PuzzleId;
                _selectedThemeId = room.ThemeId;
                if (puzzleInput != null)
                {
                    puzzleInput.text = GetPuzzleDisplayName(room.PuzzleId);
                    puzzleInput.interactable = false;
                }
                if (environmentInput != null)
                {
                    environmentInput.text = GetThemeDisplayName(room.ThemeId);
                    environmentInput.interactable = false;
                }

                // 3. Phân quyền đổi Tranh / Map
                bool isHost = net.IsHost;
                if (choosePuzzleButton != null) choosePuzzleButton.interactable = isHost;
                if (chooseEnvironmentButton != null) chooseEnvironmentButton.interactable = isHost;

                // 3.5. Nút Chế độ Giải trí (Peaceful) vs Hỗn loạn (Mayhem)
                _selectedGameMode = room.GameMode ?? "CLASSIC";
                UpdateGameModeButtons();
                if (peacefulButton != null)
                {
                    peacefulButton.gameObject.SetActive(true);
                    peacefulButton.interactable = isHost;
                }
                if (mayhemButton != null)
                {
                    mayhemButton.gameObject.SetActive(true);
                    mayhemButton.interactable = isHost;
                }

                // 4. Nút Create / Start / Ready (Nút trên do người dùng đặt sẵn trong Scene):
                // - Chủ phòng: "Bắt đầu chơi"
                // - Khách: "Sẵn sàng" / "Hủy sẵn sàng"
                if (createButton != null)
                {
                    createButton.gameObject.SetActive(true);
                    if (createButtonText != null)
                    {
                        if (isHost)
                        {
                            createButtonText.text = "Bắt đầu chơi";
                        }
                        else
                        {
                            var localPlayer = net.RoomPlayers.FirstOrDefault(p => p.Username.Equals(net.Username, StringComparison.OrdinalIgnoreCase));
                            bool isReady = localPlayer != null && localPlayer.Ready;
                            createButtonText.text = isReady ? "Hủy sẵn sàng" : "Sẵn sàng";
                        }
                    }
                }

                // 5. Nút Leave (Nút dưới do người dùng đặt sẵn trong Scene):
                if (leaveRoomButton != null)
                {
                    leaveRoomButton.gameObject.SetActive(true);
                }
            }
            else
            {
                // Trở về chế độ Tạo phòng bình thường ở Sảnh
                if (roomNameInput != null) roomNameInput.interactable = true;
                if (passwordInput != null) passwordInput.gameObject.SetActive(_isPrivateRoom);
                if (publicButton != null) publicButton.gameObject.SetActive(true);
                if (privateButton != null) privateButton.gameObject.SetActive(true);

                if (choosePuzzleButton != null) choosePuzzleButton.interactable = true;
                if (chooseEnvironmentButton != null) chooseEnvironmentButton.interactable = true;

                if (puzzleInput != null)
                {
                    puzzleInput.text = GetPuzzleDisplayName(_selectedPuzzleId);
                    puzzleInput.interactable = false;
                }

                if (environmentInput != null)
                {
                    environmentInput.text = GetThemeDisplayName(_selectedThemeId);
                    environmentInput.interactable = false;
                }

                // Ở Sảnh: Nút trên là "Tạo phòng"
                if (createButton != null)
                {
                    createButton.gameObject.SetActive(true);
                    if (createButtonText != null)
                    {
                        createButtonText.text = "Tạo phòng";
                    }
                }

                // Ở Sảnh: Ẩn nút "Rời phòng" vì chưa vào phòng
                // Ở Sảnh: Cập nhật hiển thị 2 nút Giải trí / Hỗn loạn
                UpdateGameModeButtons();
                if (peacefulButton != null)
                {
                    peacefulButton.gameObject.SetActive(true);
                    peacefulButton.interactable = true;
                }
                if (mayhemButton != null)
                {
                    mayhemButton.gameObject.SetActive(true);
                    mayhemButton.interactable = true;
                }
            }
        }

        public void OnSelectGameMode(string mode)
        {
            _selectedGameMode = mode;
            UpdateGameModeButtons();

            var net = PuzzleNetworkManager.Instance;
            if (net != null && _isInRoom && net.IsHost)
            {
                net.SelectGameMode(mode);
            }
        }

        private void UpdateGameModeButtons()
        {
            bool isComp = _selectedGameMode == "COMPETITIVE";
            UpdateButtonState(peacefulButton, !isComp);
            UpdateButtonState(mayhemButton, isComp);
        }

        private void OnMainActionClicked()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            if (_isInRoom)
            {
                // Đang trong phòng:
                // - Chủ phòng -> Bắt đầu chơi
                // - Người chơi khác -> Bật/tắt Sẵn sàng
                if (net.IsHost)
                {
                    net.StartGame();
                }
                else
                {
                    var localPlayer = net.RoomPlayers.FirstOrDefault(p => p.Username.Equals(net.Username, StringComparison.OrdinalIgnoreCase));
                    bool currentReady = localPlayer != null && localPlayer.Ready;
                    net.SetReady(!currentReady);
                }
            }
            else
            {
                // Chưa vào phòng: Nút hoạt động như TẠO PHÒNG
                OnCreateRoom();
            }
        }

        private void OnLeaveRoomClicked()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.LeaveRoom();
            }
        }

        private void EnsureModals()
        {
            if (choosePuzzleModal == null)
            {
                var canvas = transform.root;
                var view = canvas.Find("ChoosePuzzleView") ?? canvas.Find("Canvas/ChoosePuzzleView");
                if (view != null)
                {
                    choosePuzzleModal = view.GetComponent<LobbyChooseModalUI>() ?? view.gameObject.AddComponent<LobbyChooseModalUI>();
                }
            }

            if (chooseEnvironmentModal == null)
            {
                var canvas = transform.root;
                var view = canvas.Find("ChooseEnvironmentView") ?? canvas.Find("Canvas/ChooseEnvironmentView");
                if (view != null)
                {
                    chooseEnvironmentModal = view.GetComponent<LobbyChooseModalUI>() ?? view.gameObject.AddComponent<LobbyChooseModalUI>();
                }
            }
        }

        private void OnChoosePuzzleClicked()
        {
            EnsureModals();
            if (choosePuzzleModal != null)
            {
                choosePuzzleModal.Open((selectedId, displayName) =>
                {
                    _selectedPuzzleId = selectedId;
                    if (puzzleInput != null) puzzleInput.text = GetPuzzleDisplayName(selectedId);

                    // Nếu đang ở trong phòng và là Chủ phòng: Đổi trực tiếp trên Server!
                    var net = PuzzleNetworkManager.Instance;
                    if (net != null && net.CurrentRoom != null && net.IsHost)
                    {
                        net.SelectPuzzle(selectedId);
                    }
                });
            }
        }

        private void OnChooseEnvironmentClicked()
        {
            EnsureModals();
            if (chooseEnvironmentModal != null)
            {
                chooseEnvironmentModal.Open((selectedId, displayName) =>
                {
                    _selectedThemeId = selectedId;
                    if (environmentInput != null) environmentInput.text = GetThemeDisplayName(selectedId);

                    // Nếu đang ở trong phòng và là Chủ phòng: Đổi trực tiếp trên Server!
                    var net = PuzzleNetworkManager.Instance;
                    if (net != null && net.CurrentRoom != null && net.IsHost)
                    {
                        net.SelectTheme(selectedId);
                    }
                });
            }
        }

        public void InitDefaultValues()
        {
            var net = PuzzleNetworkManager.Instance;
            if (roomNameInput != null && string.IsNullOrEmpty(roomNameInput.text))
            {
                string uname = net != null && !string.IsNullOrEmpty(net.Username) ? net.Username : "Tôi";
                roomNameInput.text = $"Phòng của {uname}";
            }

            if (string.IsNullOrEmpty(_selectedPuzzleId))
            {
                _selectedPuzzleId = net?.ShopPuzzles?.FirstOrDefault()?.Id ?? "sunset_3x3";
            }
            if (string.IsNullOrEmpty(_selectedThemeId))
            {
                _selectedThemeId = net?.ShopThemes?.FirstOrDefault()?.Id ?? "SCN_Nature";
            }

            if (puzzleInput != null)
            {
                puzzleInput.text = GetPuzzleDisplayName(_selectedPuzzleId);
                puzzleInput.interactable = false;
            }

            if (environmentInput != null)
            {
                environmentInput.text = GetThemeDisplayName(_selectedThemeId);
                environmentInput.interactable = false;
            }
        }

        public void SetPrivacy(bool isPrivate)
        {
            _isPrivateRoom = isPrivate;

            if (passwordInput != null)
            {
                passwordInput.gameObject.SetActive(_isPrivateRoom && !_isInRoom);
                if (!_isPrivateRoom) passwordInput.text = string.Empty;
            }

            UpdateButtonState(publicButton, !_isPrivateRoom);
            UpdateButtonState(privateButton, _isPrivateRoom);
        }

        private void UpdateButtonState(Button btn, bool isActive)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                if (isActive)
                {
                    if (activeGoldSprite != null) img.sprite = activeGoldSprite;
                    img.color = activeGoldSprite != null ? Color.white : new Color(1f, 0.82f, 0.2f, 1f);
                }
                else
                {
                    if (inactiveDarkSprite != null) img.sprite = inactiveDarkSprite;
                    img.color = inactiveDarkSprite != null ? Color.white : new Color(0.25f, 0.28f, 0.35f, 0.85f);
                }
            }
        }

        private void OnCreateRoom()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            string roomName = roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text)
                ? roomNameInput.text.Trim()
                : $"Phòng của {net.Username}";

            string puzzleId = !string.IsNullOrWhiteSpace(_selectedPuzzleId)
                ? _selectedPuzzleId
                : ResolvePuzzleId(puzzleInput != null ? puzzleInput.text : "sunset_3x3");

            string env = !string.IsNullOrWhiteSpace(_selectedThemeId)
                ? _selectedThemeId
                : ResolveThemeId(environmentInput != null ? environmentInput.text : "SCN_Nature");

            string password = _isPrivateRoom && passwordInput != null
                ? passwordInput.text.Trim()
                : string.Empty;

            // Target time = 0: Mặc định vô hạn (Relax / Untimed) hoặc server tự tính theo size nếu Competitive
            net.CreateRoom(roomName, 4, puzzleId, env, password, 0, _selectedGameMode);
        }

        public static string SanitizeDisplayName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            int idx = name.IndexOf('(');
            if (idx > 0) name = name.Substring(0, idx).Trim();
            return name;
        }

        public static string GetPuzzleDisplayName(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return string.Empty;
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                string name = net.GetPuzzleName(puzzleId);
                if (!string.IsNullOrEmpty(name)) return SanitizeDisplayName(name);
            }

            var p = ContentRegistry.Instance?.GetPuzzleData(puzzleId);
            if (p != null && !string.IsNullOrEmpty(p.displayName)) return SanitizeDisplayName(p.displayName);

            return SanitizeDisplayName(puzzleId);
        }

        public static string GetThemeDisplayName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return string.Empty;
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                string name = net.GetThemeName(themeId);
                if (!string.IsNullOrEmpty(name)) return SanitizeDisplayName(name);
            }

            var t = ContentRegistry.Instance?.GetTheme(themeId);
            if (t != null && !string.IsNullOrEmpty(t.displayName)) return SanitizeDisplayName(t.displayName);

            return SanitizeDisplayName(themeId);
        }

        public static string ResolvePuzzleId(string inputOrId)
        {
            if (string.IsNullOrEmpty(inputOrId)) return "sunset_3x3";
            var net = PuzzleNetworkManager.Instance;
            if (net?.ShopPuzzles != null)
            {
                var match = net.ShopPuzzles.FirstOrDefault(p =>
                    string.Equals(p.Id, inputOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Name, inputOrId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match.Id;
            }
            return inputOrId;
        }

        public static string ResolveThemeId(string inputOrId)
        {
            if (string.IsNullOrEmpty(inputOrId)) return "SCN_Nature";
            var net = PuzzleNetworkManager.Instance;
            if (net?.ShopThemes != null)
            {
                var match = net.ShopThemes.FirstOrDefault(t =>
                    string.Equals(t.Id, inputOrId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t.Name, inputOrId, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match.Id;
            }
            return inputOrId;
        }
    }
}
