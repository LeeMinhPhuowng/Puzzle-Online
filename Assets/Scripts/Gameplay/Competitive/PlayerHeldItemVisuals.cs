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
        private RemotePlayerAvatar remoteAvatar;
        private string displayedItem;

        private void Awake()
        {
            SetItem("");
        }

        private void Start()
        {
            remoteAvatar = GetComponent<RemotePlayerAvatar>();
        }

        private void Update()
        {
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

        public bool Contains(Transform child) =>
            (flashlight != null && child.IsChildOf(flashlight.transform)) ||
            (ink != null && child.IsChildOf(ink.transform)) ||
            (glove != null && child.IsChildOf(glove.transform)) ||
            (magnet != null && child.IsChildOf(magnet.transform)) ||
            (crowbar != null && child.IsChildOf(crowbar.transform));

        private void OnDisable() => SetItem("");
    }
}
