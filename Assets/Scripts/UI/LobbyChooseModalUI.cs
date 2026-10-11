using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleOnline.Core;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển modal chọn bộ tranh hoặc không gian (ChoosePuzzleView / ChooseEnvironmentView).
    /// Tự động nạp danh sách, hỗ trợ lọc tìm kiếm và gán giá trị được chọn vào Form tạo bàn.
    /// </summary>
    public class LobbyChooseModalUI : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private bool isEnvironmentMode = false;

        [Header("UI References")]
        [SerializeField] private RectTransform itemsContent;
        [SerializeField] private GameObject searchItemPrefab;
        [SerializeField] private TMP_InputField filterInput;
        [SerializeField] private Button closeButton;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();
        private string _filterText = string.Empty;
        private Action<string, string> _onSelectedCallback;

        private void Awake()
        {
            EnsureReferences();

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (filterInput != null) filterInput.onValueChanged.AddListener(OnFilterChanged);

            SetupScrollContent(itemsContent);
        }

        private void OnEnable()
        {
            transform.SetAsLastSibling();
            RefreshItems();
        }

        public void Open(Action<string, string> onSelected = null)
        {
            _onSelectedCallback = onSelected;
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            RefreshItems();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void EnsureReferences()
        {
            if (itemsContent == null)
            {
                var t = transform.Find("Scroll View/Viewport/Content");
                if (t != null) itemsContent = t.GetComponent<RectTransform>();
            }

            if (filterInput == null)
            {
                var t = transform.Find("Filter") ?? transform.Find("Input");
                if (t != null) filterInput = t.GetComponent<TMP_InputField>();
            }

            if (closeButton == null)
            {
                var t = transform.Find("Back") ?? transform.Find("Close");
                if (t != null) closeButton = t.GetComponent<Button>();
            }

            if (searchItemPrefab == null)
            {
                searchItemPrefab = Resources.Load<GameObject>("SearchItem")
                                ?? Resources.Load<GameObject>("Prefabs/SearchItem");
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

            vlg.spacing = 10f;
            vlg.padding = new RectOffset(12, 12, 10, 10);
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

        public void RefreshItems()
        {
            EnsureReferences();

            if (itemsContent == null || searchItemPrefab == null) return;

            foreach (var item in _spawnedItems) if (item != null) Destroy(item);
            _spawnedItems.Clear();

            var net = PuzzleNetworkManager.Instance;
            string filter = _filterText.Trim().ToLowerInvariant();

            if (isEnvironmentMode)
            {
                // Danh sách không gian (Nạp trực tiếp từ Database qua Server ShopThemes)
                var themeMap = new Dictionary<string, (string name, string detail)>(StringComparer.OrdinalIgnoreCase);

                if (net?.ShopThemes != null)
                {
                    foreach (var st in net.ShopThemes)
                    {
                        if (st == null || string.IsNullOrEmpty(st.Id)) continue;
                        string cleanName = SanitizeItemName(st.Name);
                        string detail = !string.IsNullOrEmpty(st.Description)
                            ? FixFontQuestionMarks(st.Description)
                            : (st.Price > 0 ? $"{st.Price} Xu" : "Miễn phí");
                        themeMap[st.Id] = (cleanName, detail);
                    }
                }

                // Fallback ScriptableObject chỉ khi DB chưa có item đó
                var localThemes = ContentRegistry.Instance.GetAllThemes();
                if (localThemes != null)
                {
                    foreach (var t in localThemes)
                    {
                        if (t != null && !string.IsNullOrEmpty(t.themeId) && !themeMap.ContainsKey(t.themeId))
                            themeMap[t.themeId] = (SanitizeItemName(t.displayName), FixFontQuestionMarks(t.description));
                    }
                }

                foreach (var kvp in themeMap)
                {
                    string id = kvp.Key;
                    var info = kvp.Value;

                    if (!string.IsNullOrEmpty(filter) && !info.name.ToLowerInvariant().Contains(filter) && !id.ToLowerInvariant().Contains(filter))
                        continue;

                    GameObject go = Instantiate(searchItemPrefab, itemsContent);
                    go.SetActive(true);
                    _spawnedItems.Add(go);

                    var searchItem = go.GetComponent<UISearchItem>() ?? go.AddComponent<UISearchItem>();
                    searchItem.Bind(id, info.name, info.detail, null, (selectedId, selectedName) =>
                    {
                        _onSelectedCallback?.Invoke(selectedId, selectedName);
                        Close();
                    });
                }
            }
            else
            {
                // Danh sách bộ xếp hình (Nạp trực tiếp từ Database qua Server ShopPuzzles)
                var puzzleMap = new Dictionary<string, (string name, string detail)>(StringComparer.OrdinalIgnoreCase);

                if (net?.ShopPuzzles != null)
                {
                    foreach (var sp in net.ShopPuzzles)
                    {
                        if (sp == null || string.IsNullOrEmpty(sp.Id)) continue;
                        puzzleMap[sp.Id] = (SanitizeItemName(sp.Name), $"{sp.Rows}×{sp.Columns} Mảnh ({sp.Rows * sp.Columns} mảnh)");
                    }
                }

                // Fallback ScriptableObject chỉ khi DB chưa có item đó
                var localPuzzles = ContentRegistry.Instance.GetAllPuzzleData();
                if (localPuzzles != null)
                {
                    foreach (var p in localPuzzles)
                    {
                        if (p == null || string.IsNullOrEmpty(p.puzzleId)) continue;
                        string cleanName = SanitizeItemName(p.displayName);
                        // Tránh duplicate theo puzzleId, theo tên, hoặc theo prefix
                        bool exists = puzzleMap.ContainsKey(p.puzzleId)
                            || puzzleMap.Values.Any(v => string.Equals(v.name, cleanName, StringComparison.OrdinalIgnoreCase))
                            || puzzleMap.Keys.Any(k => k.StartsWith(p.puzzleId, StringComparison.OrdinalIgnoreCase) || p.puzzleId.StartsWith(k, StringComparison.OrdinalIgnoreCase));
                        if (!exists)
                        {
                            puzzleMap[p.puzzleId] = (cleanName, $"{p.rows}×{p.columns} Mảnh ({p.TotalPieces} mảnh)");
                        }
                    }
                }

                foreach (var kvp in puzzleMap)
                {
                    string id = kvp.Key;
                    var info = kvp.Value;

                    if (!string.IsNullOrEmpty(filter) && !info.name.ToLowerInvariant().Contains(filter) && !id.ToLowerInvariant().Contains(filter))
                        continue;

                    GameObject go = Instantiate(searchItemPrefab, itemsContent);
                    go.SetActive(true);
                    _spawnedItems.Add(go);

                    var searchItem = go.GetComponent<UISearchItem>() ?? go.AddComponent<UISearchItem>();
                    searchItem.Bind(id, info.name, info.detail, null, (selectedId, selectedName) =>
                    {
                        _onSelectedCallback?.Invoke(selectedId, selectedName);
                        Close();
                    });
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(itemsContent);
        }

        private void OnFilterChanged(string text)
        {
            _filterText = text ?? string.Empty;
            RefreshItems();
        }

        private static string SanitizeItemName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            // Bỏ phần đóng mở ngoặc cạnh tên: ví dụ "Sunset (Hoàng hôn)" -> "Sunset"
            int parenIdx = name.IndexOf('(');
            if (parenIdx > 0)
            {
                name = name.Substring(0, parenIdx).Trim();
            }
            return name;
        }

        private static string FixFontQuestionMarks(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Contains("??"))
            {
                if (text.IndexOf("thi", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("nhi", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Không gian thiên nhiên trong lành, xanh mát";
                if (text.IndexOf("l", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("c", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Không gian lớp học ấm cúng, hoài niệm";
                if (text.IndexOf("anh", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("hoa", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Không gian hoa anh đào lãng mạn";
            }
            return text;
        }
    }
}
