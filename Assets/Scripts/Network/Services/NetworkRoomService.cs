using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Utility;

namespace PuzzleOnline.Network
{
    /// <summary>
    /// Quản lý sảnh chờ (Lobby), phòng chơi (Room), cửa hàng (Shop) và tình trạng kết nối phòng.
    /// </summary>
    public class NetworkRoomService
    {
        private readonly RealtimeClient _client;

        public RoomInfo CurrentRoom { get; set; }
        public List<PlayerInfo> RoomPlayers { get; } = new List<PlayerInfo>();
        public List<RoomInfo> LobbyRooms { get; } = new List<RoomInfo>();
        public List<PuzzleDefinition> ShopPuzzles { get; } = new List<PuzzleDefinition>();
        public List<ThemeDefinition> ShopThemes { get; } = new List<ThemeDefinition>();
        public HashSet<string> OwnedPuzzles { get; } = new HashSet<string>();
        public HashSet<string> OwnedThemes { get; } = new HashSet<string>();

        public bool IsTimedOut { get; set; } = false;
        public ReconnectPromptInfo PendingReconnect { get; set; }

        public event Action OnLobbyUpdated;
        public event Action OnRoomUpdated;
        public event Action OnRoomLeft;
        public event Action OnGameTimeout;
        public event Action OnGameContinued;
        public event Action<ReconnectPromptInfo> OnReconnectPromptReceived;
        public event Action<string, string> OnPasswordRequired;
        public event Action<int> OnCoinsUpdated;

        public NetworkRoomService(RealtimeClient client)
        {
            _client = client;
        }

        public void RefreshLobby()
        {
            _client.Send("LOBBY_GET");
            _client.Send("SHOP_LIST");
        }

        public void CreateRoom(string name, int max, string puzzleId, string themeId, string password = "", int targetTime = 300, string gameMode = "CLASSIC")
        {
            _client.Send("CREATE_ROOM", ("name", name), ("max", max), ("puzzleId", puzzleId), ("themeId", themeId), ("password", password ?? string.Empty), ("targetTime", targetTime), ("gameMode", gameMode));
        }

        public void SelectTimeLimit(int seconds)
        {
            _client.Send("SELECT_TIME_LIMIT", ("seconds", seconds));
        }

        public void SelectGameMode(string gameMode)
        {
            _client.Send("SELECT_GAME_MODE", ("gameMode", gameMode));
        }

        public void SwitchTeam(string team = "")
        {
            _client.Send("SWITCH_TEAM", ("team", team));
        }

        public void PickupItem(string itemId)
        {
            _client.Send("PICKUP_ITEM", ("itemId", itemId));
        }

        public void UseSabotage(string itemType, string targetUser = "", int pieceId = -1)
        {
            _client.Send("USE_SABOTAGE", ("itemType", itemType), ("targetUser", targetUser), ("pieceId", pieceId));
        }

        public void JoinRoom(string roomId, string password = "")
        {
            _client.Send("JOIN_ROOM", ("roomId", roomId), ("password", password ?? string.Empty));
        }

        public void LeaveRoom()
        {
            _client.Send("LEAVE_ROOM");
        }

        public void SetReady(bool ready)
        {
            _client.Send("READY", ("ready", ready ? "true" : "false"));
        }

        public void SelectPuzzle(string puzzleId)
        {
            _client.Send("SELECT_PUZZLE", ("puzzleId", puzzleId));
        }

        public void SelectTheme(string themeId)
        {
            _client.Send("SELECT_THEME", ("themeId", themeId));
        }

        public void StartGame()
        {
            _client.Send("START_GAME");
        }

        public void SendBuy(string kind, string id)
        {
            _client.Send("BUY", ("kind", kind), ("id", id));
        }

        public void SendEquipTheme(string id)
        {
            _client.Send("EQUIP_THEME", ("id", id));
        }

        public void SendReconnectDecision(bool accept, string roomId)
        {
            if (_client == null || !_client.IsConnected) return;
            PendingReconnect = null;
            _client.Send("RECONNECT_DECISION", ("accept", accept ? "true" : "false"), ("roomId", roomId));
        }

