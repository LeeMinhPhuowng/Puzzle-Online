using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển hoạt ảnh DOTween và chuyển đổi giữa Menu chính và Form Đăng Nhập / Đăng Ký trong SCN_Menu.
    /// Khớp chính xác với Hierarchy scene SCN_Menu:
    /// - Title
    /// - Login (nút Join ngoài Menu)
    /// - Settings
    /// - Quit
    /// - Image (Auth Panel container):
    ///     - Login Form (Username, Password)
    ///     - Register Form (Username, Password, Confirm Password)
    ///     - Login (Nút chuyển sang tab Đăng Nhập)
    ///     - Register (Nút chuyển sang tab Đăng Ký)
    ///     - Submit (Nút xác nhận dùng chung ở dưới)
    ///     - Status (TMP Text hiển thị trạng thái)
    /// </summary>
    public class MainMenuUIController : MonoBehaviour
    {
        [Header("Menu Elements (Hierarchy SCN_Menu)")]
        [Tooltip("Logo / Title của Game")]
        [SerializeField] private RectTransform titleRect;

        [Tooltip("Nút Join (Login / Tham gia ngoài Menu chính)")]
        [SerializeField] private Button joinButton;

        [Tooltip("Nút Cài đặt")]
        [SerializeField] private Button settingsButton;

        [Tooltip("Nút Thoát")]
        [SerializeField] private Button quitButton;

        [Tooltip("CanvasGroup cho các nút Menu nếu có")]
        [SerializeField] private CanvasGroup buttonsCanvasGroup;

        [Header("Auth Panel (Container: Image trong SCN_Menu)")]
        [Tooltip("Panel cha bọc toàn bộ form (GameObject Image trong scene)")]
        [SerializeField] private RectTransform authFormRect;

        [Header("Form Switch, Submit & Back Buttons (Dùng chung)")]
        [Tooltip("Nút chuyển sang Tab Đăng Nhập (Image/Login)")]
        [SerializeField] private Button loginTabButton;

        [Tooltip("Nút chuyển sang Tab Đăng Ký (Image/Register)")]
        [SerializeField] private Button registerTabButton;

        [Tooltip("Nút Submit dùng chung ở dưới (Image/Submit)")]
        [SerializeField] private Button submitButton;

        [Tooltip("Nút Quay lại 3 nút ban đầu (Image/Back)")]
        [SerializeField] private Button backButton;

        [Header("Login Form Elements")]
        [SerializeField] private GameObject loginFormGo;
        [SerializeField] private TMP_InputField loginUsernameInput;
        [SerializeField] private TMP_InputField loginPasswordInput;

        [Header("Register Form Elements")]
        [SerializeField] private GameObject registerFormGo;
        [SerializeField] private TMP_InputField registerUsernameInput;
        [SerializeField] private TMP_InputField registerPasswordInput;
        [SerializeField] private TMP_InputField registerConfirmPasswordInput;

        [Header("Status & Server Settings")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 7778;

        [Header("Animation Settings")]
        [SerializeField] private float animDuration = 0.55f;
        [SerializeField] private Ease animEase = Ease.OutBack;
        [Tooltip("Độ trễ từ khi Title quay về đến khi nút Join bắt đầu vào")]
        [SerializeField] private float titleToJoinDelay = 0.18f;
        [Tooltip("Độ trễ lần lượt giữa các nút khi xuất hiện lại")]
        [SerializeField] private float buttonStaggerDelay = 0.12f;

        // Vị trí lưu ban đầu
        private Vector2 _titleOriginalPos;
        private Vector3 _titleOriginalScale = Vector3.one;

        private Vector2 _joinOriginalPos;
        private Vector2 _settingsOriginalPos;
        private Vector2 _quitOriginalPos;

        // CanvasGroup riêng của từng nút menu để fade độc lập, không làm mờ Canvas gốc
        private CanvasGroup _joinCg;
        private CanvasGroup _settingsCg;
        private CanvasGroup _quitCg;

        private Canvas _rootCanvas;
        private RectTransform _rootCanvasRect;
        private bool _isAuthOpen = false;
        private bool _isCurrentTabLogin = true;
        private string _lastKnownUsername = null;
        private float _statusMessageTimer = 0f;

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            if (_rootCanvas == null) _rootCanvas = FindFirstObjectByType<Canvas>();
            if (_rootCanvas != null) _rootCanvasRect = _rootCanvas.GetComponent<RectTransform>();

            // Tự động tìm và liên kết mọi thành phần theo đúng cấu trúc Hierarchy hiện tại của SCN_Menu
            AutoWireReferences();
        }

        private void OnEnable()
        {
            if (PuzzleOnlineUIManager.Instance != null)
            {
                PuzzleOnlineUIManager.Instance.EnableAuthViewDrawing = false;
            }

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnError += HandleNetworkError;
            }
        }

        private void OnDisable()
        {
            if (PuzzleOnlineUIManager.Instance != null)
            {
                PuzzleOnlineUIManager.Instance.EnableAuthViewDrawing = true;
            }

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnError -= HandleNetworkError;
            }
        }

        private void Start()
        {
            if (PuzzleOnlineUIManager.Instance != null)
            {
                PuzzleOnlineUIManager.Instance.EnableAuthViewDrawing = false;
            }

            // Đồng bộ Host và Port từ Inspector: ưu tiên giá trị trên PuzzleNetworkManager (nếu gắn vào obj Managers)
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                if (!string.IsNullOrEmpty(net.ServerHost) && net.ServerHost != "127.0.0.1")
                {
                    serverHost = net.ServerHost;
                    serverPort = net.ServerPort;
                }
                else if (!string.IsNullOrEmpty(serverHost))
                {
                    net.ConfigureConnection(serverHost, serverPort);
                }

                if (net.Client == null || (!net.Client.IsConnected && !net.Client.IsConnecting))
                {
                    net.ConnectToServer(net.ServerHost, net.ServerPort);
                }
            }

            // Đảm bảo CanvasGroup gốc của Canvas luôn mở tương tác và hiển thị 100%
            if (_rootCanvas != null)
            {
                CanvasGroup rootCg = _rootCanvas.GetComponent<CanvasGroup>();
                if (rootCg != null)
                {
                    rootCg.alpha = 1f;
                    rootCg.interactable = true;
                    rootCg.blocksRaycasts = true;
                }
            }

            // Cấu hình chế độ che mật khẩu (Password Masking) cho các ô nhập mật khẩu
            SetupPasswordField(loginPasswordInput);
            SetupPasswordField(registerPasswordInput);
            SetupPasswordField(registerConfirmPasswordInput);

            // Gán sự kiện Click cho 3 nút Menu chính
            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinClicked);
                joinButton.onClick.AddListener(OnJoinClicked);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsClicked);
                settingsButton.onClick.AddListener(OnSettingsClicked);
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuitClicked);
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            // Gán sự kiện cho 2 nút chuyển tab và 1 nút submit dùng chung
            if (loginTabButton != null)
            {
                loginTabButton.onClick.RemoveListener(OnSwitchToLogin);
                loginTabButton.onClick.AddListener(OnSwitchToLogin);
            }
            if (registerTabButton != null)
            {
                registerTabButton.onClick.RemoveListener(OnSwitchToRegister);
                registerTabButton.onClick.AddListener(OnSwitchToRegister);
            }
            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmit);
                submitButton.onClick.AddListener(OnSubmit);
            }
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }

            // Lưu trạng thái Title ban đầu
            if (titleRect != null)
            {
                _titleOriginalPos = titleRect.anchoredPosition;
                _titleOriginalScale = titleRect.localScale;
            }

            // Lưu vị trí gốc và tạo CanvasGroup riêng cho từng nút Menu
            if (joinButton != null)
            {
                _joinOriginalPos = joinButton.GetComponent<RectTransform>().anchoredPosition;
                _joinCg = GetOrAddCanvasGroup(joinButton);
            }
            if (settingsButton != null)
            {
                _settingsOriginalPos = settingsButton.GetComponent<RectTransform>().anchoredPosition;
                _settingsCg = GetOrAddCanvasGroup(settingsButton);
            }
            if (quitButton != null)
            {
                _quitOriginalPos = quitButton.GetComponent<RectTransform>().anchoredPosition;
                _quitCg = GetOrAddCanvasGroup(quitButton);
            }

            // Ẩn Auth Panel (Image) ban đầu
            if (authFormRect != null)
            {
                authFormRect.localScale = Vector3.one * 0.8f;
                authFormRect.gameObject.SetActive(false);
            }

            // Mặc định khởi tạo ở tab Login
            SwitchTab(true);

            // Kiểm tra trạng thái đăng nhập ban đầu
            if (net != null)
            {
                _lastKnownUsername = net.Username;
            }

            if (string.IsNullOrEmpty(_lastKnownUsername))
            {
                // Chưa đăng nhập -> Chạy hoạt ảnh mở màn Menu chính khi vào Play mode
                PlayMainMenuEntranceAnimation(dropTitleFromTop: true);
            }
            else
            {
                // Đã đăng nhập sẵn -> Ẩn menu để Lobby vẽ
                OnUserLoggedIn();
            }
        }

        private void Update()
        {
            var net = PuzzleNetworkManager.Instance;
            string currentUsername = net != null ? net.Username : string.Empty;

            // 1. Phát hiện sự kiện Đăng xuất (trước đó có username, giờ thành rỗng)
            if (!string.IsNullOrEmpty(_lastKnownUsername) && string.IsNullOrEmpty(currentUsername))
            {
                OnUserLoggedOut();
            }
            // 2. Phát hiện sự kiện Đăng nhập thành công (trước đó rỗng, giờ có username)
            else if (string.IsNullOrEmpty(_lastKnownUsername) && !string.IsNullOrEmpty(currentUsername))
            {
                OnUserLoggedIn();
            }
            _lastKnownUsername = currentUsername;

            // Cập nhật nhãn trạng thái kết nối
            if (_isAuthOpen && statusText != null)
            {
                if (_statusMessageTimer > 0f)
                {
                    _statusMessageTimer -= Time.deltaTime;
                }
                else if (net != null && net.Client != null)
                {
                    statusText.text = net.Client.ConnectionLabel;
                    statusText.color = net.Client.IsConnected ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.85f, 0.85f, 0.9f, 0.8f);
                }
            }

            // Phím tắt khi đang mở Auth: ESC (Quay lại), TAB (Chuyển ô), ENTER (Xác nhận)
            if (_isAuthOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    OnBackClicked();
                }
                else if (Input.GetKeyDown(KeyCode.Tab))
                {
                    HandleTabNavigation();
                }
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OnSubmit();
                }
            }
        }

        private void SetupPasswordField(TMP_InputField inputField)
        {
            if (inputField != null)
            {
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.inputType = TMP_InputField.InputType.Password;
                inputField.asteriskChar = '•';
                inputField.ForceLabelUpdate();
            }
        }

        private void HandleTabNavigation()
        {
            if (_isCurrentTabLogin)
            {
                if (loginUsernameInput != null && loginUsernameInput.isFocused && loginPasswordInput != null)
                    loginPasswordInput.Select();
                else if (loginPasswordInput != null && loginPasswordInput.isFocused && submitButton != null)
                    submitButton.Select();
            }
            else
            {
                if (registerUsernameInput != null && registerUsernameInput.isFocused && registerPasswordInput != null)
                    registerPasswordInput.Select();
                else if (registerPasswordInput != null && registerPasswordInput.isFocused && registerConfirmPasswordInput != null)
                    registerConfirmPasswordInput.Select();
                else if (registerConfirmPasswordInput != null && registerConfirmPasswordInput.isFocused && submitButton != null)
                    submitButton.Select();
            }
        }

        /// <summary>
        /// Chuyển đổi giữa tab Đăng Nhập và Đăng Ký.
        /// </summary>
        public void SwitchTab(bool isLogin)
        {
            _isCurrentTabLogin = isLogin;

            if (loginFormGo != null) loginFormGo.SetActive(isLogin);
            if (registerFormGo != null) registerFormGo.SetActive(!isLogin);

            if (isLogin)
            {
                if (loginUsernameInput != null) loginUsernameInput.Select();
                if (statusText != null && (statusText.text == "Nhập thông tin tạo tài khoản" || string.IsNullOrEmpty(statusText.text)))
                    statusText.text = "Sẵn sàng đăng nhập";
            }
            else
            {
                if (registerUsernameInput != null) registerUsernameInput.Select();
                if (statusText != null)
                    statusText.text = "Nhập thông tin tạo tài khoản";
            }
        }

        public void OnSwitchToRegister()
        {
            AudioManager.Instance?.PlayClick();
            SwitchTab(false);
        }

        public void OnSwitchToLogin()
        {
            AudioManager.Instance?.PlayClick();
            SwitchTab(true);
        }

        /// <summary>
        /// Nút Submit dùng chung cho cả 2 tab.
        /// </summary>
        public void OnSubmit()
        {
            if (_isCurrentTabLogin)
                OnSubmitLogin();
            else
                OnSubmitRegister();
        }

        /// <summary>
        /// Sự kiện khi nhấn nút JOIN:
        /// 1. 3 nút Menu (Login, Settings, Quit) trôi dạt hoàn toàn sang trái và mờ dần.
        /// 2. Tiêu đề (Title) chuyển động mượt mà về giữa màn hình (Center-Top).
        /// 3. Auth Panel (Image) phóng to hiện rõ tại chính giữa màn hình.
        /// </summary>
        public void OnJoinClicked()
        {
            if (_isAuthOpen) return;
            _isAuthOpen = true;

            AudioManager.Instance?.PlayClick();

            // 1. Khóa tương tác các nút menu ngay lập tức
            if (_joinCg != null) { _joinCg.interactable = false; _joinCg.blocksRaycasts = false; }
            if (_settingsCg != null) { _settingsCg.interactable = false; _settingsCg.blocksRaycasts = false; }
            if (_quitCg != null) { _quitCg.interactable = false; _quitCg.blocksRaycasts = false; }

            // 2. Hoạt ảnh trôi 3 nút sang trái (vượt qua mép trái màn hình > 1000px)
            Sequence hideButtonsSeq = DOTween.Sequence();
            const float slideOffset = -1000f;

            if (joinButton != null)
            {
                RectTransform rt = joinButton.GetComponent<RectTransform>();
                hideButtonsSeq.Join(rt.DOAnchorPosX(_joinOriginalPos.x + slideOffset, animDuration * 0.7f).SetEase(Ease.InCubic));
                if (_joinCg != null) hideButtonsSeq.Join(_joinCg.DOFade(0f, animDuration * 0.6f));
            }
            if (settingsButton != null)
            {
                RectTransform rt = settingsButton.GetComponent<RectTransform>();
                hideButtonsSeq.Join(rt.DOAnchorPosX(_settingsOriginalPos.x + slideOffset, animDuration * 0.75f).SetEase(Ease.InCubic));
                if (_settingsCg != null) hideButtonsSeq.Join(_settingsCg.DOFade(0f, animDuration * 0.65f));
            }
            if (quitButton != null)
            {
                RectTransform rt = quitButton.GetComponent<RectTransform>();
                hideButtonsSeq.Join(rt.DOAnchorPosX(_quitOriginalPos.x + slideOffset, animDuration * 0.8f).SetEase(Ease.InCubic));
                if (_quitCg != null) hideButtonsSeq.Join(_quitCg.DOFade(0f, animDuration * 0.7f));
            }

            if (buttonsCanvasGroup != null && _rootCanvas != null && buttonsCanvasGroup.gameObject != _rootCanvas.gameObject)
            {
                hideButtonsSeq.Join(buttonsCanvasGroup.DOFade(0f, animDuration * 0.6f));
            }

            hideButtonsSeq.OnComplete(() =>
            {
                if (joinButton != null) joinButton.gameObject.SetActive(false);
                if (settingsButton != null) settingsButton.gameObject.SetActive(false);
                if (quitButton != null) quitButton.gameObject.SetActive(false);
            });

            // 3. Chuyển Title về giữa màn hình (Center-Top)
            if (titleRect != null)
            {
                titleRect.DOKill();
                float canvasWidth = _rootCanvasRect != null && _rootCanvasRect.rect.width > 0f ? _rootCanvasRect.rect.width : Screen.width;
                const float targetScale = 1f;
                float centerTargetX = (canvasWidth - titleRect.rect.width * targetScale) * 0.5f + 15f;
                Vector2 targetPos = new Vector2(centerTargetX, -50f);

                titleRect.DOAnchorPos(targetPos, animDuration).SetEase(animEase);
                titleRect.DOScale(Vector3.one * targetScale, animDuration).SetEase(animEase);
            }

            // 4. Hiện Auth Panel (Image) chính giữa màn hình (không dùng CanvasGroup)
            if (authFormRect != null)
            {
                authFormRect.DOKill();
                authFormRect.gameObject.SetActive(true);
                authFormRect.localScale = Vector3.one * 0.8f;

                // Mặc định mở tab Login
                SwitchTab(true);

                authFormRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    if (loginUsernameInput != null) loginUsernameInput.Select();
                });
            }
        }

        /// <summary>
        /// Hoạt ảnh xuất hiện của Menu chính:
        /// - dropTitleFromTop = true: Title rơi nhẹ từ trên xuống (khi vào Play mode hoặc sau khi Logout).
        /// - dropTitleFromTop = false: Title di chuyển từ chính giữa về góc trái (khi bấm Back từ Auth).
        /// - 3 nút Menu lần lượt đi từ mép trái màn hình vào với độ trễ liên tiếp.
        /// </summary>
        public void PlayMainMenuEntranceAnimation(bool dropTitleFromTop = true)
        {
            Sequence entranceSeq = DOTween.Sequence();

            // 1. Title xuất hiện
            if (titleRect != null)
            {
                titleRect.DOKill();
                titleRect.gameObject.SetActive(true);
                if (dropTitleFromTop)
                {
                    titleRect.anchoredPosition = new Vector2(_titleOriginalPos.x, _titleOriginalPos.y + 250f);
                    titleRect.localScale = _titleOriginalScale * 0.85f;
                }
                entranceSeq.Join(titleRect.DOAnchorPos(_titleOriginalPos, animDuration).SetEase(animEase));
                entranceSeq.Join(titleRect.DOScale(_titleOriginalScale, animDuration).SetEase(animEase));
            }

            // 2. Chuẩn bị 3 nút menu ở ngoài mép trái
            const float slideOffset = -1000f;
            const float buttonSlideDuration = 0.45f;

            if (joinButton != null)
            {
                joinButton.gameObject.SetActive(true);
                RectTransform rt = joinButton.GetComponent<RectTransform>();
                rt.DOKill();
                rt.anchoredPosition = new Vector2(_joinOriginalPos.x + slideOffset, _joinOriginalPos.y);
                if (_joinCg != null)
                {
                    _joinCg.DOKill();
                    _joinCg.alpha = 0f;
                    _joinCg.interactable = false;
                    _joinCg.blocksRaycasts = false;
                }
            }
            if (settingsButton != null)
            {
                settingsButton.gameObject.SetActive(true);
                RectTransform rt = settingsButton.GetComponent<RectTransform>();
                rt.DOKill();
                rt.anchoredPosition = new Vector2(_settingsOriginalPos.x + slideOffset, _settingsOriginalPos.y);
                if (_settingsCg != null)
                {
                    _settingsCg.DOKill();
                    _settingsCg.alpha = 0f;
                    _settingsCg.interactable = false;
                    _settingsCg.blocksRaycasts = false;
                }
            }
            if (quitButton != null)
            {
                quitButton.gameObject.SetActive(true);
                RectTransform rt = quitButton.GetComponent<RectTransform>();
                rt.DOKill();
                rt.anchoredPosition = new Vector2(_quitOriginalPos.x + slideOffset, _quitOriginalPos.y);
                if (_quitCg != null)
                {
                    _quitCg.DOKill();
                    _quitCg.alpha = 0f;
                    _quitCg.interactable = false;
                    _quitCg.blocksRaycasts = false;
                }
            }

            // Thời điểm các nút bắt đầu vào: sau khi Title bắt đầu di chuyển một khoảng ngắn (titleToJoinDelay)
            float joinStartTime = Mathf.Max(0.05f, titleToJoinDelay);

            // Nút 1: Join
            if (joinButton != null)
            {
                RectTransform rt = joinButton.GetComponent<RectTransform>();
                entranceSeq.Insert(joinStartTime, rt.DOAnchorPos(_joinOriginalPos, buttonSlideDuration).SetEase(Ease.OutBack));
                if (_joinCg != null) entranceSeq.Insert(joinStartTime, _joinCg.DOFade(1f, buttonSlideDuration * 0.7f));
            }

            // Nút 2: Settings (delay sau Join)
            float settingsStartTime = joinStartTime + buttonStaggerDelay;
            if (settingsButton != null)
            {
                RectTransform rt = settingsButton.GetComponent<RectTransform>();
                entranceSeq.Insert(settingsStartTime, rt.DOAnchorPos(_settingsOriginalPos, buttonSlideDuration).SetEase(Ease.OutBack));
                if (_settingsCg != null) entranceSeq.Insert(settingsStartTime, _settingsCg.DOFade(1f, buttonSlideDuration * 0.7f));
            }

            // Nút 3: Quit (delay sau Settings)
            float quitStartTime = settingsStartTime + buttonStaggerDelay;
            if (quitButton != null)
            {
                RectTransform rt = quitButton.GetComponent<RectTransform>();
                entranceSeq.Insert(quitStartTime, rt.DOAnchorPos(_quitOriginalPos, buttonSlideDuration).SetEase(Ease.OutBack));
                if (_quitCg != null) entranceSeq.Insert(quitStartTime, _quitCg.DOFade(1f, buttonSlideDuration * 0.7f));
            }

            if (buttonsCanvasGroup != null && _rootCanvas != null && buttonsCanvasGroup.gameObject != _rootCanvas.gameObject)
            {
                entranceSeq.Insert(joinStartTime, buttonsCanvasGroup.DOFade(1f, buttonSlideDuration));
            }

            entranceSeq.OnComplete(() =>
            {
                if (_joinCg != null) { _joinCg.interactable = true; _joinCg.blocksRaycasts = true; }
                if (_settingsCg != null) { _settingsCg.interactable = true; _settingsCg.blocksRaycasts = true; }
                if (_quitCg != null) { _quitCg.interactable = true; _quitCg.blocksRaycasts = true; }
            });
        }

        /// <summary>
        /// Quay trở lại Menu chính từ Form Auth:
        /// 1. Ẩn ngay lập tức Form Auth (không delay).
        /// 2. Title quay về góc trái.
        /// 3. Nút Join trôi vào ngay sau khi Title bắt đầu về, tiếp theo là Settings và Quit.
        /// </summary>
        public void OnBackClicked()
        {
            if (!_isAuthOpen) return;
            _isAuthOpen = false;

            AudioManager.Instance?.PlayClick();

            // 1. Ẩn form Auth ngay lập tức
            if (authFormRect != null)
            {
                authFormRect.DOKill();
                authFormRect.gameObject.SetActive(false);
            }

            // 2. Chạy hoạt ảnh đưa Title từ giữa về góc trái và 3 nút trôi vào
            PlayMainMenuEntranceAnimation(dropTitleFromTop: false);
        }

        private void OnUserLoggedIn()
        {
            _isAuthOpen = false;
            if (authFormRect != null)
            {
                authFormRect.DOKill();
                authFormRect.gameObject.SetActive(false);
            }

            // Chuyển màn hình từ SCN_Menu sang SCN_Room
            UnityEngine.SceneManagement.SceneManager.LoadScene("SCN_Room");
        }

        private void OnUserLoggedOut()
        {
            _isAuthOpen = false;

            if (authFormRect != null)
            {
                authFormRect.DOKill();
                authFormRect.gameObject.SetActive(false);
            }

            // Đảm bảo Canvas hiển thị và mở lại tương tác
            if (_rootCanvas != null)
            {
                CanvasGroup rootCg = _rootCanvas.GetComponent<CanvasGroup>();
                if (rootCg != null)
                {
                    rootCg.alpha = 1f;
                    rootCg.interactable = true;
                    rootCg.blocksRaycasts = true;
                }
            }

            // Chạy hoạt ảnh mở màn Menu chính khi vừa Đăng xuất
            PlayMainMenuEntranceAnimation(dropTitleFromTop: true);
        }

        public void OnSubmitLogin()
        {
            AudioManager.Instance?.PlayClick();

            string username = loginUsernameInput != null ? loginUsernameInput.text.Trim() : "";
            string password = loginPasswordInput != null ? loginPasswordInput.text : "";

            if (string.IsNullOrEmpty(username))
            {
                SetStatus("Vui lòng nhập tên tài khoản!", true);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatus("Vui lòng nhập mật khẩu!", true);
                return;
            }

            SetStatus("Đang kết nối & đăng nhập...", false);

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                string targetHost = !string.IsNullOrEmpty(net.ServerHost) ? net.ServerHost : serverHost;
                int targetPort = net.ServerPort > 0 ? net.ServerPort : serverPort;
                if (net.Client == null || (!net.Client.IsConnected && !net.Client.IsConnecting))
                {
                    net.ConnectToServer(targetHost, targetPort);
                }
                net.Login(username, password);
            }
        }

        public void OnSubmitRegister()
        {
            AudioManager.Instance?.PlayClick();

            string username = registerUsernameInput != null ? registerUsernameInput.text.Trim() : "";
            string password = registerPasswordInput != null ? registerPasswordInput.text : "";
            string confirmPassword = registerConfirmPasswordInput != null ? registerConfirmPasswordInput.text : "";

            if (string.IsNullOrEmpty(username))
            {
                SetStatus("Vui lòng nhập tên tài khoản đăng ký!", true);
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                SetStatus("Vui lòng nhập mật khẩu!", true);
                return;
            }

            if (password != confirmPassword)
            {
                SetStatus("Mật khẩu xác nhận không khớp!", true);
                return;
            }

            SetStatus("Đang đăng ký tài khoản...", false);

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                string targetHost = !string.IsNullOrEmpty(net.ServerHost) ? net.ServerHost : serverHost;
                int targetPort = net.ServerPort > 0 ? net.ServerPort : serverPort;
                if (net.Client == null || (!net.Client.IsConnected && !net.Client.IsConnecting))
                {
                    net.ConnectToServer(targetHost, targetPort);
                }
                net.Register(username, password);
            }
        }

        private void HandleNetworkError(string errorMessage)
        {
            SetStatus(errorMessage, true);
        }

        private void SetStatus(string message, bool isError)
        {
            _statusMessageTimer = isError ? 5f : 3f;
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isError ? new Color(1f, 0.4f, 0.4f) : new Color(0.85f, 0.9f, 1f);
            }
        }

        private void OnSettingsClicked()
        {
            AudioManager.Instance?.PlayClick();
            PuzzleOnlineUIManager.Instance?.ShowToast("Cài đặt âm thanh và đồ họa sẽ được bổ sung!");
        }

        private void OnQuitClicked()
        {
            AudioManager.Instance?.PlayClick();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private CanvasGroup GetOrAddCanvasGroup(Component comp)
        {
            if (comp == null) return null;
            CanvasGroup cg = comp.GetComponent<CanvasGroup>();
            if (cg == null) cg = comp.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private void AutoWireReferences()
        {
            if (_rootCanvas == null) return;
            Transform canvasTr = _rootCanvas.transform;

            // 1. Khớp Title
            if (titleRect == null)
            {
                var t = canvasTr.Find("Title");
                if (t != null) titleRect = t.GetComponent<RectTransform>();
            }

            // 2. Khớp Nút Menu: Login (Join), Settings, Quit
            if (joinButton == null)
            {
                var b = canvasTr.Find("Login") ?? canvasTr.Find("Join");
                if (b != null) joinButton = b.GetComponent<Button>();
            }

            if (settingsButton == null)
            {
                var b = canvasTr.Find("Settings");
                if (b != null) settingsButton = b.GetComponent<Button>();
            }

            if (quitButton == null)
            {
                var b = canvasTr.Find("Quit");
                if (b != null) quitButton = b.GetComponent<Button>();
            }

            // Tránh việc buttonsCanvasGroup trỏ vào Canvas gốc
            if (buttonsCanvasGroup != null && buttonsCanvasGroup.gameObject == _rootCanvas.gameObject)
            {
                buttonsCanvasGroup = null;
            }

            // 3. Khớp Auth Panel (Container: Image trong SCN_Menu)
            Transform authPanelTr = canvasTr.Find("Image") ?? canvasTr.Find("AuthForm") ?? canvasTr.Find("AuthPanel");
            if (authPanelTr != null)
            {
                authFormRect = authPanelTr.GetComponent<RectTransform>();

                // Khớp 2 nút chuyển tab nằm trực tiếp dưới Image
                if (loginTabButton == null)
                {
                    var btn = authPanelTr.Find("Login");
                    if (btn != null) loginTabButton = btn.GetComponent<Button>();
                }

                if (registerTabButton == null)
                {
                    var btn = authPanelTr.Find("Register");
                    if (btn != null) registerTabButton = btn.GetComponent<Button>();
                }

                // Khớp nút Submit dùng chung
                if (submitButton == null)
                {
                    var btn = authPanelTr.Find("Submit");
                    if (btn != null) submitButton = btn.GetComponent<Button>();
                }

                // Khớp nút Back quay lại 3 nút ban đầu
                if (backButton == null)
                {
                    var btn = authPanelTr.Find("Back");
                    if (btn != null) backButton = btn.GetComponent<Button>();
                }

                // Khớp Login Form
                Transform lfTr = authPanelTr.Find("Login Form");
                if (lfTr != null)
                {
                    loginFormGo = lfTr.gameObject;
                    if (loginUsernameInput == null)
                    {
                        var un = lfTr.Find("Username");
                        if (un != null) loginUsernameInput = un.GetComponent<TMP_InputField>();
                    }
                    if (loginPasswordInput == null)
                    {
                        var pw = lfTr.Find("Password");
                        if (pw != null) loginPasswordInput = pw.GetComponent<TMP_InputField>();
                    }
                }

                // Khớp Register Form
                Transform rfTr = authPanelTr.Find("Register Form");
                if (rfTr != null)
                {
                    registerFormGo = rfTr.gameObject;
                    if (registerUsernameInput == null)
                    {
                        var un = rfTr.Find("Username");
                        if (un != null) registerUsernameInput = un.GetComponent<TMP_InputField>();
                    }
                    if (registerPasswordInput == null)
                    {
                        var pw = rfTr.Find("Password");
                        if (pw != null) registerPasswordInput = pw.GetComponent<TMP_InputField>();
                    }
                    if (registerConfirmPasswordInput == null)
                    {
                        var cp = rfTr.Find("Confirm Password");
                        if (cp != null) registerConfirmPasswordInput = cp.GetComponent<TMP_InputField>();
                    }
                }

                // Khớp Status Text
                if (statusText == null)
                {
                    var st = authPanelTr.Find("Status");
                    if (st != null) statusText = st.GetComponent<TextMeshProUGUI>();
                }
            }
        }
    }
}
