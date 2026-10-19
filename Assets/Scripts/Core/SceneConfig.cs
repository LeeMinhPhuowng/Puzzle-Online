using System;
using UnityEngine;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Quy định cấu hình không gian phòng chơi (Scene Config):
    /// - Giới hạn tọa độ min/max X/Z cho người chơi (không đi xuyên tường / ra ngoài map).
    /// - Giới hạn tọa độ min/max X/Z cho các mảnh ghép (trên bàn chơi và khu vực quanh bàn).
    /// - Vùng sinh các hộp quà (Gift Boxes) rải rác trong phạm vi người chơi di chuyển được.
    /// </summary>
    public class SceneConfig : MonoBehaviour
    {
        private static SceneConfig _instance;
        public static SceneConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<SceneConfig>();
                    if (_instance == null)
                    {
                        var prefab = Resources.Load<GameObject>("SceneConfig");
                        if (prefab != null)
                        {
                            var go = Instantiate(prefab);
                            go.name = "[SceneConfig]";
                            _instance = go.GetComponent<SceneConfig>();
                        }
                        else
                        {
                            var go = new GameObject("[SceneConfig]");
                            _instance = go.AddComponent<SceneConfig>();
                        }
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [Header("Tùy Chọn Tọa Độ Cụ Thể (Explicit Bounds)")]
        [Tooltip("Bật nếu muốn nhập trực tiếp min/max X/Z tuyệt đối")]
        public bool useExplicitBounds = false;

        [Header("Giới Hạn Di Chuyển Người Chơi (Player Bounds)")]
        public float playerMinX = -8f;
        public float playerMaxX = 8f;
        public float playerMinZ = -8f;
        public float playerMaxZ = 8f;

        [Header("Giới Hạn Vị Trí Mảnh Ghép (Piece Bounds)")]
        public float pieceMinX = -2.5f;
        public float pieceMaxX = 2.5f;
        public float pieceMinZ = -2.5f;
        public float pieceMaxZ = 2.5f;

        [Header("Vùng Sinh Hộp Quà (Gift Spawn Bounds)")]
        public float giftMinX = -6f;
        public float giftMaxX = 6f;
        public float giftMinZ = -6f;
        public float giftMaxZ = 6f;
        public float giftSpawnHeightOffset = 0.15f;

        [Header("Tùy Chọn Tự Động Định Tâm Theo Bàn/Phòng (Relative Bounds)")]
        [Tooltip("Nếu không bật Explicit Bounds, kích thước sẽ lấy quanh tâm phòng / bàn chơi")]
        public Transform centerReference;
        public Vector2 playerAreaSize = new Vector2(14f, 14f);
        public Vector2 pieceAreaSize = new Vector2(3.5f, 3.5f);
        public Vector2 giftSpawnAreaSize = new Vector2(11f, 11f);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;
            EnsureCenterReference();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void EnsureCenterReference()
        {
            if (centerReference == null)
            {
                // Tự động tìm bàn chơi hoặc cụm vị trí ghế
                var board = FindFirstObjectByType<PuzzleSystem.Gameplay.PuzzleBoard>();
                if (board != null) centerReference = board.transform;
                else
                {
                    var posParent = GameObject.Find("Positions") ?? GameObject.Find("positions");
                    if (posParent != null) centerReference = posParent.transform;
                }
            }
        }

        public (float minX, float maxX, float minZ, float maxZ) GetPlayerBounds()
        {
            if (useExplicitBounds) return (playerMinX, playerMaxX, playerMinZ, playerMaxZ);
            EnsureCenterReference();
            Vector3 c = centerReference != null ? centerReference.position : Vector3.zero;
            return (c.x - playerAreaSize.x * 0.5f, c.x + playerAreaSize.x * 0.5f,
                    c.z - playerAreaSize.y * 0.5f, c.z + playerAreaSize.y * 0.5f);
        }

        public (float minX, float maxX, float minZ, float maxZ) GetPieceBounds()
        {
            if (useExplicitBounds) return (pieceMinX, pieceMaxX, pieceMinZ, pieceMaxZ);
            EnsureCenterReference();
            Vector3 c = centerReference != null ? centerReference.position : Vector3.zero;
            return (c.x - pieceAreaSize.x * 0.5f, c.x + pieceAreaSize.x * 0.5f,
                    c.z - pieceAreaSize.y * 0.5f, c.z + pieceAreaSize.y * 0.5f);
        }

        public (float minX, float maxX, float minZ, float maxZ) GetGiftBounds()
        {
            if (useExplicitBounds) return (giftMinX, giftMaxX, giftMinZ, giftMaxZ);
            EnsureCenterReference();
            Vector3 c = centerReference != null ? centerReference.position : Vector3.zero;
            return (c.x - giftSpawnAreaSize.x * 0.5f, c.x + giftSpawnAreaSize.x * 0.5f,
                    c.z - giftSpawnAreaSize.y * 0.5f, c.z + giftSpawnAreaSize.y * 0.5f);
        }

        public Vector3 ClampPlayerPosition(Vector3 pos)
        {
            var b = GetPlayerBounds();
            pos.x = Mathf.Clamp(pos.x, b.minX, b.maxX);
            pos.z = Mathf.Clamp(pos.z, b.minZ, b.maxZ);
            return pos;
        }

        public Vector3 ClampPiecePosition(Vector3 pos)
        {
            var b = GetPieceBounds();
            pos.x = Mathf.Clamp(pos.x, b.minX, b.maxX);
            pos.z = Mathf.Clamp(pos.z, b.minZ, b.maxZ);
            return pos;
        }

        public bool IsInsidePlayerBounds(Vector3 pos)
        {
            var b = GetPlayerBounds();
            return pos.x >= b.minX && pos.x <= b.maxX && pos.z >= b.minZ && pos.z <= b.maxZ;
        }

        public Vector3 GetRandomGiftSpawnPosition()
        {
            var b = GetGiftBounds();
            float x = UnityEngine.Random.Range(b.minX, b.maxX);
            float z = UnityEngine.Random.Range(b.minZ, b.maxZ);
            EnsureCenterReference();
            float baseY = centerReference != null ? centerReference.position.y : 0f;

            // Dò độ cao sàn (Ground raycast)
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0 && Physics.Raycast(new Vector3(x, baseY + 20f, z), Vector3.down,
                out var hit, 40f, 1 << groundLayer, QueryTriggerInteraction.Ignore))
            {
                baseY = hit.point.y;
            }

            return new Vector3(x, baseY + giftSpawnHeightOffset, z);
        }

        private void OnDrawGizmos()
        {
            DrawBounds(0.35f);
        }

        private void OnDrawGizmosSelected()
        {
            DrawBounds(0.9f);
        }

        private void DrawBounds(float alpha)
        {
            // Vẽ hộp xanh lá: Giới hạn người chơi
            var pb = GetPlayerBounds();
            Gizmos.color = new Color(0.2f, 1f, 0.2f, alpha);
            Vector3 pCenter = new Vector3((pb.minX + pb.maxX) * 0.5f, 1f, (pb.minZ + pb.maxZ) * 0.5f);
            Vector3 pSize = new Vector3(pb.maxX - pb.minX, 2.5f, pb.maxZ - pb.minZ);
            Gizmos.DrawWireCube(pCenter, pSize);

            // Vẽ hộp xanh dương: Giới hạn mảnh ghép
            var pieb = GetPieceBounds();
            Gizmos.color = new Color(0.2f, 0.5f, 1f, alpha);
            Vector3 pieCenter = new Vector3((pieb.minX + pieb.maxX) * 0.5f, 1f, (pieb.minZ + pieb.maxZ) * 0.5f);
            Vector3 pieSize = new Vector3(pieb.maxX - pieb.minX, 1.5f, pieb.maxZ - pieb.minZ);
            Gizmos.DrawWireCube(pieCenter, pieSize);

            // Vẽ hộp vàng: Vùng sinh quà
            var gb = GetGiftBounds();
            Gizmos.color = new Color(1f, 0.9f, 0.2f, alpha);
            Vector3 gCenter = new Vector3((gb.minX + gb.maxX) * 0.5f, 0.5f, (gb.minZ + gb.maxZ) * 0.5f);
            Vector3 gSize = new Vector3(gb.maxX - gb.minX, 1f, gb.maxZ - gb.minZ);
            Gizmos.DrawWireCube(gCenter, gSize);
        }
    }
}