        public bool HandleMessage(NetMessage message, List<FriendInfo> friendsListToPopulate)
        {
            switch (message.Type)
            {
                case "LOBBY_SNAPSHOT":
                    if (message.Data.ContainsKey("coins")) OnCoinsUpdated?.Invoke(message.GetInt("coins"));
                    LobbyRooms.Clear();
                    LobbyRooms.AddRange(ModelParser.Rooms(message.Get("rooms")));
                    if (friendsListToPopulate != null)
                    {
                        friendsListToPopulate.Clear();
                        friendsListToPopulate.AddRange(ModelParser.Friends(message.Get("friends")));
                    }
                    OnLobbyUpdated?.Invoke();
                    return true;

                case "SHOP_LIST":
                    if (message.Data.ContainsKey("coins")) OnCoinsUpdated?.Invoke(message.GetInt("coins"));
                    ShopPuzzles.Clear();
                    ShopPuzzles.AddRange(ModelParser.Puzzles(message.Get("puzzles")));
                    ShopThemes.Clear();
                    ShopThemes.AddRange(ModelParser.Themes(message.Get("themes")));
                    OwnedPuzzles.Clear();
                    foreach (var p in ShopPuzzles) if (p.Owned) OwnedPuzzles.Add(p.Id);
                    OwnedThemes.Clear();
                    foreach (var t in ShopThemes) if (t.Owned) OwnedThemes.Add(t.Id);
                    OnLobbyUpdated?.Invoke();
                    return true;

                case "ROOM_SNAPSHOT":
                    if (PendingReconnect != null) return true;

                    var room = new RoomInfo
                    {
                        Id = message.Get("roomId"),
                        Name = message.Get("name"),
                        Host = message.Get("host"),
                        Count = message.GetInt("count"),
                        MaxPlayers = message.GetInt("max"),
                        State = message.Get("state"),
                        PuzzleId = message.Get("puzzleId"),
                        ThemeId = message.Get("themeId", "SCN_Nature"),
                        HasSave = message.GetBool("hasSave"),
                        HasPassword = message.GetBool("hasPassword"),
                        TargetTimeSeconds = message.GetInt("targetTime", 300),
                        GameMode = message.Get("gameMode", "CLASSIC")
                    };
                    CurrentRoom = room;

                    RoomPlayers.Clear();
                    RoomPlayers.AddRange(ModelParser.Players(message.Get("players")));
                    OnRoomUpdated?.Invoke();
                    return true;

                case "ROOM_LEFT":
                    CurrentRoom = null;
                    IsTimedOut = false;
                    CursorManager.Unlock();
                    OnRoomLeft?.Invoke();
                    return true;

                case "DISCONNECTED":
                case "CONNECTION_FAILED":
                    CurrentRoom = null;
                    IsTimedOut = false;
                    PendingReconnect = null;
                    CursorManager.Unlock();
                    return true;

                case "GAME_TIMEOUT":
                    IsTimedOut = true;
                    CursorManager.Unlock();
                    OnGameTimeout?.Invoke();
                    return true;

                case "GAME_CONTINUED":
                    IsTimedOut = false;
                    if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
                    {
                        CursorManager.Lock();
                    }
                    OnGameContinued?.Invoke();
                    return true;

                case "PURCHASED":
                    if (message.Data.ContainsKey("coins")) OnCoinsUpdated?.Invoke(message.GetInt("coins"));
                    RefreshLobby();
                    return true;

                case "THEME_EQUIPPED":
                    RefreshLobby();
                    return true;

                case "RECONNECT_PROMPT":
                    PendingReconnect = new ReconnectPromptInfo
                    {
                        RoomId = message.Get("roomId"),
                        RoomName = message.Get("roomName"),
                        Count = message.GetInt("count"),
                        Max = message.GetInt("max"),
                        State = message.Get("state"),
                        PuzzleId = message.Get("puzzleId")
                    };
                    OnReconnectPromptReceived?.Invoke(PendingReconnect);
                    return true;

                case "ERROR":
                    string errCode = message.Get("code");
                    if (errCode == "ROOM_FULL" || errCode == "ROOM_NOT_FOUND")
                    {
                        PendingReconnect = null;
                    }
                    if (errCode == "PASSWORD_REQUIRED")
                    {
                        OnPasswordRequired?.Invoke(message.Get("roomId", ""), message.Get("roomName", ""));
                    }
                    return false;
            }
            return false;
        }

        public void ClearRoom()
        {
            CurrentRoom = null;
            RoomPlayers.Clear();
            IsTimedOut = false;
        }
    }
}
