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
            microphoneButton.onClick.AddListener(ToggleMicrophone);
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
            timer.gameObject.SetActive(network.IsCompetitive);
            int seconds = Mathf.Max(0, network.RemainingSeconds);
            timer.text = network.RemainingSeconds < 0 ? "∞" : $"{seconds / 60:00}:{seconds % 60:00}";
            ranking.text = string.Join("\n", network.RoomPlayers.OrderByDescending(p => p.Score)
                .Select((p, i) => $"{i + 1}. {p.Username}{(p.IsHost ? " ★" : "")}    {p.Score} đ"));
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
            rolePanel.SetActive(network.IsCompetitive && !blocked && Time.unscaledTime < roleExpires);
            roleText.text = network.IsSaboteur ? "ĐỘI PHÁ HOẠI\nTìm hộp quà và ngăn đội Xây dựng hoàn thành!"
                : "ĐỘI XÂY DỰNG\nHợp tác ghép tranh trước khi hết giờ!";
            var inventory = SaboteurInventoryHUD.Instance;
            itemPanel.SetActive(network.IsCompetitive && network.IsSaboteur && !blocked && !IsChatOpen && !network.GameplaySyncService.HasRoundEnded);
            if (inventory != null)
            {
                itemText.text = inventory.Instruction;
                pickupText.text = inventory.NearbyPrompt;
            }
            targetText.text = SaboteurTargetingSystem.Instance?.TargetHint ?? "";
            reticle.SetActive(!overhead && !PuzzleOnlineUIManager.IsModalOrChatOpen);
            UpdateTargetButtons();
        }

        private void UpdateTargetButtons()
        {
            string type = SaboteurInventoryHUD.Instance?.CurrentItem;
            bool playerItem = type == "FLASHLIGHT" || type == "SLAP" || type == "MAGNET";
            var builders = network.RoomPlayers.Where(p => p.Connected && p.Team == "BUILDER" && p.Username != network.Username).ToArray();
            for (int i = 0; i < targetButtons.Length; i++)
            {
                bool show = playerItem && itemPanel.activeSelf && i < builders.Length;
                targetButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                targetNames[i].text = builders[i].Username;
                targetButtons[i].interactable = SaboteurTargetingSystem.Instance != null &&
                    SaboteurTargetingSystem.Instance.CanQuickTarget(builders[i].Username) && !SaboteurInventoryHUD.Instance.IsUsePending;
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
