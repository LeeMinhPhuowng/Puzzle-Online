using System;
using UnityEngine;
using PuzzleOnline.Network;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Giao diện vật phẩm dành riêng cho Đội Phá Hoại (Saboteurs).
    /// Người chơi nhặt hộp quà để nhận 1 vật phẩm ngẫu nhiên.
    /// Phải sử dụng vật phẩm hiện tại thì mới được nhặt hộp quà khác (không cần phím chuyển slot).
    /// </summary>
    public class SaboteurInventoryHUD : MonoBehaviour
    {
        public static SaboteurInventoryHUD Instance { get; private set; }

        public string CurrentItem { get; private set; } = "";

        private string nearbyItemPrompt = "";
        private float nearbyItemTimer = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            SaboteurWorldItem.OnItemNearby += HandleItemNearby;
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemPickedUp += HandleItemPickedUp;
                net.OnGameStarted += HandleGameStarted;
            }
        }

        private void OnDisable()
        {
            SaboteurWorldItem.OnItemNearby -= HandleItemNearby;
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemPickedUp -= HandleItemPickedUp;
                net.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            CurrentItem = "";
        }

        private void HandleItemNearby(SaboteurWorldItem item)
        {
            if (!string.IsNullOrEmpty(CurrentItem))
            {
                nearbyItemPrompt = "Bạn đang giữ vật phẩm! Hãy dùng hết trước khi nhặt hộp mới.";
            }
            else
            {
                nearbyItemPrompt = "Nhấn [E] để mở Hộp Quà 🎁";
            }
            nearbyItemTimer = 0.25f;
        }

        private void HandleItemPickedUp(string itemId, string itemType, string byUser)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            if (string.Equals(byUser, net.Username, StringComparison.OrdinalIgnoreCase))
            {
                CurrentItem = itemType;
            }
        }

        private void Update()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive || !net.IsSaboteur) return;

            if (nearbyItemTimer > 0f)
            {
                nearbyItemTimer -= Time.deltaTime;
                if (nearbyItemTimer <= 0f)
                {
                    nearbyItemPrompt = "";
                }
            }
        }

        public string GetSelectedItemType()
        {
            return CurrentItem;
        }

        public void ConsumeSelectedItem()
        {
            CurrentItem = "";
        }

        public string GetItemDisplayName(string itemType)
        {
            return itemType switch
            {
                "FLASHLIGHT" => "🔦 Đèn Pin",
                "INK" => "🧪 Bình Mực",
                "SLAP" => "🧤 Găng Tay",
                "MAGNET" => "🧲 Nam Châm",
                "SHOVEL" => "⛏️ Xà Beng",
                "GIFTBOX" => "🎁 Hộp Quà",
                _ => itemType
            };
        }

        private void OnGUI()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive || !net.IsSaboteur) return;

            // Prompt thông báo nhặt hộp quà [E]
            if (!string.IsNullOrEmpty(nearbyItemPrompt))
            {
                var promptStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                promptStyle.normal.textColor = string.IsNullOrEmpty(CurrentItem) ? Color.yellow : new Color(1f, 0.5f, 0.5f);
                float pWidth = 480f;
                float pHeight = 44f;
                GUI.Box(new Rect((Screen.width - pWidth) / 2f, Screen.height * 0.62f, pWidth, pHeight), nearbyItemPrompt, promptStyle);
            }

            // Giao diện ô vật phẩm đơn giản góc dưới màn hình
            float slotWidth = 240f;
            float slotHeight = 75f;
            float startX = (Screen.width - slotWidth) / 2f;
            float startY = Screen.height - slotHeight - 20f;

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1f, 0.45f, 0.45f);
            GUI.Label(new Rect(startX, startY - 24f, slotWidth, 22f), "⚔️ VẬT PHẨM ĐANG GIỮ", titleStyle);

            Rect slotRect = new Rect(startX, startY, slotWidth, slotHeight);
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = !string.IsNullOrEmpty(CurrentItem) ? new Color(0.2f, 0.5f, 0.8f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.85f);
            GUI.Box(slotRect, GUIContent.none);
            GUI.backgroundColor = oldBg;

            if (!string.IsNullOrEmpty(CurrentItem))
            {
                var nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                nameStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(slotRect.x, slotRect.y + 10f, slotWidth, 26f), GetItemDisplayName(CurrentItem), nameStyle);

                var actionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter
                };
                actionStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(slotRect.x, slotRect.y + 40f, slotWidth, 20f), "Click chuột trái để dùng", actionStyle);

                // Hướng dẫn tác dụng phía trên
                string hint = CurrentItem switch
                {
                    "SHOVEL" => "⛏️ XÀ BENG: Nhấp chuột trái vào 1 mảnh ĐÃ GHÉP ĐÚNG trên bảng tranh để cạy văng ra!",
                    "INK" => "🧪 BÌNH MỰC: Nhấp chuột trái vào BÀN CHƠI để làm đen các mảnh ghép!",
                    "SLAP" => "🧤 GĂNG TAY: Nhấp chuột trái vào NGƯỜI CHƠI khác để tát choáng 3.5s và rơi mảnh!",
                    "MAGNET" => "🧲 NAM CHÂM: Nhấp chuột trái vào NGƯỜI CHƠI khác để giật rơi mảnh ghép trên tay!",
                    "FLASHLIGHT" => "🔦 ĐÈN PIN: Nhấp chuột trái vào NGƯỜI CHƠI khác để rọi mù trắng màn hình!",
                    _ => ""
                };

                var guideStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };
                guideStyle.normal.textColor = Color.white;
                GUI.Box(new Rect((Screen.width - 660f) / 2f, startY - 28f, 660f, 26f), hint, guideStyle);
            }
            else
            {
                var emptyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };
                emptyStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(slotRect.x, slotRect.y + 25f, slotWidth, 24f), "<Chưa có vật phẩm - Tìm hộp quà 🎁>", emptyStyle);
            }
        }
    }
}
