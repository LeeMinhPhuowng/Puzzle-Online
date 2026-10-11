using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị thông tin một người bạn trong Scroll View Bạn bè.
    /// Tự động tìm hoặc liên kết các thành phần UI để tránh NullReferenceException.
    /// </summary>
    public class UIFriendItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI friendNameText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image statusDot;
        [SerializeField] private Button inviteButton;
        [SerializeField] private TextMeshProUGUI inviteButtonText;
        [SerializeField] private Image background;

        [Header("Colors")]
        [SerializeField] private Color onlineColor = new Color(0.2f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color offlineColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color playingColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        private FriendInfo _currentFriend;
        private Action<string> _onInviteCallback;

        private void Awake()
        {
            EnsureComponents();
        }

        /// <summary>
        /// Tự động tìm và cấu hình các component UI nếu chưa được gán trong Inspector
        /// </summary>
        public void EnsureComponents()
        {
            if (background == null)
                background = GetComponent<Image>();

            // Tự tìm hoặc tạo Text hiển thị Tên bạn bè
            if (friendNameText == null)
            {
                var t = transform.Find("Name") ?? transform.Find("FriendName") ?? transform.Find("Username");
                if (t != null) friendNameText = t.GetComponent<TextMeshProUGUI>();
            }

            // Tự tìm hoặc tạo Text hiển thị Trạng thái
            if (statusText == null)
            {
                var t = transform.Find("Status") ?? transform.Find("StatusText");
                if (t != null) statusText = t.GetComponent<TextMeshProUGUI>();
            }

            // Tự tìm chấm trạng thái
            if (statusDot == null)
            {
                var t = transform.Find("StatusDot") ?? transform.Find("Dot") ?? transform.Find("Signal");
                if (t != null) statusDot = t.GetComponent<Image>();
            }

            // Tự tìm nút Mời
            if (inviteButton == null)
            {
                var t = transform.Find("InviteBtn") ?? transform.Find("Button") ?? transform.Find("InviteButton");
                if (t != null) inviteButton = t.GetComponent<Button>();
            }

            if (inviteButton != null && inviteButtonText == null)
            {
                inviteButtonText = inviteButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        /// <summary>
        /// So khớp dữ liệu từ FriendInfo và hiển thị lên giao diện
        /// </summary>
        public void Bind(FriendInfo friend, Action<string> onInvite)
        {
            _currentFriend = friend;
            _onInviteCallback = onInvite;

            EnsureComponents();

            if (friend == null) return;

            // 1. Tên bạn bè
            if (friendNameText != null)
            {
                friendNameText.text = friend.Username;
            }

            // 2. Trạng thái (Online / Offline / Đang chơi)
            string displayStatus = "Offline";
            Color dotColor = offlineColor;

            if (friend.Online)
            {
                if (!string.IsNullOrEmpty(friend.Status) && friend.Status.Contains("PLAYING"))
                {
                    displayStatus = "Đang chơi";
                    dotColor = playingColor;
                }
                else if (!string.IsNullOrEmpty(friend.Status) && friend.Status.Contains("ROOM"))
                {
                    displayStatus = "Trong phòng";
                    dotColor = playingColor;
                }
                else
                {
                    displayStatus = "Trực tuyến";
                    dotColor = onlineColor;
                }
            }

            if (statusText != null)
            {
                statusText.text = displayStatus;
            }

            if (statusDot != null)
            {
                statusDot.color = dotColor;
            }

            // 3. Nút Mời vào bàn
            if (inviteButton != null)
            {
                inviteButton.interactable = friend.Online;
                inviteButton.onClick.RemoveAllListeners();
                inviteButton.onClick.AddListener(() =>
                {
                    _onInviteCallback?.Invoke(_currentFriend.Username);
                });
            }

            if (inviteButtonText != null)
            {
                inviteButtonText.text = friend.Online ? "Mời" : "Bận";
            }
        }
    }
}
