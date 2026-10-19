using System;
using System.Collections;
using UnityEngine;
using PuzzleOnline.Network;
using PuzzleSystem.Gameplay;

namespace PuzzleOnline.Gameplay.Competitive
{
    public sealed class PlayerHeldItemVisuals : MonoBehaviour
    {
        [SerializeField] private GameObject flashlight;
        [SerializeField] private GameObject ink;
        [SerializeField] private GameObject glove;
        [SerializeField] private GameObject magnet;
        [SerializeField] private GameObject crowbar;
        [SerializeField] private Animator animator;

        private RemotePlayerAvatar remoteAvatar;
        private string displayedItem = "";
        private bool isAnimationPlaying = false;
        private Coroutine hideCoroutine;

        private void Awake()
        {
            // Tự động tìm kiếm các model item dưới Head nếu chưa được gán trong Inspector
            if (flashlight == null) flashlight = transform.Find("Head/portable_searchlight")?.gameObject;
            if (ink == null) ink = transform.Find("Head/encre")?.gameObject;
            if (glove == null) glove = transform.Find("Head/gloves")?.gameObject;
            if (magnet == null) magnet = transform.Find("Head/Magnet")?.gameObject;
            if (crowbar == null) crowbar = transform.Find("Head/crowbar")?.gameObject;

            // Khởi tạo Animator và gán Player.controller
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            if (animator.runtimeAnimatorController == null)
            {
                animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Player");
            }

            SetItem("");
        }

        private void Start()
        {
            remoteAvatar = GetComponent<RemotePlayerAvatar>();
        }

        private void OnEnable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemUsed += HandleItemUsed;
            }
        }

        private void OnDisable()
        {
            var net = PuzzleNetworkManager.Instance;
            if (net != null)
            {
                net.OnItemUsed -= HandleItemUsed;
            }
            SetItem("");
        }

        private void HandleItemUsed(string itemType, string byUser)
        {
            var net = PuzzleNetworkManager.Instance;
            string myUser = remoteAvatar != null ? remoteAvatar.username : net?.Username;
            if (!string.IsNullOrEmpty(myUser) && string.Equals(myUser, byUser, StringComparison.OrdinalIgnoreCase))
            {
                PlayUseAnimation(itemType);
            }
        }

        private void Update()
        {
            if (isAnimationPlaying) return;

            var net = PuzzleNetworkManager.Instance;
            string user = remoteAvatar != null ? remoteAvatar.username : net?.Username;
            string item = net != null && net.IsGameActive && !net.GameplaySyncService.HasRoundEnded
                ? net.GameplaySyncService.GetHeldItem(user) : "";
            if (displayedItem != item) SetItem(item);
        }

        public void SetItem(string item)
        {
            displayedItem = item ?? "";
            if (flashlight != null) flashlight.SetActive(displayedItem == "FLASHLIGHT");
            if (ink != null) ink.SetActive(displayedItem == "INK");
            if (glove != null) glove.SetActive(displayedItem == "SLAP");
            if (magnet != null) magnet.SetActive(displayedItem == "MAGNET");
            if (crowbar != null) crowbar.SetActive(displayedItem == "SHOVEL");
        }

        /// <summary>
        /// Phát animation khi người chơi sử dụng vật phẩm và duy trì hiển thị model trong lúc vung/tác động.
        /// </summary>
        public void PlayUseAnimation(string itemType = null)
        {
            string targetItem = !string.IsNullOrEmpty(itemType) ? itemType : displayedItem;
            if (!string.IsNullOrEmpty(targetItem))
            {
                SetItem(targetItem);
            }

            if (animator == null) animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.Play("Item", 0, 0f);
            }

            if (hideCoroutine != null) StopCoroutine(hideCoroutine);
            hideCoroutine = StartCoroutine(HideItemAfterAnimation(0.45f));
        }

        private IEnumerator HideItemAfterAnimation(float duration)
        {
            isAnimationPlaying = true;
            yield return new WaitForSeconds(duration);
            isAnimationPlaying = false;

            var net = PuzzleNetworkManager.Instance;
            string user = remoteAvatar != null ? remoteAvatar.username : net?.Username;
            string currentNetItem = net != null && net.IsGameActive && !net.GameplaySyncService.HasRoundEnded
                ? net.GameplaySyncService.GetHeldItem(user) : "";
            SetItem(currentNetItem);
        }

        public bool Contains(Transform child) =>
            (flashlight != null && child.IsChildOf(flashlight.transform)) ||
            (ink != null && child.IsChildOf(ink.transform)) ||
            (glove != null && child.IsChildOf(glove.transform)) ||
            (magnet != null && child.IsChildOf(magnet.transform)) ||
            (crowbar != null && child.IsChildOf(crowbar.transform));
    }
}
