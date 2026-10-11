using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Giao diện phòng chờ (Waiting Room): Danh sách người chơi trên 4 ghế, cài đặt ván chơi của host, chat phòng chờ.
    /// </summary>
    public class UIRoomView
    {
        private Vector2 _waitingRoomChatScroll;
        private string _chatInput = "";

        public void ResetScroll()
        {
            _waitingRoomChatScroll.y = float.MaxValue;
        }

        public void Draw(UIStyles styles, List<string> chatHistory)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;
            var room = net.CurrentRoom;
            if (room == null) return;

            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), styles.BgColor);
            styles.DrawTopBar($"Phòng Chờ: {room.Name} (Mã: {room.Id})");

            bool isHost = room.Host == net.Username;
            bool isCompetitive = room.GameMode == "COMPETITIVE";

            // Left: Room Settings & Host Controls (Width 520)
            Rect leftPanel = new Rect(80, 115, 520, 890);
            GUI.Box(leftPanel, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(110, 140, 460, 35), "Thông Tin Ván Chơi", styles.HeaderStyle);

            GUI.Label(new Rect(110, 175, 460, 20), $"Chủ phòng: {room.Host}  {(isHost ? "(Bạn là Host)" : "")}", styles.MutedStyle);
            GUI.Label(new Rect(110, 196, 460, 20), $"Chế độ: {(isCompetitive ? "⚔️ Cạnh Tranh (Phá Hoại)" : "🤝 Hợp Tác (Cổ Điển)")}  {(room.HasPassword ? "🔒 Riêng tư" : "🌐 Công khai")}", styles.MutedStyle);
            GUI.Label(new Rect(110, 217, 460, 20), $"Thời gian mục tiêu: {UIStyles.FormatTimeLimit(room.TargetTimeSeconds)}", styles.MutedStyle);

            if (isHost)
            {
                if (styles.DrawButton(new Rect(110, 238, 460, 32), isCompetitive ? "Chế độ: ⚔️ CẠNH TRANH (Bấm để đổi)" : "Chế độ: 🤝 CỔ ĐIỂN (Bấm để đổi)", isCompetitive))
                {
                    net.SelectGameMode(isCompetitive ? "CLASSIC" : "COMPETITIVE");
                }
            }

            float puzzleY = isHost ? 278f : 242f;
            GUI.Label(new Rect(110, puzzleY, 460, 20), "Bộ xếp hình đang chọn:", styles.MutedStyle);
            GUI.Label(new Rect(110, puzzleY + 20, 460, 26), UIStyles.GetPuzzleName(room.PuzzleId), styles.BoldStyle);

            var pIds = new List<string>();
            if (net != null && net.ShopPuzzles != null && net.ShopPuzzles.Count > 0)
            {
                foreach (var p in net.ShopPuzzles) pIds.Add(p.Id);
            }
            else
            {
                var allPuzzles = ServiceLocator.Get<IPuzzleCatalog>()?.GetAllPuzzleData();
                if (allPuzzles != null && allPuzzles.Count > 0)
                {
                    foreach (var p in allPuzzles) if (p != null) pIds.Add(p.puzzleId);
                }
                else
                {
                    pIds.AddRange(new[] { "sunset_3x3", "forest_4x3", "ocean_4x4", "rei_10x10" });
                }
            }

            if (isHost)
            {
                for (int i = 0; i < pIds.Count; i++)
                {
                    string pid = pIds[i];
                    bool owned = net != null && net.OwnedPuzzles != null && net.OwnedPuzzles.Contains(pid);
                    bool active = room.PuzzleId == pid;
                    GUI.enabled = owned;
                    if (styles.DrawButton(new Rect(110, puzzleY + 48 + i * 44, 460, 38), UIStyles.GetPuzzleName(pid) + (owned ? "" : " (Chưa mở)"), active, false, false, 12))
                    {
                        net.SelectPuzzle(pid);
                    }
                    GUI.enabled = true;
                }
            }

            // Environment selection
            float envY = isHost ? (puzzleY + 48 + pIds.Count * 44 + 14) : 295f;
            GUI.Label(new Rect(110, envY, 460, 20), "Không gian 3D (3D Environment):", styles.MutedStyle);
            GUI.Label(new Rect(110, envY + 20, 460, 26), UIStyles.GetThemeName(room.ThemeId), styles.BoldStyle);

            if (isHost)
            {
                var themeCat = ServiceLocator.Get<IThemeCatalog>() ?? ContentRegistry.Instance;
                var allThemes = themeCat.GetAllThemes();
                for (int i = 0; i < allThemes.Count; i++)
                {
                    var t = allThemes[i];
                    string tid = t.themeId;
                    bool owned = net != null && net.OwnedThemes != null && net.OwnedThemes.Contains(tid);
                    bool active = room.ThemeId == tid;
                    GUI.enabled = owned;
                    if (styles.DrawButton(new Rect(110, envY + 48 + i * 46, 460, 40), UIStyles.GetThemeName(tid) + (owned ? "" : " (Chưa mở)"), active))
                    {
                        net.SelectTheme(tid);
                    }
                    GUI.enabled = true;
                }
            }

            // Ready & Start Game buttons
            var me = net.RoomPlayers.Find(p => p.Username == net.Username);
            bool ready = me != null && me.Ready;

            if (isCompetitive)
            {
                var infoBoxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                infoBoxStyle.normal.textColor = Color.yellow;
                GUI.Box(new Rect(110, 735, 460, 52), "🎲 Phe sẽ được hệ thống phân ngẫu nhiên khi bắt đầu", infoBoxStyle);
            }

            if (styles.DrawButton(new Rect(110, 800, 460, 56), ready ? "✓ ĐÃ SẴN SÀNG" : "SẴN SÀNG", ready))
            {
                net.SetReady(!ready);
            }

            if (isHost)
            {
                if (styles.DrawButton(new Rect(110, 870, 460, 60), "BẮT ĐẦU VÁN CHƠI", true))
                {
                    net.StartGame();
                }
            }

            if (styles.DrawButton(new Rect(110, 945, 460, 44), "Rời phòng", false, false, true))
            {
                net.LeaveRoom();
            }

            // Middle: 4 Chairs / Players (Width 640)
            Rect midPanel = new Rect(630, 115, 640, 890);
            GUI.Box(midPanel, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(660, 140, 580, 35), $"Thành Viên ({net.RoomPlayers.Count}/{room.MaxPlayers})", styles.HeaderStyle);

            for (int i = 0; i < 4; i++)
            {
                Rect slotRect = new Rect(660, 200 + i * 140, 580, 120);
                styles.DrawSolidRect(slotRect, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                if (i < net.RoomPlayers.Count)
                {
                    var p = net.RoomPlayers[i];
                    Color statCol = p.Connected ? (p.Ready ? styles.SuccessColor : styles.GoldColor) : styles.DangerColor;
                    styles.DrawSolidRect(new Rect(685, slotRect.y + 35, 16, 16), statCol);

                    string teamTag = "";
                    if (isCompetitive)
                    {
                        if (net.IsGameActive)
                        {
                            teamTag = p.Team == "SABOTEUR" ? " [😈 Phá Hoại]" : " [🔨 Thợ Xây]";
                        }
                        else
                        {
                            teamTag = " [🎲 Chờ phân đội]";
                        }
                    }
                    GUI.Label(new Rect(720, slotRect.y + 25, 340, 35), p.Username + teamTag + (p.IsHost ? " ★ (Chủ phòng)" : ""), styles.BoldStyle);
                    GUI.Label(new Rect(720, slotRect.y + 65, 300, 25), p.Connected ? (p.Ready ? "Đã sẵn sàng" : "Đang chờ sẵn sàng") : "Mất kết nối", styles.MutedStyle);

                    GUI.Label(new Rect(1080, slotRect.y + 40, 140, 35), p.Ready ? "SẴN SÀNG" : "CHỜ", p.Ready ? styles.BadgeStyle : styles.MutedStyle);
                }
                else
                {
                    GUI.Label(new Rect(720, slotRect.y + 45, 300, 35), "Ghế trống (Đang chờ người chơi...)", styles.MutedStyle);
                }
            }

            // Right: Chat & Friends (Width 530)
            Rect rightPanel = new Rect(1300, 115, 540, 890);
            GUI.Box(rightPanel, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(1330, 140, 480, 35), "Trò Chuyện & Bạn Bè", styles.HeaderStyle);
            GUI.Label(new Rect(1330, 180, 480, 25), "Nhấn giữ phím 'V' để nói Voice Chat", styles.MutedStyle);

            DrawChatBox(styles, new Rect(1330, 220, 480, 680), chatHistory);
        }

        private void DrawChatBox(UIStyles styles, Rect rect, List<string> chatHistory)
        {
            Rect logRect = new Rect(rect.x, rect.y, rect.width, rect.height - 80);
            styles.DrawSolidRect(logRect, new Color(0.10f, 0.11f, 0.16f, 0.9f));

            Rect viewRect = new Rect(0, 0, rect.width - 30, Mathf.Max(logRect.height, chatHistory.Count * 28));
            _waitingRoomChatScroll = GUI.BeginScrollView(logRect, _waitingRoomChatScroll, viewRect);
            for (int i = 0; i < chatHistory.Count; i++)
            {
                GUI.Label(new Rect(10, i * 28 + 4, rect.width - 40, 25), chatHistory[i], styles.BodyStyle);
            }
            GUI.EndScrollView();

            // Emoji Bar
            string[] emojis = { "😊", "👍", "🎉", "🔥", "❤️", "👏" };
            for (int e = 0; e < emojis.Length; e++)
            {
                if (GUI.Button(new Rect(rect.x + e * 42, rect.y + rect.height - 75, 38, 30), emojis[e], styles.BtnStyle))
                {
                    PuzzleNetworkManager.Instance?.SendEmoji(emojis[e]);
                }
            }

            // Input Row
            _chatInput = GUI.TextField(new Rect(rect.x, rect.y + rect.height - 38, rect.width - 90, 38), _chatInput, styles.InputStyle);
            if (GUI.Button(new Rect(rect.x + rect.width - 80, rect.y + rect.height - 38, 80, 38), "Gửi", styles.PrimaryBtnStyle) ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && GUIUtility.keyboardControl != 0))
            {
                if (!string.IsNullOrWhiteSpace(_chatInput))
                {
                    PuzzleNetworkManager.Instance?.SendChat(_chatInput.Trim());
                    _chatInput = "";
                }
            }
        }
    }
}
