using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace PlayerController // Or any other appropriate namespace
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
        [SerializeField] private bool lockPositionAtSeat = true;

        public bool LockPositionAtSeat
        {
            get => lockPositionAtSeat;
            set => lockPositionAtSeat = value;
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
        /// The player prefab can be present before PuzzleBoard is initialized (or
        /// when a scene is tested directly in the editor).  Make seating owned by
        /// the player itself so its editor placement can never become its in-game
        /// spawn point.
        /// </summary>
        private IEnumerator SnapToMultiplayerSeat()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            var positions = GameObject.Find("Positions") ?? GameObject.Find("positions");
            if (positions == null)
            {
                Debug.LogWarning("[PlayerController] Không tìm thấy object 'Positions'; giữ nguyên vị trí Player.");
                yield break;
            }

            var seats = new List<Transform>();
            for (int number = 1; number <= 4; number++)
            {
                var seat = positions.transform.Find($"Position ({number})");
                if (seat != null) seats.Add(seat);
            }

            if (seats.Count == 0)
            {
                Debug.LogWarning("[PlayerController] 'Positions' không có Position (1) đến Position (4).");
                yield break;
            }

            int slotIndex = 0;
            var network = PuzzleOnline.Network.PuzzleNetworkManager.Instance;
            if (network != null && network.RoomPlayers.Count > 0)
            {
                for (int i = 0; i < network.RoomPlayers.Count; i++)
                {
                    if (network.RoomPlayers[i].Username == network.Username)
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }

            Transform targetSeat = seats[slotIndex % seats.Count];
            Vector3 lookDirection = positions.transform.position - targetSeat.position;
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
            if (controller != null) controller.enabled = true;
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
        }

        private void HandleGroundCheck()
        {
            // Check if player is grounded
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

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
