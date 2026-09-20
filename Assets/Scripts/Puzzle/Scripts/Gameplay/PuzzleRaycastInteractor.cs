using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    public class PuzzleRaycastInteractor : MonoBehaviour
    {
        [Header("Camera & Raycast Settings")]
        [Tooltip("Camera chính dùng để raycast (nếu để trống sẽ lấy Camera.main)")]
        public Camera playerCamera;
        public float maxInteractDistance = 6f;
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

            if (PuzzleOnline.UI.PuzzleOnlineUIManager.IsModalOrChatOpen)
            {
                if (heldPiece != null)
                {
                    ReleasePiece();
                }
                return;
            }

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
                    PuzzleOnline.Audio.PuzzleAudioService.Instance?.PlayRotate();
                }
                else if (allowScrollWheelRotation && Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
                {
                    float angle = Input.mouseScrollDelta.y > 0 ? 90f : -90f;
                    targetWorldRot *= Quaternion.Euler(0f, angle, 0f);
                    PuzzleOnline.Audio.PuzzleAudioService.Instance?.PlayRotate();
                }

                // Cập nhật vị trí kéo rê theo mặt phẳng bàn
                UpdateHeldPosition();

                // Đồng bộ vị trí mạng theo nhịp 20Hz
                if (Time.unscaledTime - lastNetworkSyncTime >= 0.05f)
                {
                    lastNetworkSyncTime = Time.unscaledTime;
                    SendHeldPieceNetworkUpdate();
                }

                // Thả mảnh ghép
                if (Input.GetMouseButtonUp(0))
                {
                    ReleasePiece();
                }
            }
        }

        private float lastNetworkSyncTime;

        private void SendHeldPieceNetworkUpdate()
        {
            if (heldPiece == null) return;
            Vector3 local = heldPiece.transform.localPosition;
            int rot = Mathf.RoundToInt(heldPiece.transform.localEulerAngles.y);
            PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendMovePiece(heldPiece.pieceId, local.x, local.z, rot);
        }

        private void TryPickupPiece()
        {
            Ray ray = GetInteractionRay();
            RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractDistance, pieceLayer);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && !piece.isPlaced && !piece.isRemoteControlled)
                {
                    heldPiece = piece;
                    heldPiece.SetHeld(true);
                    PuzzleOnline.Audio.PuzzleAudioService.Instance?.PlayPick();
                    PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendLockPiece(piece.pieceId);

                    // Thiết lập mặt phẳng di chuyển theo phương ngang tại độ cao nhấc bổng
                    Transform board = piece.transform.parent;
                    float planeY = (board != null) ? (board.position.y + liftHeight) : (piece.transform.position.y + liftHeight);
                    tablePlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
                    targetWorldPos = piece.transform.position;
                    targetWorldPos.y = planeY;
                    targetWorldRot = piece.transform.rotation;
                    break;
                }
            }
        }

        private void UpdateHeldPosition()
        {
            Ray ray = GetInteractionRay();
            if (tablePlane.Raycast(ray, out float enter) && enter > 0f)
            {
                targetWorldPos = ray.GetPoint(enter);
            }

            // Giới hạn trong viền mép bàn (để mảnh không thể văng rơi ra ngoài bàn)
            var board = heldPiece.GetComponentInParent<PuzzleBoard>();
            if (board != null)
            {
                targetWorldPos = board.ClampToTableBounds(targetWorldPos, 0.05f);
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
            bool snapped = releasing.TrySnap(snapDistanceTolerance, snapAngleTolerance);
            if (!snapped)
            {
                // Nếu chưa khớp, hạ mảnh chạm về độ cao phẳng chuẩn của mặt bàn cờ (không bao giờ trừ âm làm chìm đất)
                Transform board = releasing.transform.parent;
                if (board != null)
                {
                    Vector3 local = releasing.transform.localPosition;
                    local.y = releasing.correctLocalPos.y + (releasing.pieceId * 0.0003f);
                    releasing.transform.localPosition = local;
                    int rot = Mathf.RoundToInt(releasing.transform.localEulerAngles.y);
                    PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendReleasePiece(releasing.pieceId, local.x, local.z, rot);
                }
                else
                {
                    Vector3 p = releasing.transform.position;
                    p.y -= liftHeight;
                    releasing.transform.position = p;
                    PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendReleasePiece(releasing.pieceId, 0f, 0f, 0);
                }
            }
            else
            {
                Vector3 local = releasing.transform.localPosition;
                int rot = Mathf.RoundToInt(releasing.transform.localEulerAngles.y);
                PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendPlacePiece(releasing.pieceId, local.x, local.z, rot);
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
