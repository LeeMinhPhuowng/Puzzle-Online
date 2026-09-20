using UnityEngine;
using System.Collections.Generic;
using PuzzleSystem.Core;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleCatalogManager : MonoBehaviour
    {
        public static PuzzleCatalogManager Instance { get; private set; }

        [Header("Puzzle Catalog")]
        [SerializeField] private List<PuzzleData> registeredPuzzles = new List<PuzzleData>();

        [Header("Default Puzzle Set")]
        [SerializeField] private PuzzleData defaultPuzzle;

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
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeCatalog();
        }

        private void InitializeCatalog()
        {
            catalog.Clear();

#if UNITY_EDITOR
            // Tự động tìm nạp tất cả PuzzleData trong project nếu chưa kịp kéo vào Inspector
            if (registeredPuzzles == null || registeredPuzzles.Count == 0)
            {
                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PuzzleData");
                foreach (var guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var data = UnityEditor.AssetDatabase.LoadAssetAtPath<PuzzleData>(path);
                    if (data != null && !registeredPuzzles.Contains(data))
                    {
                        registeredPuzzles.Add(data);
                    }
                }
            }
#endif

            foreach (var p in registeredPuzzles)
            {
                if (p != null && !string.IsNullOrEmpty(p.puzzleId))
                {
                    catalog[p.puzzleId] = p;
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

            if (catalog.TryGetValue(puzzleId, out var data))
            {
                return data;
            }

            Debug.LogWarning("[PuzzleCatalogManager] Không tìm thấy PuzzleData với ID: " + puzzleId);
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
