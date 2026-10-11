using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleSystem.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Hiển thị phòng chờ 3D với 4 mảnh ghép lơ lửng và mô hình nhân vật:
    /// - 4 mảnh ghép 3D trôi bồng bềnh không trọng lực (Floating Puzzle Pieces)
    /// - Nhân vật đứng trên mảnh ghép tương ứng khi vào phòng với tỉ lệ chuẩn xác
    /// - Hiển thị Nameplate nổi (Tên, Vương miện Host, Trạng thái Sẵn sàng cho khách)
    /// - Render qua Camera chuyên dụng và xuất ra RawImage trên UI Canvas
    /// </summary>
    public class LobbyRoom3DView : MonoBehaviour
    {
        [Header("=== UI Reference ===")]
        [SerializeField] private RawImage targetRawImage;
        [SerializeField] private GameObject roomViewContainer;

        [Header("=== 3D Stage Settings ===")]
        [SerializeField] private Vector3 stagePosition = new Vector3(250f, 100f, 250f);
        [SerializeField] private GameObject playerPrefab;

        private RenderTexture _renderTexture;
        private Camera _roomCamera;
        private GameObject _stageRoot;

        private class PieceSlot
        {
            public int index;
            public GameObject pieceObject;
            public Vector3 initialLocalPos;
            public float floatPhase;
            public Transform standPoint;
            public TextMesh nameplateText;
            public GameObject currentAvatar;
            public Material pieceMaterial;
        }

        private readonly List<PieceSlot> _slots = new List<PieceSlot>();
        private bool _isInitialized = false;

        private void Awake()
        {
            if (roomViewContainer == null) roomViewContainer = gameObject;

            if (targetRawImage == null)
            {
                targetRawImage = GetComponentInChildren<RawImage>(true);
            }

            if (targetRawImage != null)
            {
                targetRawImage.raycastTarget = false;
            }

            if (playerPrefab == null)
            {
                playerPrefab = Resources.Load<GameObject>("Player")
                            ?? Resources.Load<GameObject>("Prefabs/Player");
            }
        }

        private void Start()
        {
            InitializeStage();

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnRoomUpdated += OnRoomUpdated;
                net.OnRoomLeft += OnRoomLeft;
            }

            // Kiểm tra trạng thái hiện tại
            if (net != null && net.CurrentRoom != null)
            {
                SetVisible(true);
                RefreshPlayers();
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

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_stageRoot != null)
            {
                Destroy(_stageRoot);
            }
        }

        public void Initialize(RawImage rawImage, GameObject avatarPrefab = null)
        {
            if (rawImage != null)
            {
                targetRawImage = rawImage;
                targetRawImage.raycastTarget = false;
            }

            if (avatarPrefab != null)
            {
                playerPrefab = avatarPrefab;
            }

            roomViewContainer = gameObject;
            InitializeStage();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);

            if (roomViewContainer != null && roomViewContainer != gameObject)
            {
                roomViewContainer.SetActive(visible);
            }

            if (targetRawImage != null)
            {
                targetRawImage.gameObject.SetActive(visible);
            }

            if (visible)
            {
                if (!_isInitialized)
                {
                    InitializeStage();
                }

                if (_stageRoot != null) _stageRoot.SetActive(true);
                if (_roomCamera != null) _roomCamera.enabled = true;

                RefreshPlayers();
            }
            else
            {
                if (_stageRoot != null) _stageRoot.SetActive(false);
                if (_roomCamera != null) _roomCamera.enabled = false;
            }
        }

        private void InitializeStage()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            if (targetRawImage == null)
            {
                targetRawImage = GetComponentInChildren<RawImage>(true);
            }

            if (playerPrefab == null)
            {
                playerPrefab = Resources.Load<GameObject>("Player")
                            ?? Resources.Load<GameObject>("Prefabs/Player");
            }

            // 1. Tạo Root cho 3D Stage ở vị trí biệt lập
            _stageRoot = new GameObject("[LobbyRoom3D_Stage]");
            _stageRoot.transform.position = stagePosition;

            // 2. Tạo Camera chiếu góc trên chéo xuống (Top-down Isometric View)
            var camGo = new GameObject("Room3DCamera");
            camGo.transform.SetParent(_stageRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 4.4f, -5.2f);
            camGo.transform.localEulerAngles = new Vector3(33f, 0f, 0f);

            _roomCamera = camGo.AddComponent<Camera>();
            _roomCamera.clearFlags = CameraClearFlags.SolidColor;
            _roomCamera.backgroundColor = new Color(0f, 0f, 0f, 0f); // Nền hoàn toàn trong suốt (Alpha = 0)
            _roomCamera.fieldOfView = 48f;
            _roomCamera.nearClipPlane = 0.3f;
            _roomCamera.farClipPlane = 50f;
            _roomCamera.cullingMask = -1; // Render toàn bộ layer của stage

            // 3. Tạo RenderTexture gán cho Camera và RawImage
            _renderTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
            _renderTexture.antiAliasing = 4;
            _renderTexture.Create();
            _roomCamera.targetTexture = _renderTexture;

            if (targetRawImage != null)
            {
                targetRawImage.texture = _renderTexture;
                targetRawImage.color = Color.white;
            }

            // 4. Ánh sáng vàng kim hoàng gia & Ambient Light
            var lightGo = new GameObject("StageLight");
            lightGo.transform.SetParent(_stageRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(2f, 6f, -3f);
            lightGo.transform.localEulerAngles = new Vector3(50f, -30f, 0f);

            var dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.94f, 0.82f);
            dirLight.intensity = 1.35f;

            var fillLightGo = new GameObject("FillLight");
            fillLightGo.transform.SetParent(_stageRoot.transform, false);
            fillLightGo.transform.localPosition = new Vector3(-3f, 4f, 2f);
            var fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.35f, 0.55f, 0.95f);
            fillLight.range = 15f;
            fillLight.intensity = 2f;

            // 5. Tạo 4 Mảnh Ghép Lơ Lửng (2x2)
            Vector3[] slotPositions = new Vector3[]
            {
                new Vector3(-1.45f, 0f, 0.95f),  // Slot 0 (Host - Góc trên trái)
                new Vector3(1.45f, 0f, 0.95f),   // Slot 1 (Góc trên phải)
                new Vector3(-1.45f, 0f, -0.95f), // Slot 2 (Góc dưới trái)
                new Vector3(1.45f, 0f, -0.95f)   // Slot 3 (Góc dưới phải)
            };

            for (int i = 0; i < 4; i++)
            {
                CreatePieceSlot(i, slotPositions[i]);
            }
        }

        private void CreatePieceSlot(int index, Vector3 localPos)
        {
            var pieceRoot = new GameObject($"FloatingPiece_Slot_{index}");
            pieceRoot.transform.SetParent(_stageRoot.transform, false);
            pieceRoot.transform.localPosition = localPos;
            pieceRoot.transform.localScale = Vector3.one;

            // 1. Khối Visual của mảnh ghép (chỉ scale khối này để không làm biến dạng StandPoint và Avatar)
            var visualGo = new GameObject("PieceVisual");
            visualGo.transform.SetParent(pieceRoot.transform, false);
            visualGo.transform.localPosition = Vector3.zero;

            Mesh pieceMesh = LoadSamplePuzzleMesh(index);
            var mf = visualGo.AddComponent<MeshFilter>();
            mf.sharedMesh = pieceMesh;

            var mr = visualGo.AddComponent<MeshRenderer>();
            Material mat = CreatePieceMaterial(index);
            mr.material = mat;

            float topY = 0.20f;
            if (pieceMesh != null)
            {
                var b = pieceMesh.bounds;
                float maxHoriz = Mathf.Max(b.size.x, b.size.z);
                if (maxHoriz > 0.01f && maxHoriz < 0.8f)
                {
                    // Mảnh thật từ Sunset có bề ngang ~0.27m, bề dày ~0.005m
                    float sHoriz = 1.6f / maxHoriz; // Phóng to bề ngang bệ đứng thành 1.6m
                    float sThick = 25f;            // Tăng độ dày lên ~0.15m để thành bệ đứng
                    visualGo.transform.localScale = new Vector3(sHoriz, sThick, sHoriz);
                    topY = Mathf.Max(0.15f, b.max.y * sThick);
                }
                else
                {
                    visualGo.transform.localScale = Vector3.one;
                    topY = Mathf.Max(0.15f, b.max.y);
                }
            }

            // 2. Điểm đứng cho Avatar (trực tiếp dưới pieceRoot với scale 1:1:1 chuẩn xác)
            var standGo = new GameObject("StandPoint");
            standGo.transform.SetParent(pieceRoot.transform, false);
            standGo.transform.localPosition = new Vector3(0f, topY, 0f);
            standGo.transform.localScale = Vector3.one;

            // 3. Bảng tên / Status 3D nổi trên đầu (scale 1:1:1 chuẩn xác)
            var nameGo = new GameObject("Nameplate");
            nameGo.transform.SetParent(pieceRoot.transform, false);
            nameGo.transform.localPosition = new Vector3(0f, topY + 1.85f, 0f);
            nameGo.transform.localScale = Vector3.one;
            nameGo.transform.localEulerAngles = new Vector3(25f, 0f, 0f); // Nghiêng nhẹ về hướng Camera

            var txt = nameGo.AddComponent<TextMesh>();
            txt.alignment = TextAlignment.Center;
            txt.anchor = TextAnchor.MiddleCenter;
            txt.fontSize = 32;
            txt.characterSize = 0.045f;
            txt.color = Color.white;
            txt.text = "<color=#64748b>[ TRỐNG ]</color>";

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                txt.font = font;
                var tmr = nameGo.GetComponent<MeshRenderer>();
                if (tmr != null) tmr.material = font.material;
            }

            var slot = new PieceSlot
            {
                index = index,
                pieceObject = pieceRoot,
                initialLocalPos = localPos,
                floatPhase = index * 1.57f, // Mỗi mảnh dập dềnh lệch pha nhau
                standPoint = standGo.transform,
                nameplateText = txt,
                pieceMaterial = mat
            };

            _slots.Add(slot);
        }

        private Mesh LoadSamplePuzzleMesh(int index)
        {
            // Thử nạp mesh thật từ PuzzleData trong Resources
            try
            {
                var sunset = Resources.Load<PuzzleData>("Puzzles/sunset");
                if (sunset != null && sunset.pieces != null && sunset.pieces.Count > index)
                {
                    if (sunset.pieces[index].mesh != null) return sunset.pieces[index].mesh;
                }
            }
            catch { }

            // Tạo Mesh khối 3D Mảnh Ghép Hoàng Gia bo tròn
            return CreateProceduralPieceMesh();
        }

        private Mesh CreateProceduralPieceMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "FloatingPuzzlePieceMesh";

            float w = 1.35f;
            float h = 0.22f;
            float d = 1.35f;

            // Khối hộp vát cạnh làm bệ đỡ mảnh ghép
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-w/2, 0, -d/2), new Vector3(w/2, 0, -d/2), new Vector3(w/2, 0, d/2), new Vector3(-w/2, 0, d/2), // Dưới
                new Vector3(-w/2 * 0.9f, h, -d/2 * 0.9f), new Vector3(w/2 * 0.9f, h, -d/2 * 0.9f),
                new Vector3(w/2 * 0.9f, h, d/2 * 0.9f), new Vector3(-w/2 * 0.9f, h, d/2 * 0.9f) // Trên
            };

            int[] triangles = new int[]
            {
                // Top
                4, 5, 6, 4, 6, 7,
                // Bottom
                0, 2, 1, 0, 3, 2,
                // Front
                0, 1, 5, 0, 5, 4,
                // Right
                1, 2, 6, 1, 6, 5,
                // Back
                2, 3, 7, 2, 7, 6,
                // Left
                3, 0, 4, 3, 4, 7
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreatePieceMaterial(int index)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit")
                                 ?? Shader.Find("Standard")
                                 ?? Shader.Find("Mobile/Diffuse");
            Material mat = new Material(standardShader);
            mat.color = new Color(0.12f, 0.16f, 0.25f, 1f); // Nền xanh navy hoàng gia
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.65f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.85f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
            return mat;
        }

        private void Update()
        {
            if (_stageRoot == null || !_stageRoot.activeInHierarchy) return;

            // Hiệu ứng dập dềnh không trọng lực (Sine wave floating & slight tilt)
            float t = Time.time * 1.4f;
            foreach (var slot in _slots)
            {
                if (slot.pieceObject == null) continue;

                float yOffset = Mathf.Sin(t + slot.floatPhase) * 0.09f;
                float tiltZ = Mathf.Sin(t * 0.8f + slot.floatPhase) * 1.8f;
                float tiltX = Mathf.Cos(t * 0.7f + slot.floatPhase) * 1.4f;

                slot.pieceObject.transform.localPosition = slot.initialLocalPos + new Vector3(0f, yOffset, 0f);
                slot.pieceObject.transform.localEulerAngles = new Vector3(tiltX, 0f, tiltZ);

                // Nameplate luôn hướng về Camera
                if (slot.nameplateText != null && _roomCamera != null)
                {
                    Vector3 lookDir = slot.nameplateText.transform.position - _roomCamera.transform.position;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        slot.nameplateText.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
            }
        }

        private void OnRoomUpdated()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null && net.CurrentRoom != null)
            {
                SetVisible(true);
                RefreshPlayers();
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

        public void RefreshPlayers()
        {
            if (!_isInitialized)
            {
                InitializeStage();
            }

            var net = PuzzleNetworkManager.Instance;
            if (net == null || net.CurrentRoom == null)
            {
                ClearAllSlots();
                return;
            }

            var players = net.RoomPlayers ?? new List<PlayerInfo>();

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (i < players.Count)
                {
                    var p = players[i];
                    SetupPlayerSlot(slot, p);
                }
                else
                {
                    SetupEmptySlot(slot);
                }
            }
        }

        private void SetupPlayerSlot(PieceSlot slot, PlayerInfo player)
        {
            // 1. Tạo Avatar nhân vật nếu chưa có
            if (slot.currentAvatar == null)
            {
                slot.currentAvatar = SpawnCleanAvatar(slot.standPoint, slot.index);
            }

            if (slot.currentAvatar != null)
            {
                slot.currentAvatar.SetActive(true);
            }

            // 2. Bảng tên & Trạng thái: Chủ phòng chỉ hiện tên và vương miện host, không cần hiện sẵn sàng
            if (slot.nameplateText != null)
            {
                if (player.IsHost)
                {
                    slot.nameplateText.text = $"<color=#f59e0b>★ CHỦ PHÒNG ★</color>\n<b>{player.Username}</b>";
                }
                else
                {
                    string readyBadge = player.Ready
                        ? "<color=#22c55e>✔ SẴN SÀNG</color>"
                        : "<color=#eab308>⏳ CHỜ SẴN SÀNG</color>";
                    slot.nameplateText.text = $"<b>{player.Username}</b>\n{readyBadge}";
                }
            }

            // 3. Màu sắc hào quang mảnh ghép
            if (slot.pieceMaterial != null)
            {
                Color glowColor = player.IsHost
                    ? new Color(0.95f, 0.75f, 0.2f, 1f)  // Vàng kim chủ phòng
                    : (player.Ready ? new Color(0.2f, 0.85f, 0.45f, 1f) : new Color(0.3f, 0.45f, 0.75f, 1f));

                slot.pieceMaterial.color = glowColor;
            }
        }

        private void SetupEmptySlot(PieceSlot slot)
        {
            if (slot.currentAvatar != null)
            {
                Destroy(slot.currentAvatar);
                slot.currentAvatar = null;
            }

            if (slot.nameplateText != null)
            {
                slot.nameplateText.text = $"<color=#64748b>[ TRỐNG ]\nSlot {slot.index + 1}</color>";
            }

            if (slot.pieceMaterial != null)
            {
                slot.pieceMaterial.color = new Color(0.10f, 0.12f, 0.18f, 0.7f);
            }
        }

        private void ClearAllSlots()
        {
            foreach (var slot in _slots)
            {
                SetupEmptySlot(slot);
            }
        }

        private GameObject SpawnCleanAvatar(Transform parent, int slotIndex)
        {
            if (playerPrefab == null)
            {
                playerPrefab = Resources.Load<GameObject>("Player")
                            ?? Resources.Load<GameObject>("Prefabs/Player");
            }

            GameObject avatar = null;
            if (playerPrefab != null)
            {
                avatar = Instantiate(playerPrefab, parent, false);
            }
            else
            {
                // Fallback Avatar 3D Capsule nếu prefab không khả dụng
                avatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                avatar.transform.SetParent(parent, false);
                avatar.transform.localScale = new Vector3(0.5f, 0.75f, 0.5f);
            }

            if (avatar == null) return null;

            avatar.transform.localPosition = Vector3.zero;
            avatar.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // Quay mặt về hướng Camera
            avatar.transform.localScale = Vector3.one * 0.65f; // Tỉ lệ chuẩn khớp hoàn hảo với bệ đứng 1.6m

            // 1. Phải gỡ bỏ các MonoBehaviour (PlayerController, FirstPersonController, v.v.) TRƯỚC!
            // Dùng DestroyImmediate để hủy ngay lập tức, giải phóng ràng buộc RequireComponent!
            foreach (var mb in avatar.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string typeName = mb.GetType().Name.ToLowerInvariant();
                if (typeName.Contains("camera") || typeName.Contains("controller") || typeName.Contains("movement") || typeName.Contains("input") || typeName.Contains("player"))
                {
                    mb.enabled = false;
                    DestroyImmediate(mb);
                }
            }

            // 2. Sau khi PlayerController đã bị gỡ, gỡ bỏ an toàn CharacterController, Colliders, Rigidbodies
            foreach (var cc in avatar.GetComponentsInChildren<CharacterController>(true))
            {
                cc.enabled = false;
                DestroyImmediate(cc);
            }
            foreach (var col in avatar.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
                DestroyImmediate(col);
            }
            foreach (var rb in avatar.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
            foreach (var cam in avatar.GetComponentsInChildren<Camera>(true)) DestroyImmediate(cam.gameObject);
            foreach (var al in avatar.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(al);

            // 3. Gán màu sắc đại diện cho nhân vật theo slot
            Color[] slotColors = new Color[]
            {
                new Color(0.95f, 0.75f, 0.20f), // Vàng kim (Host)
                new Color(0.25f, 0.75f, 0.95f), // Xanh dương Cyan
                new Color(0.35f, 0.85f, 0.45f), // Xanh lục Emerald
                new Color(0.85f, 0.40f, 0.85f)  // Tím hồng Violet
            };
            Color avatarColor = slotColors[Mathf.Clamp(slotIndex, 0, slotColors.Length - 1)];

            foreach (var r in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.name.StartsWith("Eye", StringComparison.OrdinalIgnoreCase)) continue;
                if (r is MeshRenderer mr)
                {
                    mr.material.color = avatarColor;
                }
            }

            return avatar;
        }
    }
}
