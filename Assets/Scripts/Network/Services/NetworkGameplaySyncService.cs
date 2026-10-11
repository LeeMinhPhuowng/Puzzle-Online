using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleOnline.Network
{
    /// <summary>
    /// Quản lý đồng bộ hóa ván chơi ghép hình: nhấc, di chuyển, xoay, đặt, thả mảnh ghép,
    /// lưu/tải ván và đồng bộ chuyển động của người chơi.
    /// </summary>
    public class NetworkGameplaySyncService
    {
        private readonly RealtimeClient _client;

        public string CurrentPuzzleId { get; set; } = "sunset_3x3";
        public string CurrentThemeId { get; set; } = "SCN_Nature";
        public int RemainingSeconds { get; set; }
        public int PlacedPieces { get; set; }
        public int TotalPieces { get; set; }
        public bool IsGameActive { get; set; }
        public Dictionary<int, PieceInfo> ActivePieces { get; } = new Dictionary<int, PieceInfo>();
        public List<WorldItemInfo> ActiveWorldItems { get; } = new List<WorldItemInfo>();

        public event Action OnGameStarted;
        public event Action OnGameUpdated;
        public event Action<int, string> OnPieceLockedEvent;
        public event Action<int, float, float, int> OnPieceMovedEvent;
        public event Action<int, float, float, int, string> OnPiecePlacedEvent;
        public event Action<int, float, float, int> OnPieceReleasedEvent;
        public event Action<string, int> OnGameCompleteEvent;
        public event Action<string, float, float> OnPlayerLookReceived;
        public event Action<string, Vector3, float, float> OnPlayerPoseReceived;

        public event Action<WorldItemInfo> OnItemSpawned;
        public event Action<string, string, string> OnItemPickedUp;
        public event Action<string, string, string, float> OnSabotageTriggered;
        public event Action<List<int>, float, string> OnInkSplash;
        public event Action<int, float, float, int, string> OnPieceUnfit;
        public event Action<string, string> OnGameOverResult;

        private float _lastSentLookYaw = -9999f;
        private float _lastSentLookPitch = -9999f;
        private Vector3 _lastSentLookPos = new Vector3(-9999f, -9999f, -9999f);
        private float _lastLookSendTime = 0f;

        public NetworkGameplaySyncService(RealtimeClient client)
        {
            _client = client;
        }

        public void SendLockPiece(int pieceId)
        {
            _client.Send("LOCK_PIECE", ("pieceId", pieceId), ("id", pieceId));
        }

        public void SendMovePiece(int pieceId, float x, float y, int rotation)
        {
            _client.Send("MOVE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation));
        }

        public void SendRotatePiece(int pieceId, int angleDelta)
        {
            _client.Send("ROTATE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("degrees", angleDelta), ("delta", angleDelta));
        }

        public void SendPlacePiece(int pieceId, float x, float y, int rotation)
        {
            _client.Send("PLACE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation), ("placed", "true"));
        }

        public void SendReleasePiece(int pieceId, float x = 0f, float y = 0f, int rotation = 0)
        {
            _client.Send("RELEASE_PIECE", ("pieceId", pieceId), ("id", pieceId), ("x", x), ("y", y), ("rot", rotation));
        }

        public void SendSaveGame(string label = "Ván chơi đã lưu")
        {
            _client.Send("SAVE_GAME", ("label", label));
        }

        public void SendLoadGame()
        {
            _client.Send("LOAD_GAME");
        }

        public void SendRestartGame()
        {
            _client.Send("RESTART_GAME");
        }

        public void SendContinueGame()
        {
            _client.Send("CONTINUE_GAME");
        }

        public void SendLook(float yaw, float pitch, Vector3? position = null)
        {
            if (_client == null || !_client.IsConnected || !IsGameActive) return;

            Vector3 pos = position ?? Vector3.zero;
            bool hasPos = position.HasValue;

            if (Time.time - _lastLookSendTime < 0.066f) return;
            bool rotChanged = Mathf.Abs(yaw - _lastSentLookYaw) >= 0.3f || Mathf.Abs(pitch - _lastSentLookPitch) >= 0.3f;
            bool posChanged = hasPos && Vector3.Distance(pos, _lastSentLookPos) >= 0.04f;

            if (!rotChanged && !posChanged) return;

            _lastSentLookYaw = yaw;
            _lastSentLookPitch = pitch;
            if (hasPos) _lastSentLookPos = pos;
            _lastLookSendTime = Time.time;

            if (hasPos)
            {
                _client.Send("LOOK", ("yaw", yaw), ("pitch", pitch), ("x", pos.x), ("y", pos.y), ("z", pos.z));
            }
            else
            {
                _client.Send("LOOK", ("yaw", yaw), ("pitch", pitch));
            }
        }

        public bool HandleMessage(NetMessage message, string localUsername, Action<int> addScoreCallback, Action addUniqueCompleteCallback)
        {
            switch (message.Type)
            {
                case "GAME_SNAPSHOT":
                    bool isInitialStart = !IsGameActive;
                    IsGameActive = true;
                    CurrentPuzzleId = message.Get("puzzleId", CurrentPuzzleId);
                    CurrentThemeId = message.Get("themeId", CurrentThemeId);
                    RemainingSeconds = message.GetInt("remaining");
                    PlacedPieces = message.GetInt("placed");
                    TotalPieces = message.GetInt("total");

                    var pieces = ModelParser.Pieces(message.Get("pieces"));
                    ActivePieces.Clear();
                    foreach (var p in pieces) ActivePieces[p.Id] = p;

                    if (message.Data.ContainsKey("items"))
                    {
                        ActiveWorldItems.Clear();
                        ActiveWorldItems.AddRange(ModelParser.WorldItems(message.Get("items")));
                    }

                    if (isInitialStart)
                    {
                        OnGameStarted?.Invoke();
                    }
                    OnGameUpdated?.Invoke();
                    return true;

                case "ITEM_SPAWN":
                    var spItem = new WorldItemInfo
                    {
                        Id = message.Get("itemId"),
                        ItemType = message.Get("type"),
                        X = message.GetFloat("x"),
                        Y = message.GetFloat("y"),
                        Z = message.GetFloat("z")
                    };
                    ActiveWorldItems.Add(spItem);
                    OnItemSpawned?.Invoke(spItem);
                    return true;

                case "ITEM_PICKED_UP":
                    string pItemId = message.Get("itemId");
                    string pItemType = message.Get("type");
                    string pBy = message.Get("by");
                    ActiveWorldItems.RemoveAll(it => it.Id == pItemId);
                    OnItemPickedUp?.Invoke(pItemId, pItemType, pBy);
                    return true;

                case "PIECE_UNFIT":
                    int unfitId = message.GetInt("pieceId", message.GetInt("id"));
                    float uX = message.GetFloat("x");
                    float uY = message.GetFloat("y");
                    int uRot = message.GetInt("rot");
                    string uBy = message.Get("by");
                    PlacedPieces = message.GetInt("placed", Mathf.Max(0, PlacedPieces - 1));
                    if (ActivePieces.TryGetValue(unfitId, out var unfitPiece))
                    {
                        unfitPiece.Placed = false;
                        unfitPiece.LockedBy = string.Empty;
                        unfitPiece.X = uX;
                        unfitPiece.Y = uY;
                        unfitPiece.Rotation = uRot;
                    }
                    OnPieceUnfit?.Invoke(unfitId, uX, uY, uRot, uBy);
                    return true;

                case "INK_SPLASH":
                    string rawIds = message.Get("pieceIds");
                    float inkDur = message.GetFloat("duration", 8f);
                    string inkBy = message.Get("by");
                    var idList = new List<int>();
                    if (!string.IsNullOrEmpty(rawIds))
                    {
                        foreach (var part in rawIds.Split(','))
                        {
                            if (int.TryParse(part.Trim(), out int pid)) idList.Add(pid);
                        }
                    }
                    OnInkSplash?.Invoke(idList, inkDur, inkBy);
                    return true;

                case "SABOTAGE_TRIGGERED":
                    string sType = message.Get("item");
                    string sTarget = message.Get("target");
                    string sBy = message.Get("by");
                    float sDur = message.GetFloat("duration", 3.5f);
                    OnSabotageTriggered?.Invoke(sType, sTarget, sBy, sDur);
                    return true;

                case "GAME_OVER":
                    string goResult = message.Get("result");
                    string goWinnerTeam = message.Get("winnerTeam");
                    OnGameOverResult?.Invoke(goResult, goWinnerTeam);
                    return true;

                case "PIECE_LOCKED":
                    int lockId = message.GetInt("pieceId", message.GetInt("id"));
                    string lockedBy = message.Get("by");
                    if (ActivePieces.TryGetValue(lockId, out var lockPiece))
                        lockPiece.LockedBy = lockedBy;
                    OnPieceLockedEvent?.Invoke(lockId, lockedBy);
                    return true;

                case "PIECE_MOVED":
                    int moveId = message.GetInt("pieceId", message.GetInt("id"));
                    float mx = message.GetFloat("x");
                    float my = message.GetFloat("y");
                    int mrot = message.GetInt("rot");
                    if (ActivePieces.TryGetValue(moveId, out var movePiece))
                    {
                        movePiece.X = mx;
                        movePiece.Y = my;
                        movePiece.Rotation = mrot;
                    }
                    OnPieceMovedEvent?.Invoke(moveId, mx, my, mrot);
                    return true;

                case "PIECE_PLACED":
                    int placeId = message.GetInt("pieceId", message.GetInt("id"));
                    float px = message.GetFloat("x");
                    float py = message.GetFloat("y");
                    int prot = message.GetInt("rot");
                    string placedBy = message.Get("by");
                    PlacedPieces = message.GetInt("placed", PlacedPieces + 1);
                    TotalPieces = message.GetInt("total", TotalPieces);
                    if (ActivePieces.TryGetValue(placeId, out var placedPiece))
                    {
                        placedPiece.Placed = true;
                        placedPiece.LockedBy = string.Empty;
                        placedPiece.X = px;
                        placedPiece.Y = py;
                        placedPiece.Rotation = prot;
                    }
                    if (placedBy == localUsername) addScoreCallback?.Invoke(10);
                    OnPiecePlacedEvent?.Invoke(placeId, px, py, prot, placedBy);
                    return true;

                case "PIECE_RELEASED":
                    int relId = message.GetInt("pieceId", message.GetInt("id"));
                    float rx = message.GetFloat("x");
                    float ry = message.GetFloat("y");
                    int rrot = message.GetInt("rot");
                    if (ActivePieces.TryGetValue(relId, out var relPiece))
                    {
                        relPiece.LockedBy = string.Empty;
                        if (message.Get("x") != null)
                        {
                            relPiece.X = rx;
                            relPiece.Y = ry;
                            relPiece.Rotation = rrot;
                        }
                    }
                    OnPieceReleasedEvent?.Invoke(relId, rx, ry, rrot);
                    return true;

                case "GAME_COMPLETE":
                    string winner = message.Get("winner");
                    int bonus = message.GetInt("bonus", 50);
                    addUniqueCompleteCallback?.Invoke();
                    OnGameCompleteEvent?.Invoke(winner, bonus);
                    return true;

                case "PLAYER_LOOK":
                    string lookUser = message.Get("username");
                    float lookYaw = message.GetFloat("yaw");
                    float lookPitch = message.GetFloat("pitch");
                    OnPlayerLookReceived?.Invoke(lookUser, lookYaw, lookPitch);
                    if (!string.IsNullOrEmpty(message.Get("x")))
                    {
                        Vector3 remotePos = new Vector3(
                            message.GetFloat("x"),
                            message.GetFloat("y"),
                            message.GetFloat("z")
                        );
                        OnPlayerPoseReceived?.Invoke(lookUser, remotePos, lookYaw, lookPitch);
                    }
                    return true;
            }
            return false;
        }

        public void ResetGameState()
        {
            IsGameActive = false;
            ActivePieces.Clear();
            ActiveWorldItems.Clear();
            PlacedPieces = 0;
            TotalPieces = 0;
        }
    }
}
