using System.Collections.Generic;
using UnityEngine;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Tạo không gian 3D nền cho Sảnh Chờ (SCN_Lobby):
    /// - Tán xạ các mảnh puzzle 3D lơ lửng trước ống kính Camera
    /// - Hiệu ứng bồng bềnh không trọng lực (gentle floating & tumbling)
    /// - Chuyển động Camera Cinematic nhẹ nhàng tạo chiều sâu
    /// </summary>
    public class Floating3DPuzzleBackground : MonoBehaviour
    {
        [Header("=== Nguồn Mảnh Puzzle 3D ===")]
        [Tooltip("Prefab chứa các mảnh puzzle thực tế (ví dụ: sunset.prefab)")]
        [SerializeField] private GameObject puzzleSourcePrefab;

        [Header("=== Số Lượng & Phân Bố ===")]
        [SerializeField] private int pieceCount = 22;
        [SerializeField] private Vector3 spawnSpread = new Vector3(14f, 8f, 7f);
        [SerializeField] private float centerDistance = 7f;
        [SerializeField] private float minScale = 0.8f;
        [SerializeField] private float maxScale = 1.8f;

        [Header("=== Chuyển Động Bồng Bềnh (Zero Gravity) ===")]
        [SerializeField] private float floatSpeed = 0.8f;
        [SerializeField] private float floatDistance = 0.45f;
        [SerializeField] private float minRotationSpeed = 4f;
        [SerializeField] private float maxRotationSpeed = 16f;

        [Header("=== Camera Cinematic Drift ===")]
        [SerializeField] private bool enableCameraDrift = true;
        [SerializeField] private float cameraDriftSpeed = 0.25f;
        [SerializeField] private float cameraDriftAmount = 0.35f;

        private class FloatingPiece
        {
            public Transform transform;
            public Vector3 initialPosition;
            public Vector3 rotationAxis;
            public float rotationSpeed;
            public float speedMultiplier;
            public float phaseX;
            public float phaseY;
            public float phaseZ;
        }

        private readonly List<FloatingPiece> _pieces = new List<FloatingPiece>();
        private Camera _mainCam;
        private Vector3 _camInitialPos;
        private Quaternion _camInitialRot;

        private void Start()
        {
            _mainCam = Camera.main;
            if (_mainCam != null)
            {
                _camInitialPos = _mainCam.transform.position;
                _camInitialRot = _mainCam.transform.rotation;
            }

            SpawnFloatingPieces();
        }

        private void SpawnFloatingPieces()
        {
            List<GameObject> templatePieces = CollectTemplatePieces();
            if (templatePieces == null || templatePieces.Count == 0)
            {
                Debug.LogWarning("[Floating3DPuzzleBackground] Không tìm thấy mẫu mảnh puzzle nào để sinh!");
                return;
            }

            Vector3 camPos = _mainCam != null ? _mainCam.transform.position : Vector3.zero;
            Vector3 camForward = _mainCam != null ? _mainCam.transform.forward : Vector3.forward;
            Vector3 centerPos = camPos + camForward * centerDistance;

            for (int i = 0; i < pieceCount; i++)
            {
                GameObject template = templatePieces[Random.Range(0, templatePieces.Count)];
                GameObject pieceGo = Instantiate(template, transform);
                pieceGo.name = $"FloatingPiece_{i:D2}";
                pieceGo.SetActive(true);

                // Tắt Collider để hoàn toàn không cản trở tương tác hay vật lý
                var colliders = pieceGo.GetComponentsInChildren<Collider>();
                for (int c = 0; c < colliders.Length; c++)
                {
                    colliders[c].enabled = false;
                }

                // Tắt các script gameplay nếu có đính kèm trên piece
                var monoBehaviours = pieceGo.GetComponentsInChildren<MonoBehaviour>();
                for (int m = 0; m < monoBehaviours.Length; m++)
                {
                    if (monoBehaviours[m] != this)
                    {
                        monoBehaviours[m].enabled = false;
                    }
                }

                // Vị trí phân tán ngẫu nhiên hình hộp trước camera
                Vector3 randomOffset = new Vector3(
                    Random.Range(-spawnSpread.x * 0.5f, spawnSpread.x * 0.5f),
                    Random.Range(-spawnSpread.y * 0.5f, spawnSpread.y * 0.5f),
                    Random.Range(-spawnSpread.z * 0.5f, spawnSpread.z * 0.5f)
                );

                Vector3 startPos = centerPos + randomOffset;
                pieceGo.transform.position = startPos;

                // Xoay góc ngẫu nhiên ban đầu
                pieceGo.transform.rotation = Random.rotation;

                // Scale ngẫu nhiên (mảnh gần to, mảnh xa nhỏ tạo chiều sâu)
                float scale = Random.Range(minScale, maxScale);
                pieceGo.transform.localScale = Vector3.one * scale;

                _pieces.Add(new FloatingPiece
                {
                    transform = pieceGo.transform,
                    initialPosition = startPos,
                    rotationAxis = Random.onUnitSphere,
                    rotationSpeed = Random.Range(minRotationSpeed, maxRotationSpeed),
                    speedMultiplier = Random.Range(0.75f, 1.25f),
                    phaseX = Random.Range(0f, Mathf.PI * 2f),
                    phaseY = Random.Range(0f, Mathf.PI * 2f),
                    phaseZ = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        private List<GameObject> CollectTemplatePieces()
        {
            var result = new List<GameObject>();

            if (puzzleSourcePrefab != null)
            {
                // Thu thập các object con có MeshRenderer
                var renderers = puzzleSourcePrefab.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    result.Add(renderers[i].gameObject);
                }

                if (result.Count > 0) return result;
            }

            // Fallback an toàn: Tìm trong Resources hoặc tự tạo mảnh cơ bản
            return result;
        }

        private void Update()
        {
            float time = Time.time * floatSpeed;

            // 1. Cập nhật nhấp nhô & tự xoay cho từng mảnh ghép
            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i];
                if (piece.transform == null) continue;

                float t = time * piece.speedMultiplier;

                // Chuyển động hình sóng Sin/Cos mượt mà 3 trục
                Vector3 bobOffset = new Vector3(
                    Mathf.Sin(t + piece.phaseX) * floatDistance * 0.6f,
                    Mathf.Cos(t * 1.1f + piece.phaseY) * floatDistance,
                    Mathf.Sin(t * 0.8f + piece.phaseZ) * floatDistance * 0.4f
                );

                piece.transform.position = piece.initialPosition + bobOffset;

                // Tự xoay êm ái
                piece.transform.Rotate(piece.rotationAxis * (piece.rotationSpeed * Time.deltaTime), Space.Self);
            }

            // 2. Chuyển động trôi nhẹ của Camera (Cinematic Drift)
            if (enableCameraDrift && _mainCam != null)
            {
                float camT = Time.time * cameraDriftSpeed;
                Vector3 camOffset = new Vector3(
                    Mathf.Sin(camT) * cameraDriftAmount,
                    Mathf.Cos(camT * 0.75f) * (cameraDriftAmount * 0.6f),
                    0f
                );

                _mainCam.transform.position = _camInitialPos + camOffset;
                _mainCam.transform.rotation = _camInitialRot * Quaternion.Euler(
                    Mathf.Cos(camT * 0.8f) * 0.8f,
                    Mathf.Sin(camT * 0.6f) * 1.2f,
                    0f
                );
            }
        }
    }
}
