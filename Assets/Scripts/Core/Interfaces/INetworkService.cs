using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Network;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Giao diện dịch vụ mạng (Network Service Contract) cho toàn bộ trò chơi.
    /// Cho phép các thành phần Gameplay, UI, Avatar giao tiếp mà không phụ thuộc trực tiếp vào PuzzleNetworkManager.
    /// </summary>
    public interface INetworkService
    {
        // Properties
        string Username { get; }
        int Coins { get; }
        string SessionToken { get; }
        int TotalScore { get; }
        int UniquePuzzlesCompleted { get; }

        RoomInfo CurrentRoom { get; }
        bool IsHost { get; }
        List<PlayerInfo> RoomPlayers { get; }
        List<RoomInfo> LobbyRooms { get; }
        List<PuzzleDefinition> ShopPuzzles { get; }
        List<ThemeDefinition> ShopThemes { get; }
        HashSet<string> OwnedPuzzles { get; }
        HashSet<string> OwnedThemes { get; }

        List<FriendInfo> Friends { get; }
        List<FriendRequestItem> PendingFriendRequests { get; }

        string CurrentPuzzleId { get; }
        string CurrentThemeId { get; }
        int RemainingSeconds { get; }
        int PlacedPieces { get; }
        int TotalPieces { get; }
        bool IsGameActive { get; }
        Dictionary<int, PieceInfo> ActivePieces { get; }

        VoiceChatController Voice { get; }
        RealtimeClient Client { get; }

        // Events
        event Action<string> OnError;
        event Action<string> OnPlayerEvent;
        event Action OnAuthSuccess;
        event Action OnLobbyUpdated;
        event Action OnFriendRequestsUpdated;
        event Action<string, long> OnFriendRequestReceived;
        event Action OnRoomUpdated;
        event Action OnRoomLeft;
        event Action OnGameStarted;
        event Action OnGameUpdated;
        event Action<int, string> OnPieceLockedEvent;
        event Action<int, float, float, int> OnPieceMovedEvent;
        event Action<int, float, float, int, string> OnPiecePlacedEvent;
        event Action<int, float, float, int> OnPieceReleasedEvent;
        event Action<string, int> OnGameCompleteEvent;
        event Action<string, string> OnChatEvent;
        event Action<string, string> OnEmojiEvent;
        event Action<string, string, string> OnInviteReceived;
        event Action<string, float, float> OnPlayerLookReceived;
        event Action<string, Vector3, float, float> OnPlayerPoseReceived;
        event Action<ReconnectPromptInfo> OnReconnectPromptReceived;
        event Action<string, string> OnPasswordRequired;
        event Action OnGameTimeout;
        event Action OnGameContinued;

        // Methods
        void ConnectToServer(string host, int port);
        void Login(string username, string password);
        void Register(string username, string password);
        void Logout();
        void RefreshLobby();
        void CreateRoom(string name, int maxPlayers, string puzzleId, string themeId, string password, int targetTimeSeconds, string gameMode = "CLASSIC");
        void JoinRoom(string roomId, string password);
        void LeaveRoom();
        void SetReady(bool ready);
        void StartGame();

        void LockPiece(int pieceId);
        void MovePiece(int pieceId, float x, float y, int rot);
        void PlacePiece(int pieceId, float x, float y, int rot);
        void ReleasePiece(int pieceId, float x = 0f, float y = 0f, int rot = 0);

        void SendLockPiece(int pieceId);
        void SendMovePiece(int pieceId, float x, float y, int rot);
        void SendPlacePiece(int pieceId, float x, float y, int rot);
        void SendReleasePiece(int pieceId, float x = 0f, float y = 0f, int rot = 0);

        void SendChat(string text);
        void SendEmoji(string emoji);
        void SendLook(float yaw, float pitch, Vector3? position = null);
    }
}
