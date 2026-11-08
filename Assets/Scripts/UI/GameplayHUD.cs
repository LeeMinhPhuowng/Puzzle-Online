using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PuzzleOnline.Network;
using PuzzleOnline.Utility;
using PuzzleOnline.Gameplay.Competitive;

namespace PuzzleOnline.UI
{
    // All layout and presentation references are authored in GameplayHUD.prefab.
    public sealed class GameplayHUD : MonoBehaviour
    {
        public static GameplayHUD Instance { get; private set; }
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private CanvasGroup inputGroup;
        [Header("Top bar")]
        [SerializeField] private TMP_Text roomName;
        [SerializeField] private TMP_Text puzzleName;
        [SerializeField] private TMP_Text themeName;
        [SerializeField] private TMP_Text progress;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text timer;
        [SerializeField] private Button menuButton;
        [Header("Left column")]
        [SerializeField] private TMP_Text ranking;
        [SerializeField] private TMP_Text tutorial;
        [SerializeField] private TMP_Text microphone;
        [SerializeField] private Button microphoneButton;
        [Header("Center")]
        [SerializeField] private GameObject rolePanel;
        [SerializeField] private TMP_Text roleText;
        [SerializeField] private Image roleBackground;
        [SerializeField] private Image roleTitleImage;
        [SerializeField] private Sprite builderRoleSprite;
        [SerializeField] private Sprite saboteurRoleSprite;
        [SerializeField] private Sprite solverTitleSprite;
        [SerializeField] private Sprite saboteurTitleSprite;
        [SerializeField] private GameObject itemPanel;
        [SerializeField] private TMP_Text itemText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private TMP_Text pickupText;
        [SerializeField] private GameObject reticle;
        [SerializeField] private Button[] targetButtons;
        [SerializeField] private TMP_Text[] targetNames;
        [Header("Chat")]
        [SerializeField] private GameObject chatPanel;
        [SerializeField] private Button chatToggle;
        [SerializeField] private Button chatClose;
        [SerializeField] private ScrollRect chatScroll;
        [SerializeField] private TMP_Text chatMessages;
        [SerializeField] private TMP_InputField chatInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button[] emojiButtons;
        [SerializeField] private string[] emojis = { "😊", "👍", "🎉", "🔥", "❤️", "👏" };
        [SerializeField] private TMP_SpriteAsset emojiSprites;
        private PuzzleNetworkManager network;
        private PuzzleOnlineUIManager coordinator;
        private float roleExpires;
        private string roundId;
        private bool scrollToBottom;
        public bool IsChatOpen => chatPanel != null && chatPanel.activeSelf;
        public bool IsTyping => chatInput != null && chatInput.isFocused;

        private void Awake()
        {
            Instance = this;
            menuButton.onClick.AddListener(() => PuzzleOnlineUIManager.Instance?.TogglePauseMenu());
            chatToggle.onClick.AddListener(() => SetChatOpen(true));
            chatClose.onClick.AddListener(() => SetChatOpen(false));
            sendButton.onClick.AddListener(SendChat);
            chatInput.onSubmit.AddListener(_ => SendChat());
            if (microphoneButton != null)
            {
                microphoneButton.gameObject.SetActive(false);
            }
            if (microphone != null)
            {
                var micPanelRt = microphone.transform.parent as RectTransform;
                if (micPanelRt != null && micPanelRt.name.Contains("MicrophonePanel"))
                {
                    micPanelRt.sizeDelta = new Vector2(330f, 48f);
                }
                var micRt = microphone.rectTransform;
                micRt.anchorMin = new Vector2(0f, 0f);
                micRt.anchorMax = new Vector2(1f, 1f);
                micRt.offsetMin = new Vector2(16f, 0f);
                micRt.offsetMax = new Vector2(-16f, 0f);
                microphone.alignment = TextAlignmentOptions.MidlineLeft;
            }
            chatMessages.spriteAsset = emojiSprites;
            chatInput.textComponent.spriteAsset = emojiSprites;
            for (int i = 0; i < emojiButtons.Length; i++)
            {
                string value = emojis[i];
                emojiButtons[i].onClick.AddListener(() => network?.SendEmoji(value));
            }
            for (int i = 0; i < targetButtons.Length; i++)
            {
                int index = i;
                targetButtons[i].onClick.AddListener(() => SaboteurTargetingSystem.Instance?.TryQuickTarget(targetNames[index].text));
            }
            if (roleBackground == null && rolePanel != null)
                roleBackground = rolePanel.GetComponent<Image>();
            if (builderRoleSprite == null && roleBackground != null)
                builderRoleSprite = roleBackground.sprite;
            if (pickupText != null)
            {
                if (hudRoot != null)
                {
                    pickupText.transform.SetParent(hudRoot.transform, false);
                    pickupText.transform.SetAsLastSibling();
                }
                var rt = pickupText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -60f);
                rt.sizeDelta = new Vector2(650f, 40f);
                pickupText.alignment = TextAlignmentOptions.Center;
                pickupText.fontSize = 20;
                pickupText.fontStyle = FontStyles.Bold;
                pickupText.color = Color.yellow;
                pickupText.gameObject.SetActive(false);
            }
            SetChatOpen(false, false);
        }

