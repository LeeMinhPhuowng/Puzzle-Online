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

        public void DrawPauseMenu(UIStyles styles)
        {
            if (_showInGameMenu) DrawInGamePauseMenu(styles, PuzzleNetworkManager.Instance);
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
