using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PuzzleSystem.Core;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleBoard : MonoBehaviour
    {
        [Header("Table References")]
        [Tooltip("Transform của mặt bàn (Plane (1), PicnicTable, etc.)")]
        public Transform tableTransform;

        [Tooltip("Danh sách ghế ngồi xung quanh bàn")]
        public List<Transform> chairTransforms = new List<Transform>();

        [Header("Active Puzzle")]
        public PuzzleData currentPuzzleData;
        public PuzzleConfiguration config;

        [Header("Physical Dimensions (Current)")]
        public float currentBoardWidth = 0.8f;
        public float currentBoardHeight = 0.6f;
        public float currentThickness = 0.005f;

        [Header("Runtime State")]
        public List<PuzzlePiece> activePieces = new List<PuzzlePiece>();
        public int placedCount = 0;
        public bool isCompleted = false;

        [Header("Quick Testing (In Play Mode)")]
        [Tooltip("Bật phím số 1, 2, 3 trong Play mode để chuyển đổi nhanh giữa 3 bộ tranh")]
        public bool enableQuickTestKeys = true;

        public event Action<float> OnProgressChanged; // Trả về tỉ lệ hoàn thành 0.0 -> 1.0
        public event Action OnPuzzleCompleted;

        private string loadedPuzzleId = "";
        private Bounds cachedTableBounds;
        private bool hasTableBounds = false;

        private void Awake()
        {
            playerPositioned = false;
            hasInitializedCamera = false;
            loadedPuzzleId = "";
        }

        private void Start()
        {
            AutoDetectSceneEnvironment();
            EnsureCameraInteractor();
            PositionLocalPlayer();

            // Nếu trong cảnh đã có sẵn các mảnh ghép (ví dụ người dùng kéo Prefab ra hoặc tự xếp)
            if (activePieces == null || activePieces.Count == 0)
            {
                var existingPieces = GetComponentsInChildren<PuzzlePiece>();
                if (existingPieces != null && existingPieces.Length > 0)
                {
                    activePieces = new List<PuzzlePiece>(existingPieces);
                    placedCount = 0;
                    foreach (var p in activePieces)
                    {
                        p.OnPiecePlaced -= HandlePiecePlaced;
                        p.OnPiecePlaced += HandlePiecePlaced;
                        if (p.isPlaced) placedCount++;
                    }
                    UpdateProgress();
                }
            }

            // Đồng bộ bộ xếp hình từ Multiplayer Network Manager nếu đang trong ván chơi mạng
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null &&
                PuzzleOnline.Network.PuzzleNetworkManager.Instance.IsGameActive)
            {
                if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                PositionLocalPlayer(true);
                SyncRemotePlayerAvatars();

                string netPuzzleId = PuzzleOnline.Network.PuzzleNetworkManager.Instance.CurrentPuzzleId;
                if (!string.IsNullOrEmpty(netPuzzleId))
                {
                    if (activePieces == null || activePieces.Count == 0 || loadedPuzzleId != netPuzzleId)
                    {
                        LoadPuzzle(netPuzzleId, true);
                    }
                    SyncPiecesFromServer();
                }
            }

            // Scene objects are initialized in an unspecified order.  Run one
            // more forced placement after the Player prefab has completed Start
            // so a manually placed Player cannot remain at its editor position.
            StartCoroutine(PositionLocalPlayerAfterSceneSetup());
        }

        private IEnumerator PositionLocalPlayerAfterSceneSetup()
        {
            yield return null;
            PositionLocalPlayer(true);

            yield return new WaitForEndOfFrame();
            PositionLocalPlayer(true);
        }

        private void OnEnable()
        {
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null)
            {
                var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
                net.OnPieceLockedEvent += HandleRemotePieceLocked;
                net.OnPieceMovedEvent += HandleRemotePieceMoved;
                net.OnPiecePlacedEvent += HandleRemotePiecePlaced;
                net.OnPieceReleasedEvent += HandleRemotePieceReleased;
                net.OnGameCompleteEvent += HandleRemoteGameComplete;
                net.OnGameStarted += HandleGameStarted;
                net.OnGameUpdated += HandleGameUpdated;
                net.OnPlayerLookReceived += HandleRemotePlayerLook;
                net.OnRoomUpdated += HandleRoomUpdatedAvatars;
            }
        }

        private void OnDisable()
        {
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null)
            {
                var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
                net.OnPieceLockedEvent -= HandleRemotePieceLocked;
                net.OnPieceMovedEvent -= HandleRemotePieceMoved;
                net.OnPiecePlacedEvent -= HandleRemotePiecePlaced;
                net.OnPieceReleasedEvent -= HandleRemotePieceReleased;
                net.OnGameCompleteEvent -= HandleRemoteGameComplete;
                net.OnGameStarted -= HandleGameStarted;
                net.OnGameUpdated -= HandleGameUpdated;
                net.OnPlayerLookReceived -= HandleRemotePlayerLook;
                net.OnRoomUpdated -= HandleRoomUpdatedAvatars;
            }
            ClearRemotePlayerAvatars();
            loadedPuzzleId = "";
            playerPositioned = false;
            hasInitializedCamera = false;
        }

        private void HandleRoomUpdatedAvatars()
        {
            SyncRemotePlayerAvatars();
        }

        private void HandleGameStarted()
        {
            if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            PositionLocalPlayer(false);
            SyncRemotePlayerAvatars();

            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance == null) return;
            string netPuzzleId = PuzzleOnline.Network.PuzzleNetworkManager.Instance.CurrentPuzzleId;
            if (!string.IsNullOrEmpty(netPuzzleId))
            {
                if (activePieces == null || activePieces.Count == 0 || loadedPuzzleId != netPuzzleId)
                {
                    LoadPuzzle(netPuzzleId, true);
                }
                SyncPiecesFromServer();
            }
        }

        private void HandleGameUpdated()
        {
            SyncRemotePlayerAvatars();

            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null &&
                PuzzleOnline.Network.PuzzleNetworkManager.Instance.IsGameActive &&
                !PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen &&
                Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            string netPuzzleId = PuzzleOnline.Network.PuzzleNetworkManager.Instance?.CurrentPuzzleId;
            if (!string.IsNullOrEmpty(netPuzzleId) &&
                (activePieces == null || activePieces.Count == 0 || loadedPuzzleId != netPuzzleId))
            {
                LoadPuzzle(netPuzzleId, true);
                SyncPiecesFromServer();
            }
            else
            {
                SyncPiecesFromServer();
            }
        }

        public void SyncPiecesFromServer()
        {
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance == null) return;
            var serverPieces = PuzzleOnline.Network.PuzzleNetworkManager.Instance.ActivePieces;

            foreach (var piece in activePieces)
            {
                if (piece == null) continue;
                if (piece.isBeingHeld) continue;

                if (serverPieces.TryGetValue(piece.pieceId, out var info))
                {
                    if (info.Placed)
                    {
                        piece.SnapToTarget();
                    }
                    else
                    {
                        piece.isPlaced = false;
                        if (!string.IsNullOrEmpty(info.LockedBy))
                        {
                            piece.SetRemoteLock(info.LockedBy);
                        }
                        else
                        {
                            piece.SetRemoteLock("");
                            // ĐỒNG BỘ VỊ TRÍ TỨC THỜI CHO CÁC MẢNH ĐÃ RẢI HOẶC THẢ TRÊN MẶT BÀN
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

            placedCount = 0;
            foreach (var p in activePieces)
            {
                if (p != null && p.isPlaced) placedCount++;
            }
            UpdateProgress();
        }

        private PuzzlePiece FindPiece(int pieceId)
        {
            return activePieces.Find(p => p.pieceId == pieceId);
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
            isCompleted = true;
            PuzzleOnline.Audio.PuzzleAudioService.Instance?.PlayComplete();
            OnPuzzleCompleted?.Invoke();
        }

        /// <summary>
        /// Tự động di chuyển tâm bàn cờ GameObject cha về đúng tâm của các mảnh ghép hiện tại mà người dùng đã xếp trong Scene,
        /// giúp khung viền màu xanh lá trùm khít toàn bộ bức tranh và các ô snap khớp 100% vào vị trí các mảnh.
        /// </summary>
        [ContextMenu("🎯 Khớp Khung Bàn Cờ Vào Các Mảnh Ghép (Snap Board To Pieces)")]
        public void SnapBoardToPieces()
        {
            var pieces = GetComponentsInChildren<PuzzlePiece>();
            if (pieces == null || pieces.Length == 0)
            {
                Debug.LogWarning("[PuzzleBoard] Không tìm thấy mảnh ghép nào trong bàn cờ!");
                return;
            }

            // 1. Tính toán tâm thực tế của các mảnh ghép đã xếp
            Vector3 sumPos = Vector3.zero;
            foreach (var p in pieces)
            {
                sumPos += p.transform.position;
            }
            Vector3 newCenter = sumPos / pieces.Length;

            // 2. Dịch chuyển Transform cha về đúng tâm đó, đồng thời giữ nguyên vị trí thế giới của các mảnh con
            foreach (var p in pieces)
            {
                p.transform.SetParent(null, true);
            }

            transform.position = newCenter;

            // Đảm bảo bàn cờ luôn nằm ngang phẳng (Vector3.up)
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            foreach (var p in pieces)
            {
                p.transform.SetParent(transform, true);
            }

            activePieces = new List<PuzzlePiece>(pieces);
            Debug.Log($"<color=green><b>[PuzzleBoard] Đã khớp khung bàn cờ ({pieces.Length} mảnh) về đúng vị trí bạn đã đặt!</b></color>");
        }

        /// <summary>
        /// Tự động căn chỉnh mặt phẳng bàn cờ nằm ngang hoàn hảo trên bề mặt cao nhất của chiếc bàn.
        /// Giữ Up vector thẳng đứng (Vector3.up), loại bỏ góc nghiêng lỗi của mô hình FBX (ví dụ Blender xoay -90 độ trục X).
        /// </summary>
        [ContextMenu("🪑 Căn Chỉnh Lên Mặt Bàn (Align To Tabletop)")]
        public void AlignToTable()
        {
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
                transform.localScale = Vector3.one;
            }

            if (tableTransform == null)
            {
                AutoDetectSceneEnvironment();
            }

            if (tableTransform == null) return;

            // 1. Tính toán Bounds chuẩn xác của bàn (ưu tiên Collider, sau đó Renderer)
            Bounds tableBounds = new Bounds();
            bool foundBounds = false;

            var colliders = tableTransform.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                if (col.GetComponent<PuzzlePiece>() != null) continue; // Bỏ qua nếu là mảnh ghép puzzle

                if (!foundBounds)
                {
                    tableBounds = col.bounds;
                    foundBounds = true;
                }
                else
                {
                    tableBounds.Encapsulate(col.bounds);
                }
            }

            if (!foundBounds)
            {
                var renderers = tableTransform.GetComponentsInChildren<Renderer>();
                foreach (var ren in renderers)
                {
                    if (ren.GetComponent<PuzzlePiece>() != null) continue;
                    if (!foundBounds)
                    {
                        tableBounds = ren.bounds;
                        foundBounds = true;
                    }
                    else
                    {
                        tableBounds.Encapsulate(ren.bounds);
                    }
                }
            }

            // Đặt gốc bàn cờ tại tâm mặt trên của bàn (+2mm để tránh z-fighting với mặt gỗ bàn)
            float topY = foundBounds ? tableBounds.max.y : tableTransform.position.y;
            Vector3 centerPos = foundBounds
                ? new Vector3(tableBounds.center.x, topY + 0.002f, tableBounds.center.z)
                : new Vector3(tableTransform.position.x, topY + 0.002f, tableTransform.position.z);

            cachedTableBounds = tableBounds;
            hasTableBounds = foundBounds;

            transform.position = centerPos;

            // Giữ nguyên rotation đã thiết lập trong scene/prefab. Mỗi môi trường
            // có thể xoay PuzzleBoard khác nhau để khớp với mặt bàn của nó.

            // Khởi tạo 4 thành chắn BoxCollider vô hình quanh 4 mép bàn
            SetupTableEdgeColliders();
        }

        /// <summary>
        /// Giữ vị trí trong phạm vi an toàn của mặt bàn (không cho mảnh trượt văng ra ngoài mép bàn).
        /// </summary>
        public Vector3 ClampToTableBounds(Vector3 worldPos, float margin = 0.05f)
        {
            if (hasTableBounds)
            {
                worldPos.x = Mathf.Clamp(worldPos.x, cachedTableBounds.min.x + margin, cachedTableBounds.max.x - margin);
                worldPos.z = Mathf.Clamp(worldPos.z, cachedTableBounds.min.z + margin, cachedTableBounds.max.z - margin);
            }
            return worldPos;
        }

        /// <summary>
        /// Tạo 4 thành chắn BoxCollider vô hình quanh mép bàn để chặn vật lý / kéo mảnh.
        /// </summary>
        public void SetupTableEdgeColliders()
        {
            if (!hasTableBounds) return;

            Transform existing = transform.Find("[TableEdgeColliders]");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            GameObject container = new GameObject("[TableEdgeColliders]");
            container.transform.SetParent(transform, true);

            float wallThickness = 0.08f;
            float wallHeight = 0.8f;
            float centerY = cachedTableBounds.max.y + (wallHeight * 0.5f);

            // 4 bức tường vô hình quanh 4 mép bàn:
            // 1. Phía Bắc (+Z)
            CreateInvisibleWall(container.transform, "Wall_North",
                new Vector3(cachedTableBounds.center.x, centerY, cachedTableBounds.max.z + wallThickness * 0.5f),
                new Vector3(cachedTableBounds.size.x + wallThickness * 2f, wallHeight, wallThickness));

            // 2. Phía Nam (-Z)
            CreateInvisibleWall(container.transform, "Wall_South",
                new Vector3(cachedTableBounds.center.x, centerY, cachedTableBounds.min.z - wallThickness * 0.5f),
                new Vector3(cachedTableBounds.size.x + wallThickness * 2f, wallHeight, wallThickness));

            // 3. Phía Đông (+X)
            CreateInvisibleWall(container.transform, "Wall_East",
                new Vector3(cachedTableBounds.max.x + wallThickness * 0.5f, centerY, cachedTableBounds.center.z),
                new Vector3(wallThickness, wallHeight, cachedTableBounds.size.z + wallThickness * 2f));

            // 4. Phía Tây (-X)
            CreateInvisibleWall(container.transform, "Wall_West",
                new Vector3(cachedTableBounds.min.x - wallThickness * 0.5f, centerY, cachedTableBounds.center.z),
                new Vector3(wallThickness, wallHeight, cachedTableBounds.size.z + wallThickness * 2f));
        }

        private void CreateInvisibleWall(Transform parent, string name, Vector3 worldCenter, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, true);
            wall.transform.position = worldCenter;
            wall.transform.rotation = Quaternion.identity;
            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
        }

        private void EnsureCameraInteractor()
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                if (cam.GetComponent<PuzzleRaycastInteractor>() == null)
                    cam.gameObject.AddComponent<PuzzleRaycastInteractor>();
                if (cam.GetComponent<PuzzleOverheadViewController>() == null)
                    cam.gameObject.AddComponent<PuzzleOverheadViewController>();
            }
        }

        private void Update()
        {
            // Phím 1, 2, 3 đã được tắt hoàn toàn để tránh làm hỏng ván chơi phòng đã tạo
        }

        private bool playerPositioned = false;
        private bool hasInitializedCamera = false;

        public void AutoDetectSceneEnvironment()
        {
            var positionsParent = GameObject.Find("Positions") ?? GameObject.Find("positions");

            if (tableTransform == null)
            {
                string[] tableCandidateNames = { "Plane (1)", "PicnicTable", "Table", "Desk", "Plane" };
                foreach (var candidate in tableCandidateNames)
                {
                    var found = GameObject.Find(candidate);
                    if (found != null)
                    {
                        tableTransform = found.transform;
                        break;
                    }
                }

                // Some environment prefabs (currently Sakura) do not expose the
                // tabletop as a separately named GameObject.  The Positions root
                // is authored at the table centre, so it is a stable board anchor.
                if (tableTransform == null && positionsParent != null)
                {
                    tableTransform = positionsParent.transform;
                }
            }

            if (chairTransforms == null || chairTransforms.Count == 0)
            {
                chairTransforms = new List<Transform>();

                // 1. Ưu tiên tìm GameObject cha mang tên 'Positions' trong Scene
                if (positionsParent != null && positionsParent.transform.childCount > 0)
                {
                    for (int i = 0; i < positionsParent.transform.childCount; i++)
                    {
                        chairTransforms.Add(positionsParent.transform.GetChild(i));
                    }
                }

                // 2. Tìm theo danh sách tên riêng lẻ nếu chưa tìm thấy
                if (chairTransforms.Count == 0)
                {
                    string[] chairNames = { "Position (1)", "Position (2)", "Position (3)", "Position (4)", "A", "A (1)", "A (2)", "A (3)" };
                    foreach (var name in chairNames)
                    {
                        var foundChair = GameObject.Find(name);
                        if (foundChair != null) chairTransforms.Add(foundChair.transform);
                    }
                }

                // Sắp xếp thứ tự tự nhiên (Position (1), Position (2)...)
                chairTransforms.Sort((a, b) =>
                {
                    int na = ExtractNumber(a.name);
                    int nb = ExtractNumber(b.name);
                    if (na != nb) return na.CompareTo(nb);
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        private static int ExtractNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var match = System.Text.RegularExpressions.Regex.Match(s, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int val))
                return val;
            return 0;
        }

        public void PositionLocalPlayer(bool force = false)
        {
            if (playerPositioned && !force) return;

            AutoDetectSceneEnvironment();

            // Tìm Player GameObject trong scene
            GameObject playerObj = null;
            var pc = FindFirstObjectByType<PlayerController.PlayerController>();
            if (pc != null)
            {
                playerObj = pc.gameObject;
            }
            else
            {
                playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            }

            if (playerObj == null) return;

            // Xác định slot index của người chơi cục bộ trong phòng
            int slotIndex = 0;
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null &&
                PuzzleOnline.Network.PuzzleNetworkManager.Instance.RoomPlayers.Count > 0)
            {
                string myName = PuzzleOnline.Network.PuzzleNetworkManager.Instance.Username;
                var players = PuzzleOnline.Network.PuzzleNetworkManager.Instance.RoomPlayers;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].Username == myName)
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }

            // Chọn vị trí ghế ngồi
            Transform seatTarget = null;
            if (chairTransforms != null && chairTransforms.Count > 0)
            {
                seatTarget = chairTransforms[slotIndex % chairTransforms.Count];
            }

            if (seatTarget != null)
            {
                // Điểm nhìn mục tiêu: tâm cụm Positions, hoặc mặt bàn, hoặc tâm board
                Vector3 lookCenter = transform.position;
                var posParent = GameObject.Find("Positions") ?? GameObject.Find("positions");
                if (posParent != null)
                {
                    lookCenter = posParent.transform.position;
                }
                else if (tableTransform != null)
                {
                    lookCenter = tableTransform.position;
                }

                // Hướng xoay ngang (Yaw) nhìn về phía tâm bàn
                Vector3 horizDir = lookCenter - seatTarget.position;
                horizDir.y = 0f;
                Quaternion lookRot = Quaternion.identity;
                if (horizDir.sqrMagnitude > 0.001f)
                {
                    lookRot = Quaternion.LookRotation(horizDir.normalized, Vector3.up);
                }

                // Dịch chuyển Player đến vị trí ghế
                if (pc != null)
                {
                    pc.TeleportTo(seatTarget.position, lookRot);
                }
                else
                {
                    var cc = playerObj.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    playerObj.transform.position = seatTarget.position;
                    playerObj.transform.rotation = lookRot;
                    if (cc != null) cc.enabled = true;
                }

                // Tính góc chúc xuống (Pitch) nhìn thẳng vào mặt bàn xếp hình
                Vector3 eyePos = seatTarget.position + Vector3.up * 1.5f;
                Vector3 eyeToTarget = lookCenter - eyePos;
                float pitch = 0f;
                if (eyeToTarget.sqrMagnitude > 0.001f)
                {
                    pitch = Quaternion.LookRotation(eyeToTarget).eulerAngles.x;
                    if (pitch > 180f) pitch -= 360f;
                    pitch = Mathf.Clamp(pitch, -30f, 60f);
                }

                var camScript = playerObj.GetComponentInChildren<FirstPersonCamera.FirstPersonCameraScript>();
                if (camScript != null)
                {
                    if (!hasInitializedCamera)
                    {
                        camScript.SetLookRotation(lookRot.eulerAngles.y, pitch);
                        hasInitializedCamera = true;
                    }
                    else
                    {
                        camScript.SetCenterHorizontalAngle(lookRot.eulerAngles.y);
                    }
                }

                playerPositioned = true;
                Debug.Log($"<color=cyan><b>[PuzzleBoard] Đã cố định vị trí Player tại ghế '{seatTarget.name}' (Slot {slotIndex + 1})</b></color>");
            }
        }

        private readonly Dictionary<string, RemotePlayerAvatar> _remoteAvatars = new Dictionary<string, RemotePlayerAvatar>(StringComparer.OrdinalIgnoreCase);

        private void HandleRemotePlayerLook(string username, float yaw, float pitch)
        {
            if (_remoteAvatars.TryGetValue(username, out var avatar) && avatar != null)
            {
                avatar.SetTargetLook(yaw, pitch);
            }
        }

        public void SyncRemotePlayerAvatars()
        {
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance == null) return;
            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            string myName = net.Username;
            var players = net.RoomPlayers;
            if (players == null || players.Count == 0) return;

            AutoDetectSceneEnvironment();

            // Tìm Player GameObject cục bộ để nhân bản mô hình
            GameObject localPlayerObj = null;
            var pc = FindFirstObjectByType<PlayerController.PlayerController>();
            if (pc != null) localPlayerObj = pc.gameObject;
            else localPlayerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");

            if (localPlayerObj == null) return;

            var activeUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (string.Equals(p.Username, myName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!p.Connected) continue;

                activeUsers.Add(p.Username);

                // Ghế ngồi tương ứng của bạn chơi
                Transform seatTarget = null;
                if (chairTransforms != null && chairTransforms.Count > 0)
                {
                    seatTarget = chairTransforms[i % chairTransforms.Count];
                }

                if (!_remoteAvatars.TryGetValue(p.Username, out var avatar) || avatar == null)
                {
                    Vector3 spawnPos = seatTarget != null ? seatTarget.position : localPlayerObj.transform.position;
                    Quaternion spawnRot = seatTarget != null ? seatTarget.rotation : Quaternion.identity;

                    GameObject remoteObj = Instantiate(localPlayerObj, spawnPos, spawnRot);
                    remoteObj.name = "[Avatar_" + p.Username + "]";

                    // Gỡ bỏ Camera và các script điều khiển khỏi Avatar của người chơi khác
                    var cam = remoteObj.GetComponentInChildren<Camera>();
                    if (cam != null) Destroy(cam.gameObject);
                    var controller = remoteObj.GetComponent<PlayerController.PlayerController>();
                    if (controller != null) Destroy(controller);
                    var cc = remoteObj.GetComponent<CharacterController>();
                    if (cc != null) Destroy(cc);

                    // Đảm bảo tất cả các MeshRenderer đều bật hiển thị rõ nét
                    var renderers = remoteObj.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var mr in renderers)
                    {
                        mr.enabled = true;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    }

                    Transform head = remoteObj.transform.Find("Head");
                    avatar = remoteObj.AddComponent<RemotePlayerAvatar>();
                    avatar.Setup(p.Username, head);

                    // Xoay mặt Avatar về phía bàn cờ
                    Vector3 lookCenter = transform.position;
                    var posParent = GameObject.Find("Positions") ?? GameObject.Find("positions");
                    if (posParent != null) lookCenter = posParent.transform.position;
                    else if (tableTransform != null) lookCenter = tableTransform.position;

                    Vector3 horizDir = lookCenter - spawnPos;
                    horizDir.y = 0f;
                    if (horizDir.sqrMagnitude > 0.001f)
                    {
                        float yaw = Quaternion.LookRotation(horizDir.normalized).eulerAngles.y;
                        Vector3 eyePos = spawnPos + Vector3.up * 1.5f;
                        Vector3 toTarget = lookCenter - eyePos;
                        float pitch = Quaternion.LookRotation(toTarget).eulerAngles.x;
                        if (pitch > 180f) pitch -= 360f;
                        avatar.SetTargetLook(yaw, pitch);
                    }

                    _remoteAvatars[p.Username] = avatar;
                    Debug.Log($"<color=green><b>[PuzzleBoard] Đã tạo Avatar 3D cho người chơi '{p.Username}' tại ghế {i + 1}</b></color>");
                }
                else if (seatTarget != null)
                {
                    if (Vector3.Distance(avatar.transform.position, seatTarget.position) > 0.1f)
                    {
                        avatar.transform.position = seatTarget.position;
                    }
                }
            }

            // Xóa Avatar của người chơi đã rời phòng
            var toRemove = new List<string>();
            foreach (var kvp in _remoteAvatars)
            {
                if (!activeUsers.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                }
            }
            foreach (var user in toRemove) _remoteAvatars.Remove(user);
        }

        private void ClearRemotePlayerAvatars()
        {
            foreach (var avatar in _remoteAvatars.Values)
            {
                if (avatar != null) Destroy(avatar.gameObject);
            }
            _remoteAvatars.Clear();
        }

        /// <summary>
        /// Nạp trực tiếp một bộ tranh từ ScriptableObject PuzzleData (đã lưu sẵn Meshes & Materials)
        /// </summary>
        public void LoadPuzzle(PuzzleData puzzleData, bool scatterPieces = true)
        {
            if (puzzleData == null)
            {
                Debug.LogWarning("[PuzzleBoard] Dữ liệu PuzzleData truyền vào bị null!");
                return;
            }

            ClearBoard();
            AutoDetectSceneEnvironment();

            this.currentPuzzleData = puzzleData;
            this.currentBoardWidth = puzzleData.boardWidth;
            this.currentBoardHeight = puzzleData.boardHeight;
            this.currentThickness = puzzleData.thickness;
            this.loadedPuzzleId = puzzleData.puzzleId;

            // Vị trí và rotation thuộc về scene/prefab. Không tự căn lại board.
            CreateGhostGuideBoard(puzzleData);

            // Chuẩn bị Materials
            Material faceMat = puzzleData.puzzleMaterial;
            if (faceMat == null && puzzleData.sourceImage != null)
            {
                faceMat = PuzzleMaterialFactory.CreateFaceMaterial(puzzleData.sourceImage);
            }

            Material sideMat = puzzleData.sideMaterial;
            if (sideMat == null)
            {
                sideMat = PuzzleMaterialFactory.CreateSideBackMaterial();
            }

            Material[] sharedMats = new Material[] { faceMat, sideMat };

            // Nạp từng mảnh ghép đã lưu trong puzzleData.pieces
            foreach (var pData in puzzleData.pieces)
            {
                if (pData.mesh == null) continue;

                GameObject pieceObj = new GameObject($"Piece_{pData.row}_{pData.col}");
                pieceObj.transform.SetParent(transform, false);

                var mf = pieceObj.AddComponent<MeshFilter>();
                mf.sharedMesh = pData.mesh;

                var mr = pieceObj.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sharedMats;

                var col = pieceObj.AddComponent<MeshCollider>();
                col.convex = true;
                col.sharedMesh = pData.mesh;

                var piece = pieceObj.AddComponent<PuzzlePiece>();
                piece.pieceId = pData.pieceId;
                piece.row = pData.row;
                piece.col = pData.col;
                piece.correctLocalPos = pData.correctLocalPosition;
                piece.correctLocalRot = pData.correctLocalRotation;
                if (pData.neighborIds != null && pData.neighborIds.Length == 4)
                {
                    Array.Copy(pData.neighborIds, piece.neighborIds, 4);
                }

                piece.OnPiecePlaced += HandlePiecePlaced;
                activePieces.Add(piece);
            }

            if (!scatterPieces)
            {
                foreach (var p in activePieces) p.SnapToTarget();
            }
            else
            {
                ScatterAllPieces(activePieces);
            }

            placedCount = 0;
            isCompleted = false;
            UpdateProgress();

            Debug.Log($"<color=cyan><b>[PuzzleBoard] Đã nạp thành công bộ tranh: {puzzleData.displayName} ({activePieces.Count} mảnh)</b></color>");
        }

        /// <summary>
        /// Nạp tranh theo puzzleId thông qua PuzzleCatalogManager
        /// </summary>
        public void LoadPuzzle(string puzzleId, bool scatterPieces = true)
        {
            if (PuzzleCatalogManager.Instance == null)
            {
                var existing = FindFirstObjectByType<PuzzleCatalogManager>();
                if (existing == null)
                {
                    var managerObj = new GameObject("[PuzzleCatalogManager]");
                    managerObj.AddComponent<PuzzleCatalogManager>();
                }
            }

            if (PuzzleCatalogManager.Instance != null)
            {
                var data = PuzzleCatalogManager.Instance.GetPuzzleData(puzzleId);
                if (data != null)
                {
                    LoadPuzzle(data, scatterPieces);
                    return;
                }
            }

            Debug.LogWarning($"[PuzzleBoard] Không tìm thấy PuzzleData với ID '{puzzleId}' trong PuzzleCatalogManager!");
        }

        /// <summary>
        /// Khởi tạo toàn bộ bàn cờ và sinh các mảnh ghép 3D trực tiếp (Runtime / Generator).
        /// </summary>
        public void BuildBoard(PuzzleConfiguration cfg, bool scatterPieces = true)
        {
            this.config = cfg;
            ClearBoard();

            if (config == null || config.sourceTexture == null)
            {
                Debug.LogWarning("PuzzleBoard: Chưa cấu hình ảnh nguồn (sourceTexture)!");
                return;
            }

            this.currentBoardWidth = cfg.boardWidth;
            this.currentBoardHeight = cfg.boardHeight;
            this.currentThickness = cfg.thickness;

            // Vị trí và rotation thuộc về scene/prefab. Không tự căn lại board.

            // 1. Sinh cấu trúc hình học 2D
            var piecePolys = PuzzleGridGenerator.GeneratePolygons(config);

            // 2. Chuẩn bị Materials
            Material faceMat = config.frontMaterial;
            if (faceMat == null)
            {
                faceMat = PuzzleMaterialFactory.CreateFaceMaterial(config.sourceTexture);
                config.frontMaterial = faceMat;
            }

            Material sideMat = config.sideBackMaterial;
            if (sideMat == null)
            {
                sideMat = PuzzleMaterialFactory.CreateSideBackMaterial();
                config.sideBackMaterial = sideMat;
            }

            Material[] sharedMats = new Material[] { faceMat, sideMat };

            // Tâm bàn cờ
            float halfW = config.boardWidth * 0.5f;
            float halfH = config.boardHeight * 0.5f;

            // 3. Dựng từng mảnh ghép
            foreach (var poly in piecePolys)
            {
                Mesh pieceMesh = PuzzleMeshBuilder.BuildPieceMesh(poly, config.thickness, config.bevelRadius);
                if (pieceMesh == null) continue;

                GameObject pieceObj = new GameObject($"Piece_{poly.row}_{poly.col}");
                pieceObj.transform.SetParent(transform, false);

                // Tọa độ mục tiêu đúng (tính từ tâm bàn cờ)
                Vector3 targetLocalPos = new Vector3(
                    poly.centerPos.x - halfW,
                    config.thickness * 0.5f,
                    poly.centerPos.y - halfH
                );

                var mf = pieceObj.AddComponent<MeshFilter>();
                mf.sharedMesh = pieceMesh;

                var mr = pieceObj.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sharedMats;

                var col = pieceObj.AddComponent<MeshCollider>();
                col.convex = true;
                col.sharedMesh = pieceMesh;

                var pComponent = pieceObj.AddComponent<PuzzlePiece>();
                pComponent.pieceId = poly.pieceId;
                pComponent.row = poly.row;
                pComponent.col = poly.col;
                pComponent.correctLocalPos = targetLocalPos;
                pComponent.correctLocalRot = Quaternion.identity;
                Array.Copy(poly.neighborIds, pComponent.neighborIds, 4);

                pComponent.OnPiecePlaced += HandlePiecePlaced;
                activePieces.Add(pComponent);
            }

            if (!scatterPieces)
            {
                foreach (var p in activePieces) p.SnapToTarget();
            }
            else
            {
                ScatterAllPieces(activePieces);
            }

            placedCount = 0;
            isCompleted = false;
            UpdateProgress();
        }

        private void ScatterPiece(PuzzlePiece piece)
        {
            if (piece != null)
            {
                ScatterAllPieces(new List<PuzzlePiece> { piece });
            }
        }

        /// <summary>
        /// Bố trí các mảnh ghép rải rác tự nhiên ôm sát quanh 4 cạnh của khung bàn cờ.
        /// Giữ khoảng cách gần tâm (3cm - 20cm tính từ mép khung), tuyệt đối không văng ra mép bàn hay rơi xuống đất.
        /// Nhờ vi phân cao độ Y (0.3mm mỗi mảnh), các mảnh rải rác tự nhiên không bao giờ bị z-fighting kể cả khi xếp gối/chạm nhau.
        /// </summary>
        private void ScatterAllPieces(List<PuzzlePiece> pieces)
        {
            if (pieces == null || pieces.Count == 0) return;

            float bw = currentBoardWidth > 0.01f ? currentBoardWidth : (config != null ? config.boardWidth : 0.8f);
            float bh = currentBoardHeight > 0.01f ? currentBoardHeight : (config != null ? config.boardHeight : 0.6f);
            float th = currentThickness > 0.001f ? currentThickness : (config != null ? config.thickness : 0.005f);

            float halfW = bw * 0.5f;
            float halfH = bh * 0.5f;

            for (int i = 0; i < pieces.Count; i++)
            {
                PuzzlePiece piece = pieces[i];
                if (piece == null || piece.isPlaced) continue;

                Vector3 localPos;

                // Phân bổ rải đều quanh 4 cạnh của khung bàn cờ (Trái, Phải, Trước, Sau):
                // Các cạnh Trái và Phải có diện tích mặt bàn dài hơn nên nhận nhiều mảnh hơn
                int side = i % 4; // 0 = Trái, 1 = Phải, 2 = Trước, 3 = Sau

                if (side == 0) // Bên Trái bàn cờ
                {
                    float x = -halfW - UnityEngine.Random.Range(0.04f, 0.20f);
                    float z = UnityEngine.Random.Range(-halfH * 0.85f, halfH * 0.85f);
                    localPos = new Vector3(x, 0f, z);
                }
                else if (side == 1) // Bên Phải bàn cờ
                {
                    float x = halfW + UnityEngine.Random.Range(0.04f, 0.20f);
                    float z = UnityEngine.Random.Range(-halfH * 0.85f, halfH * 0.85f);
                    localPos = new Vector3(x, 0f, z);
                }
                else if (side == 2) // Phía Trước bàn cờ (hướng về phía người chơi)
                {
                    float x = UnityEngine.Random.Range(-halfW * 0.85f, halfW * 0.85f);
                    float z = -halfH - UnityEngine.Random.Range(0.03f, 0.10f);
                    localPos = new Vector3(x, 0f, z);
                }
                else // Phía Sau bàn cờ (đối diện)
                {
                    float x = UnityEngine.Random.Range(-halfW * 0.85f, halfW * 0.85f);
                    float z = halfH + UnityEngine.Random.Range(0.03f, 0.10f);
                    localPos = new Vector3(x, 0f, z);
                }

                // Chuyển sang World space để kiểm tra giới hạn mép bàn an toàn (nếu có)
                if (hasTableBounds)
                {
                    Vector3 wPos = transform.TransformPoint(localPos);
                    wPos.x = Mathf.Clamp(wPos.x, cachedTableBounds.min.x + 0.06f, cachedTableBounds.max.x - 0.06f);
                    wPos.z = Mathf.Clamp(wPos.z, cachedTableBounds.min.z + 0.06f, cachedTableBounds.max.z - 0.06f);
                    localPos = transform.InverseTransformPoint(wPos);
                }

                // Vi phân cao độ Y: mỗi mảnh lệch nhau 0.3mm -> triệt tiêu 100% z-fighting kể cả khi các mảnh đè/gối lên nhau
                localPos.y = th * 0.5f + (piece.pieceId * 0.0003f);

                piece.transform.localPosition = localPos;
                piece.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f);
            }
        }

        private float DistanceToTableEdge(Vector3 origin, Vector3 dir)
        {
            if (!hasTableBounds) return 1.5f;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return 1.5f;
            dir.Normalize();

            float tMinX = (dir.x > 0) ? (cachedTableBounds.max.x - origin.x) / dir.x : ((dir.x < 0) ? (cachedTableBounds.min.x - origin.x) / dir.x : float.MaxValue);
            float tMinZ = (dir.z > 0) ? (cachedTableBounds.max.z - origin.z) / dir.z : ((dir.z < 0) ? (cachedTableBounds.min.z - origin.z) / dir.z : float.MaxValue);

            float t = Mathf.Min(tMinX, tMinZ);
            return (t > 0f && t < 10f) ? t : 1.5f;
        }

        private void HandlePiecePlaced(PuzzlePiece piece)
        {
            placedCount = 0;
            foreach (var p in activePieces)
            {
                if (p != null && p.isPlaced) placedCount++;
            }
            UpdateProgress();

            if (!isCompleted && activePieces.Count > 0 && placedCount >= activePieces.Count)
            {
                isCompleted = true;
                OnPuzzleCompleted?.Invoke();
                Debug.Log("<color=green><b>★ Chúc mừng! Bạn đã hoàn thành bức tranh Jigsaw Puzzle! ★</b></color>");
            }
        }

        private void UpdateProgress()
        {
            float ratio = activePieces.Count > 0 ? (float)placedCount / activePieces.Count : 0f;
            OnProgressChanged?.Invoke(ratio);
        }

        private GameObject guideBoardObj;

        private void CreateGhostGuideBoard(PuzzleData puzzleData)
        {
            if (guideBoardObj != null)
            {
                if (Application.isPlaying) Destroy(guideBoardObj);
                else DestroyImmediate(guideBoardObj);
                guideBoardObj = null;
            }

            if (puzzleData == null) return;

            float bw = puzzleData.boardWidth > 0.01f ? puzzleData.boardWidth : (config != null ? config.boardWidth : 0.8f);
            float bh = puzzleData.boardHeight > 0.01f ? puzzleData.boardHeight : (config != null ? config.boardHeight : 0.6f);
            float halfW = bw * 0.5f;
            float halfH = bh * 0.5f;

            // Khung viền màu trắng chỉ vị trí hội tụ thành tranh (không hiển thị ảnh làm mờ bên dưới)
            guideBoardObj = new GameObject("[PuzzleGuideBorder]");
            guideBoardObj.transform.SetParent(transform, false);

            // Đặt sát trên mặt bàn (+1mm để tránh z-fighting với mặt bàn)
            guideBoardObj.transform.localPosition = new Vector3(0f, 0.001f, 0f);
            guideBoardObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            guideBoardObj.transform.localScale = Vector3.one;

            var lr = guideBoardObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            lr.startWidth = 0.006f;
            lr.endWidth = 0.006f;

            // Tạo Material Unlit màu trắng tinh khiết
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") 
                                 ?? Shader.Find("Sprites/Default") 
                                 ?? Shader.Find("Unlit/Color");
            Material lineMat = new Material(unlitShader);
            lineMat.color = Color.white;
            lr.material = lineMat;
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.SetPosition(0, new Vector3(-halfW, -halfH, 0f));
            lr.SetPosition(1, new Vector3(halfW, -halfH, 0f));
            lr.SetPosition(2, new Vector3(halfW, halfH, 0f));
            lr.SetPosition(3, new Vector3(-halfW, halfH, 0f));
        }

        public void ClearBoard()
        {
            // Xóa sạch toàn bộ mảnh ghép trong PuzzleBoard và bất kỳ mảnh cũ nào còn sót trong Scene
            var allScenePieces = FindObjectsByType<PuzzlePiece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var piece in allScenePieces)
            {
                if (piece != null)
                {
                    piece.OnPiecePlaced -= HandlePiecePlaced;
                    if (Application.isPlaying) Destroy(piece.gameObject);
                    else DestroyImmediate(piece.gameObject);
                }
            }

            // Dọn dẹp cả GameObject cha rỗng của Prefab cũ nếu có
            string[] cleanupNames = { "forest", "ocean", "sunset", "forest.prefab", "ocean.prefab" };
            foreach (var cName in cleanupNames)
            {
                var leftover = GameObject.Find(cName);
                if (leftover != null && leftover != gameObject && leftover.transform.parent != transform)
                {
                    if (Application.isPlaying) Destroy(leftover);
                    else DestroyImmediate(leftover);
                }
            }

            if (guideBoardObj != null)
            {
                if (Application.isPlaying) Destroy(guideBoardObj);
                else DestroyImmediate(guideBoardObj);
                guideBoardObj = null;
            }

            activePieces.Clear();
            placedCount = 0;
            isCompleted = false;
        }

        // ==========================================
        // CONTEXT MENU (Click chuột phải trên Inspector để test)
        // ==========================================
        [ContextMenu("🧪 Test Load: Sunset 3x3")]
        public void TestLoadSunset() => LoadPuzzle("sunset_3x3");

        [ContextMenu("🧪 Test Load: Forest 4x3")]
        public void TestLoadForest() => LoadPuzzle("forest_4x3");

        [ContextMenu("🧪 Test Load: Ocean 4x4")]
        public void TestLoadOcean() => LoadPuzzle("ocean_4x4");

        [ContextMenu("🧹 Clear Board")]
        public void TestClearBoard() => ClearBoard();

        private void OnDrawGizmos()
        {
            // Vẽ khung viền mặt bàn cờ (màu xanh lá)
            Gizmos.color = Color.green;
            Matrix4x4 oldMat = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            float bw = currentBoardWidth > 0.01f ? currentBoardWidth : 0.8f;
            float bh = currentBoardHeight > 0.01f ? currentBoardHeight : 0.6f;
            float th = currentThickness > 0.001f ? currentThickness : 0.005f;

            Gizmos.DrawWireCube(new Vector3(0f, th * 0.5f, 0f), new Vector3(bw, th, bh));

            // Vẽ các ô vị trí khớp đúng (snap targets) của từng mảnh ghép
            if (activePieces != null && activePieces.Count > 0)
            {
                int cols = config != null && config.columns > 0 ? config.columns : (currentPuzzleData != null ? currentPuzzleData.columns : 3);
                int rows = config != null && config.rows > 0 ? config.rows : (currentPuzzleData != null ? currentPuzzleData.rows : 3);
                Vector3 slotSize = new Vector3(bw / Mathf.Max(1, cols) * 0.85f, 0.001f, bh / Mathf.Max(1, rows) * 0.85f);

                foreach (var piece in activePieces)
                {
                    if (piece == null) continue;
                    Gizmos.color = piece.isPlaced ? Color.cyan : Color.yellow;
                    Gizmos.DrawWireCube(piece.correctLocalPos, slotSize);
                }
            }

            Gizmos.matrix = oldMat;
        }
    }
}