        private void OnEnable()
        {
            BindServices();
            ResetForRound();
        }

        private void BindServices()
        {
            if (network == null && PuzzleNetworkManager.Instance != null)
            {
                network = PuzzleNetworkManager.Instance;
                network.OnGameStarted += ResetForRound;
            }
            if (coordinator == null && PuzzleOnlineUIManager.Instance != null)
            {
                coordinator = PuzzleOnlineUIManager.Instance;
                coordinator.OnChatHistoryChanged += RefreshChat;
            }
        }

        private void OnDisable()
        {
            if (network != null) network.OnGameStarted -= ResetForRound;
            if (coordinator != null) coordinator.OnChatHistoryChanged -= RefreshChat;
            network = null;
            coordinator = null;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public void ResetForRound()
        {
            roundId = network?.GameplaySyncService.GameInstanceId;
            roleExpires = Time.unscaledTime + 6f;
            if (chatInput != null) chatInput.SetTextWithoutNotify("");
            if (chatMessages != null) chatMessages.text = "";
            if (chatScroll != null) { chatScroll.StopMovement(); chatScroll.verticalNormalizedPosition = 0f; }
            SetChatOpen(false, false);
            RefreshChat();
        }

        private void Update()
        {
            BindServices();
            bool visible = network != null && network.IsGameActive && CursorManager.IsGameplayScene;
            hudRoot.SetActive(visible);
            if (!visible) return;
            if (roundId != network.GameplaySyncService.GameInstanceId) ResetForRound();
            roomName.text = "Phòng: " + network.CurrentRoom?.Name;
            puzzleName.text = "Bộ tranh: " + network.GetPuzzleName(network.CurrentPuzzleId);
            themeName.text = "Không gian: " + UIStyles.GetThemeName(network.CurrentThemeId);
            float fraction = network.TotalPieces > 0 ? (float)network.PlacedPieces / network.TotalPieces : 0;
            progress.text = $"{network.PlacedPieces}/{network.TotalPieces} mảnh ({fraction:P0})";
            progressFill.fillAmount = fraction;
            timer.gameObject.SetActive(network.HasTimer);
            int seconds = Mathf.Max(0, network.RemainingSeconds);
            timer.text = network.RemainingSeconds < 0 ? "∞" : $"{seconds / 60:00}:{seconds % 60:00}";
            ranking.text = string.Join("\n", network.RoomPlayers.OrderByDescending(p => p.Score)
                .Select((p, i) => $"{i + 1}. {p.Username}    {p.Score} điểm"));
            bool overhead = FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive;
            tutorial.text = overhead
                ? "[Z / Esc] Góc nhìn thứ nhất\n[Chuột trái] Nhấc / Kéo / Thả\n[R / Cuộn chuột] Xoay 90°\n[T] Chat    [V] Micro"
                : "[WASD] Di chuyển    [Space] Nhảy\n[Z] Góc nhìn trên cao\n[Chuột trái] Cầm / Thả\n[R / Cuộn chuột] Xoay 90°\n[T] Chat    [V] Micro    [Esc] Menu";
            var voice = network.Voice;
            microphone.text = voice == null || !voice.IsAvailable ? "Micro: Không có thiết bị"
                : voice.IsMuted ? "Micro: Đã tắt tiếng" : voice.IsTransmitting ? "Micro: Đang phát [V]" : "Micro: Sẵn sàng [V]";
            bool blocked = PuzzleOnlineUIManager.Instance != null && PuzzleOnlineUIManager.Instance.IsPauseOrDialogOpen;
            inputGroup.interactable = !blocked;
            inputGroup.blocksRaycasts = !blocked;
            // Bảng vai trò chỉ hiện trong chế độ Tranh Đấu (COMPETITIVE - chia đội Thợ Xây vs Phá Hoại)
            // Không hiện trong chế độ Hỗn Loạn (BATTLE - đua điểm tự do) và Giải Trí (CLASSIC)
            bool showRole = network.IsCompetitive && !blocked && Time.unscaledTime < roleExpires;
            rolePanel.SetActive(showRole);
            if (showRole)
            {
                if (roleBackground != null)
                    roleBackground.sprite = network.IsSaboteur ? saboteurRoleSprite : builderRoleSprite;
                if (roleTitleImage != null)
                {
                    roleTitleImage.gameObject.SetActive(true);
                    roleTitleImage.sprite = network.IsSaboteur ? saboteurTitleSprite : solverTitleSprite;
                }
                roleText.text = network.IsSaboteur ? "ĐỘI PHÁ HOẠI (TRANH ĐẤU)\nTìm hộp quà và ngăn đội Lắp ghép hoàn thành!"
                    : "ĐỘI LẮP GHÉP (TRANH ĐẤU)\nHợp tác ghép tranh trước khi hết giờ!";
            }
            var inventory = SaboteurInventoryHUD.Instance;
            bool hasItem = inventory != null && !string.IsNullOrEmpty(inventory.CurrentItem);
            bool canUseItem = network.IsBattleMode || (network.IsCompetitive && network.IsSaboteur);
            itemPanel.SetActive(canUseItem && !blocked && !IsChatOpen && !network.GameplaySyncService.HasRoundEnded && hasItem);
            if (inventory != null && hasItem)
            {
                itemText.text = inventory.Instruction;
            }
            // Cập nhật thông báo nhặt hộp quà [E]
            bool showPickup = false;
            string promptStr = "";
            Color promptColor = Color.yellow;

            if (canUseItem && !blocked && !IsChatOpen && !network.GameplaySyncService.HasRoundEnded)
            {
                // Kiểm tra bằng Raycast từ tâm ngắm Camera (dùng RaycastAll để không bị CharacterController của chính mình cản trở)
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    var hits = Physics.RaycastAll(ray, 5.5f, ~0, QueryTriggerInteraction.Collide);
                    Transform localPlayer = cam.GetComponentInParent<PlayerController.PlayerController>()?.transform;
                    foreach (var hit in hits)
                    {
                        if (localPlayer != null && hit.collider.transform.IsChildOf(localPlayer)) continue;
                        var worldItem = hit.collider.GetComponentInParent<SaboteurWorldItem>();
                        if (worldItem != null)
                        {
                            showPickup = true;
                            break;
                        }
                    }
                }

                // Hoặc kiểm tra khoảng cách lại gần (Proximity từ SaboteurInventoryHUD)
                if (!showPickup && inventory != null && !string.IsNullOrEmpty(inventory.NearbyPrompt))
                {
                    showPickup = true;
                }

                if (showPickup)
                {
                    if (hasItem)
                    {
                        promptStr = "Bạn đang giữ vật phẩm! Dùng hết trước khi nhặt hộp mới.";
                        promptColor = new Color(1f, 0.6f, 0.6f);
                    }
                    else
                    {
                        promptStr = "[E] Nhặt Hộp Quà 🎁";
                        promptColor = Color.yellow;
                    }
                }
            }

            // Trong Chế độ Tranh Đấu: Hiển thị tooltip điểm & độ hiếm của mảnh khi đang cầm hoặc lia tâm ngắm vào mảnh
            if (!showPickup && network != null && network.IsBattleMode && !blocked && !IsChatOpen)
            {
                var interactor = PuzzleSystem.Gameplay.PuzzleRaycastInteractor.Instance;
                if (interactor != null)
                {
                    var p = interactor.heldPiece ?? interactor.HoveredPiece;
                    if (p != null && !p.isPlaced)
                    {
                        showPickup = true;
                        if (p.isObscuredByInk)
                        {
                            promptStr = "???";
                            promptColor = Color.gray;
                        }
                        else
                        {
                            promptStr = $"{p.points} điểm";
                            promptColor = PuzzleSystem.Gameplay.PieceRarity.GetColor(p.points);
                        }
                    }
                }
            }

            if (pickupText != null)
            {
                if (showPickup)
                {
                    pickupText.text = promptStr;
                    pickupText.color = promptColor;
                    if (!pickupText.gameObject.activeSelf)
                    {
                        pickupText.transform.SetAsLastSibling();
                        pickupText.gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (pickupText.gameObject.activeSelf) pickupText.gameObject.SetActive(false);
                }
            }
            if (targetText != null && targetText.gameObject.activeSelf) targetText.gameObject.SetActive(false);
            reticle.SetActive(!overhead && !PuzzleOnlineUIManager.IsModalOrChatOpen);
            UpdateTargetButtons();
        }

        private void UpdateTargetButtons()
        {
            if (targetButtons == null) return;
            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] != null && targetButtons[i].gameObject.activeSelf)
                    targetButtons[i].gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!scrollToBottom || !IsChatOpen) return;
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
            scrollToBottom = false;
        }

        public void SetChatOpen(bool open, bool updateCursor = true)
        {
            chatPanel.SetActive(open);
            chatToggle.gameObject.SetActive(!open);
            if (open)
            {
                RefreshChat();
                chatInput.ActivateInputField();
                scrollToBottom = true;
            }
            else
            {
                chatInput.DeactivateInputField();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == chatInput.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (updateCursor) PuzzleOnlineUIManager.RefreshCursor();
        }

        private void RefreshChat()
        {
            var manager = PuzzleOnlineUIManager.Instance;
            if (manager != null) chatMessages.text = string.Join("\n", manager.ChatHistory);
            scrollToBottom = true;
        }

        private void SendChat()
        {
            if (!string.IsNullOrWhiteSpace(chatInput.text)) network?.SendChat(chatInput.text.Trim());
            chatInput.SetTextWithoutNotify("");
            chatInput.ActivateInputField();
        }

        private void ToggleMicrophone()
        {
            if (network?.Voice != null) network.Voice.SetMuted(!network.Voice.IsMuted);
        }
    }
}
