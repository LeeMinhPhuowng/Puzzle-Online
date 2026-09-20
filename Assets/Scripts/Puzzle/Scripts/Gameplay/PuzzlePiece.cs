using System;
using UnityEngine;

namespace PuzzleSystem.Gameplay
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class PuzzlePiece : MonoBehaviour
    {
        [Header("Piece Identity")]
        public int pieceId;
        public int row;
        public int col;

        [Header("Target Snap Position (Local to Board)")]
        public Vector3 correctLocalPos;
        public Quaternion correctLocalRot = Quaternion.identity;

        [Header("State")]
        public bool isPlaced = false;
        public bool isBeingHeld = false;
        public string lockedByPlayer = "";

        [Header("Remote Sync")]
        private Vector3 targetRemotePos;
        private Quaternion targetRemoteRot;
        private bool hasRemoteTarget = false;

        public bool isRemoteControlled
        {
            get
            {
                if (string.IsNullOrEmpty(lockedByPlayer)) return false;
                string myName = PuzzleOnline.Network.PuzzleNetworkManager.Instance?.Username;
                if (!string.IsNullOrEmpty(myName) && lockedByPlayer.Equals(myName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                return true;
            }
        }

        public int[] neighborIds = new int[4]; // 0: Top, 1: Right, 2: Bottom, 3: Left

        public event Action<PuzzlePiece> OnPiecePlaced;

        [Header("Lift & Remote Settings")]
        public float liftHeight = 0.035f;

        private Rigidbody rb;
        private Collider colComponent;
        private MeshRenderer meshRenderer;
        private Color originalColor = Color.white;
        private Material pieceMaterial;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colComponent = GetComponent<Collider>();
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                pieceMaterial = meshRenderer.material;
                if (pieceMaterial.HasProperty("_BaseColor")) originalColor = pieceMaterial.GetColor("_BaseColor");
                else if (pieceMaterial.HasProperty("_Color")) originalColor = pieceMaterial.color;
            }
            targetRemotePos = transform.localPosition;
            targetRemoteRot = transform.localRotation;
        }

        private void Update()
        {
            if (hasRemoteTarget && !isBeingHeld && !isPlaced)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetRemotePos, Time.deltaTime * 20f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRemoteRot, Time.deltaTime * 20f);
                if (Vector3.Distance(transform.localPosition, targetRemotePos) < 0.0005f)
                {
                    hasRemoteTarget = false;
                }
            }
        }

        public void SetRemoteLock(string playerName)
        {
            bool wasRemote = isRemoteControlled;
            lockedByPlayer = playerName ?? "";
            if (rb != null) rb.isKinematic = isRemoteControlled;

            if (isRemoteControlled && !wasRemote && !isPlaced)
            {
                // Khi người khác vừa nhấc mảnh lên: Nâng bổng mảnh lên không trung để mọi người đều nhìn thấy
                Vector3 current = transform.localPosition;
                current.y = correctLocalPos.y + (pieceId * 0.0003f) + liftHeight;
                targetRemotePos = current;
                hasRemoteTarget = true;

                // Đổi ánh màu báo hiệu đang có người giữ
                if (pieceMaterial != null)
                {
                    Color heldColor = Color.Lerp(originalColor, new Color(1f, 0.75f, 0.4f), 0.45f);
                    if (pieceMaterial.HasProperty("_BaseColor")) pieceMaterial.SetColor("_BaseColor", heldColor);
                    else if (pieceMaterial.HasProperty("_Color")) pieceMaterial.color = heldColor;
                }
            }
            else if (!isRemoteControlled && wasRemote && !isPlaced)
            {
                // Khi người khác thả mảnh: Hạ mảnh xuống chạm mặt phẳng chuẩn của bàn
                Vector3 current = transform.localPosition;
                current.y = correctLocalPos.y + (pieceId * 0.0003f);
                targetRemotePos = current;
                hasRemoteTarget = true;

                // Khôi phục màu gốc
                if (pieceMaterial != null)
                {
                    if (pieceMaterial.HasProperty("_BaseColor")) pieceMaterial.SetColor("_BaseColor", originalColor);
                    else if (pieceMaterial.HasProperty("_Color")) pieceMaterial.color = originalColor;
                }
            }
        }

        public void SetRemoteTarget(Vector3 localPos, Quaternion localRot)
        {
            if (isRemoteControlled && !isPlaced)
            {
                // Khi đang bị người khác kéo rê, giữ nguyên độ cao nâng bổng trên không
                localPos.y = correctLocalPos.y + (pieceId * 0.0003f) + liftHeight;
            }
            targetRemotePos = localPos;
            targetRemoteRot = localRot;
            hasRemoteTarget = true;
        }

        /// <summary>
        /// Thử kiểm tra và hút mảnh ghép vào vị trí đúng trên bàn cờ.
        /// </summary>
        public bool TrySnap(float posTolerance = 0.06f, float angleTolerance = 35f)
        {
            if (isPlaced) return true;

            Transform board = transform.parent;
            if (board == null) return false;

            Vector3 currentLocalPos = transform.localPosition;
            Quaternion currentLocalRot = transform.localRotation;

            // Tính khoảng cách trên mặt phẳng ngang X-Z của bàn cờ (bỏ qua độ nhấc Y khi đang được cầm)
            float distXZ = Vector2.Distance(
                new Vector2(currentLocalPos.x, currentLocalPos.z),
                new Vector2(correctLocalPos.x, correctLocalPos.z)
            );

            // Chênh lệch độ cao Y không quá 12cm
            float distY = Mathf.Abs(currentLocalPos.y - correctLocalPos.y);

            float angle = Quaternion.Angle(currentLocalRot, correctLocalRot);

            // Kiểm tra dung sai khoảng cách và góc xoay
            if (distXZ <= posTolerance && distY <= 0.12f && (angle <= angleTolerance || angle >= 360f - angleTolerance))
            {
                SnapToTarget();
                return true;
            }

            return false;
        }

        public void SnapToTarget()
        {
            if (isPlaced) return;
            isPlaced = true;
            isBeingHeld = false;

            transform.localPosition = correctLocalPos;
            transform.localRotation = correctLocalRot;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            OnPiecePlaced?.Invoke(this);
            PuzzleOnline.Audio.PuzzleAudioService.Instance?.PlaySnap();
        }

        public void SetHeld(bool held)
        {
            isBeingHeld = held;
            if (rb != null)
            {
                rb.isKinematic = held;
            }
        }
    }
}
