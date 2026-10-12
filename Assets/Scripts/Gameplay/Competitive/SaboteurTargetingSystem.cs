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
            if (net == null || !net.IsGameActive || !net.IsSaboteur || net.GameplaySyncService.HasRoundEnded ||
                inventory == null || inventory.IsUsePending || InteractionTargetQuery.IsBlocked)
            { selection.Set(null); return; }
            playerCamera = Camera.main;
            string item = inventory.GetSelectedItemType();
            if (HandleQuickSelectHover(item)) return;
            if (string.IsNullOrEmpty(item) || !InteractionTargetQuery.TryHit(playerCamera, 25f, out var hit))
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
            else if (item == "FLASHLIGHT" || item == "SLAP" || item == "MAGNET")
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
            return player != null && player.Connected && string.Equals(player.Team, "BUILDER", StringComparison.OrdinalIgnoreCase);
        }

        private bool HandleQuickSelectHover(string item)
        {
            if (Cursor.lockState == CursorLockMode.Locked ||
                (item != "FLASHLIGHT" && item != "SLAP" && item != "MAGNET")) return false;
            float scale = Screen.width / ResponsiveGUI.LogicalWidth;
            Vector2 pointer = new Vector2(Input.mousePosition.x / scale, (Screen.height - Input.mousePosition.y) / scale);
            float startY = ResponsiveGUI.LogicalHeight * 0.35f;
            int count = 0;
            foreach (var player in PuzzleNetworkManager.Instance.RoomPlayers)
                if (IsValidPlayer(player.Username)) count++;
            if (!new Rect(25, startY - 32, 200, 32 + count * 43).Contains(pointer)) return false;
            selection.Set(null);
            int index = 0;
            foreach (var player in PuzzleNetworkManager.Instance.RoomPlayers)
            {
                if (!IsValidPlayer(player.Username)) continue;
                if (new Rect(25, startY + index * 43, 200, 35).Contains(pointer) &&
                    TryFindVisibleAvatar(player.Username, out var avatar))
                {
                    hoveredTargetPlayer = player.Username;
                    selection.Set(avatar.gameObject);
                    break;
                }
                index++;
            }
            return true;
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

        private void OnGUI()
        {
            using var viewport = new ResponsiveGUI(fixedLayout: false);
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive || !net.IsSaboteur || net.GameplaySyncService.HasRoundEnded || PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen) return;

            string selectedItem = SaboteurInventoryHUD.Instance?.GetSelectedItemType();
            if (string.IsNullOrEmpty(selectedItem)) return;

            // Hiển thị danh sách Thợ Xây (Builders) ở cạnh bên để click nhanh nếu không raycast trúng avatar 3D
            if (selectedItem == "FLASHLIGHT" || selectedItem == "SLAP" || selectedItem == "MAGNET")
            {
                DrawBuildersQuickSelect(selectedItem, viewport.Height);
            }

            // Hiển thị Crosshair / Reticle ngắm
            var reticleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            float cx = viewport.Width / 2f;
            float cy = viewport.Height / 2f;

            if (selectedItem == "SHOVEL")
            {
                if (hoveredPlacedPiece != null)
                {
                    reticleStyle.normal.textColor = Color.green;
                    GUI.Label(new Rect(cx - 150f, cy + 30f, 300f, 30f), $"[CLICK] NẠY MẢNH #{hoveredPlacedPiece.pieceId + 1}", reticleStyle);
                }
                else
                {
                    reticleStyle.normal.textColor = Color.red;
                    GUI.Label(new Rect(cx - 150f, cy + 30f, 300f, 30f), "(Nhắm vào mảnh đã ghép đúng)", reticleStyle);
                }
            }
            else if (selectedItem == "INK")
            {
                if (isHoveringTable)
                {
                    reticleStyle.normal.textColor = new Color(0.8f, 0.3f, 1f);
                    GUI.Label(new Rect(cx - 150f, cy + 30f, 300f, 30f), "[CLICK] VẨY MỰC LÊN BÀN", reticleStyle);
                }
                else
                {
                    reticleStyle.normal.textColor = Color.red;
                    GUI.Label(new Rect(cx - 150f, cy + 30f, 300f, 30f), "(Nhắm vào mặt bàn)", reticleStyle);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(hoveredTargetPlayer))
                {
                    reticleStyle.normal.textColor = Color.green;
                    GUI.Label(new Rect(cx - 150f, cy + 30f, 300f, 30f), $"[CLICK] TÁC ĐỘNG LÊN: {hoveredTargetPlayer}", reticleStyle);
                }
            }
        }

        private void DrawBuildersQuickSelect(string itemType, float viewportHeight)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || net.RoomPlayers == null) return;

            float panelWidth = 200f;
            float panelHeight = 35f;
            float startX = 25f;
            float startY = viewportHeight * 0.35f;

            var titleStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = Color.yellow;
            GUI.Box(new Rect(startX, startY - 32f, panelWidth, 28f), "CHỌN MỤC TIÊU (BUILDERS):", titleStyle);

            int btnIndex = 0;
            foreach (var player in net.RoomPlayers)
            {
                if (player == null || string.Equals(player.Username, net.Username, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Chỉ hiển thị Builders (hoặc người chơi khác)
                if (!IsValidPlayer(player.Username)) continue;

                Rect btnRect = new Rect(startX, startY + (btnIndex * (panelHeight + 8f)), panelWidth, panelHeight);
                string btnLabel = $"🎯 {player.Username}";

                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
                if (GUI.Button(btnRect, btnLabel))
                {
                    if (TryFindVisibleAvatar(player.Username, out var avatar))
                    {
                        selection.Set(avatar.gameObject);
                        SaboteurInventoryHUD.Instance?.TryUseSelectedItem(player.Username);
                    }
                }
                GUI.backgroundColor = oldColor;

                btnIndex++;
            }
        }
    }
}
