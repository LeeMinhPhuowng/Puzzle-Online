using System;
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

        private void Start()
        {
            // Đảm bảo PuzzleBoard luôn ở cấp Root và có scale chuẩn (1,1,1)
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
                transform.localScale = Vector3.one;
            }

            AutoDetectSceneEnvironment();
            EnsureCameraInteractor();

            // Tự động nắn thẳng góc xoay nằm ngang phẳng nếu bàn cờ đang bị nghiêng (do kế thừa góc nghiêng -90 độ trục X của mô hình bàn)
            if (Vector3.Angle(transform.up, Vector3.up) > 0.5f)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }

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

            transform.position = centerPos;

            // 2. Góc xoay: Giữ vector Up hướng thẳng đứng lên trời, chỉ nhận góc quay ngang (Yaw) quanh trục Y
            Vector3 fwd = tableTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }

        private void EnsureCameraInteractor()
        {
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null && cam.GetComponent<PuzzleRaycastInteractor>() == null)
            {
                cam.gameObject.AddComponent<PuzzleRaycastInteractor>();
            }
        }

        private void Update()
        {
            if (!enableQuickTestKeys) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) LoadPuzzle("sunset_3x3");
            else if (Input.GetKeyDown(KeyCode.Alpha2)) LoadPuzzle("forest_4x3");
            else if (Input.GetKeyDown(KeyCode.Alpha3)) LoadPuzzle("ocean_4x4");
        }

        public void AutoDetectSceneEnvironment()
        {
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
            }

            if (chairTransforms == null || chairTransforms.Count == 0)
            {
                chairTransforms = new List<Transform>();
                string[] chairNames = { "A", "A (1)", "A (2)", "A (3)", "Position (1)", "Position (2)", "Position (3)", "Position (4)" };
                foreach (var name in chairNames)
                {
                    var foundChair = GameObject.Find(name);
                    if (foundChair != null) chairTransforms.Add(foundChair.transform);
                }
            }
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

            // Căn chỉnh bàn cờ nằm phẳng trên mặt bàn
            AlignToTable();

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

                if (!scatterPieces)
                {
                    piece.SnapToTarget();
                }
                else
                {
                    ScatterPiece(piece);
                }
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

            // Căn chỉnh bàn cờ nằm phẳng trên mặt bàn
            AlignToTable();

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

                if (!scatterPieces)
                {
                    pComponent.SnapToTarget();
                }
                else
                {
                    ScatterPiece(pComponent);
                }
            }

            placedCount = 0;
            isCompleted = false;
            UpdateProgress();
        }

        private void ScatterPiece(PuzzlePiece piece)
        {
            Vector3 scatterPos;
            Quaternion scatterRot = Quaternion.Euler(0f, UnityEngine.Random.Range(0, 4) * 90f, 0f);

            float bw = currentBoardWidth > 0.01f ? currentBoardWidth : (config != null ? config.boardWidth : 0.8f);
            float bh = currentBoardHeight > 0.01f ? currentBoardHeight : (config != null ? config.boardHeight : 0.6f);
            float th = currentThickness > 0.001f ? currentThickness : (config != null ? config.thickness : 0.005f);

            float tableRadius = Mathf.Max(bw, bh) * 0.8f;

            if (chairTransforms != null && chairTransforms.Count > 0)
            {
                int chairIdx = piece.pieceId % chairTransforms.Count;
                Transform targetChair = chairTransforms[chairIdx];

                Vector3 toChair = (targetChair.position - transform.position).normalized;
                float dist = UnityEngine.Random.Range(tableRadius * 0.5f, tableRadius * 1.1f);
                Vector3 worldScatter = transform.position + toChair * dist + new Vector3(
                    UnityEngine.Random.Range(-0.15f, 0.15f),
                    th * 0.5f,
                    UnityEngine.Random.Range(-0.15f, 0.15f)
                );
                scatterPos = transform.InverseTransformPoint(worldScatter);
            }
            else
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float dist = UnityEngine.Random.Range(tableRadius * 0.6f, tableRadius * 1.2f);
                scatterPos = new Vector3(
                    Mathf.Cos(angle) * dist,
                    th * 0.5f,
                    Mathf.Sin(angle) * dist
                );
            }

            piece.transform.localPosition = new Vector3(scatterPos.x, th * 0.5f, scatterPos.z);
            piece.transform.localRotation = scatterRot;
        }

        private void HandlePiecePlaced(PuzzlePiece piece)
        {
            placedCount++;
            UpdateProgress();

            if (placedCount >= activePieces.Count)
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

        public void ClearBoard()
        {
            foreach (var piece in activePieces)
            {
                if (piece != null)
                {
                    piece.OnPiecePlaced -= HandlePiecePlaced;
                    DestroyImmediate(piece.gameObject);
                }
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
