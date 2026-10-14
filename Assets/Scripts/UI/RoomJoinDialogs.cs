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

        private void Awake()
        {
            passwordSubmit.onClick.AddListener(SubmitPassword);
            passwordInput.onSubmit.AddListener(_ => SubmitPassword());
            passwordCancel.onClick.AddListener(() => passwordPanel.SetActive(false));
            inviteAccept.onClick.AddListener(() => RespondInvite(true));
            inviteDecline.onClick.AddListener(() => RespondInvite(false));
            Close();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void Start() => Bind();

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
            passwordPanel.SetActive(true);
            passwordTitle.text = "Mật khẩu phòng: " + name;
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
            invitePanel.SetActive(true);
            inviteMessage.text = $"{from} mời bạn vào phòng {name}\nMã phòng: {id}";
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
