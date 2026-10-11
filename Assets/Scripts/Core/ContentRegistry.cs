using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleSystem.Core;

namespace PuzzleOnline.Core
{
    /// <summary>
    /// Registry trung tâm quản lý toàn bộ nội dung (Bộ tranh ghép PuzzleData & Không gian 3D ThemeData).
    /// Triển khai IPuzzleCatalog và IThemeCatalog.
    /// Cho phép thêm nội dung mới qua ScriptableObject trong Unity Editor mà không cần sửa code C#.
    /// </summary>
    public class ContentRegistry : IPuzzleCatalog, IThemeCatalog
    {
        private static ContentRegistry _instance;
        public static ContentRegistry Instance => _instance ??= new ContentRegistry();

        private readonly Dictionary<string, PuzzleData> _puzzles = new Dictionary<string, PuzzleData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PuzzleData> _puzzleList = new List<PuzzleData>();

        private readonly Dictionary<string, ThemeData> _themes = new Dictionary<string, ThemeData>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ThemeData> _themeList = new List<ThemeData>();

        private PuzzleData _defaultPuzzle;
        private ThemeData _defaultTheme;

        public PuzzleData DefaultPuzzle => _defaultPuzzle;
        public ThemeData DefaultTheme => _defaultTheme;

        public ContentRegistry()
        {
            InitializeThemes();
            InitializePuzzles();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
        {
            var registry = Instance;
            ServiceLocator.Register<IPuzzleCatalog>(registry);
            ServiceLocator.Register<IThemeCatalog>(registry);
        }

        #region Theme Management
        private void InitializeThemes()
        {
            _themes.Clear();
            _themeList.Clear();

            // 1. Khởi tạo các Theme mặc định của hệ thống
            RegisterThemeInternal(CreateRuntimeTheme("SCN_Nature", "Thiên nhiên xanh mát (SCN_Nature)", "Thiên nhiên", "SCN_Nature", 0));
            RegisterThemeInternal(CreateRuntimeTheme("SCN_Classroom", "Phòng học cổ điển (SCN_Classroom)", "Phòng học", "SCN_Classroom", 150));
            RegisterThemeInternal(CreateRuntimeTheme("SCN_Sakura", "Vườn hoa anh đào (SCN_Sakura)", "Hoa anh đào", "SCN_Sakura", 300));

            // 2. Nạp thêm toàn bộ ThemeData được tạo từ Unity Editor trong thư mục Resources (nếu có)
            var customThemes = Resources.LoadAll<ThemeData>("");
            if (customThemes != null)
            {
                foreach (var t in customThemes)
                {
                    if (t != null && !string.IsNullOrEmpty(t.themeId))
                    {
                        RegisterThemeInternal(t);
                    }
                }
            }

            _themes.TryGetValue("SCN_Nature", out _defaultTheme);
        }

        private static ThemeData CreateRuntimeTheme(string id, string display, string @short, string scene, int price)
        {
            var t = ScriptableObject.CreateInstance<ThemeData>();
            t.themeId = id;
            t.displayName = display;
            t.shortName = @short;
            t.sceneName = scene;
            t.price = price;
            return t;
        }

        private void RegisterThemeInternal(ThemeData theme)
        {
            if (theme == null || string.IsNullOrEmpty(theme.themeId)) return;
            if (_themes.ContainsKey(theme.themeId))
            {
                _themes[theme.themeId] = theme;
                int idx = _themeList.FindIndex(x => x.themeId.Equals(theme.themeId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _themeList[idx] = theme;
            }
            else
            {
                _themes.Add(theme.themeId, theme);
                _themeList.Add(theme);
            }
        }

        public ThemeData GetTheme(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return _defaultTheme;
            if (_themes.TryGetValue(themeId, out var t)) return t;
            return null;
        }

        public IReadOnlyList<ThemeData> GetAllThemes() => _themeList;

        public string GetThemeDisplayName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return string.Empty;
            var t = GetTheme(themeId);
            return t != null ? t.displayName : themeId;
        }

        public string GetThemeShortName(string themeId)
        {
            if (string.IsNullOrEmpty(themeId)) return string.Empty;
            var t = GetTheme(themeId);
            return t != null ? t.shortName : themeId;
        }
        #endregion

        #region Puzzle Management
        private void InitializePuzzles()
        {
            _puzzles.Clear();
            _puzzleList.Clear();

            // Nạp toàn bộ PuzzleData từ Resources
            var resPuzzles = Resources.LoadAll<PuzzleData>("");
            if (resPuzzles != null)
            {
                foreach (var p in resPuzzles)
                {
                    if (p != null && !string.IsNullOrEmpty(p.puzzleId))
                    {
                        RegisterPuzzle(p);
                    }
                }
            }

            if (_puzzleList.Count > 0)
            {
                _defaultPuzzle = _puzzleList[0];
            }
        }

        public void RegisterPuzzle(PuzzleData puzzle)
        {
            if (puzzle == null || string.IsNullOrEmpty(puzzle.puzzleId)) return;
            if (_puzzles.ContainsKey(puzzle.puzzleId))
            {
                _puzzles[puzzle.puzzleId] = puzzle;
                int idx = _puzzleList.FindIndex(x => x.puzzleId.Equals(puzzle.puzzleId, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0) _puzzleList[idx] = puzzle;
            }
            else
            {
                _puzzles.Add(puzzle.puzzleId, puzzle);
                _puzzleList.Add(puzzle);
            }
        }

        public PuzzleData GetPuzzleData(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return _defaultPuzzle;

            if (_puzzles.TryGetValue(puzzleId, out var p)) return p;

            // Thử load trực tiếp từ Resources nếu mới tạo lúc runtime
            var loaded = Resources.Load<PuzzleData>($"Puzzles/{puzzleId}") ?? Resources.Load<PuzzleData>(puzzleId);
            if (loaded != null)
            {
                RegisterPuzzle(loaded);
                return loaded;
            }

            return _defaultPuzzle;
        }

        public IReadOnlyList<PuzzleData> GetAllPuzzleData() => _puzzleList;

        public string GetPuzzleDisplayName(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return string.Empty;

            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null)
            {
                string netName = PuzzleOnline.Network.PuzzleNetworkManager.Instance.GetPuzzleName(puzzleId);
                if (!string.IsNullOrEmpty(netName) && !netName.Equals(puzzleId, StringComparison.OrdinalIgnoreCase))
                    return netName;
            }

            var p = GetPuzzleData(puzzleId);
            if (p != null)
            {
                return $"{p.displayName} ({p.rows}×{p.columns})";
            }

            return puzzleId;
        }
        #endregion
    }
}
