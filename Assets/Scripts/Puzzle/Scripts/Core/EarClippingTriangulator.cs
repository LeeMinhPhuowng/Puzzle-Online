using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Core
{
    public static class EarClippingTriangulator
    {
        /// <summary>
        /// Phân rã đa giác 2D đơn thành danh sách chỉ số tam giác (Triangles) bằng giải thuật Ear Clipping.
        /// Trả về mảng chỉ số tương ứng với các đỉnh trong polygon.
        /// </summary>
        public static int[] Triangulate(IList<Vector2> polygon)
        {
            int n = polygon != null ? polygon.Count : 0;
            if (n < 3) return new int[0];

            List<int> indices = new List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);

            // Kiểm tra winding order: Nếu Clockwise (diện tích âm), đảo ngược danh sách chỉ số để chuẩn CCW
            if (ComputeSignedArea(polygon, indices) < 0)
            {
                indices.Reverse();
            }

            List<int> triangles = new List<int>((n - 2) * 3);

            int maxIterations = n * 4; // Phòng ngừa infinite loop nếu đa giác tự cắt hoặc suy biến
            int count = indices.Count;

            while (count > 3 && maxIterations-- > 0)
            {
                bool earFound = false;

                for (int i = 0; i < count; i++)
                {
                    int prevIdx = indices[(i - 1 + count) % count];
                    int currIdx = indices[i];
                    int nextIdx = indices[(i + 1) % count];

                    Vector2 a = polygon[prevIdx];
                    Vector2 b = polygon[currIdx];
                    Vector2 c = polygon[nextIdx];

                    // Phải là góc lồi (Convex)
                    if (!IsConvex(a, b, c))
                        continue;

                    // Kiểm tra xem có đỉnh nào khác nằm bên trong tam giác (a, b, c) hay không
                    bool hasPointInside = false;
                    for (int j = 0; j < count; j++)
                    {
                        int testIdx = indices[j];
                        if (testIdx == prevIdx || testIdx == currIdx || testIdx == nextIdx)
                            continue;

                        if (IsPointInTriangle(polygon[testIdx], a, b, c))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }

                    if (!hasPointInside)
                    {
                        // Cắt tai này
                        triangles.Add(prevIdx);
                        triangles.Add(currIdx);
                        triangles.Add(nextIdx);

                        indices.RemoveAt(i);
                        count--;
                        earFound = true;
                        break;
                    }
                }

                // Nếu không tìm được tai lồi lý tưởng do dung sai số thực, cắt tai có góc nhỏ nhất
                if (!earFound && count > 3)
                {
                    int fallbackIdx = 0;
                    triangles.Add(indices[(fallbackIdx - 1 + count) % count]);
                    triangles.Add(indices[fallbackIdx]);
                    triangles.Add(indices[(fallbackIdx + 1) % count]);
                    indices.RemoveAt(fallbackIdx);
                    count--;
                }
            }

            // Tam giác cuối cùng còn lại
            if (count == 3)
            {
                triangles.Add(indices[0]);
                triangles.Add(indices[1]);
                triangles.Add(indices[2]);
            }

            return triangles.ToArray();
        }

        private static float ComputeSignedArea(IList<Vector2> poly, List<int> idxs)
        {
            float area = 0f;
            int c = idxs.Count;
            for (int i = 0; i < c; i++)
            {
                Vector2 p1 = poly[idxs[i]];
                Vector2 p2 = poly[idxs[(i + 1) % c]];
                area += (p1.x * p2.y - p2.x * p1.y);
            }
            return area * 0.5f;
        }

        private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            // Vector ab và bc quay sang trái (cross product z > 0 trong hệ toạ độ 2D chuẩn CCW)
            return ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x)) >= -1e-6f;
        }

        private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float crossA = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            float crossB = (c.x - b.x) * (p.y - b.y) - (c.y - b.y) * (p.x - b.x);
            float crossC = (a.x - c.x) * (p.y - c.y) - (a.y - c.y) * (p.x - c.x);

            bool hasNeg = (crossA < 1e-6f) || (crossB < 1e-6f) || (crossC < 1e-6f);
            bool hasPos = (crossA > -1e-6f) || (crossB > -1e-6f) || (crossC > -1e-6f);

            return !(hasNeg && hasPos);
        }
    }
}
