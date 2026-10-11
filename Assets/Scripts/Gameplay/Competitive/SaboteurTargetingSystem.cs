using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleSystem.Gameplay;

namespace PuzzleOnline.Gameplay.Competitive
{
    /// <summary>
    /// Hệ thống nhắm mục tiêu (Targeting System) cho Đội Phá Hoại (Saboteur).
    /// - Nhắm Player: Đèn pin, Găng tay, Nam châm.
    /// - Nhắm Bàn: Bình mực.
    /// - Nhắm Mảnh ghép đã fit: Cái xẻng.
    /// </summary>
    public class SaboteurTargetingSystem : MonoBehaviour
    {
        public static SaboteurTargetingSystem Instance { get; private set; }

        private Camera playerCamera;
        private string hoveredTargetPlayer = "";
        private PuzzlePiece hoveredPlacedPiece = null;
        private bool isHoveringTable = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            playerCamera = Camera.main;
        }

        private void Update()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive || !net.IsSaboteur) return;

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }

            string selectedItem = SaboteurInventoryHUD.Instance?.GetSelectedItemType();
            if (string.IsNullOrEmpty(selectedItem))
            {
                hoveredTargetPlayer = "";
                hoveredPlacedPiece = null;
                isHoveringTable = false;
                return;
            }

            // Thực hiện raycast từ tâm màn hình hoặc con trỏ chuột
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            hoveredTargetPlayer = "";
            hoveredPlacedPiece = null;
            isHoveringTable = false;

            RaycastHit[] hits = Physics.RaycastAll(ray, 25f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Kiểm tra mảnh ghép cho XẺNG
                if (selectedItem == "SHOVEL")
                {
                    var piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                    if (piece != null && piece.isPlaced)
                    {
                        hoveredPlacedPiece = piece;
                        break;
                    }
                }
                // Kiểm tra bàn chơi cho MỰC
                else if (selectedItem == "INK")
                {
                    var board = hit.collider.GetComponentInParent<PuzzleBoard>();
                    if (board != null || hit.collider.name.ToLower().Contains("table"))
                    {
                        isHoveringTable = true;
                        break;
                    }
                }
                // Kiểm tra người chơi cho FLASHLIGHT, SLAP, MAGNET
                else if (selectedItem == "FLASHLIGHT" || selectedItem == "SLAP" || selectedItem == "MAGNET")
                {
                    string hitPlayerName = FindPlayerNameFromHit(hit.collider);
                    if (!string.IsNullOrEmpty(hitPlayerName) && !hitPlayerName.Equals(net.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        hoveredTargetPlayer = hitPlayerName;
                        break;
                    }
                }
            }

            // Xử lý click chuột trái để KÍCH HOẠT VẬT PHẨM
            if (Input.GetMouseButtonDown(0))
            {
                ExecuteSabotage(selectedItem);
            }
        }

        private string FindPlayerNameFromHit(Collider col)
        {
            // Kiểm tra tên GameObject hoặc Component cha
            Transform cur = col.transform;
            while (cur != null)
            {
                // Thông thường remote player avatar có tên là username hoặc có text mesh
                var tm = cur.GetComponentInChildren<TextMesh>();
                if (tm != null && !string.IsNullOrEmpty(tm.text))
                {
                    return tm.text.Trim();
                }

                // Hoặc kiểm tra nếu GameObject chứa tên người chơi trong RoomPlayers
                var net = PuzzleNetworkManager.Instance;
                if (net != null && net.RoomPlayers != null)
                {
                    foreach (var p in net.RoomPlayers)
                    {
                        if (cur.name.Contains(p.Username))
                        {
                            return p.Username;
                        }
                    }
                }
                cur = cur.parent;
            }
            return "";
        }

        private void ExecuteSabotage(string itemType)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            switch (itemType)
            {
                case "SHOVEL":
                    if (hoveredPlacedPiece != null)
                    {
                        net.UseSabotage("SHOVEL", "", hoveredPlacedPiece.pieceId);
                        SaboteurInventoryHUD.Instance?.ConsumeSelectedItem();
                    }
                    break;

                case "INK":
                    if (isHoveringTable)
                    {
                        net.UseSabotage("INK");
                        SaboteurInventoryHUD.Instance?.ConsumeSelectedItem();
                    }
                    break;

                case "SLAP":
                case "MAGNET":
                case "FLASHLIGHT":
                    if (!string.IsNullOrEmpty(hoveredTargetPlayer))
                    {
                        net.UseSabotage(itemType, hoveredTargetPlayer);
                        SaboteurInventoryHUD.Instance?.ConsumeSelectedItem();
                    }
                    break;
            }
        }

        private void OnGUI()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive || !net.IsSaboteur) return;

            string selectedItem = SaboteurInventoryHUD.Instance?.GetSelectedItemType();
            if (string.IsNullOrEmpty(selectedItem)) return;

            // Hiển thị danh sách Thợ Xây (Builders) ở cạnh bên để click nhanh nếu không raycast trúng avatar 3D
            if (selectedItem == "FLASHLIGHT" || selectedItem == "SLAP" || selectedItem == "MAGNET")
            {
                DrawBuildersQuickSelect(selectedItem);
            }

            // Hiển thị Crosshair / Reticle ngắm
            var reticleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

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

        private void DrawBuildersQuickSelect(string itemType)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null || net.RoomPlayers == null) return;

            float panelWidth = 200f;
            float panelHeight = 35f;
            float startX = 25f;
            float startY = Screen.height * 0.35f;

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
                if (player.Team == "SABOTEUR") continue;

                Rect btnRect = new Rect(startX, startY + (btnIndex * (panelHeight + 8f)), panelWidth, panelHeight);
                string btnLabel = $"🎯 {player.Username}";

                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);
                if (GUI.Button(btnRect, btnLabel))
                {
                    net.UseSabotage(itemType, player.Username);
                    SaboteurInventoryHUD.Instance?.ConsumeSelectedItem();
                }
                GUI.backgroundColor = oldColor;

                btnIndex++;
            }
        }
    }
}
