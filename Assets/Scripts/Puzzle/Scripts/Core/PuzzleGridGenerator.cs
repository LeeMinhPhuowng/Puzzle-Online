using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleSystem.Core
{
    public class PuzzlePiecePolygon
    {
        public int row;
        public int col;
        public int pieceId;

        public Vector2 centerPos; // Tọa độ tâm mảnh trong hệ toạ độ phẳng của bàn
        public List<Vector2> worldPolygon = new List<Vector2>(); // Toạ độ toàn cục trên bàn
        public List<Vector2> localPolygon = new List<Vector2>(); // Toạ độ tương đối so với centerPos
        public List<Vector2> uvs = new List<Vector2>();          // UV tương ứng trên bức tranh [0, 1]

        public int[] neighborIds = new int[4]; // 0: Top, 1: Right, 2: Bottom, 3: Left (-1 nếu là cạnh biên)
    }

    public static class PuzzleGridGenerator
    {
        /// <summary>
        /// Sinh toàn bộ danh sách polygon 2D của các mảnh ghép dựa theo cấu hình.
        /// Đảm bảo các cạnh giáp nhau dùng chung dữ liệu spline, triệt tiêu khe hở.
        /// </summary>
        public static List<PuzzlePiecePolygon> GeneratePolygons(PuzzleConfiguration config)
        {
            int rows = config.rows;
            int cols = config.columns;
            float cellW = config.CellWidth;
            float cellH = config.CellHeight;

            // Khởi tạo Random theo seed
            int effectiveSeed = config.randomizeSeed ? UnityEngine.Random.Range(10000, 999999) : config.seed;
            var rand = new System.Random(effectiveSeed);

            // 1. CẠNH NGANG (Horizontal Edges):
            // Có (rows - 1) hàng cạnh ngang bên trong.
            // Cạnh ngang hIndex tại y = (rows - 1 - hIndex) * cellH (giữa row hIndex và row hIndex + 1)
            // Điểm bắt đầu từ X = c * cellW đến X = (c + 1) * cellW
            // Chiều chuẩn: đi từ Tây sang Đông (Left -> Right)
            List<Vector2>[,] hEdges = new List<Vector2>[rows - 1, cols];
            for (int r = 0; r < rows - 1; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int dir = rand.Next(0, 2) == 0 ? 1 : -1;
                    float offsetRatio = (float)(rand.NextDouble() * 0.16 - 0.08); // Lệch tâm ngẫu nhiên +-8%
                    float tabHVar = config.tabHeightRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));
                    float headWVar = config.headWidthRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));

                    float y = (rows - 1 - r) * cellH;
                    Vector2 start = new Vector2(c * cellW, y);
                    Vector2 end = new Vector2((c + 1) * cellW, y);

                    hEdges[r, c] = BezierHelper.GenerateJigsawEdgePoints(
                        start, end, dir,
                        tabHVar,
                        config.neckWidthRatio,
                        headWVar,
                        offsetRatio,
                        config.samplesPerSegment
                    );
                }
            }

            // 2. CẠNH DỌC (Vertical Edges):
            // Có (cols - 1) cột cạnh dọc bên trong.
            // Cạnh dọc vIndex tại x = (c + 1) * cellW (giữa col c và col c + 1)
            // Điểm bắt đầu từ Y = (rows - 1 - r) * cellH đến Y = (rows - r) * cellH
            // Chiều chuẩn: đi từ Nam lên Bắc (Bottom -> Top)
            List<Vector2>[,] vEdges = new List<Vector2>[rows, cols - 1];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols - 1; c++)
                {
                    int dir = rand.Next(0, 2) == 0 ? 1 : -1;
                    float offsetRatio = (float)(rand.NextDouble() * 0.16 - 0.08);
                    float tabHVar = config.tabHeightRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));
                    float headWVar = config.headWidthRatio * (float)(1f + (rand.NextDouble() * 0.16 - 0.08));

                    float x = (c + 1) * cellW;
                    float yMin = (rows - 1 - r) * cellH;
                    float yMax = (rows - r) * cellH;
                    Vector2 start = new Vector2(x, yMin);
                    Vector2 end = new Vector2(x, yMax);

                    vEdges[r, c] = BezierHelper.GenerateJigsawEdgePoints(
                        start, end, dir,
                        tabHVar,
                        config.neckWidthRatio,
                        headWVar,
                        offsetRatio,
                        config.samplesPerSegment
                    );
                }
            }

            // 3. TỔ HỢP CÁC MẢNH GHÉP (Piece Polygons):
            List<PuzzlePiecePolygon> pieces = new List<PuzzlePiecePolygon>(rows * cols);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var piece = new PuzzlePiecePolygon
                    {
                        row = r,
                        col = c,
                        pieceId = r * cols + c
                    };

                    float xMin = c * cellW;
                    float xMax = (c + 1) * cellW;
                    float yMin = (rows - 1 - r) * cellH;
                    float yMax = (rows - r) * cellH;

                    Vector2 cornerBL = new Vector2(xMin, yMin);
                    Vector2 cornerBR = new Vector2(xMax, yMin);
                    Vector2 cornerTR = new Vector2(xMax, yMax);
                    Vector2 cornerTL = new Vector2(xMin, yMax);

                    piece.centerPos = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);

                    // Thiết lập neighbor IDs (Top, Right, Bottom, Left)
                    piece.neighborIds[0] = (r > 0) ? (r - 1) * cols + c : -1;
                    piece.neighborIds[1] = (c < cols - 1) ? r * cols + (c + 1) : -1;
                    piece.neighborIds[2] = (r < rows - 1) ? (r + 1) * cols + c : -1;
                    piece.neighborIds[3] = (c > 0) ? r * cols + (c - 1) : -1;

                    // Lắp ghép 4 cạnh theo chiều Counter-Clockwise:
                    // 1. Cạnh Đáy (South): BL -> BR (Tây sang Đông)
                    List<Vector2> southEdge;
                    if (r == rows - 1)
                    {
                        southEdge = new List<Vector2> { cornerBL, cornerBR };
                    }
                    else
                    {
                        // Dùng chung cạnh ngang với mảnh bên dưới (r+1)
                        southEdge = hEdges[r, c]; // Chiều chuẩn đã là BL -> BR
                    }

                    // 2. Cạnh Phải (East): BR -> TR (Nam lên Bắc)
                    List<Vector2> eastEdge;
                    if (c == cols - 1)
                    {
                        eastEdge = new List<Vector2> { cornerBR, cornerTR };
                    }
                    else
                    {
                        // Dùng chung cạnh dọc với mảnh bên phải (c+1)
                        eastEdge = vEdges[r, c]; // Chiều chuẩn đã là BR -> TR
                    }

                    // 3. Cạnh Đỉnh (North): TR -> TL (Đông sang Tây)
                    List<Vector2> northEdge;
                    if (r == 0)
                    {
                        northEdge = new List<Vector2> { cornerTR, cornerTL };
                    }
                    else
                    {
                        // Dùng chung cạnh ngang với mảnh bên trên (r-1), đảo chiều (TR -> TL)
                        var shared = hEdges[r - 1, c];
                        northEdge = new List<Vector2>(shared);
                        northEdge.Reverse();
                    }

                    // 4. Cạnh Trái (West): TL -> BL (Bắc xuống Nam)
                    List<Vector2> westEdge;
                    if (c == 0)
                    {
                        westEdge = new List<Vector2> { cornerTL, cornerBL };
                    }
                    else
                    {
                        // Dùng chung cạnh dọc với mảnh bên trái (c-1), đảo chiều (TL -> BL)
                        var shared = vEdges[r, c - 1];
                        westEdge = new List<Vector2>(shared);
                        westEdge.Reverse();
                    }

                    // Gộp 4 cạnh vào vòng đa giác khép kín (bỏ điểm trùng ở các góc tiếp giáp)
                    List<Vector2> fullLoop = new List<Vector2>();
                    AppendEdge(fullLoop, southEdge);
                    AppendEdge(fullLoop, eastEdge);
                    AppendEdge(fullLoop, northEdge);
                    AppendEdge(fullLoop, westEdge);

                    // Đảm bảo không bị trùng lặp điểm đầu và cuối
                    if (fullLoop.Count > 1 && Vector2.Distance(fullLoop[0], fullLoop[fullLoop.Count - 1]) < 0.0001f)
                    {
                        fullLoop.RemoveAt(fullLoop.Count - 1);
                    }

                    piece.worldPolygon = fullLoop;

                    // Tính local polygon (dời về gốc toạ độ tâm của mảnh) và UV
                    float boardW = config.boardWidth;
                    float boardH = config.boardHeight;

                    for (int i = 0; i < fullLoop.Count; i++)
                    {
                        Vector2 pt = fullLoop[i];
                        piece.localPolygon.Add(pt - piece.centerPos);

                        // UV chuẩn hoá theo toàn bộ bức ảnh [0, 1]
                        float u = Mathf.Clamp01(pt.x / boardW);
                        float v = Mathf.Clamp01(pt.y / boardH);
                        piece.uvs.Add(new Vector2(u, v));
                    }

                    pieces.Add(piece);
                }
            }

            return pieces;
        }

        private static void AppendEdge(List<Vector2> poly, List<Vector2> edge)
        {
            for (int i = 0; i < edge.Count; i++)
            {
                if (poly.Count > 0 && Vector2.Distance(poly[poly.Count - 1], edge[i]) < 0.0001f)
                    continue; // Bỏ qua nếu trùng điểm trước đó
                poly.Add(edge[i]);
            }
        }
    }
}
