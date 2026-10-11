using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Utility;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Quản lý mặt bàn, ranh giới bàn chơi, tự động căn chỉnh và các bức tường vô hình chống rơi mảnh ghép.
    /// </summary>
    public class PuzzleTableManager : MonoBehaviour
    {
        [Header("Table Alignment & Bounds")]
        [Tooltip("Transform của chiếc bàn trong Scene (nếu để trống sẽ tự động dò tìm)")]
        public Transform tableTransform;

        private Bounds cachedTableBounds;
        private bool hasTableBounds = false;

        public Bounds CachedTableBounds => cachedTableBounds;
        public bool HasTableBounds => hasTableBounds;

        /// <summary>
        /// Tự động phát hiện chiếc bàn trong Scene dựa trên tên hoặc cấu trúc Positions.
        /// </summary>
        public void AutoDetectSceneEnvironment()
        {
            var positionsParent = SeatFinder.FindPositionsParent();

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

                if (tableTransform == null && positionsParent != null)
                {
                    tableTransform = positionsParent.transform;
                }
            }
        }

        /// <summary>
        /// Căn chỉnh bàn cờ nằm ngang hoàn hảo trên bề mặt bàn cao nhất.
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

            Bounds tableBounds = new Bounds();
            bool foundBounds = false;

            var colliders = tableTransform.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                if (col.GetComponent<PuzzlePiece>() != null) continue;

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

            float topY = foundBounds ? tableBounds.max.y : tableTransform.position.y;
            Vector3 centerPos = foundBounds
                ? new Vector3(tableBounds.center.x, topY + 0.002f, tableBounds.center.z)
                : new Vector3(tableTransform.position.x, topY + 0.002f, tableTransform.position.z);

            cachedTableBounds = tableBounds;
            hasTableBounds = foundBounds;

            transform.position = centerPos;

            SetupTableEdgeColliders();
        }

        /// <summary>
        /// Tạo 4 thành chắn BoxCollider vô hình quanh mép bàn để chặn vật lý / kéo mảnh ra khỏi bàn.
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

        /// <summary>
        /// Giữ vị trí trong phạm vi an toàn của mặt bàn (không cho mảnh trượt ra ngoài mép bàn).
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
        /// Dịch chuyển tâm GameObject cha về tâm của các mảnh ghép hiện tại.
        /// </summary>
        [ContextMenu("🎯 Khớp Khung Bàn Cờ Vào Các Mảnh Ghép (Snap Board To Pieces)")]
        public void SnapBoardToPieces(List<PuzzlePiece> pieces)
        {
            if (pieces == null || pieces.Count == 0)
            {
                var scenePieces = GetComponentsInChildren<PuzzlePiece>();
                if (scenePieces == null || scenePieces.Length == 0)
                {
                    Debug.LogWarning("[PuzzleTableManager] Không tìm thấy mảnh ghép nào trong bàn cờ!");
                    return;
                }
                pieces = new List<PuzzlePiece>(scenePieces);
            }

            Vector3 sumPos = Vector3.zero;
            foreach (var p in pieces)
            {
                if (p != null) sumPos += p.transform.position;
            }
            Vector3 newCenter = sumPos / pieces.Count;

            foreach (var p in pieces)
            {
                if (p != null) p.transform.SetParent(null, true);
            }

            transform.position = newCenter;

            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

            foreach (var p in pieces)
            {
                if (p != null) p.transform.SetParent(transform, true);
            }

            Debug.Log($"<color=green><b>[PuzzleTableManager] Đã khớp khung bàn cờ ({pieces.Count} mảnh) về đúng tâm!</b></color>");
        }
    }
}
