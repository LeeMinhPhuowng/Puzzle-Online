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

        public int[] neighborIds = new int[4]; // 0: Top, 1: Right, 2: Bottom, 3: Left

        public event Action<PuzzlePiece> OnPiecePlaced;

        private Rigidbody rb;
        private Collider colComponent;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            colComponent = GetComponent<Collider>();
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
