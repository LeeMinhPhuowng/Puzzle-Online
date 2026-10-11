using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PuzzleOnline.Utility;

namespace PlayerController
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float runSpeed = 6f;
        [SerializeField] private float jumpHeight = 2f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Ground Check")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundDistance = 0.4f;
        [SerializeField] private LayerMask groundMask = 1;

        [Header("Movement Smoothing")]
        [SerializeField] private float accelerationTime = 0.1f;
        [SerializeField] private float decelerationTime = 0.1f;

        [Header("Input Settings")]
        [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode jumpKey = KeyCode.Space;

        // Components
        private CharacterController controller;

        [Header("Seat & Position Settings")]
        [Tooltip("Cố định vị trí tại ghế ngồi (không di chuyển WASD, chỉ xoay chuột và tương tác)")]
        [SerializeField] private bool lockPositionAtSeat = false;

        public bool LockPositionAtSeat
        {
            get => lockPositionAtSeat;
            set
            {
                lockPositionAtSeat = value;
                if (controller != null) controller.enabled = !value;
            }
        }

        // Movement variables
        private Vector3 velocity;
        private bool isGrounded;
        private Vector2 currentInputVector;
        private Vector2 smoothInputVelocity;

        // Movement state
        private bool isRunning;
        private float currentSpeed;

        void Start()
        {
            // Get required components
            controller = GetComponent<CharacterController>();

            // Create ground check if it doesn't exist
            if (groundCheck == null)
            {
                GameObject groundCheckObj = new GameObject("GroundCheck");
                groundCheckObj.transform.SetParent(transform);
                groundCheckObj.transform.localPosition = new Vector3(0, -controller.height / 2, 0);
                groundCheck = groundCheckObj.transform;
            }

            StartCoroutine(SnapToMultiplayerSeat());
        }

        /// <summary>
        /// Tự động đưa Player về đúng ghế ngồi theo thứ tự slot trong phòng chơi.
        /// </summary>
        private IEnumerator SnapToMultiplayerSeat()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            // Nếu đang test trực tiếp trong scene (không trong phòng chơi online), giữ nguyên vị trí đặt Player trong scene
            var netCheck = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (netCheck == null || (netCheck.CurrentRoom == null && !netCheck.IsGameActive))
            {
                yield break;
            }

            // 1. Nếu trong scene có PuzzleBoard, hãy để PuzzleBoard định vị chuẩn xác và đồng bộ Avatar
            var board = FindFirstObjectByType<PuzzleSystem.Gameplay.PuzzleBoard>();
            if (board != null)
            {
                board.PositionLocalPlayer(true);
                yield break;
            }

            var seats = SeatFinder.FindSeats();
            if (seats.Count == 0)
            {
                Debug.LogWarning("[PlayerController] Không tìm thấy ghế ngồi nào; giữ nguyên vị trí Player.");
                yield break;
            }

            int slotIndex = 0;
            var network = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (network != null && network.RoomPlayers != null)
            {
                slotIndex = SeatFinder.GetSlotIndex(network.Username, network.RoomPlayers);
            }

            Transform positionsCenter = SeatFinder.FindPositionsParent();
            Transform targetSeat = seats[slotIndex % seats.Count];
            Vector3 lookDirection = (positionsCenter != null ? positionsCenter.position : transform.position) - targetSeat.position;
            lookDirection.y = 0f;
            Quaternion targetRotation = lookDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : targetSeat.rotation;

            TeleportTo(targetSeat.position, targetRotation);
            Debug.Log($"[PlayerController] Đã đặt Player vào {targetSeat.name} (slot {slotIndex + 1}).");
        }

        public void TeleportTo(Vector3 targetPosition, Quaternion targetRotation)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            Physics.SyncTransforms();
            if (controller != null && !lockPositionAtSeat) controller.enabled = true;
        }

        void Update()
        {
            if (FirstPersonCamera.FirstPersonCameraScript.IsOverheadActive) return;
            if (lockPositionAtSeat) return;

            HandleGroundCheck();
            HandleInput();
            HandleMovement();
            HandleGravityAndJump();

            // Apply movement to character controller
            controller.Move(velocity * Time.deltaTime);

            if (velocity.sqrMagnitude > 0.01f)
            {
                var cam = GetComponentInChildren<FirstPersonCamera.FirstPersonCameraScript>();
                float yaw = cam != null ? cam.YRotation : transform.eulerAngles.y;
                float pitch = cam != null ? cam.XRotation : 0f;
                PuzzleOnline.Network.PuzzleNetworkManager.Instance?.SendLook(yaw, pitch, transform.position);
            }
        }

        private void HandleGroundCheck()
        {
            // Kiểm tra tiếp đất dựa trên layer "Ground" hoặc groundMask
            int groundLayerIndex = LayerMask.NameToLayer("Ground");
            int effectiveMask = groundLayerIndex != -1 ? (1 << groundLayerIndex) : groundMask.value;
            if (effectiveMask == 0) effectiveMask = groundMask.value != 0 ? groundMask.value : 1; // Fallback Default nếu mask 0

            Vector3 checkOrigin = transform.position;
            float checkRadius = 0.3f;
            float checkDist = groundDistance;

            if (controller != null)
            {
                checkOrigin = transform.position + controller.center - new Vector3(0f, (controller.height * 0.5f) - controller.radius, 0f);
                checkRadius = Mathf.Max(0.15f, controller.radius * 0.9f);
                checkDist = groundDistance;
            }
            else if (groundCheck != null)
            {
                checkOrigin = groundCheck.position;
            }

            // Raycast / SphereCast xuống dưới để kiểm tra các object có Layer là Ground
            isGrounded = Physics.CheckSphere(checkOrigin, checkRadius + checkDist, effectiveMask, QueryTriggerInteraction.Ignore);
            if (!isGrounded && controller != null && controller.isGrounded)
            {
                isGrounded = true;
            }

            // Reset velocity when grounded
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; // Small negative value to keep grounded
            }
        }

        private void HandleInput()
        {
            // Get input
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            // Check if running
            isRunning = Input.GetKey(runKey);

            // Create input vector
            Vector2 targetInputVector = new Vector2(horizontal, vertical).normalized;

            // Smooth input for better movement feel
            float smoothTime = targetInputVector.magnitude > 0 ? accelerationTime : decelerationTime;
            currentInputVector = Vector2.SmoothDamp(currentInputVector, targetInputVector, ref smoothInputVelocity, smoothTime);
        }

        private void HandleMovement()
        {
            // Calculate current speed based on running state
            currentSpeed = isRunning ? runSpeed : walkSpeed;

            // Calculate movement direction relative to player rotation
            Vector3 moveDirection = transform.right * currentInputVector.x + transform.forward * currentInputVector.y;

            // Apply movement
            velocity.x = moveDirection.x * currentSpeed;
            velocity.z = moveDirection.z * currentSpeed;
        }

        private void HandleGravityAndJump()
        {
            // Handle jumping
            if (Input.GetKeyDown(jumpKey) && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Apply gravity
            velocity.y += gravity * Time.deltaTime;
        }

        // Public methods for external access
        public bool IsGrounded()
        {
            return isGrounded;
        }

        public bool IsRunning()
        {
            return isRunning && currentInputVector.magnitude > 0.1f;
        }

        public bool IsMoving()
        {
            return currentInputVector.magnitude > 0.1f;
        }

        public float GetCurrentSpeed()
        {
            return currentSpeed;
        }

        public Vector3 GetVelocity()
        {
            return velocity;
        }

        public void SetMovementSpeeds(float newWalkSpeed, float newRunSpeed)
        {
            walkSpeed = newWalkSpeed;
            runSpeed = newRunSpeed;
        }

        public void SetJumpHeight(float newJumpHeight)
        {
            jumpHeight = newJumpHeight;
        }

        // Gizmos for debugging
        private void OnDrawGizmosSelected()
        {
            if (groundCheck != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
            }
        }
    }
}
