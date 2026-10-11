using UnityEngine;
using PuzzleOnline.Network;
using PuzzleOnline.Audio;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Màn hình đăng nhập / đăng ký và kết nối máy chủ.
    /// </summary>
    public class UIAuthView
    {
        private string _serverHost = "127.0.0.1";
        private string _serverPort = "7778";
        private string _username = "";
        private string _password = "";

        public void Draw(UIStyles styles)
        {
            styles.DrawSolidRect(new Rect(0, 0, UIStyles.VirtualWidth, UIStyles.VirtualHeight), styles.BgColor);

            // Left Hero banner
            GUI.Box(new Rect(80, 80, 850, 920), GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(130, 130, 750, 60), "PUZZLE ONLINE", styles.TitleStyle);
            GUI.Label(new Rect(130, 195, 750, 40), "Cùng nhau ghép tranh thời gian thực trong không gian 3D", styles.MutedStyle);

            DrawAuthFeature(styles, new Rect(130, 270, 750, 140), "01", "3D Multiplayer Thời Gian Thực",
                "Đồng bộ kéo thả, xoay và hút ghép mảnh ghép tức thì giữa tất cả người chơi qua Java Server.");
            DrawAuthFeature(styles, new Rect(130, 430, 750, 140), "02", "3 Không Gian Phòng Chơi (3D Environments)",
                "Tự do lựa chọn bối cảnh: Khu vườn thiên nhiên, Phòng học cổ điển, hay Vườn hoa anh đào!");
            DrawAuthFeature(styles, new Rect(130, 590, 750, 140), "03", "Trò Chuyện & Voice Chat (Nhấn giữ 'V')",
                "Giao tiếp trực tiếp với bạn bè qua chat chữ, biểu cảm cảm xúc và voice chat micro.");
            DrawAuthFeature(styles, new Rect(130, 750, 750, 140), "04", "Hệ Thống Xu & Bộ Sưu Tập",
                "Ghép hoàn thành tranh để nhận xu thưởng, mở khóa thêm nhiều bộ tranh và không gian 3D mới.");

            // Right Form box
            Rect formBox = new Rect(990, 150, 850, 780);
            GUI.Box(formBox, GUIContent.none, styles.CardStyle);
            GUI.Label(new Rect(1050, 200, 730, 50), "Đăng Nhập / Đăng Ký", styles.HeaderStyle);
            GUI.Label(new Rect(1050, 255, 730, 30), "Tạo tài khoản mới được tặng ngay 300 xu", styles.MutedStyle);

            // Host & Port row
            GUI.Label(new Rect(1050, 310, 200, 25), "Máy chủ (Host)", styles.MutedStyle);
            _serverHost = GUI.TextField(new Rect(1050, 340, 480, 46), _serverHost, styles.InputStyle);
            GUI.Label(new Rect(1550, 310, 150, 25), "Cổng (Port)", styles.MutedStyle);
            _serverPort = GUI.TextField(new Rect(1550, 340, 230, 46), _serverPort, styles.InputStyle);

            GUI.Label(new Rect(1050, 410, 730, 25), "Tên tài khoản", styles.MutedStyle);
            _username = GUI.TextField(new Rect(1050, 440, 730, 50), _username, styles.InputStyle);

            GUI.Label(new Rect(1050, 515, 730, 25), "Mật khẩu", styles.MutedStyle);
            _password = GUI.PasswordField(new Rect(1050, 545, 730, 50), _password, '•', styles.InputStyle);

            if (GUI.Button(new Rect(1050, 640, 350, 60), "Đăng ký mới", styles.BtnStyle))
            {
                AudioManager.Instance?.PlayClick();
                if (int.TryParse(_serverPort, out var p))
                {
                    PuzzleNetworkManager.Instance?.ConnectToServer(_serverHost, p);
                    PuzzleNetworkManager.Instance?.Register(_username.Trim(), _password);
                }
            }

            if (GUI.Button(new Rect(1430, 640, 350, 60), "Đăng nhập", styles.PrimaryBtnStyle))
            {
                AudioManager.Instance?.PlayClick();
                if (int.TryParse(_serverPort, out var p))
                {
                    PuzzleNetworkManager.Instance?.ConnectToServer(_serverHost, p);
                    PuzzleNetworkManager.Instance?.Login(_username.Trim(), _password);
                }
            }

            // Connection indicator
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                Color connCol = net.Client.IsConnected ? styles.SuccessColor : styles.MutedColor;
                styles.DrawSolidRect(new Rect(1052, 740, 14, 14), connCol);
                GUI.Label(new Rect(1075, 735, 700, 30), net.Client.ConnectionLabel, styles.MutedStyle);
            }
        }

        private void DrawAuthFeature(UIStyles styles, Rect rect, string num, string title, string desc)
        {
            styles.DrawSolidRect(rect, new Color(0.10f, 0.11f, 0.16f, 0.8f));
            styles.DrawSolidRect(new Rect(rect.x + 20, rect.y + 25, 45, 30), styles.AccentColor);
            GUI.Label(new Rect(rect.x + 20, rect.y + 25, 45, 30), num, styles.BadgeStyle);
            GUI.Label(new Rect(rect.x + 80, rect.y + 22, rect.width - 100, 32), title, styles.BoldStyle);
            GUI.Label(new Rect(rect.x + 80, rect.y + 58, rect.width - 100, 65), desc, styles.MutedStyle);
        }
    }
}
