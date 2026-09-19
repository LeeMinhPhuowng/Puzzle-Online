using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PuzzleSystem.Core;
using PuzzleSystem.Gameplay;
using PuzzleSystem.Image;

namespace PuzzleSystem.Editor
{
    public class PuzzleGeneratorWindow : EditorWindow
    {
        [MenuItem("Tools/Puzzle Online/Jigsaw Puzzle Generator", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<PuzzleGeneratorWindow>("Jigsaw Puzzle Generator");
            window.minSize = new Vector2(500, 650);
            window.Show();
        }

        [MenuItem("Tools/Puzzle Online/➕ Create PuzzleBoard in Scene", false, 20)]
        public static void CreatePuzzleBoardInScene()
        {
            var existingBoard = Object.FindFirstObjectByType<PuzzleBoard>();
            if (existingBoard != null)
            {
                Selection.activeGameObject = existingBoard.gameObject;
                EditorGUIUtility.PingObject(existingBoard.gameObject);
                Debug.Log("<color=yellow>[Puzzle Online] PuzzleBoard đã tồn tại trong Scene!</color>");
                return;
            }

            GameObject boardObj = new GameObject("PuzzleBoard");
            var board = boardObj.AddComponent<PuzzleBoard>();
            board.AutoDetectSceneEnvironment();

            if (Object.FindFirstObjectByType<PuzzleCatalogManager>() == null)
            {
                GameObject catObj = new GameObject("[PuzzleCatalogManager]");
                catObj.AddComponent<PuzzleCatalogManager>();
                Undo.RegisterCreatedObjectUndo(catObj, "Create PuzzleCatalogManager");
            }

            Undo.RegisterCreatedObjectUndo(boardObj, "Create PuzzleBoard");
            Selection.activeGameObject = boardObj;
            Debug.Log("<color=cyan><b>[Puzzle Online] Đã tạo thành công PuzzleBoard và tự động kết nối mặt bàn trong Scene!</b></color>");
        }

        private PuzzleConfiguration config = new PuzzleConfiguration();
        private Vector2 scrollPos;
        private bool show2DPreview = true;
        private Vector3[] cachedPreviewSegments = null;
        private int lastPreviewHash = -1;
        private Rect lastPreviewRect;
        private bool autoLivePreview = true;

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            DrawHeader();
            EditorGUILayout.Space(8);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawImageSettings();
            EditorGUILayout.Space(6);

            DrawGridSettings();
            EditorGUILayout.Space(6);

            DrawTabHoleSettings();
            EditorGUILayout.Space(6);

            DrawPhysicalDimensions();
            EditorGUILayout.Space(6);

            DrawMaterialsSection();
            EditorGUILayout.Space(10);

            Draw2DPreviewSection();
            EditorGUILayout.Space(12);

            DrawActionButtons();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.7f, 1f) }
            };
            GUILayout.Label("🧩 Procedural Jigsaw Puzzle Generator", titleStyle);
            EditorGUILayout.LabelField("Biến bất kỳ ảnh 2D thành bộ Puzzle 3D procedural khớp viền hoàn hảo.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawImageSettings()
        {
            EditorGUILayout.LabelField("1. Hình Ảnh Đầu Vào (Source Image)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            config.sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Ảnh Tranh", config.sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (config.autoRandomizeSeed)
                {
                    config.seed = UnityEngine.Random.Range(10000, 999999);
                }
                // Reset materials khi đổi ảnh để tránh dùng nhầm material của bộ puzzle trước
                config.frontMaterial = null;
                config.sideBackMaterial = null;
                InvalidatePreview();
            }

            if (config.sourceTexture != null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("✂️ Auto-Crop theo tỉ lệ Lưới", EditorStyles.miniButton))
                {
                    float targetAspect = (float)config.columns / config.rows;
                    Texture2D cropped = ImageEditorUtility.AutoFitAspect(config.sourceTexture, targetAspect);
                    if (cropped != null)
                    {
                        // Lưu ngay ảnh đã crop ra đĩa để Unity quản lý Asset GUID vĩnh viễn
                        string origPath = AssetDatabase.GetAssetPath(config.sourceTexture);
                        string folder = "Assets/PuzzleImages";
                        if (!string.IsNullOrEmpty(origPath))
                        {
                            folder = Path.GetDirectoryName(origPath).Replace("\\", "/");
                        }
                        string croppedPath = $"{folder}/{config.sourceTexture.name}_Cropped.png";
                        byte[] bytes = cropped.EncodeToPNG();
                        File.WriteAllBytes(croppedPath, bytes);
                        AssetDatabase.ImportAsset(croppedPath, ImportAssetOptions.ForceUpdate);

                        TextureImporter importer = AssetImporter.GetAtPath(croppedPath) as TextureImporter;
                        if (importer != null)
                        {
                            importer.isReadable = true;
                            importer.textureCompression = TextureImporterCompression.Uncompressed;
                            importer.SaveAndReimport();
                        }

                        config.sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(croppedPath);
                        config.frontMaterial = null;
                        config.sideBackMaterial = null;
                        InvalidatePreview();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGridSettings()
        {
            EditorGUILayout.LabelField("2. Cấu Hình Lưới Mảnh Ghép", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Nút bấm nhanh các kích thước thông dụng
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Kích thước nhanh:");
            if (GUILayout.Button("2x2", EditorStyles.miniButtonLeft)) SetDimensions(2, 2);
            if (GUILayout.Button("3x3", EditorStyles.miniButtonMid)) SetDimensions(3, 3);
            if (GUILayout.Button("4x4", EditorStyles.miniButtonMid)) SetDimensions(4, 4);
            if (GUILayout.Button("6x4", EditorStyles.miniButtonMid)) SetDimensions(4, 6);
            if (GUILayout.Button("8x8", EditorStyles.miniButtonRight)) SetDimensions(8, 8);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            config.rows = EditorGUILayout.IntSlider("Số Hàng (Rows)", config.rows, 2, 50);
            config.columns = EditorGUILayout.IntSlider("Số Cột (Columns)", config.columns, 2, 50);

            EditorGUILayout.HelpBox($"Tổng số mảnh ghép: {config.TotalPieces} mảnh ({config.rows} x {config.columns})", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            config.seed = EditorGUILayout.IntField("Random Seed", config.seed);
            if (GUILayout.Button("🎲 Đổi Seed", GUILayout.Width(90)))
            {
                config.seed = UnityEngine.Random.Range(10000, 999999);
                InvalidatePreview();
            }
            EditorGUILayout.EndHorizontal();

            config.autoRandomizeSeed = EditorGUILayout.ToggleLeft("🎲 Tự động đổi vị trí & hình dạng khớp nối khi đổi ảnh", config.autoRandomizeSeed);

            if (EditorGUI.EndChangeCheck())
            {
                InvalidatePreview();
            }

            EditorGUILayout.EndVertical();
        }

        private void SetDimensions(int r, int c)
        {
            config.rows = r;
            config.columns = c;
            InvalidatePreview();
        }

        private void DrawTabHoleSettings()
        {
            EditorGUILayout.LabelField("3. Hình Dạng Khớp Nối (Tab / Hole)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginChangeCheck();
            config.tabHeightRatio = EditorGUILayout.Slider("Độ nhô của Tab (Height)", config.tabHeightRatio, 0.12f, 0.35f);
            config.neckWidthRatio = EditorGUILayout.Slider("Độ rộng cổ Tab (Neck)", config.neckWidthRatio, 0.1f, 0.28f);
            config.headWidthRatio = EditorGUILayout.Slider("Độ rộng đầu Tab (Head)", config.headWidthRatio, 0.2f, 0.45f);
            config.samplesPerSegment = EditorGUILayout.IntSlider("Độ mịn đường cong", config.samplesPerSegment, 3, 8);

            if (EditorGUI.EndChangeCheck())
            {
                InvalidatePreview();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPhysicalDimensions()
        {
            EditorGUILayout.LabelField("4. Kích Thước Vật Lý 3D Trên Bàn (Mét)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Nút chọn nhanh kích thước vật lý
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Preset kích thước:");
            if (GUILayout.Button("Bàn (0.8x0.6m)", EditorStyles.miniButtonLeft)) { config.boardWidth = 0.8f; config.boardHeight = 0.6f; }
            if (GUILayout.Button("Lớn (2x1.5m)", EditorStyles.miniButtonMid)) { config.boardWidth = 2f; config.boardHeight = 1.5f; }
            if (GUILayout.Button("Cực đại (8x8m)", EditorStyles.miniButtonRight)) { config.boardWidth = 8f; config.boardHeight = 8f; }
            EditorGUILayout.EndHorizontal();

            config.boardWidth = EditorGUILayout.Slider("Chiều rộng bàn cờ (m)", config.boardWidth, 0.2f, 8f);
            config.boardHeight = EditorGUILayout.Slider("Chiều dài bàn cờ (m)", config.boardHeight, 0.2f, 8f);
            config.thickness = EditorGUILayout.Slider("Độ dày mảnh (m)", config.thickness, 0.002f, 0.03f);
            config.bevelRadius = EditorGUILayout.Slider("Độ bo viền (Bevel)", config.bevelRadius, 0.0002f, 0.003f);

            EditorGUILayout.EndVertical();
        }

        private void DrawMaterialsSection()
        {
            EditorGUILayout.LabelField("5. Chất Liệu (Materials)", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            config.frontMaterial = (Material)EditorGUILayout.ObjectField("Material Mặt Tranh", config.frontMaterial, typeof(Material), false);
            config.sideBackMaterial = (Material)EditorGUILayout.ObjectField("Material Cạnh & Lưng", config.sideBackMaterial, typeof(Material), false);

            if (GUILayout.Button("🎨 Tự Động Tạo & Gán Material Chuẩn URP", EditorStyles.miniButton))
            {
                string matFolder = "Assets/Scripts/Puzzle/Materials";
                string prefix = config.sourceTexture != null ? config.sourceTexture.name : "Puzzle";
                var (faceMat, sideMat) = PuzzleMaterialFactory.CreateAndSaveMaterialAssets(config.sourceTexture, matFolder, prefix);
                config.frontMaterial = faceMat;
                config.sideBackMaterial = sideMat;
                if (faceMat.HasProperty("_BaseMap") && faceMat.GetTexture("_BaseMap") is Texture2D loadedTex)
                {
                    config.sourceTexture = loadedTex;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void Draw2DPreviewSection()
        {
            show2DPreview = EditorGUILayout.Foldout(show2DPreview, "👁️ Xem Trước 2D (Cutline Preview)", true, EditorStyles.foldoutHeader);
            if (!show2DPreview) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float previewWidth = Mathf.Min(position.width - 35, 320f);
            float aspect = (float)config.boardWidth / Mathf.Max(0.01f, config.boardHeight);
            float previewHeight = previewWidth / aspect;

            Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

            // Vẽ ảnh nền
            if (config.sourceTexture != null)
            {
                GUI.DrawTexture(previewRect, config.sourceTexture, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.18f));
                GUIStyle msgStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(previewRect, "Chưa chọn hình ảnh", msgStyle);
            }

            // Với lưới rất lớn (> 400 mảnh), cho phép tùy chọn bật/tắt vẽ live để tuyệt đối không lag
            if (config.TotalPieces > 400)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"⚡ Lưới lớn ({config.TotalPieces} mảnh):", EditorStyles.miniLabel);
                autoLivePreview = EditorGUILayout.ToggleLeft("Vẽ Live", autoLivePreview, GUILayout.Width(75));
                if (GUILayout.Button("🔄 Cập Nhật Preview", EditorStyles.miniButton))
                {
                    UpdatePreviewSegments(previewRect, force: true);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (autoLivePreview || config.TotalPieces <= 400)
            {
                UpdatePreviewSegments(previewRect, force: false);
            }

            // Vẽ toàn bộ đường cắt trong 1 lệnh GPU DrawLines duy nhất (chỉ chạy trong sự kiện Repaint)
            if (Event.current.type == EventType.Repaint && cachedPreviewSegments != null && cachedPreviewSegments.Length > 0)
            {
                Handles.color = new Color(1f, 1f, 1f, 0.8f);
                Handles.DrawLines(cachedPreviewSegments);
            }

            EditorGUILayout.EndVertical();
        }

        private void InvalidatePreview()
        {
            cachedPreviewSegments = null;
            lastPreviewHash = -1;
        }

        private void UpdatePreviewSegments(Rect previewRect, bool force)
        {
            int currentHash = config.rows ^ (config.columns << 6) ^ (config.seed << 12) ^
                              (int)(config.tabHeightRatio * 1000) ^ (int)(config.neckWidthRatio * 1000) ^
                              (int)(config.headWidthRatio * 1000);

            if (!force && cachedPreviewSegments != null && lastPreviewHash == currentHash && lastPreviewRect == previewRect)
            {
                return;
            }

            lastPreviewHash = currentHash;
            lastPreviewRect = previewRect;

            int rows = config.rows;
            int cols = config.columns;
            float bw = config.boardWidth;
            float bh = config.boardHeight;
            float cellW = bw / cols;
            float cellH = bh / rows;

            // Lưới càng nhiều mảnh thì giảm số mẫu spline của preview để đảm bảo 120 FPS
            int samples = config.TotalPieces > 200 ? 2 : Mathf.Clamp(config.samplesPerSegment, 2, 4);
            var rand = new System.Random(config.seed);

            List<Vector3> lines = new List<Vector3>();

            // 1. Cạnh ngang bên trong
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int dir = rand.Next(0, 2) == 0 ? 1 : -1;
                    float offset = (float)(rand.NextDouble() * 0.16 - 0.08);
                    float tabH = config.tabHeightRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));
                    float headW = config.headWidthRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));

                    float y = (rows - 1 - r) * cellH;
                    Vector2 start = new Vector2(c * cellW, y);
                    Vector2 end = new Vector2((c + 1) * cellW, y);

                    var pts = BezierHelper.GenerateJigsawEdgePoints(start, end, dir, tabH, config.neckWidthRatio, headW, offset, samples);
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        lines.Add(ToScreen(pts[i], previewRect, bw, bh));
                        lines.Add(ToScreen(pts[i + 1], previewRect, bw, bh));
                    }
                }
            }

            // 2. Cạnh dọc bên trong
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int dir = rand.Next(0, 2) == 0 ? 1 : -1;
                    float offset = (float)(rand.NextDouble() * 0.16 - 0.08);
                    float tabH = config.tabHeightRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));
                    float headW = config.headWidthRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));

                    float x = (c + 1) * cellW;
                    Vector2 start = new Vector2(x, (rows - 1 - r) * cellH);
                    Vector2 end = new Vector2(x, (rows - r) * cellH);

                    var pts = BezierHelper.GenerateJigsawEdgePoints(start, end, dir, tabH, config.neckWidthRatio, headW, offset, samples);
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        lines.Add(ToScreen(pts[i], previewRect, bw, bh));
                        lines.Add(ToScreen(pts[i + 1], previewRect, bw, bh));
                    }
                }
            }

            // 3. Khung viền ngoài
            Vector3 tl = ToScreen(new Vector2(0, bh), previewRect, bw, bh);
            Vector3 tr = ToScreen(new Vector2(bw, bh), previewRect, bw, bh);
            Vector3 br = ToScreen(new Vector2(bw, 0), previewRect, bw, bh);
            Vector3 bl = ToScreen(new Vector2(0, 0), previewRect, bw, bh);

            lines.Add(tl); lines.Add(tr);
            lines.Add(tr); lines.Add(br);
            lines.Add(br); lines.Add(bl);
            lines.Add(bl); lines.Add(tl);

            cachedPreviewSegments = lines.ToArray();
        }

        private static Vector3 ToScreen(Vector2 p, Rect rect, float bw, float bh)
        {
            return new Vector3(
                rect.x + (p.x / bw) * rect.width,
                rect.y + (1f - p.y / bh) * rect.height,
                0f
            );
        }

        private void DrawActionButtons()
        {
            GUI.enabled = config.sourceTexture != null;

            GUIStyle bigBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            // 1. Sinh trực tiếp trong Scene
            if (GUILayout.Button("🚀 Sinh Thử Nghiệm Trong Scene (Spawn On Table)", bigBtn))
            {
                SpawnPuzzleInScene();
            }

            EditorGUILayout.Space(4);

            // 2. Export ra Prefab & Meshes
            if (GUILayout.Button("💾 Xuất Thành Prefab, Meshes & PuzzleData Asset", GUILayout.Height(30)))
            {
                ExportAssetsAndPrefab();
            }

            GUI.enabled = true;
        }

        private void SpawnPuzzleInScene()
        {
            // Đảm bảo Materials tồn tại dưới dạng asset để không bị rỗng trong scene
            if (config.frontMaterial == null || config.sideBackMaterial == null)
            {
                string matFolder = "Assets/Scripts/Puzzle/Materials";
                string prefix = config.sourceTexture != null ? config.sourceTexture.name : "Puzzle";
                var (faceMat, sideMat) = PuzzleMaterialFactory.CreateAndSaveMaterialAssets(config.sourceTexture, matFolder, prefix);
                if (config.frontMaterial == null) config.frontMaterial = faceMat;
                if (config.sideBackMaterial == null) config.sideBackMaterial = sideMat;
                if (faceMat.HasProperty("_BaseMap") && faceMat.GetTexture("_BaseMap") is Texture2D loadedTex)
                {
                    config.sourceTexture = loadedTex;
                }
            }

            PuzzleBoard board = FindFirstObjectByType<PuzzleBoard>();
            if (board == null)
            {
                GameObject boardObj = new GameObject("PuzzleBoard");
                board = boardObj.AddComponent<PuzzleBoard>();
            }

            board.AutoDetectSceneEnvironment();
            board.BuildBoard(config, scatterPieces: true);

            // Gắn Interactor cho Camera nếu chưa có
            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null && cam.GetComponent<PuzzleRaycastInteractor>() == null)
            {
                cam.gameObject.AddComponent<PuzzleRaycastInteractor>();
            }

            Selection.activeGameObject = board.gameObject;
            Undo.RegisterCreatedObjectUndo(board.gameObject, "Spawn Puzzle Board");
            Debug.Log($"<color=cyan><b>Đã sinh thành công bộ Puzzle {config.TotalPieces} mảnh trên bàn cờ trong Scene!</b></color>");
        }

        private void ExportAssetsAndPrefab()
        {
            string puzzleName = config.sourceTexture != null ? config.sourceTexture.name : "CustomPuzzle";
            string baseFolder = $"Assets/GeneratedPuzzles/{puzzleName}";
            string meshFolder = $"{baseFolder}/Meshes";
            string matFolder = $"{baseFolder}/Materials";

            if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);
            if (!Directory.Exists(meshFolder)) Directory.CreateDirectory(meshFolder);
            if (!Directory.Exists(matFolder)) Directory.CreateDirectory(matFolder);

            AssetDatabase.StartAssetEditing();

            // 1. Tạo và lưu vĩnh viễn Material Assets
            var (faceMat, sideMat) = PuzzleMaterialFactory.CreateAndSaveMaterialAssets(config.sourceTexture, matFolder, puzzleName);
            config.frontMaterial = faceMat;
            config.sideBackMaterial = sideMat;
            if (faceMat.HasProperty("_BaseMap") && faceMat.GetTexture("_BaseMap") is Texture2D persistentTex)
            {
                config.sourceTexture = persistentTex;
            }

            var polys = PuzzleGridGenerator.GeneratePolygons(config);
            PuzzleData puzzleData = ScriptableObject.CreateInstance<PuzzleData>();
            puzzleData.puzzleId = puzzleName;
            puzzleData.displayName = puzzleName;
            puzzleData.rows = config.rows;
            puzzleData.columns = config.columns;
            puzzleData.boardWidth = config.boardWidth;
            puzzleData.boardHeight = config.boardHeight;
            puzzleData.thickness = config.thickness;
            puzzleData.sourceImage = config.sourceTexture;
            puzzleData.puzzleMaterial = faceMat;
            puzzleData.sideMaterial = sideMat;

            GameObject rootPrefab = new GameObject(puzzleName);
            var boardComponent = rootPrefab.AddComponent<PuzzleBoard>();
            boardComponent.config = config;

            float halfW = config.boardWidth * 0.5f;
            float halfH = config.boardHeight * 0.5f;
            Material[] sharedMats = new Material[] { faceMat, sideMat };

            foreach (var poly in polys)
            {
                Mesh mesh = PuzzleMeshBuilder.BuildPieceMesh(poly, config.thickness, config.bevelRadius);
                string meshPath = $"{meshFolder}/Piece_{poly.row}_{poly.col}.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);

                GameObject pieceObj = new GameObject($"Piece_{poly.row}_{poly.col}");
                pieceObj.transform.SetParent(rootPrefab.transform, false);

                Vector3 targetLocalPos = new Vector3(
                    poly.centerPos.x - halfW,
                    config.thickness * 0.5f,
                    poly.centerPos.y - halfH
                );

                // Đặt sẵn vị trí và góc xoay hoàn chỉnh trên bàn cờ trong Prefab
                pieceObj.transform.localPosition = targetLocalPos;
                pieceObj.transform.localRotation = Quaternion.identity;

                var mf = pieceObj.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;

                var mr = pieceObj.AddComponent<MeshRenderer>();
                mr.sharedMaterials = sharedMats;

                var col = pieceObj.AddComponent<MeshCollider>();
                col.convex = true;
                col.sharedMesh = mesh;

                var piece = pieceObj.AddComponent<PuzzlePiece>();
                piece.pieceId = poly.pieceId;
                piece.row = poly.row;
                piece.col = poly.col;
                piece.correctLocalPos = targetLocalPos;
                piece.correctLocalRot = Quaternion.identity;
                boardComponent.activePieces.Add(piece);

                var pieceData = new PuzzlePieceData
                {
                    pieceId = poly.pieceId,
                    row = poly.row,
                    col = poly.col,
                    correctLocalPosition = targetLocalPos,
                    correctLocalRotation = Quaternion.identity,
                    mesh = mesh
                };
                puzzleData.pieces.Add(pieceData);
            }

            string dataPath = $"{baseFolder}/{puzzleName}_Data.asset";
            AssetDatabase.CreateAsset(puzzleData, dataPath);

            AssetDatabase.StopAssetEditing();

            // Lưu Prefab
            string prefabPath = $"{baseFolder}/{puzzleName}_Prefab.prefab";
            PrefabUtility.SaveAsPrefabAsset(rootPrefab, prefabPath);
            DestroyImmediate(rootPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Hoàn tất Export", $"Đã xuất thành công bộ Puzzle:\n- Data: {dataPath}\n- Prefab: {prefabPath}\n- {polys.Count} Meshes trong {meshFolder}", "OK");
        }
    }
}
