using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleRaycastInteractor : MonoBehaviour
    {
        [Header("Camera & Raycast Settings")]
        [Tooltip("Camera chính dùng để raycast (nếu để trống sẽ lấy Camera.main)")]
        public Camera playerCamera;
        public float maxInteractDistance = 4f;
        public LayerMask pieceLayer = ~0;

        [Header("Pick & Place Parameters")]
        public float liftHeight = 0.035f; // Độ nhấc bổng khi cầm mảnh
        public float positionSmoothSpeed = 25f;
        public float snapDistanceTolerance = 0.06f;
        public float snapAngleTolerance = 35f;
        public KeyCode rotateKey = KeyCode.R;
        public bool allowScrollWheelRotation = true;

        [Header("Current Interaction State")]
        public PuzzlePiece heldPiece = null;
        private Plane tablePlane;
        private Vector3 targetWorldPos;
        private Quaternion targetWorldRot;

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (playerCamera == null) return;

            // Xử lý bắt đầu cầm mảnh ghép
            if (Input.GetMouseButtonDown(0))
            {
                TryPickupPiece();
            }

            // Khi đang cầm mảnh
            if (heldPiece != null)
            {
                // Xoay mảnh 90 độ khi bấm phím R (hoặc lăn chuột)
                if (Input.GetKeyDown(rotateKey))
                {
                    targetWorldRot *= Quaternion.Euler(0f, 90f, 0f);
                }
                else if (allowScrollWheelRotation && Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
                {
                    float angle = Input.mouseScrollDelta.y > 0 ? 90f : -90f;
                    targetWorldRot *= Quaternion.Euler(0f, angle, 0f);
                }

                // Cập nhật vị trí kéo rê theo mặt phẳng bàn
                UpdateHeldPosition();

                // Thả mảnh ghép
                if (Input.GetMouseButtonUp(0))
                {
                    ReleasePiece();
                }
            }
        }

        private void TryPickupPiece()
        {
            Ray ray = GetInteractionRay();
            if (Physics.Raycast(ray, out RaycastHit hit, maxInteractDistance, pieceLayer))
            {
                PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && !piece.isPlaced)
                {
                    heldPiece = piece;
                    heldPiece.SetHeld(true);

                    // Thiết lập mặt phẳng di chuyển theo phương ngang tại độ cao nhấc bổng
                    Transform board = piece.transform.parent;
                    float planeY = (board != null) ? (board.position.y + liftHeight) : (piece.transform.position.y + liftHeight);
                    tablePlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
                    targetWorldPos = piece.transform.position;
                    targetWorldPos.y = planeY;
                    targetWorldRot = piece.transform.rotation;
                }
            }
        }

        private void UpdateHeldPosition()
        {
            Ray ray = GetInteractionRay();
            if (tablePlane.Raycast(ray, out float enter))
            {
                targetWorldPos = ray.GetPoint(enter);
            }

            // Di chuyển mượt về targetWorldPos
            heldPiece.transform.position = Vector3.Lerp(
                heldPiece.transform.position,
                targetWorldPos,
                Time.deltaTime * positionSmoothSpeed
            );

            // Xoay mượt về targetWorldRot
            heldPiece.transform.rotation = Quaternion.Slerp(
                heldPiece.transform.rotation,
                targetWorldRot,
                Time.deltaTime * positionSmoothSpeed
            );
        }

        private void ReleasePiece()
        {
            if (heldPiece == null) return;

            PuzzlePiece releasing = heldPiece;
            heldPiece = null;
            releasing.SetHeld(false);

            // Kiểm tra hút vào vị trí đúng trên bàn cờ
            if (!releasing.TrySnap(snapDistanceTolerance, snapAngleTolerance))
            {
                // Nếu chưa khớp, hạ mảnh chạm về độ cao phẳng chuẩn của mặt bàn cờ (không bao giờ trừ âm làm chìm đất)
                Transform board = releasing.transform.parent;
                if (board != null)
                {
                    Vector3 local = releasing.transform.localPosition;
                    local.y = releasing.correctLocalPos.y;
                    releasing.transform.localPosition = local;
                }
                else
                {
                    Vector3 p = releasing.transform.position;
                    p.y -= liftHeight;
                    releasing.transform.position = p;
                }
            }
        }

        private Ray GetInteractionRay()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // Chế độ First-Person: Raycast từ tâm màn hình camera
                return playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            else
            {
                // Chế độ chuột tự do: Raycast theo toạ độ con trỏ chuột
                return playerCamera.ScreenPointToRay(Input.mousePosition);
            }
        }
    }
}
