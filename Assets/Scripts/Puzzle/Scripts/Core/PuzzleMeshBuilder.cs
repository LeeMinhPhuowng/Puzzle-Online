using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Core
{
    public static class PuzzleMeshBuilder
    {
        /// <summary>
        /// Xây dựng Mesh 3D hoàn chỉnh cho 1 mảnh puzzle với 2 Submeshes:
        /// Submesh 0: Mặt trên (Chứa tranh chính, UV chuẩn)
        /// Submesh 1: Các cạnh thành và mặt đáy (Chất liệu giấy bồi / gỗ)
        /// </summary>
        public static Mesh BuildPieceMesh(PuzzlePiecePolygon piece, float thickness, float bevelRadius = 0.0008f)
        {
            var poly = piece.localPolygon;
            int n = poly.Count;
            if (n < 3) return null;

            Mesh mesh = new Mesh();
            mesh.name = $"Piece_{piece.row}_{piece.col}";

            float halfThick = thickness * 0.5f;

            // Tính toán tam giác 2D của mặt phẳng
            int[] rawTriangles = EarClippingTriangulator.Triangulate(poly);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            List<int> submesh0Triangles = new List<int>(); // Face (Tranh)
            List<int> submesh1Triangles = new List<int>(); // Sides & Back

            // -------------------------------------------------------------
            // 1. MẶT TRÊN (TOP FACE - SUBMESH 0)
            // -------------------------------------------------------------
            int topBaseIndex = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                // 2D x -> 3D X, 2D y -> 3D Z, Y là chiều cao
                vertices.Add(new Vector3(poly[i].x, halfThick, poly[i].y));
                normals.Add(Vector3.up);
                uvs.Add(piece.uvs[i]);
            }

            // Đảo thứ tự (0, 2, 1) để normal hướng lên trên (+Y, Clockwise khi nhìn từ trên xuống)
            for (int i = 0; i < rawTriangles.Length; i += 3)
            {
                submesh0Triangles.Add(topBaseIndex + rawTriangles[i]);
                submesh0Triangles.Add(topBaseIndex + rawTriangles[i + 2]);
                submesh0Triangles.Add(topBaseIndex + rawTriangles[i + 1]);
            }

            // -------------------------------------------------------------
            // 2. MẶT DƯỚI (BOTTOM FACE - SUBMESH 1)
            // -------------------------------------------------------------
            int botBaseIndex = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(poly[i].x, -halfThick, poly[i].y));
                normals.Add(Vector3.down);
                // UV mặt đáy (dùng tỉ lệ local để tiling texture giấy bồi)
                uvs.Add(new Vector2(poly[i].x * 5f, poly[i].y * 5f));
            }

            // Thứ tự nguyên bản (0, 1, 2) để normal hướng xuống dưới (-Y)
            for (int i = 0; i < rawTriangles.Length; i += 3)
            {
                submesh1Triangles.Add(botBaseIndex + rawTriangles[i]);
                submesh1Triangles.Add(botBaseIndex + rawTriangles[i + 1]);
                submesh1Triangles.Add(botBaseIndex + rawTriangles[i + 2]);
            }

            // -------------------------------------------------------------
            // 3. CÁC CẠNH THÀNH (EXTRUDED SIDE WALLS - SUBMESH 1)
            // -------------------------------------------------------------
            float accumulatedU = 0f;

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;

                Vector2 p0 = poly[i];
                Vector2 p1 = poly[next];

                float segLength = Vector2.Distance(p0, p1);

                // Tính pháp tuyến ngoài của đoạn cạnh 2D
                Vector2 edgeDir = (p1 - p0).normalized;
                Vector2 edgeNormal2D = new Vector2(edgeDir.y, -edgeDir.x); // Quay 90 độ ra ngoài
                Vector3 wallNormal = new Vector3(edgeNormal2D.x, 0f, edgeNormal2D.y);

                int wallBase = vertices.Count;

                // 4 đỉnh của mặt tường chữ nhật:
                // v0: Đỉnh trên trái, v1: Đỉnh trên phải
                // v2: Đỉnh dưới trái, v3: Đỉnh dưới phải
                vertices.Add(new Vector3(p0.x, halfThick, p0.y));
                vertices.Add(new Vector3(p1.x, halfThick, p1.y));
                vertices.Add(new Vector3(p0.x, -halfThick, p0.y));
                vertices.Add(new Vector3(p1.x, -halfThick, p1.y));

                normals.Add(wallNormal);
                normals.Add(wallNormal);
                normals.Add(wallNormal);
                normals.Add(wallNormal);

                float u0 = accumulatedU;
                float u1 = accumulatedU + segLength;
                accumulatedU = u1;

                uvs.Add(new Vector2(u0, 1f));
                uvs.Add(new Vector2(u1, 1f));
                uvs.Add(new Vector2(u0, 0f));
                uvs.Add(new Vector2(u1, 0f));

                // 2 tam giác của quad thành bên:
                // Quad: v0(top-left), v1(top-right), v2(bot-left), v3(bot-right)
                // Tri 1: v0 -> v1 -> v3
                // Tri 2: v0 -> v3 -> v2
                submesh1Triangles.Add(wallBase + 0);
                submesh1Triangles.Add(wallBase + 1);
                submesh1Triangles.Add(wallBase + 3);

                submesh1Triangles.Add(wallBase + 0);
                submesh1Triangles.Add(wallBase + 3);
                submesh1Triangles.Add(wallBase + 2);
            }

            // Gán dữ liệu vào Mesh
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);

            mesh.subMeshCount = 2;
            mesh.SetTriangles(submesh0Triangles, 0);
            mesh.SetTriangles(submesh1Triangles, 1);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            return mesh;
        }
    }
}
