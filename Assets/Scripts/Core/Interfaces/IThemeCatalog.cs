using System.Collections.Generic;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Giao diện kho dữ liệu không gian 3D (Theme Catalog Contract).
    /// </summary>
    public interface IThemeCatalog
    {
        ThemeData GetTheme(string themeId);
        IReadOnlyList<ThemeData> GetAllThemes();
        string GetThemeDisplayName(string themeId);
        string GetThemeShortName(string themeId);
        ThemeData DefaultTheme { get; }
    }
}
