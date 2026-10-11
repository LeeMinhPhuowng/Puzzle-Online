using System;
using UnityEngine;

namespace PuzzleOnline.Network
{
    /// <summary>
    /// Quản lý xác thực tài khoản người chơi (Đăng nhập, Đăng ký, Đăng xuất) và lưu trữ thông tin phiên.
    /// </summary>
    public class NetworkAuthService
    {
        private readonly RealtimeClient _client;

        public string Username { get; private set; } = string.Empty;
        public int Coins { get; set; }
        public string SessionToken { get; private set; } = string.Empty;
        public int TotalScore { get; set; }
        public int UniquePuzzlesCompleted { get; set; }

        public event Action OnAuthSuccess;
        public event Action<string> OnError;

        public NetworkAuthService(RealtimeClient client)
        {
            _client = client;
        }

        public void Register(string user, string pass)
        {
            _client.Send("REGISTER", ("username", user), ("password", pass));
        }

        public void Login(string user, string pass)
        {
            _client.Send("LOGIN", ("username", user), ("password", pass));
        }

        public void Logout()
        {
            _client.Send("LOGOUT");
            SessionToken = string.Empty;
            Username = string.Empty;
        }

        public bool HandleMessage(NetMessage message)
        {
            switch (message.Type)
            {
                case "AUTH_OK":
                    Username = message.Get("username");
                    Coins = message.GetInt("coins", 0);
                    SessionToken = message.Get("token");
                    OnAuthSuccess?.Invoke();
                    return true;

                case "ERROR":
                    OnError?.Invoke(message.Get("message", "Lỗi không xác định"));
                    return false;
            }
            return false;
        }
    }
}
