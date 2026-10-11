using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PuzzleSystem.Core;
using PuzzleOnline.Utility;
using PuzzleOnline.Core;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Điều phối chính của bàn chơi xếp hình (Core Board Orchestrator).
    /// Chịu trách nhiệm quản lý vòng đời ván chơi, tạo mảnh ghép, tiến độ và phân rải mảnh ghép ở tâm bàn.
    /// Các nhiệm vụ phụ được ủy quyền cho:
    /// - PuzzleTableManager: Căn chỉnh mặt bàn, ranh giới và tường vô hình
    /// - PuzzlePlayerSyncManager: Vị trí ngồi và Avatar người chơi từ xa
    /// - PuzzleNetworkSync: Lắng nghe và đồng bộ dữ liệu mảnh ghép từ Server
    /// - PuzzleGhostGuide: Quản lý tranh mẫu mờ (Ghost Target)
    /// </summary>
    [RequireComponent(typeof(PuzzleTableManager))]
    [RequireComponent(typeof(PuzzlePlayerSyncManager))]
    [RequireComponent(typeof(PuzzleNetworkSync))]
    [RequireComponent(typeof(PuzzleGhostGuide))]
    public class PuzzleBoard : MonoBehaviour
    {
        [Header("Configuration")]
        public PuzzleConfiguration config;

        [Header("Materials (Optional Overrides)")]
        public Material defaultFaceMaterial;
        public Material defaultSideMaterial;

        [Header("State")]
        [SerializeField] public List<PuzzlePiece> activePieces = new List<PuzzlePiece>();
        [SerializeField] private int placedCount = 0;
        [SerializeField] private int totalPieces = 0;
        [SerializeField] private bool isCompleted = false;
        private string loadedPuzzleId = "";
        private PuzzleData currentPuzzleData = null;

        public float currentBoardWidth;
        public float currentBoardHeight;
        public float currentThickness;

        // Events
        public event Action<PuzzlePiece> OnPiecePlaced;
        public event Action OnPuzzleCompleted;
        public event Action<int, int> OnProgressChanged;

        // Sub-modules
        public PuzzleTableManager TableManager { get; private set; }
        public PuzzlePlayerSyncManager PlayerSyncManager { get; private set; }
        public PuzzleNetworkSync NetworkSync { get; private set; }
        public PuzzleGhostGuide GhostGuide { get; private set; }

        // Public getters for sub-modules & other scripts
        public List<PuzzlePiece> ActivePieces => activePieces;
        public string LoadedPuzzleId => loadedPuzzleId;
        public int PlacedCount => placedCount;
        public int TotalPieces => totalPieces;
        public bool IsCompleted => isCompleted;
        public PuzzleData CurrentPuzzleData => currentPuzzleData;

        // Inspector compatibility accessors
        public Transform tableTransform
        {
            get => TableManager != null ? TableManager.tableTransform : null;
            set { if (TableManager != null) TableManager.tableTransform = value; }
        }

        public List<Transform> chairTransforms
        {
            get => PlayerSyncManager != null ? PlayerSyncManager.chairTransforms : null;
            set { if (PlayerSyncManager != null) PlayerSyncManager.chairTransforms = value; }
        }

        private void Awake()
        {
            TableManager = GetComponent<PuzzleTableManager>() ?? gameObject.AddComponent<PuzzleTableManager>();
            PlayerSyncManager = GetComponent<PuzzlePlayerSyncManager>() ?? gameObject.AddComponent<PuzzlePlayerSyncManager>();
            NetworkSync = GetComponent<PuzzleNetworkSync>() ?? gameObject.AddComponent<PuzzleNetworkSync>();
            GhostGuide = GetComponent<PuzzleGhostGuide>() ?? gameObject.AddComponent<PuzzleGhostGuide>();

            int pieceLayer = LayerMask.NameToLayer("Piece");
            if (pieceLayer != -1)
            {
                Physics.IgnoreLayerCollision(pieceLayer, pieceLayer, true);
            }
        }

        private void Start()
        {
            TableManager.AutoDetectSceneEnvironment();
            EnsureCameraInteractor();
            PlayerSyncManager.PositionLocalPlayer();

            // Nếu trong scene đã có sẵn các mảnh ghép (kéo Prefab ra hoặc xếp sẵn)
            if (activePieces == null || activePieces.Count == 0)
            {
                var existingPieces = GetComponentsInChildren<PuzzlePiece>();
                if (existingPieces == null || existingPieces.Length == 0)
                {
                    existingPieces = FindObjectsByType<PuzzlePiece>(FindObjectsSortMode.None);
                }
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
                    EnsureGhostGuideBoard();
                    IgnoreCollisionsBetweenPieces();

                    // Khi setup bàn: nếu các mảnh chưa ghép, tự động chất đống rải rác ở chính giữa ghost target
                    var currentNet = ServiceLocator.Get<INetworkService>();
                    if (placedCount == 0 && (currentNet == null || !currentNet.IsGameActive))
                    {
                        ScatterAllPieces(activePieces);
                    }
                }
            }

            // Đồng bộ bộ xếp hình từ Multiplayer Network Manager nếu đang trong ván chơi mạng
            var net = ServiceLocator.Get<INetworkService>();
            if (net != null && net.IsGameActive)
            {
                var ui = ServiceLocator.Get<IUIService>();
                if (ui != null && !ui.IsModalOrChatOpen)
                {
                    CursorManager.Lock();
                }
                PlayerSyncManager.PositionLocalPlayer(true);
                PlayerSyncManager.SyncRemotePlayerAvatars();

                string netPuzzleId = net.CurrentPuzzleId;
                if (!string.IsNullOrEmpty(netPuzzleId))
                {
                    if (activePieces == null || activePieces.Count == 0 || loadedPuzzleId != netPuzzleId)
                    {
                        LoadPuzzle(netPuzzleId, true);
                    }
                    NetworkSync.SyncPiecesFromServer();
                }
            }

            StartCoroutine(PositionLocalPlayerAfterSceneSetup());
        }

        private IEnumerator PositionLocalPlayerAfterSceneSetup()
        {
            yield return null;
            PlayerSyncManager.PositionLocalPlayer(true);
            yield return new WaitForEndOfFrame();
            PlayerSyncManager.PositionLocalPlayer(true);
        }

        private void OnDisable()
        {
            PlayerSyncManager?.ClearRemotePlayerAvatars();
            loadedPuzzleId = "";
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

        /// <summary>
        /// Nạp bộ Puzzle từ PuzzleData asset.
        /// </summary>
        public void LoadPuzzle(PuzzleData puzzleData, bool scatterPieces = true)
        {
            if (puzzleData == null)
            {
                Debug.LogError("[PuzzleBoard] PuzzleData truyền vào là null!");
                return;
            }

            ClearBoard();

            currentPuzzleData = puzzleData;
            loadedPuzzleId = puzzleData.puzzleId;
            currentBoardWidth = puzzleData.boardWidth;
            currentBoardHeight = puzzleData.boardHeight;
            currentThickness = puzzleData.thickness;

            GhostGuide.CreateGhostGuideBoard(puzzleData, config, activePieces);

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

            foreach (var pData in puzzleData.pieces)
            {
                if (pData.mesh == null) continue;

                GameObject pieceObj = new GameObject($"Piece_{pData.row}_{pData.col}");
                pieceObj.transform.SetParent(transform, false);

                int pieceLayer = LayerMask.NameToLayer("Piece");
                if (pieceLayer != -1) pieceObj.layer = pieceLayer;

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
                piece.correctLocalRot = Quaternion.identity;
                Array.Copy(pData.neighborIds, piece.neighborIds, 4);

                piece.OnPiecePlaced += HandlePiecePlaced;
                activePieces.Add(piece);
            }

            IgnoreCollisionsBetweenPieces();

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

        /// <summary>
        /// Nạp puzzle bằng mã ID đã đăng ký trong PuzzleCatalogManager hoặc Resources.
        /// </summary>
        public void LoadPuzzle(string puzzleId, bool scatterPieces = true)
        {
            if (string.IsNullOrEmpty(puzzleId)) return;

            PuzzleData data = null;
            var catalog = ServiceLocator.Get<IPuzzleCatalog>();
            if (catalog != null)
            {
                data = catalog.GetPuzzleData(puzzleId);
            }

            if (data == null)
            {
                data = Resources.Load<PuzzleData>($"Puzzles/{puzzleId}") ?? Resources.Load<PuzzleData>(puzzleId);
            }

            if (data != null)
            {
                LoadPuzzle(data, scatterPieces);
            }
            else
            {
                Debug.LogWarning($"[PuzzleBoard] Không tìm thấy PuzzleData với ID '{puzzleId}'!");
            }
        }

        /// <summary>
        /// Xây dựng bàn cờ thủ công dựa trên PuzzleConfiguration.
        /// </summary>
        public void BuildBoard(PuzzleConfiguration cfg, bool scatterPieces = true)
        {
            if (cfg == null || cfg.sourceTexture == null) return;
            ClearBoard();

            config = cfg;
            currentBoardWidth = cfg.boardWidth;
            currentBoardHeight = cfg.boardHeight;
            currentThickness = cfg.thickness;

            GhostGuide.CreateGhostGuideBoard(null, cfg, activePieces);

            var piecePolys = PuzzleGridGenerator.GeneratePolygons(config);
            Material faceMat = config.frontMaterial;
            if (faceMat == null)
            {
                faceMat = defaultFaceMaterial != null ? defaultFaceMaterial : PuzzleMaterialFactory.CreateFaceMaterial(config.sourceTexture);
                config.frontMaterial = faceMat;
            }

            Material sideMat = config.sideBackMaterial;
            if (sideMat == null)
            {
                sideMat = defaultSideMaterial != null ? defaultSideMaterial : PuzzleMaterialFactory.CreateSideBackMaterial();
                config.sideBackMaterial = sideMat;
            }

            Material[] sharedMats = new Material[] { faceMat, sideMat };

            float halfW = config.boardWidth * 0.5f;
            float halfH = config.boardHeight * 0.5f;

            foreach (var poly in piecePolys)
            {
                Mesh pieceMesh = PuzzleMeshBuilder.BuildPieceMesh(poly, config.thickness, config.bevelRadius);
                if (pieceMesh == null) continue;

                GameObject pieceObj = new GameObject($"Piece_{poly.row}_{poly.col}");
                pieceObj.transform.SetParent(transform, false);

                int pieceLayer = LayerMask.NameToLayer("Piece");
                if (pieceLayer != -1) pieceObj.layer = pieceLayer;

                Vector3 targetLocalPos = new Vector3(poly.centerPos.x - halfW, cfg.thickness * 0.5f, poly.centerPos.y - halfH);

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

            IgnoreCollisionsBetweenPieces();

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

        public void ScatterPiece(PuzzlePiece piece)
        {
            if (piece != null)
            {
                ScatterAllPieces(new List<PuzzlePiece> { piece });
            }
        }

        /// <summary>
        /// Bố trí các mảnh ghép chất đống rải rác ở chính giữa bàn (khung ghost target).
        /// </summary>
        public void ScatterAllPieces(List<PuzzlePiece> pieces)
        {
            if (pieces == null || pieces.Count == 0) return;

            float bw = currentBoardWidth > 0.01f ? currentBoardWidth : (config != null ? config.boardWidth : 0.8f);
            float bh = currentBoardHeight > 0.01f ? currentBoardHeight : (config != null ? config.boardHeight : 0.6f);
            float th = currentThickness > 0.001f ? currentThickness : (config != null ? config.thickness : 0.005f);

            float halfW = bw * 0.5f;
            float halfH = bh * 0.5f;

            float pileRadiusX = halfW * 0.35f;
            float pileRadiusZ = halfH * 0.35f;

            int[] shuffled = new int[pieces.Count];
            for (int i = 0; i < pieces.Count; i++) shuffled[i] = i;
            for (int i = 0; i < pieces.Count; i++)
            {
                int r = UnityEngine.Random.Range(i, pieces.Count);
                int tmp = shuffled[i];
                shuffled[i] = shuffled[r];
                shuffled[r] = tmp;
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                PuzzlePiece piece = pieces[i];
                if (piece == null || piece.isPlaced) continue;

                int rank = Array.IndexOf(shuffled, i);
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = Mathf.Sqrt(UnityEngine.Random.Range(0f, 1f));
                float x = Mathf.Cos(angle) * pileRadiusX * dist;
                float z = Mathf.Sin(angle) * pileRadiusZ * dist;

                Vector3 localPos = new Vector3(x, 0f, z);

                if (TableManager != null && TableManager.HasTableBounds)
                {
                    Vector3 wPos = transform.TransformPoint(localPos);
                    wPos.x = Mathf.Clamp(wPos.x, TableManager.CachedTableBounds.min.x + 0.06f, TableManager.CachedTableBounds.max.x - 0.06f);
                    wPos.z = Mathf.Clamp(wPos.z, TableManager.CachedTableBounds.min.z + 0.06f, TableManager.CachedTableBounds.max.z - 0.06f);
                    localPos = transform.InverseTransformPoint(wPos);
                }

                float moundHeight = (1f - dist) * 0.003f;
                localPos.y = th * 0.5f + (rank * 0.0005f) + moundHeight;

                piece.transform.localPosition = localPos;
                piece.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f);
            }
        }

        public void IgnoreCollisionsBetweenPieces()
        {
            int pieceLayer = LayerMask.NameToLayer("Piece");
            if (pieceLayer != -1)
            {
                Physics.IgnoreLayerCollision(pieceLayer, pieceLayer, true);
            }

            var pieces = GetComponentsInChildren<PuzzlePiece>(true);
            var colliders = new List<Collider>();
            foreach (var p in pieces)
            {
                if (p != null)
                {
                    if (pieceLayer != -1) p.gameObject.layer = pieceLayer;
                    var c = p.GetComponent<Collider>();
                    if (c != null) colliders.Add(c);
                }
            }

            for (int i = 0; i < colliders.Count; i++)
            {
                for (int j = i + 1; j < colliders.Count; j++)
                {
                    if (colliders[i] != null && colliders[j] != null)
                    {
                        Physics.IgnoreCollision(colliders[i], colliders[j], true);
                    }
                }
            }
        }

        private void HandlePiecePlaced(PuzzlePiece piece)
        {
            RecalculateProgress();

            if (!isCompleted && activePieces.Count > 0 && placedCount >= activePieces.Count)
            {
                CompleteGame();
            }
        }

        public void RecalculateProgress()
        {
            placedCount = 0;
            foreach (var p in activePieces)
            {
                if (p != null && p.isPlaced) placedCount++;
            }
            UpdateProgress();
        }

        private void UpdateProgress()
        {
            totalPieces = activePieces != null ? activePieces.Count : 0;
            OnProgressChanged?.Invoke(placedCount, totalPieces);
        }

        public void CompleteGame()
        {
            isCompleted = true;
            ServiceLocator.Get<IAudioService>()?.PlayComplete();
            OnPuzzleCompleted?.Invoke();
        }

        public void ClearBoard()
        {
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

            GhostGuide?.ClearGhostGuide();
            activePieces.Clear();
            placedCount = 0;
            isCompleted = false;
        }

        // =========================================================================
        // BACKWARD COMPATIBILITY FORWARDING METHODS
        // Đảm bảo tất cả các script khác gọi board.PositionLocalPlayer(), board.AlignToTable(), ...
        // vẫn hoạt động bình thường 100%.
        // =========================================================================
        public void PositionLocalPlayer(bool force = false) => PlayerSyncManager?.PositionLocalPlayer(force);
        public void SyncRemotePlayerAvatars() => PlayerSyncManager?.SyncRemotePlayerAvatars();
        public void ClearRemotePlayerAvatars() => PlayerSyncManager?.ClearRemotePlayerAvatars();
        public void AlignToTable() => TableManager?.AlignToTable();
        public void SnapBoardToPieces() => TableManager?.SnapBoardToPieces(activePieces);
        public void SetupTableEdgeColliders() => TableManager?.SetupTableEdgeColliders();
        public Vector3 ClampToTableBounds(Vector3 worldPos, float margin = 0.05f) => TableManager != null ? TableManager.ClampToTableBounds(worldPos, margin) : worldPos;
        public void AutoDetectSceneEnvironment() => TableManager?.AutoDetectSceneEnvironment();
        public void SyncPiecesFromServer() => NetworkSync?.SyncPiecesFromServer();
        public void EnsureGhostGuideBoard() => GhostGuide?.EnsureGhostGuideBoard(currentPuzzleData, config, activePieces);

        // Debug context helpers
        public void TestLoadSunset() => LoadPuzzle("sunset_3x3");
        public void TestLoadForest() => LoadPuzzle("forest_4x3");
        public void TestLoadOcean() => LoadPuzzle("ocean_4x4");
        public void TestClearBoard() => ClearBoard();
    }
}
