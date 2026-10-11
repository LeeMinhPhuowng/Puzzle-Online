using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Giao diện Sảnh Chờ (Lobby): Quản lý Phòng chơi, Bạn bè, Cửa hàng và Hồ sơ.
    /// </summary>
    public class UILobbyView
    {
        public enum MainTab { Rooms, Friends, Shop, Profile }
        private MainTab _currentTab = MainTab.Rooms;

        // Lobby Create Room Fields
        private string _createRoomName = "Phòng thư giãn";
        private int _createMaxPlayers = 4;
        private bool _createIsPrivate = false;
        private string _createPassword = "";
        private string _selectedPuzzleId = "sunset_3x3";
        private string _selectedThemeId = "SCN_Nature";
        private int _createTargetTime = 300;
        private string _joinCodeInput = "";
        private string _addFriendInput = "";

        // Scroll Positions
        private Vector2 _roomListScroll;
        private Vector2 _friendListScroll;
        private Vector2 _shopScroll;

        public void Draw(UIStyles styles, Action<string> showToast, Action<RoomInfo> onRequirePassword)
        {
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), styles.BgColor);

            // Top Header Bar
            styles.DrawTopBar("Sảnh Chờ Trực Tuyến");

            // Navigation Tabs
            Rect tabBar = new Rect(80, 115, 1760, 60);
            styles.DrawSolidRect(tabBar, styles.CardColor);

            var net = PuzzleNetworkManager.Instance;
            string friendsTabLabel = net != null && net.PendingFriendRequests.Count > 0
                ? $"BẠN BÈ ({net.PendingFriendRequests.Count})"
                : "BẠN BÈ";

            if (DrawTabBtn(styles, new Rect(90, 122, 280, 46), "PHÒNG CHƠI", _currentTab == MainTab.Rooms)) _currentTab = MainTab.Rooms;
            if (DrawTabBtn(styles, new Rect(380, 122, 280, 46), friendsTabLabel, _currentTab == MainTab.Friends))
            {
                _currentTab = MainTab.Friends;
                net?.SendGetFriendRequests();
            }
            if (DrawTabBtn(styles, new Rect(670, 122, 280, 46), "CỬA HÀNG", _currentTab == MainTab.Shop)) _currentTab = MainTab.Shop;
            if (DrawTabBtn(styles, new Rect(960, 122, 280, 46), "HỒ SƠ CÁ NHÂN", _currentTab == MainTab.Profile)) _currentTab = MainTab.Profile;

            // Content based on selected tab
            switch (_currentTab)
            {
                case MainTab.Rooms: DrawLobbyRoomsTab(styles, showToast, onRequirePassword); break;
                case MainTab.Friends: DrawLobbyFriendsTab(styles); break;
                case MainTab.Shop: DrawLobbyShopTab(styles); break;
                case MainTab.Profile: DrawLobbyProfileTab(styles); break;
            }
        }

        private bool DrawTabBtn(UIStyles styles, Rect rect, string text, bool active)
        {
            return styles.DrawButton(rect, text, active);
        }

        private void DrawLobbyRoomsTab(UIStyles styles, Action<string> showToast, Action<RoomInfo> onRequirePassword)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            // Left: Create Room & Join by Code (Width 600)
            Rect leftPanel = new Rect(80, 195, 600, 810);
            GUI.Box(leftPanel, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(110, 215, 540, 35), "Tạo Phòng Mới", styles.HeaderStyle);

            GUI.Label(new Rect(110, 255, 540, 20), "Tên phòng", styles.MutedStyle);
            _createRoomName = GUI.TextField(new Rect(110, 278, 540, 38), _createRoomName, styles.InputStyle);

            GUI.Label(new Rect(110, 322, 540, 20), "Số lượng người chơi", styles.MutedStyle);
            for (int i = 2; i <= 4; i++)
            {
                bool sel = _createMaxPlayers == i;
                if (styles.DrawButton(new Rect(110 + (i - 2) * 185, 344, 170, 36), $"{i} Người", sel))
                {
                    _createMaxPlayers = i;
                }
            }

            GUI.Label(new Rect(110, 386, 540, 20), "Chế độ phòng", styles.MutedStyle);
            if (styles.DrawButton(new Rect(110, 408, 265, 36), "🌐 Công khai", !_createIsPrivate))
            {
                _createIsPrivate = false;
            }
            if (styles.DrawButton(new Rect(385, 408, 265, 36), "🔒 Có mật khẩu", _createIsPrivate))
            {
                _createIsPrivate = true;
            }

            float currentY = 450f;
            if (_createIsPrivate)
            {
                GUI.Label(new Rect(110, currentY, 540, 20), "Mật khẩu phòng", styles.MutedStyle);
                currentY += 22;
                _createPassword = GUI.PasswordField(new Rect(110, currentY, 540, 36), _createPassword, '•', styles.InputStyle);
                currentY += 44;
            }

            GUI.Label(new Rect(110, currentY, 540, 20), "Thời gian mục tiêu", styles.MutedStyle);
            currentY += 22;
            int[] times = { 180, 300, 600, 0 };
            string[] timeLabels = { "3 Phút", "5 Phút", "10 Phút", "Vô hạn" };
            for (int t = 0; t < 4; t++)
            {
                bool selTime = _createTargetTime == times[t];
                if (styles.DrawButton(new Rect(110 + t * 138, currentY, 126, 34), timeLabels[t], selTime))
                {
                    _createTargetTime = times[t];
                }
            }
            currentY += 42;

            GUI.Label(new Rect(110, currentY, 540, 20), "Chọn bộ xếp hình", styles.MutedStyle);
            currentY += 22;
            DrawPuzzleSelector(styles, new Rect(110, currentY, 540, 56), showToast);
            currentY += 64;

            GUI.Label(new Rect(110, currentY, 540, 20), "Chọn không gian 3D", styles.MutedStyle);
            currentY += 22;
            DrawThemeSelector(styles, new Rect(110, currentY, 540, 56), showToast);
            currentY += 66;

            if (styles.DrawButton(new Rect(110, currentY, 540, 48), "TẠO PHÒNG NGAY", true))
            {
                string pwd = _createIsPrivate ? _createPassword.Trim() : "";
                if (_createIsPrivate && string.IsNullOrEmpty(pwd))
                {
                    showToast?.Invoke("Vui lòng nhập mật khẩu cho phòng.");
                }
                else
                {
                    net.CreateRoom(_createRoomName.Trim(), _createMaxPlayers, _selectedPuzzleId, _selectedThemeId, pwd, _createTargetTime);
                }
            }
            currentY += 56;

            // Quick Join by Code
            GUI.Label(new Rect(110, currentY, 540, 20), "Tham gia nhanh bằng mã phòng", styles.MutedStyle);
            currentY += 22;
            _joinCodeInput = GUI.TextField(new Rect(110, currentY, 360, 44), _joinCodeInput.ToUpper(), styles.InputStyle);
            if (styles.DrawButton(new Rect(485, currentY, 165, 44), "Vào phòng", false))
            {
                if (!string.IsNullOrWhiteSpace(_joinCodeInput))
                {
                    string targetCode = _joinCodeInput.Trim().ToUpper();
                    var found = net.LobbyRooms.Find(r => r.Id.Equals(targetCode, StringComparison.OrdinalIgnoreCase));
                    if (found != null && found.HasPassword)
                    {
                        onRequirePassword?.Invoke(found);
                    }
                    else
                    {
                        net.JoinRoom(targetCode, "");
                    }
                }
            }

            // Right: Public Rooms List (Width 1130)
            Rect rightPanel = new Rect(710, 195, 1130, 810);
            GUI.Box(rightPanel, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(740, 220, 800, 40), "Danh Sách Phòng Đang Chờ", styles.HeaderStyle);

            if (styles.DrawButton(new Rect(1680, 220, 130, 40), "Làm mới", false))
            {
                net.RefreshLobby();
            }

            Rect listArea = new Rect(740, 280, 1070, 700);
            Rect viewArea = new Rect(0, 0, 1040, Mathf.Max(690, net.LobbyRooms.Count * 110));
            _roomListScroll = GUI.BeginScrollView(listArea, _roomListScroll, viewArea);

            if (net.LobbyRooms.Count == 0)
            {
                GUI.Label(new Rect(20, 20, 1000, 40), "Hiện tại chưa có phòng nào. Hãy tạo phòng mới bên trái!", styles.MutedStyle);
            }
            else
            {
                for (int i = 0; i < net.LobbyRooms.Count; i++)
                {
                    var room = net.LobbyRooms[i];
                    Rect rCard = new Rect(0, i * 110, 1040, 96);
                    styles.DrawSolidRect(rCard, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                    GUI.Label(new Rect(20, rCard.y + 14, 300, 30), room.Name, styles.BoldStyle);

                    // Badge: Public or Password
                    Rect badgeRect = new Rect(330, rCard.y + 17, 120, 24);
                    if (room.HasPassword)
                    {
                        styles.DrawSolidRect(badgeRect, new Color(0.9f, 0.28f, 0.28f, 0.25f));
                        GUI.Label(badgeRect, "🔒 Có mật khẩu", styles.BadgeStyle);
                    }
                    else
                    {
                        styles.DrawSolidRect(badgeRect, new Color(0.24f, 0.75f, 0.45f, 0.25f));
                        GUI.Label(badgeRect, "🌐 Công khai", styles.BadgeStyle);
                    }

                    GUI.Label(new Rect(20, rCard.y + 50, 400, 25), $"Chủ phòng: {room.Host}  •  Mã: {room.Id}", styles.MutedStyle);

                    GUI.Label(new Rect(480, rCard.y + 14, 240, 26), $"Bộ tranh: {UIStyles.GetPuzzleName(room.PuzzleId)}", styles.BodyStyle);
                    GUI.Label(new Rect(480, rCard.y + 40, 240, 24), $"Không gian: {UIStyles.GetThemeName(room.ThemeId)}", styles.MutedStyle);
                    GUI.Label(new Rect(480, rCard.y + 64, 240, 24), $"Mục tiêu: {UIStyles.FormatTimeLimit(room.TargetTimeSeconds)}", styles.MutedStyle);

                    GUI.Label(new Rect(730, rCard.y + 32, 130, 30), $"{room.Count}/{room.MaxPlayers} Người", styles.BoldStyle);

                    string joinBtnText = room.HasPassword ? "Nhập Khóa" : "Tham Gia";
                    if (styles.DrawButton(new Rect(880, rCard.y + 24, 140, 48), joinBtnText, true))
                    {
                        if (room.HasPassword)
                        {
                            onRequirePassword?.Invoke(room);
                        }
                        else
                        {
                            net.JoinRoom(room.Id, "");
                        }
                    }
                }
            }
            GUI.EndScrollView();
        }

        private void DrawPuzzleSelector(UIStyles styles, Rect rect, Action<string> showToast)
        {
            var net = ServiceLocator.Get<INetworkService>();
            var pList = new List<(string id, string name, int rows, int cols)>();
            if (net != null && net.ShopPuzzles != null && net.ShopPuzzles.Count > 0)
            {
                foreach (var p in net.ShopPuzzles)
                {
                    pList.Add((p.Id, p.Name, p.Rows, p.Columns));
                }
            }
            else
            {
                var allPuzzles = ServiceLocator.Get<IPuzzleCatalog>()?.GetAllPuzzleData();
                if (allPuzzles != null && allPuzzles.Count > 0)
                {
                    foreach (var p in allPuzzles)
                    {
                        if (p != null) pList.Add((p.puzzleId, p.displayName, p.rows, p.columns));
                    }
                }
                else
                {
                    pList.Add(("sunset_3x3", "Hoàng hôn", 3, 3));
                    pList.Add(("forest_4x3", "Rừng xanh", 3, 4));
                    pList.Add(("ocean_4x4", "Đại dương", 4, 4));
                    pList.Add(("rei_10x10", "Ayanami Rei", 10, 10));
                }
            }

            int count = pList.Count;
            float gap = 6f;
            float btnW = (rect.width - (count - 1) * gap) / count;
            float h = rect.height > 0 ? rect.height : 56f;

            for (int i = 0; i < count; i++)
            {
                var item = pList[i];
                bool owned = net != null && net.OwnedPuzzles != null && net.OwnedPuzzles.Contains(item.id);
                bool sel = _selectedPuzzleId == item.id;
                string label = $"{item.name}\n({item.rows}×{item.cols})";
                if (styles.DrawButton(new Rect(rect.x + i * (btnW + gap), rect.y, btnW, h), label, sel, false, false, 12))
                {
                    if (owned) _selectedPuzzleId = item.id;
                    else showToast?.Invoke("Bạn chưa sở hữu bộ tranh này. Hãy mua tại Cửa hàng!");
                }
            }
        }

        private void DrawThemeSelector(UIStyles styles, Rect rect, Action<string> showToast)
        {
            var net = ServiceLocator.Get<INetworkService>();
            var themeCat = ServiceLocator.Get<IThemeCatalog>() ?? ContentRegistry.Instance;
            var allThemes = themeCat.GetAllThemes();
            int count = allThemes.Count;
            float gap = 12f;
            float btnW = (rect.width - (count - 1) * gap) / count;
            float h = rect.height > 0 ? rect.height : 70f;
            for (int i = 0; i < count; i++)
            {
                var t = allThemes[i];
                bool owned = net != null && net.OwnedThemes != null && net.OwnedThemes.Contains(t.themeId);
                bool sel = _selectedThemeId == t.themeId;
                string label = $"{t.shortName}\n({t.themeId})";
                if (styles.DrawButton(new Rect(rect.x + i * (btnW + gap), rect.y, btnW, h), label, sel))
                {
                    if (owned) _selectedThemeId = t.themeId;
                    else showToast?.Invoke("Bạn chưa mở khóa Không gian 3D này. Hãy vào Cửa hàng!");
                }
            }
        }

        private void DrawLobbyFriendsTab(UIStyles styles)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, styles.CardStyle);

            GUI.Label(new Rect(120, 225, 600, 40), "Danh Sách Bạn Bè & Lời Mời", styles.HeaderStyle);

            // Add friend row
            GUI.Label(new Rect(1100, 230, 200, 30), "Thêm bạn mới:", styles.MutedStyle);
            _addFriendInput = GUI.TextField(new Rect(1250, 225, 340, 44), _addFriendInput, styles.InputStyle);
            if (GUI.Button(new Rect(1610, 225, 190, 44), "Gửi lời mời", styles.PrimaryBtnStyle))
            {
                AudioManager.Instance?.PlayClick();
                if (!string.IsNullOrWhiteSpace(_addFriendInput))
                {
                    net.SendAddFriend(_addFriendInput.Trim());
                    _addFriendInput = "";
                }
            }

            int pendingCount = net.PendingFriendRequests.Count;
            int friendsCount = net.Friends.Count;

            int pendingHeight = pendingCount > 0 ? (50 + pendingCount * 85 + 20) : 0;
            int friendsHeight = 50 + Mathf.Max(1, friendsCount) * 85;
            int totalContentHeight = pendingHeight + friendsHeight + 40;

            Rect listArea = new Rect(120, 295, 1680, 680);
            Rect viewArea = new Rect(0, 0, 1650, Mathf.Max(670, totalContentHeight));
            _friendListScroll = GUI.BeginScrollView(listArea, _friendListScroll, viewArea);

            float currentY = 10;

            // 1. Pending friend requests
            if (pendingCount > 0)
            {
                GUI.Label(new Rect(20, currentY, 800, 35), $"Lời Mời Kết Bạn Đang Chờ Duyệt ({pendingCount})", styles.BoldStyle);
                currentY += 40;

                for (int i = 0; i < pendingCount; i++)
                {
                    var req = net.PendingFriendRequests[i];
                    Rect r = new Rect(0, currentY, 1650, 76);
                    styles.DrawSolidRect(r, new Color(0.20f, 0.23f, 0.32f, 0.95f));

                    // Avatar icon dot (yellow pending)
                    styles.DrawSolidRect(new Rect(20, r.y + 30, 16, 16), new Color(0.95f, 0.77f, 0.05f));

                    GUI.Label(new Rect(50, r.y + 16, 400, 30), req.SenderUsername, styles.BoldStyle);
                    GUI.Label(new Rect(50, r.y + 44, 500, 25), $"Đã gửi lúc: {req.CreatedAt}", styles.MutedStyle);

                    if (GUI.Button(new Rect(1310, r.y + 16, 150, 44), "Chấp nhận", styles.PrimaryBtnStyle))
                    {
                        AudioManager.Instance?.PlayClick();
                        net.SendAcceptFriendRequest(req.RequestId);
                    }

                    if (GUI.Button(new Rect(1480, r.y + 16, 150, 44), "Từ chối", styles.BtnStyle))
                    {
                        AudioManager.Instance?.PlayClick();
                        net.SendRejectFriendRequest(req.RequestId);
                    }

                    currentY += 85;
                }

                currentY += 20;
            }

            // 2. Friends list
            GUI.Label(new Rect(20, currentY, 800, 35), $"Bạn Bè Hiện Tại ({friendsCount})", styles.BoldStyle);
            currentY += 40;

            if (friendsCount == 0)
            {
                GUI.Label(new Rect(20, currentY + 10, 1000, 35), "Chưa có bạn bè nào. Hãy nhập tên tài khoản ở góc trên bên phải để gửi lời mời!", styles.MutedStyle);
            }
            else
            {
                for (int i = 0; i < friendsCount; i++)
                {
                    var f = net.Friends[i];
                    Rect r = new Rect(0, currentY, 1650, 76);
                    styles.DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                    Color statusCol = f.Online ? styles.SuccessColor : styles.MutedColor;
                    styles.DrawSolidRect(new Rect(20, r.y + 30, 16, 16), statusCol);

                    GUI.Label(new Rect(50, r.y + 20, 400, 35), f.Username, styles.BoldStyle);
                    GUI.Label(new Rect(500, r.y + 24, 400, 30), f.Online ? $"Trực tuyến ({f.Status})" : "Ngoại tuyến", styles.MutedStyle);

                    if (net.CurrentRoom != null)
                    {
                        GUI.enabled = f.Online && f.Status == "IDLE";
                        if (GUI.Button(new Rect(1480, r.y + 16, 150, 44), "Mời vào phòng", styles.BtnStyle))
                        {
                            AudioManager.Instance?.PlayClick();
                            net.SendInvite(f.Username);
                        }
                        GUI.enabled = true;
                    }

                    currentY += 85;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawLobbyShopTab(UIStyles styles)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, styles.CardStyle);

            GUI.Label(new Rect(120, 225, 800, 40), "Cửa Hàng Trò Chơi", styles.HeaderStyle);
            GUI.Label(new Rect(1450, 230, 350, 35), $"Số xu hiện có: {net.Coins} xu", styles.BoldStyle);

            Rect listArea = new Rect(120, 280, 1680, 700);
            Rect viewArea = new Rect(0, 0, 1650, 800);
            _shopScroll = GUI.BeginScrollView(listArea, _shopScroll, viewArea);

            // Puzzles Section
            GUI.Label(new Rect(0, 10, 600, 35), "1. BỘ TRANH GHÉP (PUZZLES)", styles.BoldStyle);
            for (int i = 0; i < net.ShopPuzzles.Count; i++)
            {
                var p = net.ShopPuzzles[i];
                Rect r = new Rect(0, 50 + i * 95, 1650, 82);
                styles.DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                GUI.Label(new Rect(25, r.y + 18, 400, 30), p.Name, styles.BoldStyle);
                GUI.Label(new Rect(25, r.y + 48, 400, 25), $"Kích thước: {p.Rows}x{p.Columns} mảnh  •  Thời gian: {p.Seconds}s", styles.MutedStyle);

                if (p.Owned)
                {
                    GUI.Label(new Rect(1450, r.y + 26, 180, 35), "✓ ĐÃ SỞ HỮU", styles.BoldStyle);
                }
                else
                {
                    GUI.Label(new Rect(1250, r.y + 26, 180, 35), $"{p.Price} xu", styles.BoldStyle);
                    GUI.enabled = net.Coins >= p.Price;
                    if (styles.DrawButton(new Rect(1450, r.y + 18, 180, 48), "MUA NGAY", false, true))
                    {
                        net.SendBuy("puzzle", p.Id);
                    }
                    GUI.enabled = true;
                }
            }

            // 3D Environments Section
            float themeY = 60 + net.ShopPuzzles.Count * 95 + 20;
            GUI.Label(new Rect(0, themeY, 800, 35), "2. KHÔNG GIAN PHÒNG CHƠI 3D (3D ENVIRONMENTS)", styles.BoldStyle);
            for (int i = 0; i < net.ShopThemes.Count; i++)
            {
                var t = net.ShopThemes[i];
                Rect r = new Rect(0, themeY + 40 + i * 95, 1650, 82);
                styles.DrawSolidRect(r, new Color(0.16f, 0.19f, 0.26f, 0.9f));

                GUI.Label(new Rect(25, r.y + 18, 400, 30), t.Name, styles.BoldStyle);
                GUI.Label(new Rect(25, r.y + 48, 400, 25), $"Scene: {t.Id}  •  Không gian trải nghiệm 3D độc quyền", styles.MutedStyle);

                if (t.Owned)
                {
                    GUI.Label(new Rect(1450, r.y + 26, 180, 35), "✓ ĐÃ SỞ HỮU", styles.BoldStyle);
                }
                else
                {
                    GUI.Label(new Rect(1250, r.y + 26, 180, 35), $"{t.Price} xu", styles.BoldStyle);
                    GUI.enabled = net.Coins >= t.Price;
                    if (styles.DrawButton(new Rect(1450, r.y + 18, 180, 48), "MỞ KHÓA", false, true))
                    {
                        net.SendBuy("theme", t.Id);
                    }
                    GUI.enabled = true;
                }
            }

            GUI.EndScrollView();
        }

        private void DrawLobbyProfileTab(UIStyles styles)
        {
            var net = PuzzleNetworkManager.Instance;
            if (net == null) return;

            Rect panel = new Rect(80, 195, 1760, 810);
            GUI.Box(panel, GUIContent.none, styles.CardStyle);

            GUI.Label(new Rect(120, 225, 800, 40), "Hồ Sơ & Thống Kê Người Chơi", styles.HeaderStyle);

            // 4 Stats Cards
            DrawStatCard(styles, new Rect(120, 300, 380, 180), "TỔNG ĐIỂM", net.TotalScore.ToString(), "Điểm cộng dồn từ việc ghép đúng mảnh (+10/mảnh)");
            int totalPuzzles = net.ShopPuzzles.Count > 0 ? net.ShopPuzzles.Count : 4;
            int totalThemes = net.ShopThemes.Count > 0 ? net.ShopThemes.Count : 3;
            DrawStatCard(styles, new Rect(540, 300, 380, 180), "BỘ TRANH SỞ HỮU", $"{net.OwnedPuzzles.Count}/{totalPuzzles}", "Các bộ tranh: Hoàng hôn, Rừng xanh, Đại dương, Rei...");
            DrawStatCard(styles, new Rect(960, 300, 380, 180), "KHÔNG GIAN 3D", $"{net.OwnedThemes.Count}/{totalThemes}", "Các môi trường: Thiên nhiên, Phòng học, Sakura");
            DrawStatCard(styles, new Rect(1380, 300, 380, 180), "BỘ TRANH HOÀN THÀNH", net.UniquePuzzlesCompleted.ToString(), "Số ván chơi đã ghép trọn vẹn bức tranh cùng đồng đội");

            // Additional details card
            Rect infoCard = new Rect(120, 520, 1640, 440);
            styles.DrawSolidRect(infoCard, new Color(0.16f, 0.19f, 0.26f, 0.9f));
            GUI.Label(new Rect(160, 550, 800, 35), "Thông Tin Tài Khoản", styles.BoldStyle);
            GUI.Label(new Rect(160, 600, 500, 30), $"Tên tài khoản: {net.Username}", styles.BodyStyle);
            GUI.Label(new Rect(160, 640, 500, 30), $"Số xu tích lũy: {net.Coins} xu", styles.BodyStyle);
            GUI.Label(new Rect(160, 680, 500, 30), $"Trạng thái: Trực tuyến (Sẵn sàng)", styles.BodyStyle);
            GUI.Label(new Rect(160, 720, 800, 30), $"Phiên kết nối: {net.SessionToken}", styles.MutedStyle);
        }

        private void DrawStatCard(UIStyles styles, Rect rect, string title, string val, string sub)
        {
            styles.DrawSolidRect(rect, new Color(0.16f, 0.19f, 0.26f, 0.9f));
            GUI.Label(new Rect(rect.x + 25, rect.y + 20, rect.width - 50, 25), title, styles.MutedStyle);
            GUI.Label(new Rect(rect.x + 25, rect.y + 55, rect.width - 50, 50), val, styles.TitleStyle);
            GUI.Label(new Rect(rect.x + 25, rect.y + 115, rect.width - 50, 50), sub, styles.MutedStyle);
        }
    }
}
