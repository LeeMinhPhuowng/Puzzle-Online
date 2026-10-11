using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleOnline.Core;
using PuzzleSystem.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển Cửa hàng (ShopView):
    /// - Tự động tải danh sách Không gian 3D và Bộ tranh ghép từ Server Database và Unity ContentRegistry.
    /// - Hiển thị giá tiền xu, trạng thái đã sở hữu, và xử lý giao dịch mua.
    /// - Hỗ trợ lọc tìm kiếm vật phẩm theo thời gian thực.
    /// </summary>
    public class LobbyShopUI : MonoBehaviour
    {
        [Header("Scroll Contents")]
        [SerializeField] private RectTransform environmentsContent;
        [SerializeField] private RectTransform puzzlesContent;

        [Header("Item Prefab")]
        [SerializeField] private GameObject shopItemPrefab;

        [Header("Controls")]
        [SerializeField] private TMP_InputField searchInput;
        [SerializeField] private TMP_InputField puzzleSearchInput;
        [SerializeField] private TMP_InputField envSearchInput;
        [SerializeField] private Button closeButton;

        private readonly List<GameObject> _spawnedEnvItems = new List<GameObject>();
        private readonly List<GameObject> _spawnedPuzzleItems = new List<GameObject>();

        private string _searchFilter = string.Empty;
        private string _puzzleFilter = string.Empty;
        private string _envFilter = string.Empty;

        private void Awake()
        {
            EnsureReferences();

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (searchInput != null) searchInput.onValueChanged.AddListener(OnSearchChanged);
            if (puzzleSearchInput != null) puzzleSearchInput.onValueChanged.AddListener(OnPuzzleSearchChanged);
            if (envSearchInput != null) envSearchInput.onValueChanged.AddListener(OnEnvSearchChanged);

            SetupScrollContent(environmentsContent);
            SetupScrollContent(puzzlesContent);
        }

        private void OnEnable()
        {
            transform.SetAsLastSibling();
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated += RefreshShopItems;
                net.RefreshLobby();
            }

            RefreshShopItems();
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnLobbyUpdated -= RefreshShopItems;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            RefreshShopItems();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void EnsureReferences()
        {
            if (environmentsContent == null)
            {
                var t = transform.Find("Environments/Viewport/Content");
                if (t != null) environmentsContent = t.GetComponent<RectTransform>();
            }

            if (puzzlesContent == null)
            {
                var t = transform.Find("Puzzles/Viewport/Content");
                if (t != null) puzzlesContent = t.GetComponent<RectTransform>();
            }

            if (puzzleSearchInput == null)
            {
                var t = transform.Find("PuzzleSearch") ?? transform.Find("PuzzleFilter");
                if (t != null) puzzleSearchInput = t.GetComponent<TMP_InputField>();
            }

            if (envSearchInput == null)
            {
                var t = transform.Find("EnvironmentSearch") ?? transform.Find("EnvironmentFilter");
                if (t != null) envSearchInput = t.GetComponent<TMP_InputField>();
            }

            if (searchInput == null)
            {
                var t = transform.Find("Input");
                if (t != null) searchInput = t.GetComponent<TMP_InputField>();
            }

            if (closeButton == null)
            {
                var t = transform.Find("Back") ?? transform.Find("Close");
                if (t != null) closeButton = t.GetComponent<Button>();
            }

            if (shopItemPrefab == null)
            {
                shopItemPrefab = Resources.Load<GameObject>("ShopItem")
                              ?? Resources.Load<GameObject>("Prefabs/ShopItem");
            }
        }

        private void SetupScrollContent(RectTransform content)
        {
            if (content == null) return;

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.GetComponent<ContentSizeFitter>();
            if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();

            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void RefreshShopItems()
        {
            EnsureReferences();
            RenderEnvironments();
            RenderPuzzles();
        }

        private void RenderEnvironments()
        {
            if (environmentsContent == null || shopItemPrefab == null) return;

            foreach (var item in _spawnedEnvItems) if (item != null) Destroy(item);
            _spawnedEnvItems.Clear();

            var net = PuzzleNetworkManager.Instance;
            var serverThemes = net?.ShopThemes ?? new List<ThemeDefinition>();
            var localThemes = ContentRegistry.Instance.GetAllThemes();
            var owned = net?.OwnedThemes ?? new HashSet<string>();

            // Hợp nhất danh sách themes từ Server Database và Unity Local
            var themeMap = new Dictionary<string, (string name, int price, bool isOwned, string description)>(StringComparer.OrdinalIgnoreCase);

            foreach (var st in serverThemes)
            {
                if (st == null || string.IsNullOrEmpty(st.Id)) continue;
                bool isOwn = st.Owned || st.Price == 0 || owned.Contains(st.Id);
                themeMap[st.Id] = (st.Name, st.Price, isOwn, !string.IsNullOrEmpty(st.Description) ? st.Description : "Không gian 3D");
            }

            foreach (var t in localThemes)
            {
                if (t == null || string.IsNullOrEmpty(t.themeId) || themeMap.ContainsKey(t.themeId)) continue;
                bool isOwn = t.price == 0 || owned.Contains(t.themeId);
                themeMap[t.themeId] = (t.displayName, t.price, isOwn, t.description);
            }

            string filter = (!string.IsNullOrEmpty(_envFilter) ? _envFilter : _searchFilter).Trim().ToLowerInvariant();

            foreach (var kvp in themeMap)
            {
                string id = kvp.Key;
                var info = kvp.Value;

                if (!string.IsNullOrEmpty(filter) && !info.name.ToLowerInvariant().Contains(filter) && !id.ToLowerInvariant().Contains(filter))
                    continue;

                GameObject go = Instantiate(shopItemPrefab, environmentsContent);
                go.SetActive(true);
                _spawnedEnvItems.Add(go);

                var shopItem = go.GetComponent<UIShopItem>() ?? go.AddComponent<UIShopItem>();
                shopItem.Bind(id, "theme", info.name, info.price, info.isOwned, null, info.description, (k, targetId) =>
                {
                    net?.SendBuy(k, targetId);
                });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(environmentsContent);
        }

        private void RenderPuzzles()
        {
            if (puzzlesContent == null || shopItemPrefab == null) return;

            foreach (var item in _spawnedPuzzleItems) if (item != null) Destroy(item);
            _spawnedPuzzleItems.Clear();

            var net = PuzzleNetworkManager.Instance;
            var serverPuzzles = net?.ShopPuzzles ?? new List<PuzzleDefinition>();
            var localPuzzles = ContentRegistry.Instance.GetAllPuzzleData();
            var owned = net?.OwnedPuzzles ?? new HashSet<string>();

            // Hợp nhất danh sách puzzles từ Server Database và Unity Local
            var puzzleMap = new Dictionary<string, (string name, int price, bool isOwned, string details)>(StringComparer.OrdinalIgnoreCase);

            foreach (var sp in serverPuzzles)
            {
                if (sp == null || string.IsNullOrEmpty(sp.Id)) continue;
                bool isOwn = sp.Owned || sp.Price == 0 || owned.Contains(sp.Id);
                puzzleMap[sp.Id] = (sp.Name, sp.Price, isOwn, $"{sp.Rows}×{sp.Columns} Mảnh");
            }

            foreach (var p in localPuzzles)
            {
                if (p == null || string.IsNullOrEmpty(p.puzzleId) || puzzleMap.ContainsKey(p.puzzleId)) continue;
                bool isOwn = p.price == 0 || owned.Contains(p.puzzleId);
                puzzleMap[p.puzzleId] = (p.displayName, p.price, isOwn, $"{p.rows}×{p.columns} Mảnh");
            }

            string filter = (!string.IsNullOrEmpty(_puzzleFilter) ? _puzzleFilter : _searchFilter).Trim().ToLowerInvariant();

            foreach (var kvp in puzzleMap)
            {
                string id = kvp.Key;
                var info = kvp.Value;

                if (!string.IsNullOrEmpty(filter) && !info.name.ToLowerInvariant().Contains(filter) && !id.ToLowerInvariant().Contains(filter))
                    continue;

                GameObject go = Instantiate(shopItemPrefab, puzzlesContent);
                go.SetActive(true);
                _spawnedPuzzleItems.Add(go);

                var shopItem = go.GetComponent<UIShopItem>() ?? go.AddComponent<UIShopItem>();
                shopItem.Bind(id, "puzzle", info.name, info.price, info.isOwned, null, info.details, (k, targetId) =>
                {
                    net?.SendBuy(k, targetId);
                });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(puzzlesContent);
        }

        private void OnSearchChanged(string text)
        {
            _searchFilter = text ?? string.Empty;
            RefreshShopItems();
        }

        private void OnPuzzleSearchChanged(string text)
        {
            _puzzleFilter = text ?? string.Empty;
            RenderPuzzles();
        }

        private void OnEnvSearchChanged(string text)
        {
            _envFilter = text ?? string.Empty;
            RenderEnvironments();
        }
    }
}
