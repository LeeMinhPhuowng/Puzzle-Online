using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleOnline.Network;

namespace PuzzleOnline.UI
{
    /// <summary>
    /// Quản lý danh sách bạn bè (Friends Panel):
    /// - Hiển thị danh sách bạn bè từ Server qua Scroll View
    /// - Lọc / tìm kiếm bạn bè theo thời gian thực (FriendSearch)
    /// - Mở modal thêm bạn mới (AddNewFriend)
    /// </summary>
    public class LobbyFriendsPanel : MonoBehaviour
    {
        [Header("Scroll & Items")]
        [SerializeField] private RectTransform friendsContent;
        [SerializeField] private GameObject friendItemTemplate;

        [Header("Actions & Search")]
        [SerializeField] private Button addFriendButton;
        [SerializeField] private TMP_InputField friendSearchInput;

        [Header("Modal Reference")]
        [SerializeField] private AddFriendModalUI addFriendModal;

        private readonly List<GameObject> _spawnedFriendItems = new List<GameObject>();
        private List<FriendInfo> _cachedFriends = new List<FriendInfo>();

        private void Awake()
        {
            SetupScrollContent(friendsContent);

            if (addFriendButton == null)
            {
                addFriendButton = transform.Find("AddNewFriend")?.GetComponent<Button>()
                               ?? transform.Find("Header/AddNewFriend")?.GetComponent<Button>();
            }

            if (addFriendButton != null)
            {
                addFriendButton.onClick.AddListener(OnAddFriendClicked);
            }

            if (friendSearchInput != null)
            {
                friendSearchInput.onValueChanged.AddListener(OnFriendSearchChanged);
            }

            if (friendItemTemplate != null && friendItemTemplate.transform.parent == friendsContent)
            {
                friendItemTemplate.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (addFriendButton != null) addFriendButton.onClick.RemoveListener(OnAddFriendClicked);
            if (friendSearchInput != null) friendSearchInput.onValueChanged.RemoveListener(OnFriendSearchChanged);
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
            vlg.padding = new RectOffset(6, 6, 8, 8);
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

        public void SetFriends(List<FriendInfo> friends)
        {
            _cachedFriends = friends != null ? new List<FriendInfo>(friends) : new List<FriendInfo>();
            RenderFriends(_cachedFriends);
        }

        private void RenderFriends(List<FriendInfo> friends)
        {
            if (friendsContent == null || friendItemTemplate == null) return;

            foreach (var item in _spawnedFriendItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedFriendItems.Clear();

            if (friends == null || friends.Count == 0) return;

            for (int i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                GameObject itemGo = Instantiate(friendItemTemplate, friendsContent);
                itemGo.SetActive(true);
                _spawnedFriendItems.Add(itemGo);

                var friendItem = itemGo.GetComponent<UIFriendItem>();
                if (friendItem != null)
                {
                    friendItem.Bind(friend, (friendUsername) =>
                    {
                        PuzzleNetworkManager.Instance?.SendInvite(friendUsername);
                    });
                }
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(friendsContent);
        }

        private void OnFriendSearchChanged(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                RenderFriends(_cachedFriends);
                return;
            }

            string lower = query.ToLower();
            var filtered = _cachedFriends.FindAll(f =>
                (f.Username != null && f.Username.ToLower().Contains(lower))
            );
            RenderFriends(filtered);
        }

        private void OnAddFriendClicked()
        {
            if (addFriendModal == null)
            {
                addFriendModal = FindFirstObjectByType<AddFriendModalUI>(FindObjectsInactive.Include);
            }

            if (addFriendModal != null)
            {
                addFriendModal.Open();
            }
            else
            {
                var frm = GameObject.Find("AddFriendView") 
                       ?? GameObject.Find("AddFriendFrm");
                if (frm != null) frm.SetActive(true);
            }
        }
    }
}
