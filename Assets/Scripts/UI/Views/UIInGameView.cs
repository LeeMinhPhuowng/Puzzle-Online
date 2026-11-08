using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Utility;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Menu tạm dừng và các hộp thoại. GameplayHUD quản lý HUD và chat bằng Canvas.
    /// </summary>
    public class UIInGameView
    {
        // Pause Menu
        private bool _showInGameMenu = false;

        // Completion Popup
        private bool _showVictoryModal = false;
        private string _victoryWinner = "";
        private int _victoryBonus = 0;
        private string _gameOverResult = "";
        private string _gameOverWinnerTeam = "";
        private int _gameOverTopScore = 0;

        // Timeout Modal
        private bool _showTimeoutModal = false;

        // Reconnection Prompt Modal
        private ReconnectPromptInfo _reconnectPrompt = null;

        // Join Password Modal
        private RoomInfo _joiningPasswordRoom = null;
        private string _joiningPasswordRoomId = "";
        private string _joinPasswordInput = "";

        public bool ShowInGameMenu
        {
            get => _showInGameMenu;
            set => _showInGameMenu = value;
        }

        public bool IsModalOrChatOpen
        {
            get
            {
                if (_reconnectPrompt != null) return true;
                if (_joiningPasswordRoom != null || !string.IsNullOrEmpty(_joiningPasswordRoomId)) return true;
                if (_showVictoryModal) return true;
                if (_showTimeoutModal) return true;
                if (_showInGameMenu) return true;
                return false;
            }
        }

        public void SetVictory(string winner, int bonus)
        {
            _showVictoryModal = true;
            _victoryWinner = winner;
            _victoryBonus = bonus;
        }

        public void SetGameOver(string result, string winnerTeam, string winner, int score)
        {
            _showVictoryModal = true;
            _gameOverResult = result;
            _gameOverWinnerTeam = winnerTeam;
            if (!string.IsNullOrEmpty(winner)) _victoryWinner = winner;
            if (score > 0) _gameOverTopScore = score;
        }

        public void SetTimeout(bool show)
        {
            _showTimeoutModal = show;
        }

        public void SetReconnectPrompt(ReconnectPromptInfo info)
        {
            _reconnectPrompt = info;
        }

        public void SetPasswordRequired(string roomId, string roomName)
        {
            _joiningPasswordRoomId = roomId;
            _joinPasswordInput = "";
        }

        public void SetPasswordPrompt(RoomInfo room)
        {
            _joiningPasswordRoom = room;
            _joiningPasswordRoomId = room != null ? room.Id : "";
            _joinPasswordInput = "";
        }

        public void ClearPasswordModal()
        {
            _joiningPasswordRoom = null;
            _joiningPasswordRoomId = "";
            _joinPasswordInput = "";
        }

        public void ResetRoomState()
        {
            _showInGameMenu = false;
            _showVictoryModal = false;
            _showTimeoutModal = false;
            _reconnectPrompt = null;
            ClearPasswordModal();
        }

        #region InGame HUD


        private void DrawInGamePauseMenu(UIStyles styles, PuzzleNetworkManager net)
        {
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.75f));

            float panelW = 620f;
            float panelH = 550f;
            Rect panel = new Rect(UIStyles.VirtualWidth * 0.5f - panelW * 0.5f, UIStyles.VirtualHeight * 0.5f - panelH * 0.5f, panelW, panelH);

            // Nền đen bóng và viền vàng Gold kim loại sang trọng
            styles.DrawSolidRect(panel, new Color(0.08f, 0.09f, 0.12f, 0.98f));
            styles.DrawBorder(panel, 2f, styles.GoldColor);

            // Header - Tiêu đề vàng gold, phụ đề trắng sáng
            var oldTitleColor = styles.TitleStyle.normal.textColor;
            styles.TitleStyle.normal.textColor = styles.GoldColor;
            GUI.Label(new Rect(panel.x + 36, panel.y + 24, panel.width - 72, 34), "CÀI ĐẶT & TẠM DỪNG", styles.TitleStyle);
            styles.TitleStyle.normal.textColor = oldTitleColor;

            var oldMutedColor = styles.MutedStyle.normal.textColor;
            styles.MutedStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f, 1f);
            GUI.Label(new Rect(panel.x + 36, panel.y + 58, panel.width - 72, 22), "Nhấn [Esc] hoặc bấm TIẾP TỤC để quay lại bàn chơi", styles.MutedStyle);
            styles.MutedStyle.normal.textColor = oldMutedColor;

            // Kẻ phân cách Vàng mờ
            styles.DrawSolidRect(new Rect(panel.x + 36, panel.y + 86, panel.width - 72, 1.5f), new Color(1f, 0.82f, 0.22f, 0.45f));

            // Section 1: CÀI ĐẶT ÂM THANH & MICROPHONE (Khớp với Settings ở SCN_Room)
            var oldHeaderColor = styles.HeaderStyle.normal.textColor;
            styles.HeaderStyle.normal.textColor = styles.GoldColor;
            GUI.Label(new Rect(panel.x + 36, panel.y + 98, panel.width - 72, 26), "CÀI ĐẶT ÂM THANH & MICROPHONE", styles.HeaderStyle);
            styles.HeaderStyle.normal.textColor = oldHeaderColor;

            var audio = AudioManager.Instance;
            float curSFX = audio != null ? audio.SFXVolume : 1f;
            float curVoice = audio != null ? audio.VoiceVolume : 1f;
            float curMusic = audio != null ? audio.MusicVolume : 0.6f;

            // 1. Slider SFX
            float newSFX = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 128, panel.width - 72, 32), "Hiệu ứng âm thanh (SFX)", curSFX, "%", styles.GoldColor);
            if (audio != null && Mathf.Abs(newSFX - curSFX) > 0.005f)
            {
                audio.SetSFXVolume(newSFX);
            }

            // 2. Slider Voice Chat
            float newVoice = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 164, panel.width - 72, 32), "Âm lượng Voice Chat (Bạn bè)", curVoice, "%", styles.GoldColor);
            if (audio != null && Mathf.Abs(newVoice - curVoice) > 0.005f)
            {
                audio.SetVoiceVolume(newVoice);
            }

            // 3. Slider Music
            float newMusic = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 200, panel.width - 72, 32), "Nhạc nền (BGM)", curMusic, "%", styles.GoldColor);
            if (audio != null && Mathf.Abs(newMusic - curMusic) > 0.005f)
            {
                audio.SetMusicVolume(newMusic);
            }

            // 4. Slider Mic Gain
            if (net != null && net.Voice != null)
            {
                float curGainNorm = Mathf.Clamp01((net.Voice.MicGain - 0.5f) / 3.5f);
                float newGainNorm = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 236, panel.width - 72, 32), "Khuếch đại Mic (Gain)", curGainNorm, "%", styles.GoldColor);
                if (Mathf.Abs(newGainNorm - curGainNorm) > 0.005f)
                {
                    net.Voice.MicGain = 0.5f + newGainNorm * 3.5f;
                }
            }

            // Section 2: ĐIỀU KHIỂN & ĐỒ HỌA
            styles.HeaderStyle.normal.textColor = styles.GoldColor;
            GUI.Label(new Rect(panel.x + 36, panel.y + 278, panel.width - 72, 26), "ĐIỀU KHIỂN & ĐỒ HỌA", styles.HeaderStyle);
            styles.HeaderStyle.normal.textColor = oldHeaderColor;

            // Slider Mouse Sensitivity
            float curSens = PlayerPrefs.GetFloat("MouseSensitivity", 2.0f);
            float curSensNorm = Mathf.Clamp01((curSens - 0.5f) / 3.5f);
            float newSensNorm = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 308, panel.width - 72, 32), "Độ nhạy chuột (Mouse)", curSensNorm, "%", styles.GoldColor);
            if (Mathf.Abs(newSensNorm - curSensNorm) > 0.005f)
            {
                float newSens = 0.5f + newSensNorm * 3.5f;
                PlayerPrefs.SetFloat("MouseSensitivity", newSens);
                PlayerPrefs.Save();
                if (FirstPersonCamera.FirstPersonCameraScript.Instance != null)
                {
                    FirstPersonCamera.FirstPersonCameraScript.Instance.SetMouseSensitivity(newSens, newSens);
                }
            }

            // Row Toàn màn hình (Toàn màn hình: BẬT / TẮT)
            Rect fsRow = new Rect(panel.x + 36, panel.y + 348, panel.width - 72, 38);
            styles.DrawSolidRect(fsRow, new Color(0.12f, 0.14f, 0.19f, 0.95f));
            styles.DrawBorder(fsRow, 1f, new Color(0.25f, 0.28f, 0.36f, 0.7f));
            string fsLabel = Screen.fullScreen ? "Toàn màn hình: BẬT" : "Cửa sổ: BẬT";
            if (styles.DrawButton(new Rect(fsRow.x + 10, fsRow.y + 3, 220, 32), fsLabel, false, isGold: Screen.fullScreen, isDanger: false, 13))
            {
                Screen.fullScreen = !Screen.fullScreen;
            }

            // Divider vàng
            styles.DrawSolidRect(new Rect(panel.x + 36, panel.y + 406, panel.width - 72, 1.5f), new Color(1f, 0.82f, 0.22f, 0.45f));

            // Buttons: TIẾP TỤC (Vàng chữ đen) & RỜI PHÒNG (Đỏ chữ trắng)
            float btnW = (panel.width - 72 - 16) * 0.5f;
            if (styles.DrawButton(new Rect(panel.x + 36, panel.y + 428, btnW, 52), "TIẾP TỤC", false, isGold: true, isDanger: false, 18))
            {
                _showInGameMenu = false;
                CursorManager.Lock();
            }

            if (styles.DrawButton(new Rect(panel.x + 36 + btnW + 16, panel.y + 428, btnW, 52), "RỜI PHÒNG", false, isGold: false, isDanger: true, 18))
            {
                AudioManager.Instance?.StopAmbience();
                net.LeaveRoom();
            }
        }

        public void DrawPauseMenu(UIStyles styles)
        {
            // Đã chuyển toàn bộ Pause Menu sang Canvas UI SettingsView (LobbySettingsModalUI)
        }


        #endregion

        #region Modals
        public void DrawModals(UIStyles styles)
        {
            DrawVictoryModal(styles);
            DrawTimeoutModal(styles);
            DrawReconnectModal(styles);
            DrawPasswordModal(styles);
        }

        private void DrawVictoryModal(UIStyles styles)
        {
            if (!_showVictoryModal) return;

            var net = PuzzleNetworkManager.Instance;
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.85f));

            float modalW = 860f;
            float modalH = 600f;
            Rect modal = new Rect((UIStyles.VirtualWidth - modalW) * 0.5f, (UIStyles.VirtualHeight - modalH) * 0.5f, modalW, modalH);

            // Nền đen chuẩn và viền vàng kim loại sang trọng
            styles.DrawSolidRect(modal, new Color(0.08f, 0.09f, 0.12f, 0.98f));
            styles.DrawBorder(modal, 2f, styles.GoldColor);

            bool isComp = net != null && net.IsCompetitive;
            bool isBattle = net != null && net.IsBattleMode;
            bool isClassic = !isComp && !isBattle;

            // Xác định kết quả và tiêu đề (Tuyệt đối không dùng emoji)
            string titleText;
            string subText;
            bool buildersWon = _gameOverResult == "BUILDERS_WIN" || (net != null && net.PlacedPieces >= net.TotalPieces && net.TotalPieces > 0);

            if (isComp)
            {
                if (buildersWon)
                {
                    titleText = "ĐỘI THỢ XÂY CHIẾN THẮNG!";
                    subText = "Đội Thợ Xây đã xuất sắc hoàn thành bức tranh trước khi hết thời gian!";
                }
                else
                {
                    titleText = "ĐỘI PHÁ HOẠI CHIẾN THẮNG!";
                    subText = "Đã hết thời gian! Đội Phá Hoại đã ngăn chặn hoàn thành tranh thành công!";
                }
            }
            else if (isBattle)
            {
                string leadName = !string.IsNullOrEmpty(_victoryWinner) ? _victoryWinner : (net?.RoomPlayers?.OrderByDescending(p => p.Score).FirstOrDefault()?.Username ?? "");
                titleText = "KẾT THÚC HỖN LOẠN";
                subText = !string.IsNullOrEmpty(leadName) ? $"Người chiến thắng: {leadName.ToUpper()}" : "Trận đấu hỗn loạn đã kết thúc!";
            }
            else
            {
                titleText = "HOÀN THÀNH BỨC TRANH";
                subText = "Bức tranh đã được các người chơi hoàn thành trọn vẹn!";
            }

            // Header Title Vàng Gold
            var titleStyle = new GUIStyle(styles.TitleStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = styles.GoldColor;
            GUI.Label(new Rect(modal.x + 30, modal.y + 22, modal.width - 60, 38), titleText, titleStyle);

            // Đường kẻ vàng phân cách
            styles.DrawSolidRect(new Rect(modal.x + 40, modal.y + 68, modal.width - 80, 1.5f), new Color(1f, 0.82f, 0.22f, 0.45f));

            // Subtitle
            var subStyle = new GUIStyle(styles.MutedStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16
            };
            subStyle.normal.textColor = new Color(0.85f, 0.88f, 0.94f, 1f);
            GUI.Label(new Rect(modal.x + 30, modal.y + 78, modal.width - 60, 24), subText, subStyle);

            // Box Thưởng Xu
            Rect rewardBox = new Rect(modal.x + 40, modal.y + 112, modal.width - 80, 56);
            styles.DrawSolidRect(rewardBox, new Color(0.12f, 0.14f, 0.20f, 0.95f));
            styles.DrawBorder(rewardBox, 1f, new Color(1f, 0.82f, 0.22f, 0.5f));

            int myScore = 0;
            if (net?.RoomPlayers != null && !string.IsNullOrEmpty(net.Username))
            {
                var me = net.RoomPlayers.FirstOrDefault(p => p.Username.Equals(net.Username, StringComparison.OrdinalIgnoreCase));
                if (me != null) myScore = me.Score;
            }

            string rewardMain;
            string rewardSub;
            if (isComp)
            {
                bool isMyTeamWinner = (buildersWon && net != null && !net.IsSaboteur) || (!buildersWon && net != null && net.IsSaboteur);
                rewardMain = isMyTeamWinner ? $"Chiến thắng! Nhận thưởng: +{_victoryBonus} Xu" : $"Hoàn thành ván đấu. Nhận thưởng: +{_victoryBonus} Xu";
                rewardSub = $"(Đội chiến thắng: +150 Xu  |  Đội thua cuộc: +50 Xu)  |  Điểm tích lũy: +{myScore} điểm";
            }
            else if (isBattle)
            {
                rewardMain = $"Nhận thưởng: +{_victoryBonus} Xu";
                rewardSub = $"Điểm ghi được: +{myScore} điểm (đã lưu vào Tổng Điểm)  |  Thưởng: +{_victoryBonus} Xu";
            }
            else
            {
                rewardMain = $"Nhận thưởng hoàn thành: +{_victoryBonus} Xu";
                rewardSub = $"Điểm đóng góp: +{myScore} điểm (đã lưu vào Tổng Điểm)  |  Thưởng: +{_victoryBonus} Xu";
            }

            var rewMainStyle = new GUIStyle(styles.BoldStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18
            };
            rewMainStyle.normal.textColor = styles.GoldColor;
            GUI.Label(new Rect(rewardBox.x + 10, rewardBox.y + 6, rewardBox.width - 20, 24), rewardMain, rewMainStyle);

            var rewSubStyle = new GUIStyle(styles.MutedStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };
            rewSubStyle.normal.textColor = new Color(0.75f, 0.80f, 0.88f, 1f);
            GUI.Label(new Rect(rewardBox.x + 10, rewardBox.y + 30, rewardBox.width - 20, 20), rewardSub, rewSubStyle);

            // Nội dung ở giữa:
            // - Cạnh tranh: KHÔNG có bảng xếp hạng, hiện tóm tắt ván đấu
            // - Hỗn loạn & Giải trí: CÓ BẢNG XẾP HẠNG
            if (isComp)
            {
                Rect infoBox = new Rect(modal.x + 40, modal.y + 182, modal.width - 80, 320);
                styles.DrawSolidRect(infoBox, new Color(0.10f, 0.12f, 0.16f, 0.95f));
                styles.DrawBorder(infoBox, 1f, new Color(0.22f, 0.26f, 0.35f, 0.8f));

                var infoTitleStyle = new GUIStyle(styles.BoldStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17
                };
                infoTitleStyle.normal.textColor = styles.GoldColor;
                GUI.Label(new Rect(infoBox.x + 20, infoBox.y + 20, infoBox.width - 40, 28), "TỔNG KẾT VÁN ĐẤU TRANH ĐẤU", infoTitleStyle);

                var infoBodyStyle = new GUIStyle(styles.BodyStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15
                };
                infoBodyStyle.normal.textColor = new Color(0.90f, 0.92f, 0.96f, 1f);

                string myRole = (net != null && net.IsSaboteur) ? "Đội Phá Hoại" : "Đội Thợ Xây";
                string winTeamStr = buildersWon ? "Đội Thợ Xây" : "Đội Phá Hoại";
                bool iWon = (buildersWon && net != null && !net.IsSaboteur) || (!buildersWon && net != null && net.IsSaboteur);
                string myResultStr = iWon ? "CHIẾN THẮNG" : "THUA CUỘC";

                string infoText = $"Vai trò của bạn:  {myRole}\n\n" +
                                  $"Đội chiến thắng:  {winTeamStr}\n\n" +
                                  $"Kết quả cá nhân:  {myResultStr}\n\n" +
                                  $"Tiến độ ghép tranh:  {net?.PlacedPieces ?? 0}/{net?.TotalPieces ?? 0} mảnh";

                GUI.Label(new Rect(infoBox.x + 30, infoBox.y + 70, infoBox.width - 60, 200), infoText, infoBodyStyle);
            }
            else
            {
                // BẢNG XẾP HẠNG cho Giải trí & Hỗn loạn
                Rect rankBox = new Rect(modal.x + 40, modal.y + 182, modal.width - 80, 320);
                styles.DrawSolidRect(rankBox, new Color(0.10f, 0.12f, 0.16f, 0.95f));
                styles.DrawBorder(rankBox, 1f, new Color(0.22f, 0.26f, 0.35f, 0.8f));

                var rankHeaderStyle = new GUIStyle(styles.BoldStyle)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 15
                };
                rankHeaderStyle.normal.textColor = styles.GoldColor;
                string rankHeaderText = isBattle ? "BẢNG XẾP HẠNG ĐIỂM SỐ HỖN LOẠN" : "BẢNG XẾP HẠNG ĐÓNG GÓP";
                GUI.Label(new Rect(rankBox.x + 24, rankBox.y + 12, 350, 26), rankHeaderText, rankHeaderStyle);

                // Đường phân cách tiêu đề bảng
                styles.DrawSolidRect(new Rect(rankBox.x + 20, rankBox.y + 42, rankBox.width - 40, 1f), new Color(0.25f, 0.30f, 0.40f, 0.7f));

                // Danh sách người chơi sắp xếp theo điểm giảm dần
                var players = net?.RoomPlayers != null
                    ? net.RoomPlayers.OrderByDescending(p => p.Score).ToList()
                    : new List<PlayerInfo>();

                float rowY = rankBox.y + 52;
                float rowH = 36f;
                int maxRows = Mathf.Min(players.Count, 6);

                var rankRowStyle = new GUIStyle(styles.BodyStyle) { fontSize = 15 };
                var scoreRowStyle = new GUIStyle(styles.BoldStyle) { fontSize = 15, alignment = TextAnchor.MiddleRight };

                for (int i = 0; i < maxRows; i++)
                {
                    var p = players[i];
                    bool isTop1 = (i == 0 && p.Score > 0);

                    // Nền xen kẽ cho các dòng
                    if (i % 2 == 1)
                    {
                        styles.DrawSolidRect(new Rect(rankBox.x + 10, rowY, rankBox.width - 20, rowH), new Color(0.14f, 0.16f, 0.22f, 0.6f));
                    }

                    Color rowColor = isTop1 ? styles.GoldColor : new Color(0.92f, 0.94f, 0.98f, 1f);
                    rankRowStyle.normal.textColor = rowColor;
                    rankRowStyle.fontStyle = isTop1 ? FontStyle.Bold : FontStyle.Normal;
                    scoreRowStyle.normal.textColor = rowColor;

                    string rankPrefix = $"{i + 1}.";
                    string nameStr = $"{rankPrefix,-4} {p.Username}";
                    string scoreStr = $"{p.Score} điểm";

                    GUI.Label(new Rect(rankBox.x + 24, rowY + 5, 400, 26), nameStr, rankRowStyle);
                    GUI.Label(new Rect(rankBox.xMax - 260, rowY + 5, 236, 26), scoreStr, scoreRowStyle);

                    rowY += rowH + 4f;
                }

                if (players.Count == 0)
                {
                    var emptyStyle = new GUIStyle(styles.MutedStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                    GUI.Label(new Rect(rankBox.x + 20, rankBox.y + 100, rankBox.width - 40, 30), "Không có dữ liệu người chơi", emptyStyle);
                }
            }

            // Nút "QUAY VỀ SẢNH" Vàng Gold phẳng
            float btnW = 320f;
            float btnH = 50f;
            Rect btnRect = new Rect(modal.x + (modal.width - btnW) * 0.5f, modal.y + modal.height - btnH - 18, btnW, btnH);

            if (styles.DrawButton(btnRect, "QUAY VỀ SẢNH", false, isGold: true, isDanger: false, 16))
            {
                AudioManager.Instance?.PlayClick();
                AudioManager.Instance?.StopAmbience();
                _showVictoryModal = false;
                PuzzleNetworkManager.Instance?.LeaveRoom();
            }
        }

        private void DrawTimeoutModal(UIStyles styles)
        {
            if (!_showTimeoutModal) return;

            var net = PuzzleNetworkManager.Instance;
            if (net == null || !net.IsGameActive) return;

            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.85f));

            Rect cardRect = new Rect((UIStyles.VirtualWidth - 760) / 2f, (UIStyles.VirtualHeight - 460) / 2f, 760, 460);
            GUI.Box(cardRect, GUIContent.none, styles.CardStyle);
            styles.DrawBorder(cardRect, 2f, styles.GoldColor);

            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 35, 660, 45), "HẾT THỜI GIAN MỤC TIÊU", styles.TitleStyle);
            styles.DrawSolidRect(new Rect(cardRect.x + 50, cardRect.y + 88, 660, 2), new Color(0.35f, 0.4f, 0.5f, 0.6f));

            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 105, 660, 32), "Thời gian đặt ra cho màn chơi đã kết thúc!", styles.HeaderStyle);

            float percent = net.TotalPieces > 0 ? (float)net.PlacedPieces / net.TotalPieces * 100f : 0f;
            string statsText = $"  • Tiến độ hiện tại: {net.PlacedPieces}/{net.TotalPieces} mảnh ghép ({percent:F0}%)\n" +
                               $"  • Bộ tranh đang ghép: {UIStyles.GetPuzzleName(net.CurrentPuzzleId)}\n" +
                               $"  • Bối cảnh: {UIStyles.GetThemeName(net.CurrentThemeId)}";
            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 145, 660, 80), statsText, styles.BodyStyle);

            if (net.IsHost)
            {
                GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 235, 660, 45),
                    "Bạn là chủ phòng. Hãy lựa chọn tiếp tục chơi (chuyển sang chế độ thư giãn không giới hạn thời gian) hoặc khởi đầu lại ván chơi mới:",
                    styles.MutedStyle);

                if (styles.DrawButton(new Rect(cardRect.x + 50, cardRect.y + 295, 315, 58), "TIẾP TỤC CHƠI (THƯ GIÃN)", true, false, false, 16))
                {
                    _showTimeoutModal = false;
                    net.SendContinueGame();
                }

                if (styles.DrawButton(new Rect(cardRect.x + 395, cardRect.y + 295, 315, 58), "KHỞI ĐẦU LẠI", false, true, false, 17))
                {
                    _showTimeoutModal = false;
                    net.SendRestartGame();
                }

                if (styles.DrawButton(new Rect(cardRect.x + 220, cardRect.y + 375, 320, 50), "RỜI PHÒNG", false, false, true, 16))
                {
                    AudioManager.Instance?.StopAmbience();
                    _showTimeoutModal = false;
                    net.LeaveRoom();
                }
            }
            else
            {
                GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 250, 660, 50),
                    "Đang chờ chủ phòng lựa chọn tiếp tục chơi hoặc khởi đầu lại ván chơi mới...",
                    styles.MutedStyle);

                if (styles.DrawButton(new Rect(cardRect.x + 220, cardRect.y + 340, 320, 55), "RỜI PHÒNG", false, false, true, 17))
                {
                    AudioManager.Instance?.StopAmbience();
                    _showTimeoutModal = false;
                    net.LeaveRoom();
                }
            }
        }

        private void DrawReconnectModal(UIStyles styles)
        {
            if (_reconnectPrompt == null) return;

            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.82f));

            Rect cardRect = new Rect((UIStyles.VirtualWidth - 720) / 2f, (UIStyles.VirtualHeight - 430) / 2f, 720, 430);
            styles.DrawSolidRect(cardRect, styles.CardColor);
            styles.DrawBorder(cardRect, 2f, styles.AccentColor);

            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 35, 640, 40), "THÔNG BÁO KẾT NỐI LẠI", styles.HeaderStyle);
            styles.DrawSolidRect(new Rect(cardRect.x + 40, cardRect.y + 85, 640, 2), new Color(0.3f, 0.35f, 0.45f, 0.6f));

            string stateText = _reconnectPrompt.State == "PLAYING" ? "Đang diễn ra ván chơi" : "Đang chờ trong phòng";
            string msg = $"Hệ thống phát hiện bạn có một phòng chơi trước đó chưa hoàn thành:\n\n" +
                         $"  • Tên phòng: {_reconnectPrompt.RoomName}\n" +
                         $"  • Thành viên: {_reconnectPrompt.Count}/{_reconnectPrompt.Max}\n" +
                         $"  • Trạng thái: {stateText}\n\n" +
                         $"Bạn có muốn quay lại phòng này để tiếp tục không?";
            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 110, 640, 190), msg, styles.BodyStyle);

            if (styles.DrawButton(new Rect(cardRect.x + 40, cardRect.y + 330, 300, 58), "QUAY LẠI PHÒNG", true, false, false, 18))
            {
                string rid = _reconnectPrompt.RoomId;
                _reconnectPrompt = null;
                PuzzleNetworkManager.Instance?.SendReconnectDecision(true, rid);
            }

            if (styles.DrawButton(new Rect(cardRect.x + 380, cardRect.y + 330, 300, 58), "Ở LẠI TRANG CHỦ", false, false, true, 18))
            {
                string rid = _reconnectPrompt.RoomId;
                _reconnectPrompt = null;
                PuzzleNetworkManager.Instance?.SendReconnectDecision(false, rid);
                CursorManager.Unlock();
                string curScene = SceneManager.GetActiveScene().name;
                if (curScene != "SCN_Menu" && curScene != "SCN_MainMenu")
                {
                    SceneManager.LoadScene("SCN_Menu");
                }
            }
        }

        private void DrawPasswordModal(UIStyles styles)
        {
            if (_joiningPasswordRoom == null && string.IsNullOrEmpty(_joiningPasswordRoomId)) return;

            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0.04f, 0.05f, 0.08f, 0.85f));

            Rect cardRect = new Rect((UIStyles.VirtualWidth - 620) / 2f, (UIStyles.VirtualHeight - 340) / 2f, 620, 340);
            GUI.Box(cardRect, GUIContent.none, styles.CardStyle);
            styles.DrawBorder(cardRect, 2, styles.AccentColor);

            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 30, 540, 35), "PHÒNG CÓ MẬT KHẨU", styles.HeaderStyle);
            styles.DrawSolidRect(new Rect(cardRect.x + 40, cardRect.y + 75, 540, 2), new Color(0.3f, 0.35f, 0.45f, 0.6f));

            string roomName = _joiningPasswordRoom != null ? _joiningPasswordRoom.Name : ("Phòng #" + _joiningPasswordRoomId);
            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 95, 540, 25), $"Vui lòng nhập mật khẩu để tham gia: {roomName}", styles.BodyStyle);

            GUI.Label(new Rect(cardRect.x + 40, cardRect.y + 135, 540, 22), "Mật khẩu phòng:", styles.MutedStyle);
            _joinPasswordInput = GUI.PasswordField(new Rect(cardRect.x + 40, cardRect.y + 162, 540, 46), _joinPasswordInput, '•', styles.InputStyle);

            if (styles.DrawButton(new Rect(cardRect.x + 40, cardRect.y + 245, 255, 54), "VÀO PHÒNG", true, false, false, 16))
            {
                string rid = _joiningPasswordRoom != null ? _joiningPasswordRoom.Id : _joiningPasswordRoomId;
                string pwd = _joinPasswordInput;
                ClearPasswordModal();
                PuzzleNetworkManager.Instance?.JoinRoom(rid, pwd);
            }

            if (styles.DrawButton(new Rect(cardRect.x + 325, cardRect.y + 245, 255, 54), "HỦY BỎ", false, false, true, 16))
            {
                ClearPasswordModal();
            }
        }
        #endregion
    }
}
