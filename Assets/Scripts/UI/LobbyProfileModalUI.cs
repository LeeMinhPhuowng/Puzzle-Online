using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển bảng thông tin hồ sơ cá nhân của người chơi (ProfileView).
    /// Hiển thị thông số người chơi lấy trực tiếp từ database: Tiền xu, Tổng điểm, Số bộ hoàn thành,
    /// Tranh đã mua, Không gian đã mua, Mã phiên.
    /// Thiết kế chuẩn giao diện Đen/Vàng (Black & Gold) sang trọng, gọn gàng, không dùng emoji.
    /// </summary>
    public class LobbyProfileModalUI : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color CardBgColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private static readonly Color StatBoxColor = new Color(0.12f, 0.14f, 0.19f, 0.95f);
        private static readonly Color TextWhiteColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color MutedColor = new Color(0.65f, 0.70f, 0.78f, 1f);

        private GameObject _contentRoot;
        private TMP_Text _usernameText;
        private TMP_Text _statusText;

        // Stat items
        private TMP_Text _coinsText;
        private TMP_Text _totalScoreText;
        private TMP_Text _completedText;
        private TMP_Text _ownedPuzzlesText;
        private TMP_Text _ownedThemesText;
        private TMP_Text _sessionText;

        private bool _isBuilt = false;

        private void Awake()
        {
            EnsureCardStyle();
            BuildProfileUI();
        }

        private void OnEnable()
        {
            EnsureCardStyle();
            if (!_isBuilt) BuildProfileUI();

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated += RefreshProfile;
                net.RefreshLobby();
            }

            RefreshProfile();
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated -= RefreshProfile;
            }
        }

        private void EnsureCardStyle()
        {
            var img = GetComponent<Image>();
            if (img != null) img.color = CardBgColor;

            var outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            outline.effectColor = GoldColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var shadow = GetComponent<Shadow>() ?? gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(4f, -4f);

            // Title
            var titleComp = GetComponentInChildren<TMP_Text>(true);
            if (titleComp != null && (titleComp.name.Contains("Title") || titleComp.text.Contains("Hồ sơ") || titleComp.text.Contains("Profile")))
            {
                titleComp.color = GoldColor;
                titleComp.fontStyle = FontStyles.Bold;
                titleComp.text = "HỒ SƠ NGƯỜI CHƠI";
            }
        }

        public void BuildProfileUI()
        {
            var existingContent = transform.Find("ProfileDynamicContent");
            if (existingContent != null)
            {
                DestroyImmediate(existingContent.gameObject);
                _isBuilt = false;
            }

            _contentRoot = new GameObject("ProfileDynamicContent", typeof(RectTransform));
            _contentRoot.transform.SetParent(transform, false);

            var rt = _contentRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.04f, 0.10f);
            rt.anchorMax = new Vector2(0.96f, 0.88f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var vlg = _contentRoot.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 1. Header Box: Avatar + Username + Status "Trực tuyến"
            var headerBox = CreateBox(_contentRoot.transform, "HeaderBox", 80f, StatBoxColor);
            var headerHlg = headerBox.AddComponent<HorizontalLayoutGroup>();
            headerHlg.spacing = 20f;
            headerHlg.padding = new RectOffset(20, 20, 10, 10);
            headerHlg.childControlWidth = false;
            headerHlg.childControlHeight = true;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = true;

            // Avatar Frame
            var avatarFrame = CreateBox(headerBox.transform, "AvatarFrame", 60f, new Color(0.06f, 0.08f, 0.11f, 1f));
            var avatarRt = avatarFrame.GetComponent<RectTransform>();
            avatarRt.sizeDelta = new Vector2(60f, 60f);
            var avatarOutline = avatarFrame.AddComponent<Outline>();
            avatarOutline.effectColor = GoldColor;
            avatarOutline.effectDistance = new Vector2(2f, -2f);

            var avatarTxt = CreateText(avatarFrame.transform, "AvatarIcon", "USER", 18, GoldColor, TextAlignmentOptions.Center, true);
            avatarTxt.rectTransform.anchorMin = Vector2.zero;
            avatarTxt.rectTransform.anchorMax = Vector2.one;
            avatarTxt.rectTransform.offsetMin = Vector2.zero;
            avatarTxt.rectTransform.offsetMax = Vector2.zero;

            // User Info Column
            var userCol = new GameObject("UserCol", typeof(RectTransform));
            userCol.transform.SetParent(headerBox.transform, false);
            var userColRt = userCol.GetComponent<RectTransform>();
            userColRt.sizeDelta = new Vector2(400f, 60f);
            var userVlg = userCol.AddComponent<VerticalLayoutGroup>();
            userVlg.spacing = 4f;
            userVlg.childControlWidth = true;
            userVlg.childControlHeight = true;
            userVlg.childForceExpandWidth = true;
            userVlg.childForceExpandHeight = true;

            _usernameText = CreateText(userCol.transform, "UsernameText", "Người chơi", 22, TextWhiteColor, TextAlignmentOptions.Left, true);
            _statusText = CreateText(userCol.transform, "StatusText", "Trực tuyến", 14, new Color(0.4f, 0.9f, 0.5f, 1f), TextAlignmentOptions.Left, false);

            // 2. Stats Grid: 2 hàng x 3 cột (Độ rộng tổng thể bằng đúng dòng tên người chơi HeaderBox)
            var row1 = CreateBox(_contentRoot.transform, "StatsRow1", 90f, Color.clear);
            var hlg1 = row1.AddComponent<HorizontalLayoutGroup>();
            hlg1.spacing = 16f;
            hlg1.childControlWidth = true;
            hlg1.childControlHeight = true;
            hlg1.childForceExpandWidth = true;
            hlg1.childForceExpandHeight = true;

            _coinsText = CreateStatCard(row1.transform, "Tiền Xu", "0 Xu");
            _totalScoreText = CreateStatCard(row1.transform, "Tổng Điểm", "0 Điểm");
            _completedText = CreateStatCard(row1.transform, "Bộ Đã Ghép Xong", "0 Bộ Tranh");

            var row2 = CreateBox(_contentRoot.transform, "StatsRow2", 90f, Color.clear);
            var hlg2 = row2.AddComponent<HorizontalLayoutGroup>();
            hlg2.spacing = 16f;
            hlg2.childControlWidth = true;
            hlg2.childControlHeight = true;
            hlg2.childForceExpandWidth = true;
            hlg2.childForceExpandHeight = true;

            _ownedPuzzlesText = CreateStatCard(row2.transform, "Tranh Đã Mua", "0 Bộ Tranh");
            _ownedThemesText = CreateStatCard(row2.transform, "Không Gian 3D", "0 Không Gian");
            _sessionText = CreateStatCard(row2.transform, "Mã Phiên (ID)", "ONLINE");

            _isBuilt = true;
        }

        private TMP_Text CreateStatCard(Transform parent, string title, string defaultValue)
        {
            var card = CreateBox(parent, "StatCard_" + title, 90f, StatBoxColor);
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.28f, 0.36f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 14, 14);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            CreateText(card.transform, "Title", title, 13, MutedColor, TextAlignmentOptions.Left, false);
            var valText = CreateText(card.transform, "Value", defaultValue, 20, TextWhiteColor, TextAlignmentOptions.Left, true);
            return valText;
        }

        private GameObject CreateBox(Transform parent, string name, float height, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, height);
            var img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private TMP_Text CreateText(Transform parent, string name, string text, float fontSize, Color color, TextAlignmentOptions alignment, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        public void RefreshProfile()
        {
            var net = PuzzleNetworkManager.Instance;
            string username = net != null && !string.IsNullOrEmpty(net.Username) ? net.Username : "Khách";
            int coins = net != null ? net.Coins : 0;
            int totalScore = net != null ? net.TotalScore : 0;
            int completed = net != null ? net.UniquePuzzlesCompleted : 0;
            int ownedPuzzles = net != null && net.OwnedPuzzles != null ? net.OwnedPuzzles.Count : 0;
            int ownedThemes = net != null && net.OwnedThemes != null ? net.OwnedThemes.Count : 0;

            if (_usernameText != null) _usernameText.text = $"<b>{username}</b>";

            if (_coinsText != null) _coinsText.text = $"{coins:N0} Xu";
            if (_totalScoreText != null) _totalScoreText.text = $"{totalScore:N0} Điểm";
            if (_completedText != null) _completedText.text = $"{completed} Bộ Tranh";
            if (_ownedPuzzlesText != null) _ownedPuzzlesText.text = $"{ownedPuzzles} Bộ Tranh";
            if (_ownedThemesText != null) _ownedThemesText.text = $"{ownedThemes} Không Gian";

            if (_sessionText != null)
            {
                string token = net != null && net.AuthService != null ? net.AuthService.SessionToken : string.Empty;
                if (!string.IsNullOrEmpty(token))
                {
                    string shortId = token.Length > 8 ? token.Substring(0, 8).ToUpper() : token.ToUpper();
                    _sessionText.text = $"#{shortId}";
                }
                else
                {
                    _sessionText.text = "#ONLINE";
                }
            }

            if (_statusText != null)
            {
                bool connected = net != null && net.IsConnected;
                _statusText.text = connected ? "Trực tuyến" : "Ngoại tuyến";
                _statusText.color = connected ? new Color(0.4f, 0.9f, 0.5f, 1f) : MutedColor;
            }
        }
    }
}
