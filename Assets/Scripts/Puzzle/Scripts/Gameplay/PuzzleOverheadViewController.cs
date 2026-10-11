using UnityEngine;
using PuzzleSystem.Gameplay;
using FirstPersonCamera;
using PuzzleOnline.Utility;

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

        private bool isOverheadToggled = false;
        public bool IsOverheadToggled => isOverheadToggled;

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

            // Bỏ qua nếu đang mở Menu / Chat / Modal
            if (PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
            {
                return;
            }

            PuzzleBoard board = FindFirstObjectByType<PuzzleBoard>();
            if (board == null) return;

            // Xử lý Toggle phím Z
            if (Input.GetKeyDown(overheadKey) && GUIUtility.keyboardControl == 0)
            {
                SetOverhead(!isOverheadToggled);
            }

            if (isOverheadToggled)
            {
                FirstPersonCameraScript.IsOverheadActive = true;

                // Đảm bảo chuột luôn mở khóa và hiển thị tự do để người chơi click kéo thả mảnh ghép
                if (CursorManager.IsLocked)
                {
                    CursorManager.Unlock();
                }

                // Vị trí camera nhìn từ đỉnh vuông góc xuống bàn cờ
                float height = Mathf.Max(board.currentBoardWidth, board.currentBoardHeight) * 1.2f + overheadDistance;
                Vector3 targetWorldPos = board.transform.position + Vector3.up * height;
                Quaternion targetWorldRot = Quaternion.LookRotation(-Vector3.up, board.transform.forward);

                cam.transform.position = Vector3.Lerp(cam.transform.position, targetWorldPos, Time.deltaTime * transitionSpeed);
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetWorldRot, Time.deltaTime * transitionSpeed);
            }
            else if (FirstPersonCameraScript.IsOverheadActive)
            {
                // Mượt mà đưa camera trở về vị trí mắt nhân vật FPS
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

                    // Khóa lại chuột cho góc nhìn thứ nhất FPS nếu không mở menu
                    if (!PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
                    {
                        CursorManager.Lock();
                    }
                }
            }
            else
            {
                // Luôn cập nhật vị trí mắt khi đang ở chế độ bình thường
                originalLocalPos = cam.transform.localPosition;
                originalLocalRot = cam.transform.localRotation;
            }
        }

        public void SetOverhead(bool active)
        {
            if (isOverheadToggled == active) return;
            isOverheadToggled = active;

            if (isOverheadToggled)
            {
                FirstPersonCameraScript.IsOverheadActive = true;
                CursorManager.Unlock();
            }
            else
            {
                // Khi thoát overhead, nếu đang cầm mảnh dở thì thả mảnh xuống bàn an toàn
                var interactor = GetComponent<PuzzleRaycastInteractor>() ?? FindFirstObjectByType<PuzzleRaycastInteractor>();
                if (interactor != null && interactor.heldPiece != null)
                {
                    interactor.ReleasePiece();
                }
            }
        }
    }
}
