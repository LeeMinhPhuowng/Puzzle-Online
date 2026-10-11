using System;
using System.Collections.Generic;
using UnityEngine;
using PuzzleSystem.Core;
using PuzzleOnline.Core;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleCatalogManager : MonoBehaviour, IPuzzleCatalog
    {
        public static PuzzleCatalogManager Instance { get; private set; }

        [Header("Puzzle Catalog")]
        [SerializeField] private List<PuzzleData> registeredPuzzles = new List<PuzzleData>();

        [Header("Default Puzzle Set")]
        [SerializeField] private PuzzleData defaultPuzzle;
        public PuzzleData DefaultPuzzle => defaultPuzzle;

        private readonly Dictionary<string, PuzzleData> catalog = new Dictionary<string, PuzzleData>();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register<IPuzzleCatalog>(this);

            InitializeCatalog();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                ServiceLocator.Unregister<IPuzzleCatalog>();
                Instance = null;
            }
        }

        private void InitializeCatalog()
        {
            catalog.Clear();

            // 1. Tải toàn bộ PuzzleData từ Resources (hoạt động 100% cả trong Editor lẫn Runtime Build)
            var resPuzzles = Resources.LoadAll<PuzzleData>("Puzzles");
            if (resPuzzles != null)
            {
                foreach (var p in resPuzzles)
                {
                    if (p != null && !registeredPuzzles.Contains(p) && !registeredPuzzles.Exists(x => x != null && string.Equals(x.puzzleId, p.puzzleId, StringComparison.OrdinalIgnoreCase)))
                    {
                        registeredPuzzles.Add(p);
                    }
                }
            }

            var rootResPuzzles = Resources.LoadAll<PuzzleData>("");
            if (rootResPuzzles != null)
            {
                foreach (var p in rootResPuzzles)
                {
                    if (p != null && !registeredPuzzles.Contains(p) && !registeredPuzzles.Exists(x => x != null && string.Equals(x.puzzleId, p.puzzleId, StringComparison.OrdinalIgnoreCase)))
                    {
                        registeredPuzzles.Add(p);
                    }
                }
            }

#if UNITY_EDITOR
            // 2. Trong Unity Editor: tự động tìm thêm bất kỳ file PuzzleData nào trong Assets
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PuzzleData");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<PuzzleData>(path);
                if (data != null && !registeredPuzzles.Contains(data) && !registeredPuzzles.Exists(x => x != null && string.Equals(x.puzzleId, data.puzzleId, StringComparison.OrdinalIgnoreCase)))
                {
                    registeredPuzzles.Add(data);
                }
            }
#endif

            foreach (var p in registeredPuzzles)
            {
                if (p != null && !string.IsNullOrEmpty(p.puzzleId))
                {
                    catalog[p.puzzleId] = p;
                    catalog[p.puzzleId.ToLowerInvariant()] = p;

                    // Đăng ký bí danh ngắn (ví dụ 'forest' cho 'forest_4x3')
                    string prefix = p.puzzleId.Split('_')[0].ToLowerInvariant();
                    if (!catalog.ContainsKey(prefix))
                    {
                        catalog[prefix] = p;
                    }

                    // Đồng bộ tức thì với ContentRegistry toàn cục
                    ContentRegistry.Instance?.RegisterPuzzle(p);
                }
            }

            // Fallback nếu chưa chỉ định default puzzle
            if (defaultPuzzle == null && registeredPuzzles.Count > 0)
            {
                defaultPuzzle = registeredPuzzles[0];
            }
        }

        public PuzzleData GetPuzzleData(string puzzleId)
        {
            if (string.IsNullOrEmpty(puzzleId)) return defaultPuzzle;

            string key = puzzleId.Trim();
            if (catalog.TryGetValue(key, out var data)) return data;
            if (catalog.TryGetValue(key.ToLowerInvariant(), out data)) return data;

            // Thử tìm theo bí danh rút gọn (bỏ hậu tố kích thước _3x3, _4x3, v.v.)
            string prefix = key.Split('_')[0].ToLowerInvariant();
            if (catalog.TryGetValue(prefix, out data)) return data;

            // Thử nạp động từ Resources
            var res = Resources.Load<PuzzleData>($"Puzzles/{key}") 
                   ?? Resources.Load<PuzzleData>($"Puzzles/{prefix}")
                   ?? Resources.Load<PuzzleData>(key);
            if (res != null)
            {
                RegisterPuzzleData(res);
                return res;
            }

            Debug.LogWarning($"[PuzzleCatalogManager] Không tìm thấy PuzzleData với ID: '{puzzleId}'. Sử dụng tranh mặc định.");
            return defaultPuzzle;
        }

        public IReadOnlyList<PuzzleData> GetAllPuzzleData()
        {
            return registeredPuzzles;
        }

        public List<PuzzleData> GetAcquiredPuzzleData(HashSet<string> ownedIds)
        {
            List<PuzzleData> acquired = new List<PuzzleData>();
            foreach (var p in registeredPuzzles)
            {
                if (p == null) continue;

                if (p.price == 0 || (ownedIds != null && ownedIds.Contains(p.puzzleId)))
                {
                    acquired.Add(p);
                }
            }
            return acquired;
        }

        public void RegisterPuzzleData(PuzzleData newPuzzle)
        {
            if (newPuzzle == null || string.IsNullOrEmpty(newPuzzle.puzzleId)) return;

            if (!catalog.ContainsKey(newPuzzle.puzzleId))
            {
                registeredPuzzles.Add(newPuzzle);
                catalog[newPuzzle.puzzleId] = newPuzzle;
            }
        }
    }
}
