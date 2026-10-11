using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;
using PuzzleOnline.UI;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Điều khiển modal Thêm bạn & Quản lý lời mời kết bạn (AddFriendView):
    /// - Nhập tên người chơi để gửi lời mời kết bạn mới.
    /// - Hiển thị danh sách Lời mời đã nhận (IncomeRequests) với nút Đồng ý / Từ chối.
    /// - Hiển thị danh sách Lời mời đã gửi (SentRequests) với nút Hủy.
    /// - Tự động đồng bộ và làm mới danh sách từ Server.
    /// </summary>
    public class AddFriendModalUI : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private RectTransform cardPanel;
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private Button submitButton;
        [SerializeField] private Button closeButton;

        [Header("Scroll Contents")]
        [SerializeField] private RectTransform incomeContent;
        [SerializeField] private RectTransform sentContent;

        [Header("Item Prefab")]
        [SerializeField] private GameObject friendRequestItemPrefab;

        private readonly List<GameObject> _spawnedIncomeItems = new List<GameObject>();
        private readonly List<GameObject> _spawnedSentItems = new List<GameObject>();

        private void Awake()
        {
            EnsureReferences();

            if (submitButton != null) submitButton.onClick.AddListener(OnSubmit);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (usernameInput != null) usernameInput.onSubmit.AddListener((_) => OnSubmit());

            SetupScrollContent(incomeContent);
            SetupScrollContent(sentContent);
        }

        private void OnDestroy()
        {
            if (submitButton != null) submitButton.onClick.RemoveListener(OnSubmit);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (usernameInput != null) usernameInput.onSubmit.RemoveAllListeners();
        }

        private void OnEnable()
        {
            transform.SetAsLastSibling();
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnFriendRequestsUpdated += RefreshLists;
                net.SendGetFriendRequests();
            }

            RefreshLists();
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnFriendRequestsUpdated -= RefreshLists;
            }
        }

        public void Open()
        {
            transform.SetAsLastSibling();
            gameObject.SetActive(true);

            if (usernameInput != null)
            {
                usernameInput.text = string.Empty;
                usernameInput.ActivateInputField();
            }

            if (cardPanel != null)
            {
                cardPanel.localScale = Vector3.one;
            }

            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.SendGetFriendRequests();
            }

            RefreshLists();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void EnsureReferences()
        {
            if (cardPanel == null) cardPanel = GetComponent<RectTransform>();

            if (usernameInput == null)
            {
                var t = transform.Find("Input");
                if (t != null) usernameInput = t.GetComponent<TMP_InputField>();
            }

            if (submitButton == null)
            {
                var t = transform.Find("Add");
                if (t != null) submitButton = t.GetComponent<Button>();
            }

            if (closeButton == null)
            {
                var t = transform.Find("Back");
                if (t != null) closeButton = t.GetComponent<Button>();
            }

            if (incomeContent == null)
            {
                var t = transform.Find("IncomeRequests/Viewport/Content");
                if (t != null) incomeContent = t.GetComponent<RectTransform>();
            }

            if (sentContent == null)
            {
                var t = transform.Find("SentRequests/Viewport/Content");
                if (t != null) sentContent = t.GetComponent<RectTransform>();
            }

            if (friendRequestItemPrefab == null)
            {
                friendRequestItemPrefab = Resources.Load<GameObject>("FriendRequestItem")
                                       ?? Resources.Load<GameObject>("Prefabs/FriendRequestItem");
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

        public void RefreshLists()
        {
            EnsureReferences();
            RenderIncomeRequests();
            RenderSentRequests();
        }

        private void RenderIncomeRequests()
        {
            if (incomeContent == null) return;

            foreach (var item in _spawnedIncomeItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedIncomeItems.Clear();

            var net = PuzzleNetworkManager.Instance;
            var requests = net?.PendingFriendRequests ?? new List<FriendRequestItem>();

            foreach (var req in requests)
            {
                GameObject go = null;
                if (friendRequestItemPrefab != null)
                {
                    go = Instantiate(friendRequestItemPrefab, incomeContent);
                }
                else
                {
                    go = CreateFallbackItem(incomeContent, false);
                }

                if (go == null) continue;
                go.SetActive(true);
                _spawnedIncomeItems.Add(go);

                var itemScript = go.GetComponent<UIFriendRequestItem>() ?? go.AddComponent<UIFriendRequestItem>();
                itemScript.Bind(req, OnAcceptRequest, OnDeclineRequest, OnCancelRequest);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(incomeContent);
        }

        private void RenderSentRequests()
        {
            if (sentContent == null) return;

            foreach (var item in _spawnedSentItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedSentItems.Clear();

            var net = PuzzleNetworkManager.Instance;
            var requests = net?.SentFriendRequests ?? new List<FriendRequestItem>();

            foreach (var req in requests)
            {
                GameObject go = null;
                if (friendRequestItemPrefab != null)
                {
                    go = Instantiate(friendRequestItemPrefab, sentContent);
                }
                else
                {
                    go = CreateFallbackItem(sentContent, true);
                }

                if (go == null) continue;
                go.SetActive(true);
                _spawnedSentItems.Add(go);

                var itemScript = go.GetComponent<UIFriendRequestItem>() ?? go.AddComponent<UIFriendRequestItem>();
                itemScript.Bind(req, OnAcceptRequest, OnDeclineRequest, OnCancelRequest);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(sentContent);
        }

        private void OnAcceptRequest(long reqId)
        {
            PuzzleNetworkManager.Instance?.SendAcceptFriendRequest(reqId);
        }

        private void OnDeclineRequest(long reqId)
        {
            PuzzleNetworkManager.Instance?.SendRejectFriendRequest(reqId);
        }

        private void OnCancelRequest(long reqId)
        {
            PuzzleNetworkManager.Instance?.SendCancelFriendRequest(reqId);
        }

        private void OnSubmit()
        {
            if (usernameInput != null && !string.IsNullOrWhiteSpace(usernameInput.text))
            {
                string target = usernameInput.text.Trim();
                PuzzleNetworkManager.Instance?.SendAddFriend(target);
                usernameInput.text = string.Empty;
                PuzzleNetworkManager.Instance?.SendGetFriendRequests();
            }
        }

        private GameObject CreateFallbackItem(RectTransform parent, bool isSent)
        {
            var go = new GameObject("FriendRequestEntry", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 80);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.28f, 0.9f);

            var nameGo = new GameObject("Username", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0, 0.5f);
            nameRt.anchorMax = new Vector2(0, 0.5f);
            nameRt.pivot = new Vector2(0, 0.5f);
            nameRt.anchoredPosition = new Vector2(25, 12);
            nameRt.sizeDelta = new Vector2(400, 32);

            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.fontSize = 24;
            nameTmp.color = Color.white;

            var timeGo = new GameObject("Time", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            timeGo.transform.SetParent(go.transform, false);
            var timeRt = timeGo.GetComponent<RectTransform>();
            timeRt.anchorMin = new Vector2(0, 0.5f);
            timeRt.anchorMax = new Vector2(0, 0.5f);
            timeRt.pivot = new Vector2(0, 0.5f);
            timeRt.anchoredPosition = new Vector2(25, -16);
            timeRt.sizeDelta = new Vector2(400, 26);

            var timeTmp = timeGo.GetComponent<TextMeshProUGUI>();
            timeTmp.fontSize = 18;
            timeTmp.color = new Color(0.8f, 0.85f, 0.95f, 0.8f);

            return go;
        }
    }
}
