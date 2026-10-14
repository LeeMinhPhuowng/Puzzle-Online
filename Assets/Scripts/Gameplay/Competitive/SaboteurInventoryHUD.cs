using System;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Utility;

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

        public string CurrentItem
        {
            get
            {
                var net = PuzzleNetworkManager.Instance;
                return net != null && net.IsGameActive && !net.GameplaySyncService.HasRoundEnded
                    ? net.GameplaySyncService.GetHeldItem(net.Username) : "";
            }
        }
        public bool IsUsePending { get; private set; }
        private float useRequestedAt;

        public string NearbyPrompt => nearbyItemPrompt;
        public string Instruction => IsUsePending ? "Đang chờ xác nhận sử dụng..." : CurrentItem switch
        {
            "SHOVEL" => "XÀ BENG: Nhấp chuột trái vào mảnh đã ghép đúng để cạy ra.",
            "INK" => "BÌNH MỰC: Nhấp chuột trái vào bàn chơi để làm đen các mảnh ghép.",
            "SLAP" => "GĂNG TAY: Nhấp chuột trái vào người chơi để gây choáng 3.5 giây.",
            "MAGNET" => "NAM CHÂM: Nhấp chuột trái vào người chơi để giật rơi mảnh đang giữ.",
            "FLASHLIGHT" => "ĐÈN PIN: Nhấp chuột trái vào người chơi để rọi mù màn hình.",
            _ => "Chưa có vật phẩm. Tìm hộp quà và nhấn [E] để mở."
        };
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
                net.OnGameStarted += HandleGameStarted;
                net.OnError += HandleUseRejected;
                net.OnGameUpdated += HandleSnapshot;
            }
        }

        private void OnDisable()
        {
            SaboteurWorldItem.OnItemNearby -= HandleItemNearby;
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnGameStarted -= HandleGameStarted;
                net.OnError -= HandleUseRejected;
                net.OnGameUpdated -= HandleSnapshot;
            }
        }

        private void HandleGameStarted()
        {
            IsUsePending = false;
            nearbyItemPrompt = "";
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

        private void HandleUseRejected(string message) => IsUsePending = false;
        private void HandleSnapshot()
        {
            if (Time.unscaledTime - useRequestedAt >= 8f) IsUsePending = false;
        }

        private void Update()
        {
            var net = PuzzleNetworkManager.Instance;
            if (string.IsNullOrEmpty(CurrentItem)) IsUsePending = false;
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

        public bool TryUseSelectedItem(string targetUser = "", int pieceId = -1, bool fromTargetButton = false)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.Client.IsConnected || !net.IsSaboteur ||
                string.IsNullOrEmpty(CurrentItem) || IsUsePending ||
                (InteractionTargetQuery.IsBlocked && !(fromTargetButton && !PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen))) return false;
            IsUsePending = true;
            useRequestedAt = Time.unscaledTime;
            net.UseSabotage(CurrentItem, targetUser, pieceId);
            return true;
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


    }
}
