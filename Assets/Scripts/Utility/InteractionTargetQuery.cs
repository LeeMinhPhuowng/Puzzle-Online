using UnityEngine;
using UnityEngine.EventSystems;
using PuzzleOnline.UI;

namespace PuzzleOnline.Utility
{
    public static class InteractionTargetQuery
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[128];

        public static bool IsBlocked => PuzzleOnlineUIManager.IsModalOrChatOpen ||
            (Cursor.lockState != CursorLockMode.Locked && EventSystem.current != null &&
             EventSystem.current.IsPointerOverGameObject());

        public static Ray GetRay(Camera camera) => camera.ScreenPointToRay(
            Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f) : Input.mousePosition);

        public static bool TryHit(Camera camera, float distance, out RaycastHit hit, bool includePlayers = true)
            => TryHit(camera, camera != null ? GetRay(camera) : default, distance, out hit, includePlayers);

        public static bool TryHit(Camera camera, Ray ray, float distance, out RaycastHit hit, bool includePlayers = true)
        {
            hit = default;
            if (camera == null) return false;
            int count = Physics.RaycastNonAlloc(ray, Hits, distance, ~0, QueryTriggerInteraction.Collide);
            if (count == Hits.Length) return false; // Do not choose through an unreported blocker.
            float nearest = float.PositiveInfinity;
            Transform localPlayer = camera.GetComponentInParent<PlayerController.PlayerController>()?.transform;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (localPlayer != null && collider.transform.IsChildOf(localPlayer)) continue;
                if (collider.isTrigger && (!includePlayers || collider.GetComponentInParent<PuzzleSystem.Gameplay.RemotePlayerAvatar>() == null))
                    continue;
                if (Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    hit = Hits[i];
                }
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
