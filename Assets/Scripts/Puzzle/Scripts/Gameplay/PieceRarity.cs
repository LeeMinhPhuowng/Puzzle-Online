using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Định nghĩa các cấp độ độ hiếm và màu sắc tương ứng với điểm số mảnh ghép (1-5 điểm).
    /// 1 điểm: Trắng (Phổ Biến)
    /// 2 điểm: Xanh lá (Đặc Biệt)
    /// 3 điểm: Xanh dương (Hiếm)
    /// 4 điểm: Tím (Sử Thi)
    /// 5 điểm: Vàng (Huyền Thoại)
    /// </summary>
    public static class PieceRarity
    {
        public static readonly Color ColorWhite = new Color(0.95f, 0.95f, 0.95f, 1f);     // 1đ - Common
        public static readonly Color ColorGreen = new Color(0.2f, 0.95f, 0.3f, 1f);       // 2đ - Uncommon
        public static readonly Color ColorBlue = new Color(0.2f, 0.65f, 1f, 1f);         // 3đ - Rare
        public static readonly Color ColorPurple = new Color(0.75f, 0.25f, 1f, 1f);       // 4đ - Epic
        public static readonly Color ColorGold = new Color(1f, 0.85f, 0.1f, 1f);         // 5đ - Legendary

        public static Color GetColor(int points)
        {
            return points switch
            {
                1 => ColorWhite,
                2 => ColorGreen,
                3 => ColorBlue,
                4 => ColorPurple,
                5 => ColorGold,
                _ => ColorWhite
            };
        }

        public static string GetRarityName(int points)
        {
            return points switch
            {
                1 => "Phổ Biến (Trắng)",
                2 => "Đặc Biệt (Xanh Lá)",
                3 => "Hiếm (Xanh Dương)",
                4 => "Sử Thi (Tím)",
                5 => "Huyền Thoại (Vàng Kim)",
                _ => "Thường"
            };
        }
    }
}
