using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển tổng thể Sảnh Chờ (SCN_Lobby):
    /// - Quản lý thông tin tài khoản người dùng & thanh điều hướng Top Navigation
    /// - Điều phối nạp dữ liệu đến các Panel con (LobbyRoomsPanel, LobbyFriendsPanel, LobbyCreateRoomPanel)
    /// - Tự động chuyển đổi giữa Giao diện Sảnh (Lobby) và Phòng chờ 3D (Waiting Room)
    /// </summary>
    public class LobbyUIController : MonoBehaviour
    {
        [Header("=== Sub Panels ===")]
        [SerializeField] private LobbyRoomsPanel roomsPanel;
        [SerializeField] private LobbyFriendsPanel friendsPanel;
        [SerializeField] private LobbyCreateRoomPanel createRoomPanel;

        [Header("=== Waiting Room 3D & Previews ===")]
        [SerializeField] private GameObject room3DPreviewObject;
        [SerializeField] private LobbyRoom3DView room3DView;
        [SerializeField] private LobbyRoomPreviewPanel roomPreviewPanel;

        [Header("=== Top Navigation & User Info ===")]
        [SerializeField] private RectTransform navigationBar;
        [SerializeField] private RectTransform userPanel;
        [SerializeField] private RectTransform titleBack;

        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI userStatusText;
        [SerializeField] private TextMeshProUGUI coinsText;

        [SerializeField] private Button profileButton;
        [SerializeField] private Button navFriendsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button settingsButton;

        [Header("=== Dialog Views ===")]
        [SerializeField] private GameObject shopView;
        [SerializeField] private GameObject settingsView;
        [SerializeField] private GameObject profileView;

        private void Start()
        {
            UpdateUserInfo();
            InitViews();
            EnsureRoomViews();
            SyncRoomDisplay();
        }

        private void InitViews()
        {
            if (shopView == null) shopView = FindViewObject("ShopView");
            if (settingsView == null) settingsView = FindViewObject("SettingsView");
            if (profileView == null) profileView = FindViewObject("ProfileView");

            SetupModal(shopButton, shopView);
            SetupModal(settingsButton, settingsView);
            SetupModal(profileButton, profileView);
        }

        private Transform FindCanvasChild(string childName)
        {
            var canvas = transform.root;
            var t = canvas.Find(childName)
                 ?? canvas.Find($"Canvas/{childName}")
                 ?? canvas.Find($"Canvas (1)/{childName}");
            if (t != null) return t;

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name.Equals(childName, StringComparison.OrdinalIgnoreCase) && go.scene.isLoaded)
                {
                    return go.transform;
                }
            }
            return null;
        }

        private GameObject FindViewObject(string viewName)
        {
            var t = FindCanvasChild(viewName);
            return t != null ? t.gameObject : null;
        }

        private void SetupModal(Button triggerBtn, GameObject modalView)
        {
            if (modalView == null) return;
            modalView.SetActive(false);

            if (triggerBtn != null)
            {
                triggerBtn.onClick.AddListener(() =>
                {
                    modalView.transform.SetAsLastSibling();
                    modalView.SetActive(true);
                });
            }

            var buttons = modalView.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                if (btn.name.Equals("Back", StringComparison.OrdinalIgnoreCase) ||
                    btn.name.Equals("Close", StringComparison.OrdinalIgnoreCase) ||
                    btn.name.Equals("x", StringComparison.OrdinalIgnoreCase))
                {
                    btn.onClick.AddListener(() =>
                    {
                        modalView.SetActive(false);
                    });
                }
            }
        }

        private void OnEnable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated += HandleLobbyUpdated;
                net.OnFriendRequestsUpdated += HandleLobbyUpdated;
                net.OnRoomUpdated += SyncRoomDisplay;
                net.OnRoomLeft += SyncRoomDisplay;
                net.RefreshLobby();
            }

            if (navFriendsButton != null)
                navFriendsButton.onClick.AddListener(ToggleFriendsPanel);
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated -= HandleLobbyUpdated;
                net.OnFriendRequestsUpdated -= HandleLobbyUpdated;
                net.OnRoomUpdated -= SyncRoomDisplay;
                net.OnRoomLeft -= SyncRoomDisplay;
            }

            if (navFriendsButton != null) navFriendsButton.onClick.RemoveListener(ToggleFriendsPanel);
            if (profileButton != null) profileButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
            if (settingsButton != null) settingsButton.onClick.RemoveAllListeners();
        }

        private void HandleLobbyUpdated()
        {
            UpdateUserInfo();

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                if (roomsPanel != null) roomsPanel.SetRooms(net.LobbyRooms);
                if (friendsPanel != null) friendsPanel.SetFriends(net.Friends);
            }
        }

        public void SyncRoomDisplay()
        {
            var net = PuzzleNetworkManager.Instance;
            bool inRoom = net != null && net.CurrentRoom != null;

            EnsureRoomViews();

            // 1. Chuyển đổi giữa Danh sách phòng và Phòng chờ 3D
            if (roomsPanel != null) roomsPanel.gameObject.SetActive(!inRoom);
            if (room3DView != null) room3DView.SetVisible(inRoom);

            // 2. Chuyển đổi giữa Danh sách bạn bè và 2 Khung Preview (Map & Puzzle)
            if (friendsPanel != null) friendsPanel.gameObject.SetActive(!inRoom);
            if (roomPreviewPanel != null) roomPreviewPanel.SetVisible(inRoom);

            var mapP = FindCanvasChild("Map Preview");
            if (mapP != null) mapP.gameObject.SetActive(inRoom);

            var puzP = FindCanvasChild("Puzzle Preview");
            if (puzP != null) puzP.gameObject.SetActive(inRoom);

            // 3. Chuyển đổi Panel Tạo phòng
            if (createRoomPanel != null) createRoomPanel.SyncRoomState();
        }

        private void EnsureRoomViews()
        {
            // A. Khởi tạo Room3DView: Ưu tiên gắn trên GameObject Room3DPreview do người dùng tạo sẵn trong Scene
            if (room3DView == null)
            {
                room3DView = GetComponentInChildren<LobbyRoom3DView>(true);
            }

            if (room3DView == null)
            {
                // 1. Tìm GameObject Room3DPreview (hoặc Room3DView) đã tạo sẵn trong Scene
                GameObject hostGo = room3DPreviewObject;

                if (hostGo == null)
                {
                    var found = FindCanvasChild("Room3DPreview") ?? FindCanvasChild("Room3DView");
                    if (found != null) hostGo = found.gameObject;
                }

                if (hostGo == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in all)
                    {
                        if (go.scene.isLoaded && (go.name.Equals("Room3DPreview", StringComparison.OrdinalIgnoreCase) ||
                                                  go.name.Equals("Room3DView", StringComparison.OrdinalIgnoreCase)))
                        {
                            hostGo = go;
                            break;
                        }
                    }
                }

                // 2. Nếu trong Scene hoàn toàn chưa có, mới tự động sinh mới
                if (hostGo == null && roomsPanel != null)
                {
                    var parent = roomsPanel.transform.parent;
                    hostGo = new GameObject("Room3DView", typeof(RectTransform));
                    hostGo.transform.SetParent(parent, false);
                }

                if (hostGo != null)
                {
                    var rt = hostGo.GetComponent<RectTransform>();
                    if (rt == null) rt = hostGo.AddComponent<RectTransform>();

                    if (roomsPanel != null)
                    {
                        // Đặt sibling index ngay sau roomsPanel để không bao giờ che khuất các Modal (ChoosePuzzle, ChooseEnvironment, Shop...)
                        hostGo.transform.SetSiblingIndex(roomsPanel.transform.GetSiblingIndex() + 1);

                        // Nếu là đối tượng mới tạo hoặc chưa chỉnh size (100, 100), căn chỉnh theo kích thước roomsPanel
                        if (rt.sizeDelta == new Vector2(100f, 100f) || rt.sizeDelta == Vector2.zero)
                        {
                            var srcRt = roomsPanel.GetComponent<RectTransform>();
                            CopyRectTransform(srcRt, rt);
                        }
                    }

                    // Khung viền Image nếu chưa có
                    var bgImg = hostGo.GetComponent<Image>();
                    if (bgImg == null && roomsPanel != null)
                    {
                        var srcImg = roomsPanel.GetComponent<Image>();
                        if (srcImg != null && srcImg.sprite != null)
                        {
                            bgImg = hostGo.AddComponent<Image>();
                            bgImg.sprite = srcImg.sprite;
                            bgImg.type = srcImg.type;
                            bgImg.color = Color.white;
                        }
                    }
                    if (bgImg != null)
                    {
                        bgImg.raycastTarget = false; // Không cản trở click UI
                    }

                    // RawImage nhận hình ảnh từ Camera 4 mảnh ghép 3D
                    var rawImg = hostGo.GetComponentInChildren<RawImage>(true);
                    if (rawImg == null)
                    {
                        var rawGo = new GameObject("Room3DRawImage", typeof(RectTransform), typeof(RawImage));
                        rawGo.transform.SetParent(hostGo.transform, false);
                        var rawRt = rawGo.GetComponent<RectTransform>();
                        rawRt.anchorMin = Vector2.zero;
                        rawRt.anchorMax = Vector2.one;
                        rawRt.offsetMin = new Vector2(16f, 16f);
                        rawRt.offsetMax = new Vector2(-16f, -16f);
                        rawImg = rawGo.GetComponent<RawImage>();
                    }
                    if (rawImg != null)
                    {
                        rawImg.raycastTarget = false; // Không cản trở click UI
                    }

                    room3DView = hostGo.GetComponent<LobbyRoom3DView>();
                    if (room3DView == null)
                    {
                        room3DView = hostGo.AddComponent<LobbyRoom3DView>();
                    }

                    var playerPrefab = Resources.Load<GameObject>("Player") ?? Resources.Load<GameObject>("Prefabs/Player");
                    room3DView.Initialize(rawImg, playerPrefab);
                    hostGo.SetActive(false);
                }
            }
            else
            {
                // Nếu room3DView đã có sẵn, bảo đảm thứ tự hiển thị luôn nằm sau roomsPanel (trước các Modal)
                if (roomsPanel != null && room3DView.transform.parent == roomsPanel.transform.parent)
                {
                    int roomsIdx = roomsPanel.transform.GetSiblingIndex();
                    if (room3DView.transform.GetSiblingIndex() > roomsIdx + 1)
                    {
                        room3DView.transform.SetSiblingIndex(roomsIdx + 1);
                    }
                }
            }

            // B. Kết nối LobbyRoomPreviewPanel với Map Preview & Puzzle Preview có sẵn trong Scene
            var mapTransform = FindCanvasChild("Map Preview");
            var puzTransform = FindCanvasChild("Puzzle Preview");

            if (roomPreviewPanel == null)
            {
                if (mapTransform != null)
                {
                    roomPreviewPanel = mapTransform.GetComponent<LobbyRoomPreviewPanel>() ?? mapTransform.gameObject.AddComponent<LobbyRoomPreviewPanel>();
                }
                else
                {
                    roomPreviewPanel = GetComponentInChildren<LobbyRoomPreviewPanel>(true) ?? gameObject.AddComponent<LobbyRoomPreviewPanel>();
                }
            }

            // Đảm bảo Map Preview và Puzzle Preview nằm ngay sau Room3DView (trước các Modal)
            if (roomsPanel != null)
            {
                int baseIdx = roomsPanel.transform.GetSiblingIndex();
                if (mapTransform != null && mapTransform.parent == roomsPanel.transform.parent)
                {
                    mapTransform.SetSiblingIndex(baseIdx + 2);
                }
                if (puzTransform != null && puzTransform.parent == roomsPanel.transform.parent)
                {
                    puzTransform.SetSiblingIndex(baseIdx + 3);
                }
            }
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null) return;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
        }

        public void UpdateUserInfo()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            if (usernameText != null)
                usernameText.text = !string.IsNullOrEmpty(net.Username) ? net.Username : "Khách";

            if (userStatusText != null)
                userStatusText.text = "Trực tuyến";

            if (coinsText != null)
                coinsText.text = $"{net.Coins:N0} Xu";
        }

        private void ToggleFriendsPanel()
        {
            if (friendsPanel != null)
            {
                friendsPanel.gameObject.SetActive(!friendsPanel.gameObject.activeSelf);
            }
        }
    }
}
