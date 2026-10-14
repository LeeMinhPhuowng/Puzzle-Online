using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị thông tin một bàn chơi / phòng chơi trong Scroll View Bàn Chơi (Rooms).
    /// </summary>
    public class UIRoomItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI hostNameText;
        [SerializeField] private TextMeshProUGUI roomIdText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private Button joinButton;
        [SerializeField] private TextMeshProUGUI joinButtonText;

        private RoomInfo _currentRoom;
        private Action<RoomInfo> _onJoinCallback;

        private void Awake()
        {
            EnsureComponents();
        }

        public void EnsureComponents()
        {
            if (roomNameText == null)
            {
                var t = transform.Find("Name") ?? transform.Find("RoomName") ?? transform.Find("Title");
                if (t != null) roomNameText = t.GetComponent<TextMeshProUGUI>();
            }

            if (hostNameText == null)
            {
                var t = transform.Find("Host") ?? transform.Find("HostName");
                if (t != null) hostNameText = t.GetComponent<TextMeshProUGUI>();
            }

            if (roomIdText == null)
            {
                var t = transform.Find("ID");
                if (t != null) roomIdText = t.GetComponent<TextMeshProUGUI>();
            }

            if (statusText == null)
            {
                var t = transform.Find("Status") ?? transform.Find("State");
                if (t != null) statusText = t.GetComponent<TextMeshProUGUI>();
            }

            if (lockIcon == null)
            {
                var t = transform.Find("Lock") ?? transform.Find("LockIcon") ?? transform.Find("PasswordIcon");
                if (t != null) lockIcon = t.gameObject;
            }

            if (joinButton == null)
            {
                var t = transform.Find("JoinBtn") ?? transform.Find("Button") ?? transform.Find("JoinButton");
                if (t != null) joinButton = t.GetComponent<Button>();
            }

            if (joinButton != null && joinButtonText == null)
            {
                joinButtonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        public void Bind(RoomInfo room, Action<RoomInfo> onJoin)
        {
            _currentRoom = room;
            _onJoinCallback = onJoin;

            EnsureComponents();

            if (room == null) return;

            // 1. Tên phòng
            if (roomNameText != null)
            {
                roomNameText.text = !string.IsNullOrEmpty(room.Name) ? room.Name : $"Phòng #{room.Id}";
            }

            // 2. Chủ phòng
            if (hostNameText != null)
            {
                hostNameText.text = $"Chủ bàn: {room.Host}";
            }

            // 3. Số người chơi
            if (roomIdText != null)
            {
                roomIdText.text = $"Mã: {room.Id}";
            }

            // 4. Khóa mật khẩu
            if (lockIcon != null)
            {
                lockIcon.SetActive(room.HasPassword);
            }

            // 5. Trạng thái phòng
            bool isWaiting = "WAITING".Equals(room.State, StringComparison.OrdinalIgnoreCase);
            bool isFull = room.Count >= room.MaxPlayers;

            if (statusText != null)
            {
                statusText.text = $"{(isWaiting ? "Đang chờ" : "Đang chơi")} {room.Count}/{room.MaxPlayers}";
            }

            // 6. Nút Tham gia / Vào bàn
            if (joinButton != null)
            {
                bool isComplete = "COMPLETE".Equals(room.State, StringComparison.OrdinalIgnoreCase);
                bool canJoin = !isFull && (room.GameMode != "COMPETITIVE" || (isWaiting && !isComplete));
                joinButton.interactable = canJoin;
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() =>
                {
                    _onJoinCallback?.Invoke(_currentRoom);
                });
            }

            if (joinButtonText != null)
            {
                joinButtonText.text = isFull ? "Đầy" : (!isWaiting ? "Tham gia" : "Vào bàn");
            }
        }
    }
}
