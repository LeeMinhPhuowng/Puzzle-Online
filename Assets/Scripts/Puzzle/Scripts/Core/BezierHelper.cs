using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Core
{
    public static class BezierHelper
    {
        /// <summary>
        /// Tính điểm trên đường cong Bézier bậc 3 tại tham số t [0, 1].
        /// </summary>
        public static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// Sinh chuỗi điểm 2D dọc theo một cạnh Jigsaw từ startPoint đến endPoint.
        /// tabDir: +1 (tab nhô ra trái/trên), -1 (hole hõm vào), 0 (cạnh phẳng viền ngoài).
        /// </summary>
        public static List<Vector2> GenerateJigsawEdgePoints(
            Vector2 startPoint,
            Vector2 endPoint,
            int tabDir,
            float tabHeightRatio = 0.22f,
            float neckWidthRatio = 0.16f,
            float headWidthRatio = 0.32f,
            float centerOffsetRatio = 0f,
            int samplesPerSegment = 6)
        {
            List<Vector2> points = new List<Vector2>();

            // Nếu là cạnh biên phẳng
            if (tabDir == 0)
            {
                points.Add(startPoint);
                points.Add(endPoint);
                return points;
            }

            Vector2 dir = endPoint - startPoint;
            float length = dir.magnitude;
            if (length < 0.0001f)
            {
                points.Add(startPoint);
                return points;
            }

            Vector2 unitTangent = dir / length;
            // Vector pháp tuyến vuông góc với cạnh (hướng sang phía tab lồi khi tabDir = +1)
            Vector2 unitNormal = new Vector2(-unitTangent.y, unitTangent.x) * tabDir;

            // Tọa độ cục bộ dọc theo cạnh (từ 0 đến length)
            float mid = length * (0.5f + Mathf.Clamp(centerOffsetRatio, -0.1f, 0.1f));
            float tabH = length * tabHeightRatio;
            float neckW = length * (neckWidthRatio * 0.5f);
            float headW = length * (headWidthRatio * 0.5f);

            // Các điểm mốc chính dọc theo cạnh
            float baseLeft = mid - headW * 1.3f;
            float baseRight = mid + headW * 1.3f;
            float neckLeft = mid - neckW;
            float neckRight = mid + neckW;

            Vector2 p0 = startPoint;
            Vector2 p1 = startPoint + unitTangent * baseLeft;
            points.Add(p0);
            points.Add(p1);

            // Cổ trái -> Đỉnh tab
            Vector2 pNeckLeft = startPoint + unitTangent * neckLeft + unitNormal * (tabH * 0.15f);
            Vector2 pHeadLeft = startPoint + unitTangent * (mid - headW) + unitNormal * (tabH * 0.7f);
            Vector2 pTop = startPoint + unitTangent * mid + unitNormal * tabH;
            Vector2 pHeadRight = startPoint + unitTangent * (mid + headW) + unitNormal * (tabH * 0.7f);
            Vector2 pNeckRight = startPoint + unitTangent * neckRight + unitNormal * (tabH * 0.15f);
            Vector2 pBaseRight = startPoint + unitTangent * baseRight;

            // Đoạn Bézier 1: Chân cổ trái lên đầu bulb trái
            Vector2 cp1 = p1 + unitTangent * (neckW * 0.5f);
            Vector2 cp2 = pNeckLeft - unitTangent * (neckW * 0.2f);
            SampleCubicBezier(points, p1, cp1, cp2, pNeckLeft, samplesPerSegment);

            // Đoạn Bézier 2: Vòng cung đỉnh đầu bulb (từ neckLeft qua pTop đến neckRight)
            Vector2 cp3 = pNeckLeft + unitNormal * (tabH * 0.45f) - unitTangent * (headW * 0.3f);
            Vector2 cp4 = pTop - unitTangent * (headW * 0.4f);
            SampleCubicBezier(points, pNeckLeft, cp3, cp4, pTop, samplesPerSegment);

            Vector2 cp5 = pTop + unitTangent * (headW * 0.4f);
            Vector2 cp6 = pNeckRight + unitNormal * (tabH * 0.45f) + unitTangent * (headW * 0.3f);
            SampleCubicBezier(points, pTop, cp5, cp6, pNeckRight, samplesPerSegment);

            // Đoạn Bézier 3: Cổ phải về baseRight
            Vector2 cp7 = pNeckRight + unitTangent * (neckW * 0.2f);
            Vector2 cp8 = pBaseRight - unitTangent * (neckW * 0.5f);
            SampleCubicBezier(points, pNeckRight, cp7, cp8, pBaseRight, samplesPerSegment);

            // Đoạn cuối: Từ baseRight về endPoint
            points.Add(endPoint);

            return points;
        }

        private static void SampleCubicBezier(List<Vector2> list, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
        {
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                list.Add(EvaluateCubic(p0, p1, p2, p3, t));
            }
        }
    }
}
