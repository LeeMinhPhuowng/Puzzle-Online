using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Utility;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Giao diện trong ván chơi (In-Game HUD, Bảng điểm, Menu tạm dừng, Chat trong trận) và các Hộp thoại (Modals: Thắng, Hết giờ, Kết nối lại, Mật khẩu).
    /// </summary>
    public class UIInGameView
    {
        // Chat Drawer
        private bool _showInGameChat = false;
        private Vector2 _inGameChatScroll;
        private string _chatInput = "";

        // Pause Menu
        private bool _showInGameMenu = false;

        // Completion Popup
        private bool _showVictoryModal = false;
        private string _victoryWinner = "";
        private int _victoryBonus = 0;

        // Timeout Modal
        private bool _showTimeoutModal = false;

        // Reconnection Prompt Modal
        private ReconnectPromptInfo _reconnectPrompt = null;

        // Join Password Modal
        private RoomInfo _joiningPasswordRoom = null;
        private string _joiningPasswordRoomId = "";
        private string _joinPasswordInput = "";

        public bool ShowInGameChat
        {
            get => _showInGameChat;
            set => _showInGameChat = value;
        }

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
                if (_showInGameChat) return true;
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

        public void ResetChatScroll()
        {
            _inGameChatScroll.y = float.MaxValue;
        }

        public void ResetRoomState()
        {
            _showInGameChat = false;
            _showInGameMenu = false;
            _showVictoryModal = false;
            _showTimeoutModal = false;
            _reconnectPrompt = null;
            _chatInput = "";
            ClearPasswordModal();
        }

        #region InGame HUD
        public void DrawHUD(UIStyles styles, List<string> chatHistory)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            // Top Bar HUD
            Rect topHud = new Rect(40, 20, 1840, 75);
            styles.DrawSolidRect(topHud, new Color(0.10f, 0.12f, 0.18f, 0.88f));

            // Room Name & Environment
            GUI.Label(new Rect(70, 28, 450, 30), $"Phòng: {net.CurrentRoom?.Name}", styles.BoldStyle);
            GUI.Label(new Rect(70, 58, 450, 25), $"Không gian: {UIStyles.GetThemeName(net.CurrentThemeId)}", styles.MutedStyle);

            // Progress Bar
            float pct = net.TotalPieces <= 0 ? 0f : (float)net.PlacedPieces / net.TotalPieces;
            Rect progBg = new Rect(550, 35, 750, 26);
            styles.DrawSolidRect(progBg, new Color(0.18f, 0.22f, 0.30f, 1f));
            styles.DrawSolidRect(new Rect(550, 35, 750 * pct, 26), styles.AccentColor);
            GUI.Label(new Rect(550, 35, 750, 26), $"{net.PlacedPieces}/{net.TotalPieces} Mảnh ({Mathf.RoundToInt(pct * 100)}%)", styles.BadgeStyle);

            // Timer
            if (net.RemainingSeconds < 0)
            {
                GUI.Label(new Rect(1330, 30, 160, 45), "∞ Vô hạn", styles.TitleStyle);
            }
            else
            {
                int m = net.RemainingSeconds / 60;
                int s = net.RemainingSeconds % 60;
                GUI.Label(new Rect(1330, 30, 160, 45), $"{m:00}:{s:00}", styles.TitleStyle);
            }

            // Voice Status Indicator
            if (net.Voice != null && net.Voice.IsTransmitting)
            {
                styles.DrawSolidRect(new Rect(1510, 38, 14, 14), styles.SuccessColor);
                GUI.Label(new Rect(1535, 34, 160, 30), "MIC BẬT [V]", styles.BoldStyle);
            }
            else
            {
                GUI.Label(new Rect(1510, 34, 160, 30), "MIC TẮT [V]", styles.MutedStyle);
            }

            // Quick Menu button (Leave / Save)
            if (styles.DrawButton(new Rect(1690, 32, 160, 44), "Rời phòng", false, false, true))
            {
                net.LeaveRoom();
            }

            // Player Scoreboard Panel (Top Left)
            Rect scorePanel = new Rect(40, 110, 320, 50 + net.RoomPlayers.Count * 45);
            styles.DrawSolidRect(scorePanel, new Color(0.10f, 0.12f, 0.18f, 0.85f));
            GUI.Label(new Rect(55, 120, 290, 28), "BẢNG ĐIỂM", styles.BoldStyle);
            for (int i = 0; i < net.RoomPlayers.Count; i++)
            {
                var p = net.RoomPlayers[i];
                GUI.Label(new Rect(55, 155 + i * 45, 190, 30), p.Username + (p.IsHost ? " ★" : ""), styles.BodyStyle);
                GUI.Label(new Rect(250, 155 + i * 45, 90, 30), $"{p.Score} đ", styles.BoldStyle);
            }

            // Bottom Right Chat Drawer
            if (_showInGameChat)
            {
                Rect chatPanel = new Rect(1360, 600, 520, 440);
                GUI.Box(chatPanel, GUIContent.none, styles.CardStyle);
                GUI.Label(new Rect(1380, 615, 300, 30), "Trò chuyện trong ván [T]", styles.BoldStyle);
                DrawChatBox(styles, new Rect(1380, 650, 480, 370), chatHistory);
            }
            else
            {
                if (styles.DrawButton(new Rect(1720, 1010, 160, 44), "Chat [T]", false))
                {
                    _showInGameChat = true;
                    CursorManager.Unlock();
                }
            }

            if (_showInGameMenu)
            {
                DrawInGamePauseMenu(styles, net);
            }

            if (!_showInGameMenu)
            {
                bool isOverhead = FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive;

                if (!isOverhead)
                {
                    // Screen Center Reticle / White Dot (Chỉ hiện khi ở góc nhìn thứ nhất FPS)
                    float cx = UIStyles.VirtualWidth * 0.5f;
                    float cy = UIStyles.VirtualHeight * 0.5f;
                    styles.DrawSolidRect(new Rect(cx - 5, cy - 5, 10, 10), new Color(0f, 0f, 0f, 0.7f));
                    styles.DrawSolidRect(new Rect(cx - 3, cy - 3, 6, 6), Color.white);

                    // Controls Helper Bar (Bottom Center) - Chế độ FPS
                    Rect ctrlBar = new Rect(UIStyles.VirtualWidth * 0.5f - 480, 1015, 960, 40);
                    styles.DrawSolidRect(ctrlBar, new Color(0.10f, 0.12f, 0.18f, 0.85f));
                    GUI.Label(ctrlBar, "⚡ [Z] Góc nhìn trên cao (Top-down)  •  [R / Cuộn chuột] Xoay 90°  •  [Chuột trái] Cầm / Thả  •  [V] Voice Chat", styles.BadgeStyle);
                }
                else
                {
                    // Controls Helper Bar (Bottom Center) - Chế độ góc nhìn trên cao Overhead
                    Rect ctrlBar = new Rect(UIStyles.VirtualWidth * 0.5f - 490, 1015, 980, 40);
                    styles.DrawSolidRect(ctrlBar, new Color(0.08f, 0.20f, 0.28f, 0.90f));
                    styles.DrawBorder(ctrlBar, 1.5f, styles.AccentColor);
                    GUI.Label(ctrlBar, "🎯 [Z / ESC] Trở về góc nhìn thứ nhất  •  [Chuột trái] Nhấc / Kéo / Thả mảnh  •  [R / Cuộn chuột] Xoay 90°", styles.BadgeStyle);
                }
            }
        }

        private void DrawInGamePauseMenu(UIStyles styles, PuzzleNetworkManager net)
        {
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.65f));

            float panelW = 580f;
            float panelH = 600f;
            Rect panel = new Rect(UIStyles.VirtualWidth * 0.5f - panelW * 0.5f, UIStyles.VirtualHeight * 0.5f - panelH * 0.5f, panelW, panelH);
            GUI.Box(panel, GUIContent.none, styles.CardStyle);
            styles.DrawBorder(panel, 1.5f, new Color(0.25f, 0.30f, 0.42f, 0.85f));

            // Header
            GUI.Label(new Rect(panel.x + 36, panel.y + 22, panel.width - 72, 36), "CÀI ĐẶT & TẠM DỪNG", styles.TitleStyle);
            GUI.Label(new Rect(panel.x + 36, panel.y + 58, panel.width - 72, 22), "Nhấn Esc hoặc bấm TIẾP TỤC để quay lại bàn cờ", styles.MutedStyle);

            // Divider
            styles.DrawSolidRect(new Rect(panel.x + 36, panel.y + 88, panel.width - 72, 1.5f), new Color(0.20f, 0.24f, 0.32f, 1f));

            // Section Audio
            GUI.Label(new Rect(panel.x + 36, panel.y + 100, panel.width - 72, 28), "CÀI ĐẶT ÂM THANH", styles.HeaderStyle);

            var audio = AudioManager.Instance;
            float curSFX = audio != null ? audio.SFXVolume : 1f;
            float curVoice = audio != null ? audio.VoiceVolume : 1f;
            float curMusic = audio != null ? audio.MusicVolume : 0.6f;

            // Slider SFX
            float newSFX = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 138, panel.width - 72, 34), "🔊 Hiệu ứng (SFX)", curSFX);
            if (audio != null && Mathf.Abs(newSFX - curSFX) > 0.005f)
            {
                audio.SetSFXVolume(newSFX);
            }

            // Slider Voice Chat (Friend's voice)
            float newVoice = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 180, panel.width - 72, 34), "🎙️ Voice Chat (Bạn bè)", curVoice);
            if (audio != null && Mathf.Abs(newVoice - curVoice) > 0.005f)
            {
                audio.SetVoiceVolume(newVoice);
            }

            // Slider Music
            float newMusic = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 222, panel.width - 72, 34), "🎵 Nhạc nền (BGM)", curMusic);
            if (audio != null && Mathf.Abs(newMusic - curMusic) > 0.005f)
            {
                audio.SetMusicVolume(newMusic);
            }

            // Slider Mic Gain (Microphone sensitivity / amplification)
            if (net != null && net.Voice != null)
            {
                float curGainNorm = (net.Voice.MicGain - 0.5f) / 3.5f;
                float newGainNorm = styles.DrawSlider(new Rect(panel.x + 36, panel.y + 264, panel.width - 72, 34), "🎤 Khuếch đại Mic (Gain)", curGainNorm);
                if (Mathf.Abs(newGainNorm - curGainNorm) > 0.005f)
                {
                    net.Voice.MicGain = 0.5f + newGainNorm * 3.5f;
                }
            }

            // Voice Chat Quick Mute Toggle Row
            Rect micRow = new Rect(panel.x + 36, panel.y + 312, panel.width - 72, 42);
            styles.DrawSolidRect(micRow, new Color(0.11f, 0.13f, 0.18f, 0.85f));
            styles.DrawBorder(micRow, 1f, new Color(0.22f, 0.26f, 0.35f, 0.7f));

            bool isMuted = net != null && net.Voice != null && net.Voice.IsMuted;
            bool micAvailable = net != null && net.Voice != null && net.Voice.IsAvailable;
            string micStatus;
            if (!micAvailable)
            {
                micStatus = "⚠️ Micro: Không tìm thấy thiết bị thu";
            }
            else if (isMuted)
            {
                micStatus = "🔴 Micro: ĐÃ TẮT TIẾNG (MUTE)";
            }
            else if (net.Voice.IsTransmitting)
            {
                micStatus = "🟢 Micro: ĐANG PHÁT ÂM [V]";
            }
            else
            {
                micStatus = "⚪ Micro: SẴN SÀNG [V]";
            }

            GUI.Label(new Rect(micRow.x + 14, micRow.y + 9, micRow.width - 150, 24), micStatus, styles.BoldStyle);

            if (micAvailable)
            {
                string toggleBtnText = isMuted ? "BẬT MIC" : "TẮT MIC";
                if (styles.DrawButton(new Rect(micRow.xMax - 120, micRow.y + 5, 110, 32), toggleBtnText, !isMuted, false, isMuted, 13))
                {
                    net.Voice.SetMuted(!isMuted);
                }
            }

            // Test SFX Button
            if (styles.DrawButton(new Rect(panel.x + 36, panel.y + 368, 180, 32), "▶ Thử âm thanh SFX", false, false, false, 13))
            {
                audio?.PlaySnap();
            }

            // Divider
            styles.DrawSolidRect(new Rect(panel.x + 36, panel.y + 418, panel.width - 72, 1.5f), new Color(0.20f, 0.24f, 0.32f, 1f));

            // Buttons: TIẾP TỤC & RỜI PHÒNG
            float btnW = (panel.width - 72 - 16) * 0.5f;
            if (styles.DrawButton(new Rect(panel.x + 36, panel.y + 440, btnW, 48), "TIẾP TỤC", true))
            {
                _showInGameMenu = false;
                CursorManager.Lock();
            }

            if (styles.DrawButton(new Rect(panel.x + 36 + btnW + 16, panel.y + 440, btnW, 48), "RỜI PHÒNG", false, false, true))
            {
                // Keep the pause menu blocking camera input until ROOM_LEFT is confirmed.
                net.LeaveRoom();
            }
        }

        private void DrawChatBox(UIStyles styles, Rect rect, List<string> chatHistory)
        {
            Rect logRect = new Rect(rect.x, rect.y, rect.width, rect.height - 80);
            styles.DrawSolidRect(logRect, new Color(0.10f, 0.11f, 0.16f, 0.9f));

            Rect viewRect = new Rect(0, 0, rect.width - 30, Mathf.Max(logRect.height, chatHistory.Count * 28));
            _inGameChatScroll = GUI.BeginScrollView(logRect, _inGameChatScroll, viewRect);
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
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), new Color(0f, 0f, 0f, 0.75f));

            Rect modal = new Rect(UIStyles.VirtualWidth * 0.5f - 400, UIStyles.VirtualHeight * 0.5f - 240, 800, 480);
            GUI.Box(modal, GUIContent.none, styles.CardStyle);

            GUI.Label(new Rect(modal.x + 50, modal.y + 50, 700, 60), "🎉 HOÀN THÀNH XUẤT SẮC! 🎉", styles.TitleStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 130, 700, 40), "Bức tranh puzzle đã được ghép trọn vẹn!", styles.HeaderStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 190, 700, 40), $"Người chiến thắng / đóng góp nhiều nhất: {_victoryWinner}", styles.BoldStyle);
            GUI.Label(new Rect(modal.x + 50, modal.y + 250, 700, 40), $"Thưởng hoàn thành: +{_victoryBonus} xu vào tài khoản!", styles.BoldStyle);

            if (GUI.Button(new Rect(modal.x + 250, modal.y + 350, 300, 65), "QUAY VỀ SẢNH", styles.PrimaryBtnStyle))
            {
                AudioManager.Instance?.PlayClick();
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

            GUI.Label(new Rect(cardRect.x + 50, cardRect.y + 35, 660, 45), "⏰ HẾT THỜI GIAN MỤC TIÊU ⏰", styles.TitleStyle);
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
