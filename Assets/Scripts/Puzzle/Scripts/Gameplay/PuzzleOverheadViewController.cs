using UnityEngine;
using PuzzleSystem.Gameplay;
using FirstPersonCamera;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleOverheadViewController : MonoBehaviour
    {
        [Header("Settings")]
        public KeyCode overheadKey = KeyCode.Z;
        public float transitionSpeed = 10f;
        public float overheadDistance = 1.35f;

        private Camera cam;
        private Transform originalParent;
        private Vector3 originalLocalPos;
        private Quaternion originalLocalRot;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                originalLocalPos = cam.transform.localPosition;
                originalLocalRot = cam.transform.localRotation;
            }
        }

        private void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
                originalLocalPos = cam.transform.localPosition;
                originalLocalRot = cam.transform.localRotation;
            }

            PuzzleBoard board = FindFirstObjectByType<PuzzleBoard>();
            if (board == null) return;

            bool holdZ = Input.GetKey(overheadKey);

            if (holdZ)
            {
                FirstPersonCameraScript.IsOverheadActive = true;

                // Overhead pose: straight above board looking down, with camera's up aligned with board.forward
                float height = Mathf.Max(board.currentBoardWidth, board.currentBoardHeight) * 1.2f + overheadDistance;
                Vector3 targetWorldPos = board.transform.position + Vector3.up * height;
                Quaternion targetWorldRot = Quaternion.LookRotation(-Vector3.up, board.transform.forward);

                cam.transform.position = Vector3.Lerp(cam.transform.position, targetWorldPos, Time.deltaTime * transitionSpeed);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetWorldRot, Time.deltaTime * transitionSpeed);
            }
            else if (FirstPersonCameraScript.IsOverheadActive)
            {
                // Smoothly return to original first person camera local transform
                Vector3 parentPos = cam.transform.parent != null ? cam.transform.parent.TransformPoint(originalLocalPos) : originalLocalPos;
                Quaternion parentRot = cam.transform.parent != null ? cam.transform.parent.rotation * originalLocalRot : originalLocalRot;

                cam.transform.position = Vector3.Lerp(cam.transform.position, parentPos, Time.deltaTime * transitionSpeed);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, parentRot, Time.deltaTime * transitionSpeed);

                if (Vector3.Distance(cam.transform.position, parentPos) < 0.05f &&
                    Quaternion.Angle(cam.transform.rotation, parentRot) < 3f)
                {
                    cam.transform.localPosition = originalLocalPos;
                    cam.transform.localRotation = originalLocalRot;
                    FirstPersonCameraScript.IsOverheadActive = false;
                }
            }
            else
            {
                // Constantly cache eye local transform when in normal state
                originalLocalPos = cam.transform.localPosition;
                originalLocalRot = cam.transform.localRotation;
            }
        }
    }
}
