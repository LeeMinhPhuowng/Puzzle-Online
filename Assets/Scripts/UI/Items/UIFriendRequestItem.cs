using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị 1 lời mời kết bạn trong AddFriendView (lời mời đã nhận hoặc đã gửi).
    /// </summary>
    public class UIFriendRequestItem : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private TextMeshProUGUI acceptButtonText;
        [SerializeField] private Button declineButton;
        [SerializeField] private TextMeshProUGUI declineButtonText;
        [SerializeField] private Image backgroundImage;

        private FriendRequestItem _request;
        private Action<long> _onAccept;
        private Action<long> _onDecline;
        private Action<long> _onCancel;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();

            if (usernameText == null)
            {
                var t = transform.Find("Username") ?? transform.Find("Name") ?? transform.Find("Title");
                if (t != null) usernameText = t.GetComponent<TextMeshProUGUI>();
            }

            if (timeText == null)
            {
                var t = transform.Find("Time") ?? transform.Find("Date") ?? transform.Find("Status");
                if (t != null) timeText = t.GetComponent<TextMeshProUGUI>();
            }

            if (acceptButton == null)
            {
                var t = transform.Find("Accept") ?? transform.Find("AcceptBtn") ?? transform.Find("Button");
                if (t != null) acceptButton = t.GetComponent<Button>();
            }

            if (acceptButton != null && acceptButtonText == null)
            {
                acceptButtonText = acceptButton.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (declineButton == null)
            {
                var t = transform.Find("Decline") ?? transform.Find("DeclineBtn") ?? transform.Find("Reject");
                if (t != null) declineButton = t.GetComponent<Button>();
            }

            if (declineButton != null && declineButtonText == null)
            {
                declineButtonText = declineButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        public void Bind(FriendRequestItem request, Action<long> onAccept, Action<long> onDecline, Action<long> onCancel)
        {
            EnsureComponents();

            _request = request;
            _onAccept = onAccept;
            _onDecline = onDecline;
            _onCancel = onCancel;

            string targetName = !string.IsNullOrEmpty(request.TargetUsername) ? request.TargetUsername : request.SenderUsername;

            if (usernameText != null)
            {
                usernameText.text = targetName;
                usernameText.color = Color.white;
            }

            if (timeText != null)
            {
                string dt = !string.IsNullOrEmpty(request.CreatedAt) ? request.CreatedAt : "";
                timeText.text = request.IsSent ? $"<color=#f59e0b>Đang chờ phản hồi</color> • {dt}" : $"Lời mời kết bạn • {dt}";
            }

            if (request.IsSent)
            {
                // Lời mời đã gửi: chỉ có nút Hủy (Cancel)
                if (acceptButton != null) acceptButton.gameObject.SetActive(false);

                if (declineButton != null)
                {
                    declineButton.gameObject.SetActive(true);
                    if (declineButtonText != null) declineButtonText.text = "Hủy";
                    declineButton.onClick.RemoveAllListeners();
                    declineButton.onClick.AddListener(() => _onCancel?.Invoke(_request.RequestId));
                }
            }
            else
            {
                // Lời mời đã nhận: Có nút Đồng ý & Từ chối
                if (acceptButton != null)
                {
                    acceptButton.gameObject.SetActive(true);
                    if (acceptButtonText != null) acceptButtonText.text = "Đồng ý";
                    acceptButton.onClick.RemoveAllListeners();
                    acceptButton.onClick.AddListener(() => _onAccept?.Invoke(_request.RequestId));
                }

                if (declineButton != null)
                {
                    declineButton.gameObject.SetActive(true);
                    if (declineButtonText != null) declineButtonText.text = "Từ chối";
                    declineButton.onClick.RemoveAllListeners();
                    declineButton.onClick.AddListener(() => _onDecline?.Invoke(_request.RequestId));
                }
            }
        }
    }
}
