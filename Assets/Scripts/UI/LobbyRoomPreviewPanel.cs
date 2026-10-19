using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleOnline.Core;
using PuzzleSystem.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị 2 khung preview bên phải màn hình khi ở trong phòng chờ:
    /// - Khung trên: Preview Không gian chơi 3D thực tế (Render trực tiếp từ các Prefab: Forest, Classroom, Sakura trong Resources)
    /// - Khung dưới: Preview Bộ xếp hình (Puzzle source image từ ScriptableObject PuzzleData)
    /// - Tự động cập nhật thời gian thực khi Chủ phòng đổi tranh / đổi map
    /// </summary>
    public class LobbyRoomPreviewPanel : MonoBehaviour
    {
        [Header("=== Scene Preview Roots ===")]
        [SerializeField] private GameObject mapPreviewRoot;
        [SerializeField] private GameObject puzzlePreviewRoot;

        [Header("=== Map / Environment Preview (Khung Trên) ===")]
        [SerializeField] private RawImage mapPreviewImage;
        [SerializeField] private Image mapPreviewSpriteImage;

        [Header("=== Puzzle Preview (Khung Dưới) ===")]
        [SerializeField] private RawImage puzzlePreviewImage;
        [SerializeField] private Image puzzlePreviewSpriteImage;

        private enum PreviewCameraMode
        {
            GenericOrbit,
            ClassroomIndoor,
            SakuraTableFocus,
            ForestTableFocus
        }

        private const string MapLayerName = "MapPreview";
        private const string StageLayerName = "LobbyStage";

        // Studio 3D riêng biệt để render Map Preview trực tiếp từ Prefab
        private GameObject _mapStudioRoot;
        private Camera _mapCam;
        private RenderTexture _mapRenderTexture;
        private GameObject _currentMapInstance;
        private string _currentLoadedThemeId = "";
        private Vector3 _mapCenter = Vector3.zero;
        private float _mapRadius = 15f;
        private Light _studioDirLight;
        private PreviewCameraMode _camMode = PreviewCameraMode.GenericOrbit;
        private Vector3 _focalTarget = Vector3.zero;

        private static readonly Dictionary<string, Texture2D> _proceduralThemeTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        public static int GetMapLayer()
        {
            int layer = LayerMask.NameToLayer(MapLayerName);
            return layer >= 0 ? layer : 0;
        }

        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
            }
        }

        private static Transform FindChildByKeywords(Transform parent, params string[] keywords)
        {
            if (parent == null || keywords == null || keywords.Length == 0) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (child.name.IndexOf(keywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return child;
                    }
                }
                var deeper = FindChildByKeywords(child, keywords);
                if (deeper != null) return deeper;
            }
            return null;
        }

        private static Transform CreateProceduralPicnicTable(Transform parent, Vector3 localPos, int layer)
        {
            var tableRoot = new GameObject("PicnicTable");
            tableRoot.transform.SetParent(parent, false);
            tableRoot.transform.localPosition = localPos;

            Color woodColor = new Color(0.48f, 0.32f, 0.18f);      // Màu gỗ mộc ấm áp
            Color darkWoodColor = new Color(0.36f, 0.23f, 0.12f);  // Màu chân gỗ sẫm

            Renderer sampleRenderer = parent.GetComponentInChildren<Renderer>();
            Material woodMat;
            if (sampleRenderer != null && sampleRenderer.sharedMaterial != null)
            {
                woodMat = new Material(sampleRenderer.sharedMaterial);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
                woodMat = new Material(shader);
            }
            woodMat.color = woodColor;

            Material darkWoodMat = new Material(woodMat);
            darkWoodMat.color = darkWoodColor;

            // 1. Mặt bàn chính
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "TableTop";
            top.transform.SetParent(tableRoot.transform, false);
            top.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            top.transform.localScale = new Vector3(2.2f, 0.08f, 1.1f);
            var topRen = top.GetComponent<Renderer>();
            if (topRen != null) topRen.sharedMaterial = woodMat;

            // 2. 4 chân bàn
            Vector3[] legOffsets = new Vector3[]
            {
                new Vector3(-0.9f, 0.40f, -0.45f),
                new Vector3(0.9f, 0.40f, -0.45f),
                new Vector3(-0.9f, 0.40f, 0.45f),
                new Vector3(0.9f, 0.40f, 0.45f)
            };
            for (int i = 0; i < legOffsets.Length; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg_{i}";
                leg.transform.SetParent(tableRoot.transform, false);
                leg.transform.localPosition = legOffsets[i];
                leg.transform.localScale = new Vector3(0.09f, 0.80f, 0.09f);
                var legRen = leg.GetComponent<Renderer>();
                if (legRen != null) legRen.sharedMaterial = darkWoodMat;
            }

            // 3. 2 ghế dài (benches) hai bên
            var bench1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench1.name = "Bench_Front";
            bench1.transform.SetParent(tableRoot.transform, false);
            bench1.transform.localPosition = new Vector3(0f, 0.48f, -0.85f);
            bench1.transform.localScale = new Vector3(2.2f, 0.06f, 0.32f);
            var b1Ren = bench1.GetComponent<Renderer>();
            if (b1Ren != null) b1Ren.sharedMaterial = woodMat;

            var bench2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench2.name = "Bench_Back";
            bench2.transform.SetParent(tableRoot.transform, false);
            bench2.transform.localPosition = new Vector3(0f, 0.48f, 0.85f);
            bench2.transform.localScale = new Vector3(2.2f, 0.06f, 0.32f);
            var b2Ren = bench2.GetComponent<Renderer>();
            if (b2Ren != null) b2Ren.sharedMaterial = woodMat;

            // Loại bỏ Collider của các primitive vừa tạo để an toàn tuyệt đối
            foreach (var col in tableRoot.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(col);
            }

            SetLayerRecursively(tableRoot, layer);
            return tableRoot.transform;
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnRoomUpdated += OnRoomUpdated;
                net.OnRoomLeft += OnRoomLeft;
            }

            bool inRoom = net != null && net.CurrentRoom != null;
            if (inRoom)
            {
                InitMapStudio();
                var lobbyCtrl = FindFirstObjectByType<LobbyUIController>();
                bool visible = lobbyCtrl == null || !lobbyCtrl.IsFriendsOverlayVisible;
                SetVisible(visible);
                if (visible) RefreshPreviews();
            }
            else
            {
                SetVisible(false);
            }
        }

        private void OnDestroy()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnRoomUpdated -= OnRoomUpdated;
                net.OnRoomLeft -= OnRoomLeft;
            }

            if (_mapRenderTexture != null)
            {
                _mapRenderTexture.Release();
                Destroy(_mapRenderTexture);
            }

            if (_mapStudioRoot != null)
            {
                Destroy(_mapStudioRoot);
            }
        }

        public void Initialize(RawImage mapImg = null, RawImage puzImg = null, Image puzSpriteImg = null)
        {
            if (mapImg != null) mapPreviewImage = mapImg;
            if (puzImg != null) puzzlePreviewImage = puzImg;
            if (puzSpriteImg != null) puzzlePreviewSpriteImage = puzSpriteImg;

            InitMapStudio();
            RefreshPreviews();
        }

        public void SetVisible(bool visible)
        {
            EnsureReferences();

            if (mapPreviewRoot != null) mapPreviewRoot.SetActive(visible);
            if (puzzlePreviewRoot != null) puzzlePreviewRoot.SetActive(visible);

            if (_mapStudioRoot != null)
            {
                _mapStudioRoot.SetActive(visible);
            }

            if (_mapCam != null)
            {
                _mapCam.enabled = visible;
            }

            if (visible)
            {
                RefreshPreviews();
            }
        }

        private void InitMapStudio()
        {
            if (_mapStudioRoot != null) return;

            int mapLayer = GetMapLayer();
            int stageLayer = LayerMask.NameToLayer(StageLayerName);

            // Đặt Studio ở vị trí biệt lập, không trùng với phòng chờ 3D (250, 100, 250) hay Main Scene (0, 0, 0)
            _mapStudioRoot = new GameObject("[LobbyMapPreview_Studio]");
            _mapStudioRoot.transform.position = new Vector3(500f, -200f, 500f);

            var camGo = new GameObject("MapPreviewCamera");
            camGo.transform.SetParent(_mapStudioRoot.transform, false);

            _mapCam = camGo.AddComponent<Camera>();
            _mapCam.clearFlags = CameraClearFlags.SolidColor;
            _mapCam.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            _mapCam.fieldOfView = 50f;
            _mapCam.nearClipPlane = 0.3f;
            _mapCam.farClipPlane = 120f;
            _mapCam.cullingMask = 1 << mapLayer; // Chỉ render layer MapPreview, không thấy stage 4 người chơi

            _mapRenderTexture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            _mapRenderTexture.antiAliasing = 2;
            _mapRenderTexture.Create();
            _mapCam.targetTexture = _mapRenderTexture;

            if (mapPreviewImage != null)
            {
                mapPreviewImage.texture = _mapRenderTexture;
                mapPreviewImage.color = Color.white;
            }

            // Ánh sáng cho studio map (cô lập trên layer MapPreview, không chiếu vào LobbyStage)
            var lightGo = new GameObject("MapStudioLight");
            lightGo.transform.SetParent(_mapStudioRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 30f, 0f);
            lightGo.transform.localEulerAngles = new Vector3(45f, -30f, 0f);

            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.96f, 0.88f);
            dirLight.intensity = 1.25f;
            dirLight.cullingMask = 1 << mapLayer; // Chỉ chiếu sáng studio preview
            if (stageLayer >= 0)
            {
                dirLight.cullingMask &= ~(1 << stageLayer);
            }
            _studioDirLight = dirLight;

            // Cô lập studio khỏi các đèn Directional Light bên ngoài scene
            var allLights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in allLights)
            {
                if (l != null && l != _studioDirLight && l.type == LightType.Directional)
                {
                    l.cullingMask &= ~(1 << mapLayer);
                }
            }

            _mapStudioRoot.SetActive(gameObject.activeInHierarchy);
        }

        private void Update()
        {
            if (_mapStudioRoot == null || !_mapStudioRoot.activeInHierarchy || _mapCam == null) return;
            if (_currentMapInstance == null) return;

            switch (_camMode)
            {
                case PreviewCameraMode.ClassroomIndoor:
                {
                    // Camera đặt bên trong phòng học, chuyển động sway nhẹ nhàng qua lại quan sát dãy bàn học
                    float sway = Mathf.Sin(Time.time * 0.12f) * 0.35f;
                    float camX = _focalTarget.x - 2.5f + sway;
                    float camY = _focalTarget.y + 0.8f + Mathf.Cos(Time.time * 0.10f) * 0.05f;
                    float camZ = _focalTarget.z - 3.1f - sway * 0.25f;

                    _mapCam.transform.position = new Vector3(camX, camY, camZ);
                    _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.1f);
                    break;
                }
                case PreviewCameraMode.SakuraTableFocus:
                {
                    // Camera xoay quanh bàn xếp hình ở cự ly gần (~5.6m), nhìn rõ mặt bàn và các tán cây hoa anh đào
                    float t = Time.time * 0.15f;
                    float dist = 5.6f;
                    float camX = _focalTarget.x + Mathf.Cos(t) * dist;
                    float camZ = _focalTarget.z + Mathf.Sin(t) * dist;
                    float camY = _focalTarget.y + 1.85f + Mathf.Sin(t * 0.6f) * 0.12f;

                    _mapCam.transform.position = new Vector3(camX, camY, camZ);
                    _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.2f);
                    break;
                }
                case PreviewCameraMode.ForestTableFocus:
                {
                    // Camera xoay quanh bàn picnic giữa rừng ở cự ly gần (~5.8m), nhìn rõ mặt bàn chơi puzzle và tán rừng xanh
                    float t = Time.time * 0.15f;
                    float dist = 5.8f;
                    float camX = _focalTarget.x + Mathf.Cos(t) * dist;
                    float camZ = _focalTarget.z + Mathf.Sin(t) * dist;
                    float camY = _focalTarget.y + 1.85f + Mathf.Sin(t * 0.6f) * 0.12f;

                    _mapCam.transform.position = new Vector3(camX, camY, camZ);
                    _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.2f);
                    break;
                }
                case PreviewCameraMode.GenericOrbit:
                default:
                {
                    // Camera quay chậm nhẹ nhàng tạo góc nhìn toàn cảnh sinh động
                    float t = Time.time * 0.18f;
                    float dist = Mathf.Clamp(_mapRadius * 0.9f, 8f, 35f);
                    float camX = _mapCenter.x + Mathf.Cos(t) * dist;
                    float camZ = _mapCenter.z + Mathf.Sin(t) * dist;
                    float camY = _mapCenter.y + dist * 0.45f;

                    _mapCam.transform.position = new Vector3(camX, camY, camZ);
                    _mapCam.transform.LookAt(_mapCenter + Vector3.up * (_mapRadius * 0.12f));
                    break;
                }
            }
        }

        private void OnRoomUpdated()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null && net.CurrentRoom != null)
            {
                var lobbyCtrl = FindFirstObjectByType<LobbyUIController>();
                if (lobbyCtrl != null && lobbyCtrl.IsFriendsOverlayVisible)
                {
                    // Đang mở danh sách Mời bạn: giữ nguyên trạng thái ẩn của Preview
                    return;
                }

                SetVisible(true);
                RefreshPreviews();
            }
            else
            {
                SetVisible(false);
            }
        }

        private void OnRoomLeft()
        {
            SetVisible(false);
        }

        public void RefreshPreviews()
        {
            EnsureReferences();

            var net = PuzzleNetworkManager.Instance;
            string themeId = net != null ? net.CurrentThemeId : null;
            string puzzleId = net != null ? net.CurrentPuzzleId : null;

            UpdateThemePreview(themeId);
            UpdatePuzzlePreview(puzzleId);
        }

        private void UpdateThemePreview(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) themeId = "SCN_Nature";

            // 1. Tìm Prefab thực tế trong Resources
            string cleanName = themeId.Replace("SCN_", "", StringComparison.OrdinalIgnoreCase);
            GameObject mapPrefab = Resources.Load<GameObject>($"Themes/{cleanName}")
                                ?? Resources.Load<GameObject>(cleanName)
                                ?? Resources.Load<GameObject>($"Themes/{themeId}")
                                ?? Resources.Load<GameObject>(themeId);

            if (mapPrefab == null && cleanName.Equals("Nature", StringComparison.OrdinalIgnoreCase))
            {
                mapPrefab = Resources.Load<GameObject>("Forest") ?? Resources.Load<GameObject>("Themes/Forest");
            }

            // 3. Render 3D trực tiếp từ Prefab thực tế vào RenderTexture
            if (mapPrefab != null)
            {
                InitMapStudio();

                if (!_currentLoadedThemeId.Equals(themeId, StringComparison.OrdinalIgnoreCase) || _currentMapInstance == null)
                {
                    if (_currentMapInstance != null)
                    {
                        Destroy(_currentMapInstance);
                    }

                    _currentMapInstance = Instantiate(mapPrefab, _mapStudioRoot.transform, false);
                    _currentMapInstance.transform.localPosition = Vector3.zero;
                    _currentLoadedThemeId = themeId;

                    int mapLayer = GetMapLayer();
                    int stageLayer = LayerMask.NameToLayer(StageLayerName);
                    SetLayerRecursively(_currentMapInstance, mapLayer);

                    // Xử lý và giới hạn cường độ ánh sáng của các đèn có sẵn trong prefab môi trường
                    foreach (var l in _currentMapInstance.GetComponentsInChildren<Light>(true))
                    {
                        l.cullingMask = 1 << mapLayer;
                        if (stageLayer >= 0)
                        {
                            l.cullingMask &= ~(1 << stageLayer);
                        }
                        // Tránh việc point light nội thất (như Point 1 trong Classroom có intensity = 10) gây lóa trắng
                        if (l.intensity > 2.0f)
                        {
                            l.intensity = 1.8f;
                        }
                    }

                    // Loại bỏ Camera, AudioListener, Collider trong prefab môi trường để an toàn tuyệt đối
                    foreach (var cam in _currentMapInstance.GetComponentsInChildren<Camera>(true)) DestroyImmediate(cam.gameObject);
                    foreach (var al in _currentMapInstance.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(al);
                    foreach (var col in _currentMapInstance.GetComponentsInChildren<Collider>(true)) DestroyImmediate(col);

                    // Tính toán tâm và bán kính bao bọc khung cảnh để đặt Camera góc chuẩn
                    Bounds b = new Bounds(_currentMapInstance.transform.position, Vector3.zero);
                    bool hasB = false;
                    foreach (var r in _currentMapInstance.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r is ParticleSystemRenderer) continue;
                        if (!hasB) { b = r.bounds; hasB = true; }
                        else { b.Encapsulate(r.bounds); }
                    }

                    _mapCenter = hasB ? b.center : _mapStudioRoot.transform.position;
                    _mapRadius = hasB ? Mathf.Clamp(b.extents.magnitude, 6f, 35f) : 15f;

                    // Quyết định chế độ Camera dựa theo Theme
                    if (cleanName.IndexOf("Classroom", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _camMode = PreviewCameraMode.ClassroomIndoor;
                        var table = FindChildByKeywords(_currentMapInstance.transform, "Table", "Desk", "Positions");
                        if (table != null)
                        {
                            _focalTarget = table.position + Vector3.up * 0.45f;
                        }
                        else
                        {
                            _focalTarget = _currentMapInstance.transform.TransformPoint(new Vector3(0f, 0.85f, 0f));
                        }

                        if (_mapCam != null)
                        {
                            _mapCam.fieldOfView = 55f;
                            _mapCam.transform.position = _focalTarget + new Vector3(-2.6f, 0.8f, -3.1f);
                            _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.1f);
                            _mapCam.enabled = true;
                        }
                        if (_studioDirLight != null) _studioDirLight.intensity = 0.85f;
                    }
                    else if (cleanName.IndexOf("Sakura", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _camMode = PreviewCameraMode.SakuraTableFocus;
                        var table = FindChildByKeywords(_currentMapInstance.transform, "Table", "picnic", "Positions");
                        if (table != null)
                        {
                            _focalTarget = table.position + Vector3.up * 0.35f;
                        }
                        else
                        {
                            _focalTarget = _mapCenter;
                        }

                        if (_mapCam != null)
                        {
                            _mapCam.fieldOfView = 50f;
                            float dist = 5.6f;
                            _mapCam.transform.position = _focalTarget + new Vector3(dist * 0.7f, 1.85f, dist * 0.7f);
                            _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.2f);
                            _mapCam.enabled = true;
                        }
                        if (_studioDirLight != null) _studioDirLight.intensity = 1.25f;
                    }
                    else if (cleanName.IndexOf("Forest", StringComparison.OrdinalIgnoreCase) >= 0 || cleanName.IndexOf("Nature", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _camMode = PreviewCameraMode.ForestTableFocus;
                        var table = FindChildByKeywords(_currentMapInstance.transform, "Table", "picnic", "Positions");
                        if (table == null)
                        {
                            table = CreateProceduralPicnicTable(_currentMapInstance.transform, new Vector3(-0.59f, 0f, -5.89f), mapLayer);
                        }

                        if (table != null)
                        {
                            _focalTarget = table.position + Vector3.up * 0.45f;
                        }
                        else
                        {
                            _focalTarget = _currentMapInstance.transform.TransformPoint(new Vector3(-0.59f, 0.85f, -5.89f));
                        }

                        if (_mapCam != null)
                        {
                            _mapCam.fieldOfView = 50f;
                            float dist = 5.8f;
                            _mapCam.transform.position = _focalTarget + new Vector3(dist * 0.7f, 1.85f, dist * 0.7f);
                            _mapCam.transform.LookAt(_focalTarget + Vector3.up * 0.2f);
                            _mapCam.enabled = true;
                        }
                        if (_studioDirLight != null) _studioDirLight.intensity = 1.25f;
                    }
                    else
                    {
                        _camMode = PreviewCameraMode.GenericOrbit;
                        _focalTarget = _mapCenter;

                        if (_mapCam != null)
                        {
                            _mapCam.fieldOfView = 50f;
                            _mapCam.transform.position = _mapCenter + new Vector3(-_mapRadius * 0.7f, _mapRadius * 0.45f, -_mapRadius * 0.7f);
                            _mapCam.transform.LookAt(_mapCenter + Vector3.up * (_mapRadius * 0.12f));
                            _mapCam.enabled = true;
                        }
                        if (_studioDirLight != null) _studioDirLight.intensity = 1.3f;
                    }
                }

                if (mapPreviewImage != null)
                {
                    mapPreviewImage.texture = _mapRenderTexture;
                    mapPreviewImage.color = Color.white;
                    mapPreviewImage.gameObject.SetActive(true);
                    if (mapPreviewSpriteImage != null) mapPreviewSpriteImage.gameObject.SetActive(false);
                }
            }
            else
            {
                // Fallback nếu không tìm thấy prefab
                Texture2D tex = Resources.Load<Texture2D>($"Themes/{themeId}")
                             ?? Resources.Load<Texture2D>(themeId)
                             ?? GetOrCreateThemeTexture(themeId);

                if (tex != null && mapPreviewImage != null)
                {
                    mapPreviewImage.texture = tex;
                    mapPreviewImage.color = Color.white;
                    mapPreviewImage.gameObject.SetActive(true);
                    if (mapPreviewSpriteImage != null) mapPreviewSpriteImage.gameObject.SetActive(false);
                }
            }
        }

        private void UpdatePuzzlePreview(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) puzzleId = "sunset_3x3";

            Texture2D sourceTex = null;
            Sprite thumbSprite = null;

            // 1. Thử lấy từ ContentRegistry
            PuzzleData pData = null;
            try
            {
                pData = ContentRegistry.Instance?.GetPuzzleData(puzzleId);
            }
            catch { }

            // 2. Tìm trong tất cả PuzzleData trong Resources
            if (pData == null)
            {
                var allPuzzles = Resources.LoadAll<PuzzleData>("");
                if (allPuzzles != null)
                {
                    foreach (var p in allPuzzles)
                    {
                        if (p == null) continue;
                        if (string.Equals(p.puzzleId, puzzleId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(p.name, puzzleId, StringComparison.OrdinalIgnoreCase) ||
                            puzzleId.IndexOf(p.name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            p.puzzleId.IndexOf(puzzleId, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pData = p;
                            break;
                        }
                    }
                }
            }

            // 3. Fallback nạp file SO trực tiếp từ Resources/Puzzles/
            if (pData == null)
            {
                string cleanId = puzzleId.Replace("_3x3", "").Replace("_4x3", "").Replace("_4x4", "").Replace("_10x10", "").ToLowerInvariant();
                pData = Resources.Load<PuzzleData>($"Puzzles/{cleanId}")
                     ?? Resources.Load<PuzzleData>($"Puzzles/{puzzleId}")
                     ?? Resources.Load<PuzzleData>(cleanId);
            }

            // 4. Lấy field sourceImage từ ScriptableObject (SO)
            if (pData != null)
            {
                sourceTex = pData.sourceImage;
                thumbSprite = pData.thumbnail;
            }

            // 5. Fallback nếu không có ScriptableObject: tìm ảnh trong Resources
            if (sourceTex == null)
            {
                string cleanId = puzzleId.Replace("_3x3", "").Replace("_4x3", "").Replace("_4x4", "").Replace("_10x10", "");
                sourceTex = Resources.Load<Texture2D>($"PuzzleImages/{cleanId}")
                         ?? Resources.Load<Texture2D>($"PuzzleImages/{puzzleId}")
                         ?? Resources.Load<Texture2D>(cleanId)
                         ?? Resources.Load<Texture2D>(puzzleId);
            }

            if (sourceTex != null)
            {
                if (puzzlePreviewSpriteImage != null)
                {
                    var sprite = Sprite.Create(sourceTex, new Rect(0, 0, sourceTex.width, sourceTex.height), new Vector2(0.5f, 0.5f));
                    puzzlePreviewSpriteImage.sprite = sprite;
                    puzzlePreviewSpriteImage.color = Color.white;
                    puzzlePreviewSpriteImage.gameObject.SetActive(true);
                }

                if (puzzlePreviewImage != null)
                {
                    puzzlePreviewImage.texture = sourceTex;
                    puzzlePreviewImage.color = Color.white;
                    puzzlePreviewImage.gameObject.SetActive(true);
                }
            }
            else if (thumbSprite != null)
            {
                if (puzzlePreviewSpriteImage != null)
                {
                    puzzlePreviewSpriteImage.sprite = thumbSprite;
                    puzzlePreviewSpriteImage.color = Color.white;
                    puzzlePreviewSpriteImage.gameObject.SetActive(true);
                }
                if (puzzlePreviewImage != null)
                {
                    puzzlePreviewImage.gameObject.SetActive(false);
                }
            }
        }

        private void EnsureReferences()
        {
            var canvas = transform.root;

            if (mapPreviewRoot == null)
            {
                var t = canvas.Find("Map Preview")
                     ?? canvas.Find("Canvas/Map Preview")
                     ?? canvas.Find("Canvas (1)/Map Preview");
                if (t != null) mapPreviewRoot = t.gameObject;
                else if (gameObject.name.Equals("Map Preview", StringComparison.OrdinalIgnoreCase)) mapPreviewRoot = gameObject;
            }

            if (puzzlePreviewRoot == null)
            {
                var t = canvas.Find("Puzzle Preview")
                     ?? canvas.Find("Canvas/Puzzle Preview")
                     ?? canvas.Find("Canvas (1)/Puzzle Preview");
                if (t != null) puzzlePreviewRoot = t.gameObject;
            }

            if (mapPreviewImage == null && mapPreviewRoot != null)
            {
                mapPreviewImage = mapPreviewRoot.GetComponentInChildren<RawImage>(true);
            }
            if (mapPreviewImage != null) mapPreviewImage.raycastTarget = false;

            if (puzzlePreviewSpriteImage == null && puzzlePreviewRoot != null)
            {
                puzzlePreviewSpriteImage = puzzlePreviewRoot.GetComponentInChildren<Image>(true);
            }
            if (puzzlePreviewSpriteImage != null) puzzlePreviewSpriteImage.raycastTarget = false;

            if (puzzlePreviewImage == null && puzzlePreviewRoot != null)
            {
                puzzlePreviewImage = puzzlePreviewRoot.GetComponentInChildren<RawImage>(true);
            }
            if (puzzlePreviewImage != null) puzzlePreviewImage.raycastTarget = false;
        }

        private static Texture2D GetOrCreateThemeTexture(string themeId)
        {
            if (_proceduralThemeTextures.TryGetValue(themeId, out var existing) && existing != null)
            {
                return existing;
            }

            Color topColor, botColor;
            switch (themeId.ToLowerInvariant())
            {
                case "scn_nature":
                    topColor = new Color(0.15f, 0.45f, 0.28f);
                    botColor = new Color(0.08f, 0.22f, 0.14f);
                    break;
                case "scn_classroom":
                    topColor = new Color(0.55f, 0.35f, 0.18f);
                    botColor = new Color(0.28f, 0.16f, 0.08f);
                    break;
                case "scn_sakura":
                    topColor = new Color(0.72f, 0.38f, 0.52f);
                    botColor = new Color(0.35f, 0.18f, 0.28f);
                    break;
                default:
                    topColor = new Color(0.18f, 0.25f, 0.42f);
                    botColor = new Color(0.08f, 0.12f, 0.20f);
                    break;
            }

            int w = 256;
            int h = 144;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color[] cols = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                Color rowCol = Color.Lerp(botColor, topColor, t);
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w - 0.5f;
                    float ny = (float)y / h - 0.5f;
                    float vignette = 1f - (nx * nx + ny * ny) * 0.7f;
                    cols[y * w + x] = rowCol * vignette;
                }
            }

            tex.SetPixels(cols);
            tex.Apply();
            _proceduralThemeTextures[themeId] = tex;
            return tex;
        }
    }
}
