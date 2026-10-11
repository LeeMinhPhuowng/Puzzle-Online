using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleOnline.Network
{
    /// <summary>
    /// Quản lý tính năng mạng xã hội: danh sách bạn bè, lời mời kết bạn, phòng chat và emoji.
    /// </summary>
    public class NetworkSocialService
    {
        private readonly RealtimeClient _client;

        public List<FriendInfo> Friends { get; } = new List<FriendInfo>();
        public List<FriendRequestItem> PendingFriendRequests { get; } = new List<FriendRequestItem>();
        public List<FriendRequestItem> SentFriendRequests { get; } = new List<FriendRequestItem>();

        public event Action OnFriendRequestsUpdated;
        public event Action<string, long> OnFriendRequestReceived;
        public event Action<string, string> OnChatEvent;
        public event Action<string, string> OnEmojiEvent;
        public event Action<string, string, string> OnInviteReceived;
        public event Action<string> OnPlayerEvent;
        public event Action OnLobbyRefreshNeeded;

        public NetworkSocialService(RealtimeClient client)
        {
            _client = client;
        }

        public void SendAddFriend(string username)
        {
            _client.Send("FRIEND_ADD", ("username", username));
        }

        public void SendGetFriendRequests()
        {
            _client.Send("FRIEND_REQUEST_LIST");
        }

        public void SendAcceptFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_ACCEPT", ("requestId", requestId.ToString()));
        }

        public void SendRejectFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_REJECT", ("requestId", requestId.ToString()));
        }

        public void SendCancelFriendRequest(long requestId)
        {
            _client.Send("FRIEND_REQUEST_CANCEL", ("requestId", requestId.ToString()));
        }

        public void SendInvite(string username)
        {
            _client.Send("INVITE", ("username", username));
        }

        public void SendInviteResponse(string roomId, bool accept)
        {
            _client.Send("INVITE_RESPONSE", ("roomId", roomId), ("accept", accept ? "true" : "false"));
        }

        public void SendChat(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
                _client.Send("CHAT", ("text", text));
        }

        public void SendEmoji(string emoji)
        {
            if (!string.IsNullOrWhiteSpace(emoji))
                _client.Send("EMOJI", ("emoji", emoji));
        }

        public bool HandleMessage(NetMessage message)
        {
            switch (message.Type)
            {
                case "PLAYER_EVENT":
                    OnPlayerEvent?.Invoke(message.Get("message"));
                    return true;

                case "FRIEND_REQUEST_LIST":
                    PendingFriendRequests.Clear();
                    PendingFriendRequests.AddRange(ModelParser.FriendRequests(message.Get("requests"), isSent: false));
                    SentFriendRequests.Clear();
                    SentFriendRequests.AddRange(ModelParser.FriendRequests(message.Get("sentRequests"), isSent: true));
                    OnFriendRequestsUpdated?.Invoke();
                    return true;

                case "FRIEND_REQUEST_RECEIVED":
                    string frSender = message.Get("sender");
                    long frReqId = message.GetLong("requestId", 0);
                    OnPlayerEvent?.Invoke($"Bạn vừa nhận được lời mời kết bạn từ {frSender}!");
                    OnFriendRequestReceived?.Invoke(frSender, frReqId);
                    SendGetFriendRequests();
                    return true;

                case "FRIEND_REQUEST_SENT":
                    OnPlayerEvent?.Invoke($"Đã gửi lời mời kết bạn tới {message.Get("receiver")}!");
                    return true;

                case "FRIEND_REQUEST_AUTO_ACCEPTED":
                    OnPlayerEvent?.Invoke("Hai bạn đã trở thành bạn bè!");
                    OnLobbyRefreshNeeded?.Invoke();
                    SendGetFriendRequests();
                    return true;

                case "FRIEND_REQUEST_ACCEPTED":
                    OnPlayerEvent?.Invoke("Đã chấp nhận lời mời kết bạn!");
                    OnLobbyRefreshNeeded?.Invoke();
                    SendGetFriendRequests();
                    return true;

                case "FRIEND_REQUEST_REJECTED":
                    OnPlayerEvent?.Invoke("Đã từ chối lời mời kết bạn.");
                    SendGetFriendRequests();
                    return true;

                case "FRIEND_REQUEST_CANCELLED":
                    OnPlayerEvent?.Invoke("Đã hủy lời mời kết bạn.");
                    SendGetFriendRequests();
                    return true;

                case "CHAT":
                    OnChatEvent?.Invoke(message.Get("from"), message.Get("text"));
                    return true;

                case "EMOJI":
                    OnEmojiEvent?.Invoke(message.Get("from"), message.Get("emoji"));
                    return true;

                case "INVITE":
                    OnInviteReceived?.Invoke(message.Get("from"), message.Get("roomId"), message.Get("roomName"));
                    return true;
            }
            return false;
        }
    }
}
