using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Quản lý danh sách bàn chơi (Rooms Panel):
    /// - Hiển thị danh sách bàn chơi từ Server thông qua Scroll View
    /// - Lọc / tìm kiếm bàn chơi theo thời gian thực (RoomSearch)
    /// - Vào nhanh bàn chơi theo ID hoặc tên (QuickJoin)
    /// </summary>
    public class LobbyRoomsPanel : MonoBehaviour
    {
        [Header("Scroll & Items")]
        [SerializeField] private RectTransform roomsContent;
        [SerializeField] private GameObject roomItemTemplate;

        [Header("Search & Quick Join")]
        [SerializeField] private TMP_InputField roomSearchInput;
        [SerializeField] private TMP_InputField roomQuickJoinInput;
        [SerializeField] private Button quickJoinButton;

        private readonly List<GameObject> _spawnedRoomItems = new List<GameObject>();
        private List<RoomInfo> _cachedRooms = new List<RoomInfo>();

        private void Awake()
        {
            SetupScrollContent(roomsContent);

            if (quickJoinButton != null)
            {
                quickJoinButton.onClick.AddListener(OnQuickJoinClicked);
            }

            if (roomSearchInput != null)
            {
                roomSearchInput.onValueChanged.AddListener(OnRoomSearchChanged);
            }

            if (roomQuickJoinInput != null)
            {
                roomQuickJoinInput.onSubmit.AddListener((_) => OnQuickJoinClicked());
            }

            // Ẩn template gốc nếu nó nằm trong content
            if (roomItemTemplate != null && roomItemTemplate.transform.parent == roomsContent)
            {
                roomItemTemplate.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (quickJoinButton != null) quickJoinButton.onClick.RemoveListener(OnQuickJoinClicked);
            if (roomSearchInput != null) roomSearchInput.onValueChanged.RemoveListener(OnRoomSearchChanged);
            if (roomQuickJoinInput != null) roomQuickJoinInput.onSubmit.RemoveAllListeners();
        }

        private void SetupScrollContent(RectTransform content)
        {
            if (content == null) return;

            RectTransform viewport = content.parent as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.pivot = new Vector2(0f, 1f);
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = new Vector2(-16f, 0f);

                var oldMask = viewport.GetComponent<Mask>();
                if (oldMask != null) Destroy(oldMask);

                var maskImg = viewport.GetComponent<Image>();
                if (maskImg != null) maskImg.enabled = false;

                if (viewport.GetComponent<RectMask2D>() == null)
                {
                    viewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            if (viewport != null && viewport.parent is RectTransform scrollView)
            {
                scrollView.anchorMin = Vector2.zero;
                scrollView.anchorMax = Vector2.one;
                scrollView.pivot = new Vector2(0.5f, 0.5f);
                scrollView.offsetMin = new Vector2(16f, 20f);
                scrollView.offsetMax = new Vector2(-16f, -165f);
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();

            vlg.spacing = 14f;
            vlg.padding = new RectOffset(8, 8, 10, 10);
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

        public void SetRooms(List<RoomInfo> rooms)
        {
            _cachedRooms = rooms != null ? rooms.FindAll(r => r != null &&
                !(r.GameMode == "COMPETITIVE" && r.State != "WAITING")) : new List<RoomInfo>();
            OnRoomSearchChanged(roomSearchInput != null ? roomSearchInput.text : "");
        }

        private void RenderRooms(List<RoomInfo> rooms)
        {
            if (roomsContent == null || roomItemTemplate == null) return;

            foreach (var item in _spawnedRoomItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedRoomItems.Clear();

            if (rooms == null || rooms.Count == 0) return;

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                GameObject itemGo = Instantiate(roomItemTemplate, roomsContent);
                itemGo.SetActive(true);
                _spawnedRoomItems.Add(itemGo);

                var roomItem = itemGo.GetComponent<UIRoomItem>();
                if (roomItem != null)
                {
                    roomItem.Bind(room, (targetRoom) =>
                    {
                        PuzzleNetworkManager.Instance?.JoinRoom(targetRoom.Id);
                    });
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(roomsContent);
        }

        private void OnRoomSearchChanged(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                RenderRooms(_cachedRooms);
                return;
            }

            string lower = query.ToLower();
            var filtered = _cachedRooms.FindAll(r =>
                (r.Name != null && r.Name.ToLower().Contains(lower)) ||
                (r.Host != null && r.Host.ToLower().Contains(lower)) ||
                (r.Id != null && r.Id.ToLower().Contains(lower))
            );
            RenderRooms(filtered);
        }

        private void OnQuickJoinClicked()
        {

            if (roomQuickJoinInput == null || string.IsNullOrWhiteSpace(roomQuickJoinInput.text))
            {
                PuzzleOnlineUIManager.Instance?.ShowToast("Vui lòng nhập mã phòng.");
                return;
            }

            string inputVal = roomQuickJoinInput.text.Trim();
            var room = _cachedRooms.Find(r =>
                string.Equals(r.Id, inputVal, StringComparison.OrdinalIgnoreCase)
            );

            if (room != null)
            {
                PuzzleNetworkManager.Instance?.JoinRoom(room.Id);
            }
            else
            {
                PuzzleNetworkManager.Instance?.JoinRoom(inputVal);
            }
        }
    }
}
