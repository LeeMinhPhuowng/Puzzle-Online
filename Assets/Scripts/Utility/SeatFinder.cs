
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PuzzleOnline.Utility
{
    /// <summary>
    /// Cung cấp các phương thức tìm kiếm và sắp xếp vị trí ghế
    /// trong Scene, đồng thời xác định slot của người chơi.
    /// </summary>
    public static class SeatFinder
    {
        /// <summary>
        /// Tìm các vị trí ghế theo thứ tự ưu tiên:
        /// RoomSeatManager, children của Positions, rồi tên dự phòng.
        /// </summary>
        public static List<Transform> FindSeats()
        {
            // Ưu tiên danh sách ghế do RoomSeatManager cung cấp.
            var seatManager = Object.FindFirstObjectByType<PuzzleSystem.Gameplay.RoomSeatManager>();
            if (seatManager != null)
            {
                var seats = seatManager.GetValidSeats();
                if (seats.Count > 0) return seats;
            }

            // Nếu không có ghế hợp lệ từ manager, tìm trong Positions.
            var result = new List<Transform>();
            var positionsParent = FindPositionsParent();
            if (positionsParent != null && positionsParent.childCount > 0)
            {
                for (int i = 0; i < positionsParent.childCount; i++)
                {
                    var child = positionsParent.GetChild(i);
                    if (child != null) result.Add(child);
                }

                SortByNumber(result);
                if (result.Count > 0) return result;
            }

            // Phương án dự phòng cho các vị trí được đặt tên riêng lẻ.
            string[] names = {
                "Position (1)", "Position (2)", "Position (3)", "Position (4)",
                "A", "A (1)", "A (2)", "A (3)"
            };

            foreach (var name in names)
            {
                var found = GameObject.Find(name);
                if (found != null && !result.Contains(found.transform))
                    result.Add(found.transform);
            }

            return result;
        }

        /// <summary>
        /// Tìm GameObject Positions hoặc positions và trả về Transform.
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public static Transform FindPositionsParent()
        {
            var obj = GameObject.Find("Positions") ?? GameObject.Find("positions");
            return obj != null ? obj.transform : null;
        }

        /// <summary>
        /// Lấy số nguyên đầu tiên xuất hiện trong chuỗi.
        /// Trả về 0 nếu chuỗi rỗng, null hoặc không chứa số hợp lệ.
        /// </summary>
        public static int ExtractNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var match = Regex.Match(s, @"\d+");
            return match.Success && int.TryParse(match.Value, out int val) ? val : 0;
        }

        /// <summary>
        /// Sắp xếp các Transform theo số trong tên tăng dần.
        /// Nếu số bằng nhau, sắp xếp theo tên không phân biệt hoa thường.
        /// </summary>
        public static void SortByNumber(List<Transform> list)
        {
            list.Sort((a, b) =>
            {
                int na = ExtractNumber(a.name);
                int nb = ExtractNumber(b.name);
                return na != nb
                    ? na.CompareTo(nb)
                    : string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        /// <summary>
        /// Tìm index đầu tiên của người chơi có username khớp
        /// với username cần tìm, không phân biệt hoa thường.
        /// Trả về 0 nếu đầu vào không hợp lệ hoặc không tìm thấy.
        /// </summary>
        public static int GetSlotIndex(string username, List<PuzzleOnline.Network.PlayerInfo> players)
        {
            if (string.IsNullOrEmpty(username) || players == null) return 0;

            string trimmed = username.Trim();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null &&
                    string.Equals(players[i].Username?.Trim(), trimmed,
                        System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }
    }
}
