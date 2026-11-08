using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    public sealed class RoomJoinDialogs : MonoBehaviour
    {
        [SerializeField] private GameObject passwordPanel;
        [SerializeField] private TMP_Text passwordTitle;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button passwordSubmit;
        [SerializeField] private Button passwordCancel;
        [SerializeField] private GameObject invitePanel;
        [SerializeField] private TMP_Text inviteMessage;
        [SerializeField] private Button inviteAccept;
        [SerializeField] private Button inviteDecline;
        private PuzzleNetworkManager network;
        private string passwordRoom;
        private string invitedRoom;

        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.22f, 1f);
        private static readonly Color GoldHoverColor = new Color(1f, 0.88f, 0.35f, 1f);
        private static readonly Color CardBgColor = new Color(0.08f, 0.09f, 0.12f, 0.98f);
        private static readonly Color OverlayBgColor = new Color(0f, 0f, 0f, 0.75f);
        private static readonly Color InputBgColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        private static readonly Color DarkBtnColor = new Color(0.18f, 0.20f, 0.26f, 1f);
        private static readonly Color DangerBtnColor = new Color(0.42f, 0.15f, 0.18f, 1f);

        private void Awake()
        {
            passwordSubmit.onClick.AddListener(SubmitPassword);
            passwordInput.onSubmit.AddListener(_ => SubmitPassword());
            passwordCancel.onClick.AddListener(() => passwordPanel.SetActive(false));
            inviteAccept.onClick.AddListener(() => RespondInvite(true));
            inviteDecline.onClick.AddListener(() => RespondInvite(false));
            ApplyBlackGoldTheme();
            Close();
        }

        private void OnEnable()
        {
            ApplyBlackGoldTheme();
            Bind();
        }

        private void Start()
        {
            ApplyBlackGoldTheme();
            Bind();
        }

        private void ApplyBlackGoldTheme()
        {
            // Định dạng Panel Mật khẩu
            if (passwordPanel != null)
            {
                var bg = passwordPanel.GetComponent<Image>();
                if (bg != null) bg.color = OverlayBgColor;

                // Thẻ Card bên trong
                if (passwordPanel.transform.childCount > 0)
                {
                    var card = passwordPanel.transform.GetChild(0);
                    StyleDialogCard(card.gameObject);
                }
            }

            // Định dạng Panel Lời mời
            if (invitePanel != null)
            {
                var bg = invitePanel.GetComponent<Image>();
                if (bg != null) bg.color = OverlayBgColor;

                if (invitePanel.transform.childCount > 0)
                {
                    var card = invitePanel.transform.GetChild(0);
                    StyleDialogCard(card.gameObject);
                }
            }

            // Tiêu đề & Ô nhập mật khẩu
            if (passwordTitle != null)
            {
                passwordTitle.color = GoldColor;
                passwordTitle.alignment = TextAlignmentOptions.Center;
                passwordTitle.fontStyle = FontStyles.Bold;
            }

            if (passwordInput != null)
            {
                var inputBg = passwordInput.GetComponent<Image>();
                if (inputBg != null) inputBg.color = InputBgColor;

                var outline = passwordInput.GetComponent<Outline>() ?? passwordInput.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.35f, 0.38f, 0.48f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                if (passwordInput.textComponent != null)
                {
                    passwordInput.textComponent.color = Color.white;
                    passwordInput.textComponent.alignment = TextAlignmentOptions.Center;
                }
                if (passwordInput.placeholder is TMP_Text ph)
                {
                    ph.color = new Color(0.6f, 0.65f, 0.72f, 0.7f);
                    ph.alignment = TextAlignmentOptions.Center;
                }
            }

            // Nút bấm Mật khẩu
            StyleButton(passwordSubmit, GoldColor, GoldHoverColor, Color.black, "THAM GIA");
            StyleButton(passwordCancel, DarkBtnColor, new Color(0.24f, 0.27f, 0.35f, 1f), Color.white, "HỦY BỎ");

            // Lời mời
            if (inviteMessage != null)
            {
                inviteMessage.color = new Color(0.95f, 0.95f, 0.98f, 1f);
                inviteMessage.alignment = TextAlignmentOptions.Center;
            }

            // Nút bấm Lời mời
            StyleButton(inviteAccept, GoldColor, GoldHoverColor, Color.black, "ĐỒNG Ý");
            StyleButton(inviteDecline, DangerBtnColor, new Color(0.55f, 0.20f, 0.24f, 1f), Color.white, "TỪ CHỐI");
        }

        private void StyleDialogCard(GameObject cardGo)
        {
            if (cardGo == null) return;
            var img = cardGo.GetComponent<Image>();
            if (img != null) img.color = CardBgColor;

            var outline = cardGo.GetComponent<Outline>() ?? cardGo.AddComponent<Outline>();
            outline.effectColor = GoldColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var shadow = cardGo.GetComponent<Shadow>() ?? cardGo.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(4f, -4f);
        }

        private void StyleButton(Button btn, Color normalBg, Color hoverBg, Color textColor, string defaultText = null)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = normalBg;

            var cb = btn.colors;
            cb.normalColor = normalBg;
            cb.highlightedColor = hoverBg;
            cb.pressedColor = normalBg * 0.85f;
            cb.selectedColor = normalBg;
            btn.colors = cb;

            var txt = btn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.color = textColor;
                txt.fontStyle = FontStyles.Bold;
                if (!string.IsNullOrEmpty(defaultText)) txt.text = defaultText;
            }
        }

        private void Bind()
        {
            if (network != null) return;
            network = PuzzleNetworkManager.Instance;
            if (network == null) return;
            network.OnPasswordRequired += OpenPassword;
            network.OnInviteReceived += OpenInvite;
            network.OnRoomUpdated += Close;
        }

        private void OnDisable()
        {
            if (network == null) return;
            network.OnPasswordRequired -= OpenPassword;
            network.OnInviteReceived -= OpenInvite;
            network.OnRoomUpdated -= Close;
            network = null;
        }

        private void OpenPassword(string id, string name)
        {
            passwordRoom = id;
            transform.SetAsLastSibling();
            ApplyBlackGoldTheme();
            passwordPanel.SetActive(true);
            passwordTitle.text = $"<size=22><color=#FFD138><b>NHẬP MẬT KHẨU PHÒNG</b></color></size>\n<size=16><color=#D0D4DC>{name}</color></size>";
            passwordInput.SetTextWithoutNotify("");
            passwordInput.ActivateInputField();
        }

        private void SubmitPassword()
        {
            if (!string.IsNullOrWhiteSpace(passwordInput.text)) network?.JoinRoom(passwordRoom, passwordInput.text);
        }

        private void OpenInvite(string from, string id, string name)
        {
            invitedRoom = id;
            transform.SetAsLastSibling();
            ApplyBlackGoldTheme();
            invitePanel.SetActive(true);
            inviteMessage.text = $"<size=22><color=#FFD138><b>LỜI MỜI VÀO PHÒNG</b></color></size>\n\nNgười chơi <color=#FFD138><b>{from}</b></color> mời bạn tham gia phòng:\n<size=18><color=#FFFFFF><b>{name}</b></color></size>\n<size=14><color=#9AA0A6>Mã phòng: {id}</color></size>";
        }

        private void RespondInvite(bool accept)
        {
            network?.SendInviteResponse(invitedRoom, accept);
            invitePanel.SetActive(false);
        }

        private void Close()
        {
            passwordPanel.SetActive(false);
            invitePanel.SetActive(false);
        }
    }
}
