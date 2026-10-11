using System;
using UnityEngine;
using PuzzleOnline.Utility;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Lắng nghe các sự kiện mạng từ PuzzleNetworkManager và đồng bộ hóa trạng thái các mảnh ghép
    /// (khóa, di chuyển, thả, hoàn thành) giữa các người chơi trong phòng.
    /// </summary>
    public class PuzzleNetworkSync : MonoBehaviour
    {
        private PuzzleBoard board;

        private void Awake()
        {
            board = GetComponent<PuzzleBoard>();
        }

        private void OnEnable()
        {
            RegisterNetworkEvents();
        }

        private void OnDisable()
        {
            UnregisterNetworkEvents();
        }

        public void RegisterNetworkEvents()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnGameStarted += HandleGameStarted;
            net.OnGameUpdated += HandleGameUpdated;
            net.OnPieceLockedEvent += HandleRemotePieceLocked;
            net.OnPieceMovedEvent += HandleRemotePieceMoved;
            net.OnPiecePlacedEvent += HandleRemotePiecePlaced;
            net.OnPieceReleasedEvent += HandleRemotePieceReleased;
            net.OnGameCompleteEvent += HandleRemoteGameComplete;
            net.OnPieceUnfit += HandleRemotePieceUnfit;
            net.OnInkSplash += HandleRemoteInkSplash;
        }

        public void UnregisterNetworkEvents()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null) return;

            net.OnGameStarted -= HandleGameStarted;
            net.OnGameUpdated -= HandleGameUpdated;
            net.OnPieceLockedEvent -= HandleRemotePieceLocked;
            net.OnPieceMovedEvent -= HandleRemotePieceMoved;
            net.OnPiecePlacedEvent -= HandleRemotePiecePlaced;
            net.OnPieceReleasedEvent -= HandleRemotePieceReleased;
            net.OnGameCompleteEvent -= HandleRemoteGameComplete;
            net.OnPieceUnfit -= HandleRemotePieceUnfit;
            net.OnInkSplash -= HandleRemoteInkSplash;
        }

        private void HandleGameStarted()
        {
            if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
            {
                CursorManager.Lock();
            }

            if (board != null)
            {
                board.PositionLocalPlayer(false);
                board.SyncRemotePlayerAvatars();
            }

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null || board == null) return;

            string netPuzzleId = net.CurrentPuzzleId;
            if (!string.IsNullOrEmpty(netPuzzleId))
            {
                if (board.ActivePieces == null || board.ActivePieces.Count == 0 || board.LoadedPuzzleId != netPuzzleId)
                {
                    board.LoadPuzzle(netPuzzleId, true);
                }
                SyncPiecesFromServer();
            }
        }

        private void HandleGameUpdated()
        {
            if (board != null)
            {
                board.SyncRemotePlayerAvatars();
            }

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net != null && net.IsGameActive &&
                !PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen &&
                !CursorManager.IsLocked)
            {
                CursorManager.Lock();
            }

            if (net != null && board != null)
            {
                string netPuzzleId = net.CurrentPuzzleId;
                if (!string.IsNullOrEmpty(netPuzzleId) &&
                    (board.ActivePieces == null || board.ActivePieces.Count == 0 || board.LoadedPuzzleId != netPuzzleId))
                {
                    board.LoadPuzzle(netPuzzleId, true);
                }
                SyncPiecesFromServer();
            }
        }

        /// <summary>
        /// Đồng bộ vị trí, góc xoay và trạng thái khóa của tất cả mảnh ghép từ dữ liệu server về client.
        /// </summary>
        public void SyncPiecesFromServer()
        {
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (net == null || board == null || board.ActivePieces == null) return;

            var serverPieces = net.ActivePieces;
            if (serverPieces == null || serverPieces.Count == 0) return;

            string myUsername = net.Username;

            foreach (var piece in board.ActivePieces)
            {
                if (piece == null) continue;

                if (serverPieces.TryGetValue(piece.pieceId, out var info))
                {
                    if (info.Placed)
                    {
                        if (!piece.isPlaced)
                        {
                            piece.SnapToTarget();
                        }
                    }
                    else
                    {
                        bool isLockedBySomeoneElse = !string.IsNullOrEmpty(info.LockedBy) &&
                                                     !string.Equals(info.LockedBy, myUsername, StringComparison.OrdinalIgnoreCase);

                        if (isLockedBySomeoneElse)
                        {
                            Vector3 targetPos = new Vector3(info.X, piece.correctLocalPos.y + (piece.pieceId * 0.0003f), info.Y);
                            Quaternion targetRot = Quaternion.Euler(0f, info.Rotation, 0f);
                            piece.SetRemoteTarget(targetPos, targetRot);
                            piece.SetRemoteLock(info.LockedBy);
                        }
                        else
                        {
                            piece.SetRemoteLock("");
                            Vector3 targetPos = new Vector3(info.X, piece.correctLocalPos.y + (piece.pieceId * 0.0003f), info.Y);
                            Quaternion targetRot = Quaternion.Euler(0f, info.Rotation, 0f);
                            if (Vector3.Distance(piece.transform.localPosition, targetPos) > 0.003f)
                            {
                                piece.SetRemoteTarget(targetPos, targetRot);
                            }
                        }
                    }
                }
            }

            board.RecalculateProgress();
            board.EnsureGhostGuideBoard();
        }

        private PuzzlePiece FindPiece(int pieceId)
        {
            if (board == null || board.ActivePieces == null) return null;
            return board.ActivePieces.Find(p => p != null && p.pieceId == pieceId);
        }

        private void HandleRemotePieceLocked(int pieceId, string lockedBy)
        {
            var piece = FindPiece(pieceId);
            if (piece != null)
            {
                piece.SetRemoteLock(lockedBy);
            }
        }

        private void HandleRemotePieceMoved(int pieceId, float x, float y, int rot)
        {
            var piece = FindPiece(pieceId);
            if (piece != null && !piece.isBeingHeld)
            {
                Vector3 targetPos = new Vector3(x, piece.correctLocalPos.y + (piece.pieceId * 0.0003f), y);
                Quaternion targetRot = Quaternion.Euler(0f, rot, 0f);
                piece.SetRemoteTarget(targetPos, targetRot);
            }
        }

        private void HandleRemotePiecePlaced(int pieceId, float x, float y, int rot, string placedBy)
        {
            var piece = FindPiece(pieceId);
            if (piece != null)
            {
                piece.SetRemoteLock("");
                if (!piece.isPlaced)
                {
                    piece.SnapToTarget();
                }
            }
        }

        private void HandleRemotePieceReleased(int pieceId, float x, float y, int rot)
        {
            var piece = FindPiece(pieceId);
            if (piece != null)
            {
                piece.SetRemoteLock("");
                if (!piece.isPlaced && !piece.isBeingHeld)
                {
                    Vector3 targetPos = new Vector3(x, piece.correctLocalPos.y + (piece.pieceId * 0.0003f), y);
                    Quaternion targetRot = Quaternion.Euler(0f, rot, 0f);
                    piece.SetRemoteTarget(targetPos, targetRot);
                }
            }
        }

        private void HandleRemoteGameComplete(string winner, int bonus)
        {
            if (board != null)
            {
                board.CompleteGame();
            }
        }

        private void HandleRemotePieceUnfit(int pieceId, float x, float y, int rot, string by)
        {
            var piece = FindPiece(pieceId);
            if (piece != null)
            {
                Vector3 targetPos = new Vector3(x, piece.correctLocalPos.y + (piece.pieceId * 0.0003f), y);
                Quaternion targetRot = Quaternion.Euler(0f, rot, 0f);
                piece.Unfit(targetPos, targetRot);
                if (board != null)
                {
                    board.RecalculateProgress();
                }
            }
        }

        private void HandleRemoteInkSplash(System.Collections.Generic.List<int> pieceIds, float duration, string by)
        {
            if (pieceIds == null) return;
            foreach (int pid in pieceIds)
            {
                var piece = FindPiece(pid);
                if (piece != null)
                {
                    piece.ApplyInkEffect(duration);
                }
            }
        }
    }
}
