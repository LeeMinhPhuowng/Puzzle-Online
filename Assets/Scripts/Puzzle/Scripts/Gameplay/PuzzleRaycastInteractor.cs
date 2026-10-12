using UnityEngine;
using PuzzleOnline.Utility;
using PuzzleOnline.Core;

namespace PuzzleSystem.Gameplay
{
    /// <summary>
    /// Xử lý tương tác gắp thả, xoay và hút ghép mảnh qua Raycast.
    /// Sử dụng IAudioService, INetworkService, IUIService qua ServiceLocator để hỗ trợ Dependency Injection.
    /// </summary>
    public class PuzzleRaycastInteractor : MonoBehaviour
    {
        [Header("Camera & Raycast Settings")]
        [Tooltip("Camera chính dùng để raycast (nếu để trống sẽ lấy Camera.main)")]
        public Camera playerCamera;
        public float maxInteractDistance = 15f;
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
        private float lastNetworkSyncTime;
        private readonly TargetOutlineSelection selection = new TargetOutlineSelection();

        // Dependencies (Injectable for Unit Testing)
        private IAudioService _audioService;
        private INetworkService _networkService;
        private IUIService _uiService;

        public void SetDependencies(IAudioService audio, INetworkService network, IUIService ui)
        {
            _audioService = audio;
            _networkService = network;
            _uiService = ui;
        }

        private IAudioService Audio => _audioService ??= ServiceLocator.Get<IAudioService>();
        private INetworkService Network => _networkService ??= ServiceLocator.Get<INetworkService>();
        private IUIService UI => _uiService ??= ServiceLocator.Get<IUIService>();

        private void Start()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (playerCamera == null) { selection.Set(null); return; }

            if ((UI != null && UI.IsModalOrChatOpen) || InteractionTargetQuery.IsBlocked)
            {
                selection.Set(null);
                if (heldPiece != null)
                {
                    ReleasePiece();
                }
                return;
            }

            var net = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            PuzzlePiece hovered = null;
            if (!IsStunned && heldPiece == null && (net == null || !net.IsSaboteur) &&
                (net == null || !net.GameplaySyncService.HasRoundEnded) &&
                InteractionTargetQuery.TryHit(playerCamera, maxInteractDistance, out var hoverHit, includePlayers: false))
            {
                var piece = hoverHit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && !piece.isPlaced && !piece.isRemoteControlled &&
                    (pieceLayer.value & (1 << piece.gameObject.layer)) != 0) hovered = piece;
            }
            selection.Set(hovered != null ? hovered.gameObject : null);

            // Xử lý bắt đầu cầm mảnh ghép
            if (Input.GetMouseButtonDown(0))
            {
                if (hovered != null) TryPickupPiece();
            }

            // Khi đang cầm mảnh
            if (heldPiece != null)
            {
                // Xoay mảnh 90 độ khi bấm phím R (hoặc lăn chuột)
                if (Input.GetKeyDown(rotateKey))
                {
                    targetWorldRot *= Quaternion.Euler(0f, 90f, 0f);
                    Audio?.PlayRotate();
                }
                else if (allowScrollWheelRotation && Mathf.Abs(Input.mouseScrollDelta.y) > 0.1f)
                {
                    float angle = Input.mouseScrollDelta.y > 0 ? 90f : -90f;
                    targetWorldRot *= Quaternion.Euler(0f, angle, 0f);
                    Audio?.PlayRotate();
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

        public bool IsStunned => Time.time < stunEndTime;
        private float stunEndTime = 0f;
        private float magnetDebuffEndTime = 0f;

        public void Stun(float duration)
        {
            stunEndTime = Time.time + duration;
            ForceDropHeldPiece();
        }

        public void ApplyMagnetDebuff(float duration = 6f)
        {
            if (heldPiece != null)
            {
                ForceDropHeldPiece();
            }
            else
            {
                magnetDebuffEndTime = Time.time + duration;
            }
        }

        public void ForceDropHeldPiece()
        {
            if (heldPiece != null)
            {
                heldPiece.SetHeld(false);
                Network?.ReleasePiece(heldPiece.pieceId, heldPiece.transform.localPosition.x, heldPiece.transform.localPosition.z, Mathf.RoundToInt(heldPiece.transform.localEulerAngles.y));
                heldPiece = null;
            }
        }

        private void SendHeldPieceNetworkUpdate()
        {
            if (heldPiece == null) return;
            Vector3 local = heldPiece.transform.localPosition;
            int rot = Mathf.RoundToInt(heldPiece.transform.localEulerAngles.y);
            Network?.MovePiece(heldPiece.pieceId, local.x, local.z, rot);
        }

        private void TryPickupPiece()
        {
            if (IsStunned) return;

            // Chặn Saboteur không thể cầm/gắp mảnh ghép!
            if (PuzzleOnline.Network.PuzzleNetworkManager.Instance != null &&
                PuzzleOnline.Network.PuzzleNetworkManager.Instance.IsSaboteur)
            {
                return;
            }

            if (Time.time < magnetDebuffEndTime)
            {
                magnetDebuffEndTime = 0f;
                Audio?.PlayRotate();
                return;
            }

            if (InteractionTargetQuery.TryHit(playerCamera, maxInteractDistance, out var hit, includePlayers: false))
            {
                PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (piece != null && !piece.isPlaced && !piece.isRemoteControlled)
                {
                    heldPiece = piece;
                    heldPiece.SetHeld(true);
                    Audio?.PlayPick();
                    Network?.LockPiece(piece.pieceId);

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

        private void OnDisable() => selection.Set(null);

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

        public void ReleasePiece()
        {
            if (heldPiece == null) return;

            PuzzlePiece releasing = heldPiece;
            heldPiece = null;
            releasing.SetHeld(false);

            // Kiểm tra hút vào vị trí đúng trên bàn cờ
            bool snapped = releasing.TrySnap(snapDistanceTolerance, snapAngleTolerance);
            if (!snapped)
            {
                // Nếu chưa khớp, hạ mảnh chạm về độ cao phẳng chuẩn của mặt bàn cờ
                Transform board = releasing.transform.parent;
                if (board != null)
                {
                    Vector3 local = releasing.transform.localPosition;
                    local.y = releasing.correctLocalPos.y + (releasing.pieceId * 0.0003f);
                    releasing.transform.localPosition = local;
                    int rot = Mathf.RoundToInt(releasing.transform.localEulerAngles.y);
                    Network?.ReleasePiece(releasing.pieceId, local.x, local.z, rot);
                }
                else
                {
                    Vector3 p = releasing.transform.position;
                    p.y -= liftHeight;
                    releasing.transform.position = p;
                    Network?.ReleasePiece(releasing.pieceId, 0f, 0f, 0);
                }
                Audio?.PlayDrop();
            }
            else
            {
                Vector3 local = releasing.transform.localPosition;
                int rot = Mathf.RoundToInt(releasing.transform.localEulerAngles.y);
                Network?.PlacePiece(releasing.pieceId, local.x, local.z, rot);
            }
        }

        private Ray GetInteractionRay()
        {
            // Nếu đang bật góc nhìn trên cao Overhead, luôn raycast tự do theo vị trí con trỏ chuột
            if (FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive)
            {
                return playerCamera.ScreenPointToRay(Input.mousePosition);
            }

            if (CursorManager.IsLocked)
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
