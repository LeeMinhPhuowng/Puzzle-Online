using System;
using UnityEngine;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Định nghĩa bối cảnh không gian 3D (3D Environment Theme) dưới dạng ScriptableObject.
    /// Giúp thêm hoặc chỉnh sửa môi trường 3D mà không cần can thiệp vào mã nguồn C#.
    /// </summary>
    [CreateAssetMenu(fileName = "NewThemeData", menuName = "Puzzle Online/Theme Data")]
    public class ThemeData : ScriptableObject
    {
        [Header("Theme Identity")]
        public string themeId = "SCN_Nature";
        public string displayName = "Thiên nhiên xanh mát (SCN_Nature)";
        public string shortName = "Thiên nhiên";
        public string sceneName = "SCN_Nature";

        [Header("Shop & Pricing")]
        public int price = 0;
        [TextArea(2, 4)]
        public string description = "Không gian trải nghiệm 3D độc quyền";
        public Sprite thumbnail;

        [Header("Visual Properties (Tùy chọn)")]
        public Color themeColor = new Color(0.24f, 0.75f, 0.45f, 1f);
    }
}
