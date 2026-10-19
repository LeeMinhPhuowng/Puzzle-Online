using System;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Utility;
using PuzzleSystem.Gameplay;

namespace PuzzleOnline.Gameplay.Competitive
{
    public class SaboteurTargetingSystem : MonoBehaviour
    {
        public static SaboteurTargetingSystem Instance { get; private set; }
        public string TargetHint
        {
            get
            {
                var inventory = SaboteurInventoryHUD.Instance;
                string item = inventory != null ? inventory.GetSelectedItemType() : "";
                if (hoveredPlacedPiece != null)
                {
                    return item == "MAGNET"
                        ? $"[CLICK] Hút mảnh ghép #{hoveredPlacedPiece.pieceId + 1} (cố định 5s)"
                        : $"[CLICK] Cạy mảnh #{hoveredPlacedPiece.pieceId + 1}";
                }
                if (isHoveringTable) return "[CLICK] Bôi đen và giấu điểm mảnh ghép trên bàn";
                if (!string.IsNullOrEmpty(hoveredTargetPlayer))
                {
                    if (item == "SLAP") return $"[CLICK] Tát bay {hoveredTargetPlayer} lên trời!";
                    if (item == "MAGNET") return $"[CLICK] Hút giật mảnh từ {hoveredTargetPlayer} (cố định 5s)";
                    if (item == "FLASHLIGHT") return $"[CLICK] Chiếu đèn rọi mù: {hoveredTargetPlayer}";
                    return "[CLICK] Tác động lên: " + hoveredTargetPlayer;
                }
                return "Nhắm vào mục tiêu hợp lệ";
            }
        }

        public bool CanQuickTarget(string username)
        {
            playerCamera = Camera.main;
            return TryFindVisibleAvatar(username, out _);
        }
        public void TryQuickTarget(string username)
        {
            if (CanQuickTarget(username)) SaboteurInventoryHUD.Instance?.TryUseSelectedItem(username, fromTargetButton: true);
        }
        private Camera playerCamera;
        private string hoveredTargetPlayer = "";
        private PuzzlePiece hoveredPlacedPiece;
        private bool isHoveringTable;
        private readonly TargetOutlineSelection selection = new TargetOutlineSelection();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Update()
        {
            hoveredTargetPlayer = "";
            hoveredPlacedPiece = null;
            isHoveringTable = false;
            var net = PuzzleNetworkManager.Instance;
            var inventory = SaboteurInventoryHUD.Instance;
            if (net == null || !net.IsGameActive || (!net.IsSaboteur && !net.IsBattleMode) || net.GameplaySyncService.HasRoundEnded ||
                inventory == null || inventory.IsUsePending || InteractionTargetQuery.IsBlocked)
            { selection.Set(null); return; }
            playerCamera = Camera.main;
            string item = inventory.GetSelectedItemType();

            float maxRange = item switch
            {
                "SLAP" => ItemConfig.GloveRange,
                "MAGNET" => ItemConfig.MagnetRange,
                "FLASHLIGHT" => ItemConfig.FlashlightRange,
                "SHOVEL" => ItemConfig.CrowbarRange,
                "INK" => ItemConfig.InkRange,
                _ => 25f
            };

            if (string.IsNullOrEmpty(item) || !InteractionTargetQuery.TryHit(playerCamera, maxRange, out var hit))
            { selection.Set(null); return; }

            GameObject outlinedTarget = null;
            if (item == "SHOVEL")
            {
                var piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && piece.isPlaced)
                { hoveredPlacedPiece = piece; outlinedTarget = piece.gameObject; }
            }
            else if (item == "INK")
            {
                isHoveringTable = hit.collider.GetComponentInParent<PuzzleBoard>() != null ||
                    hit.collider.name.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            else if (item == "MAGNET")
            {
                // Nam châm có thể nhắm vào mảnh ghép trên bàn HOẶC người chơi khác!
                var piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && !piece.isPlaced)
                {
                    hoveredPlacedPiece = piece;
                    outlinedTarget = piece.gameObject;
                }
                else
                {
                    var avatar = hit.collider.GetComponentInParent<RemotePlayerAvatar>();
                    if (avatar != null && IsValidPlayer(avatar.username))
                    {
                        hoveredTargetPlayer = avatar.username;
                        outlinedTarget = avatar.gameObject;
                    }
                }
            }
            else if (item == "FLASHLIGHT" || item == "SLAP")
            {
                var avatar = hit.collider.GetComponentInParent<RemotePlayerAvatar>();
                if (avatar != null && IsValidPlayer(avatar.username))
                { hoveredTargetPlayer = avatar.username; outlinedTarget = avatar.gameObject; }
            }
            selection.Set(outlinedTarget);
            if (Input.GetMouseButtonDown(0))
            {
                if (hoveredPlacedPiece != null) inventory.TryUseSelectedItem(pieceId: hoveredPlacedPiece.pieceId);
                else if (isHoveringTable) inventory.TryUseSelectedItem();
                else if (!string.IsNullOrEmpty(hoveredTargetPlayer)) inventory.TryUseSelectedItem(hoveredTargetPlayer);
            }
        }

        private bool IsValidPlayer(string username)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || string.Equals(net.Username, username, StringComparison.OrdinalIgnoreCase)) return false;
            var player = net.RoomPlayers?.Find(p => string.Equals(p.Username, username, StringComparison.OrdinalIgnoreCase));
            if (player == null || !player.Connected) return false;
            if (net.IsBattleMode) return true; // Chế độ tranh đấu: có thể tác động lên bất kỳ đối thủ nào
            return string.Equals(player.Team, "BUILDER", StringComparison.OrdinalIgnoreCase);
        }



        private bool TryFindVisibleAvatar(string username, out RemotePlayerAvatar avatar)
        {
            avatar = null;
            if (!IsValidPlayer(username) || playerCamera == null || PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen) return false;
            var sync = FindFirstObjectByType<PuzzlePlayerSyncManager>();
            if (sync == null || !sync.RemoteAvatars.TryGetValue(username, out avatar) || avatar == null) return false;
            var targetCollider = avatar.GetComponentInChildren<CapsuleCollider>();
            if (targetCollider == null) return false;
            Vector3 target = targetCollider.bounds.center;
            Vector3 view = playerCamera.WorldToViewportPoint(target);
            if (view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f) return false;
            Vector3 direction = target - playerCamera.transform.position;
            if (direction.magnitude > 25f) return false;
            return InteractionTargetQuery.TryHit(playerCamera, new Ray(playerCamera.transform.position, direction.normalized),
                direction.magnitude + 0.1f, out var hit) &&
                hit.collider.GetComponentInParent<RemotePlayerAvatar>() == avatar;
        }

        private void OnDisable() => selection.Set(null);
        private void OnDestroy() { if (Instance == this) Instance = null; }




    }
}
