using UnityEngine;


namespace FirstPersonCamera
{
    public class FirstPersonCameraScript : MonoBehaviour
    {
        [Header("Mouse Look Settings")]
        [Tooltip("Degrees per mouse-axis unit. Frame-rate independent.")]
        [SerializeField] private float mouseSensitivityX = 2f;
        [SerializeField] private float mouseSensitivityY = 2f;
        [SerializeField] private bool invertMouseY = false;
        [Tooltip("Pause mouse look while the cursor is unlocked (e.g. menu open).")]
        [SerializeField] private bool ignoreInputWhenCursorUnlocked = true;

        [Header("Camera Constraints")]
        [SerializeField] private float minVerticalAngle = -90f;
        [SerializeField] private float maxVerticalAngle = 90f;

        [Header("Smoothing")]
        [Tooltip("Lower = more responsive. 0.02-0.04 feels snappy, 0.1+ feels floaty/laggy.")]
        [SerializeField] private float smoothTime = 0.02f;
        [Tooltip("Smoothing adds input lag. Leave OFF for raw, responsive aim.")]
        [SerializeField] private bool enableSmoothing = false;

        [Header("References")]
        [SerializeField] private Transform playerBody;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerController.PlayerController playerController;

        [Header("Zoom/Aim Settings")]
        [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;
        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float zoomedFOV = 30f;
        [SerializeField] private float runningFOV = 70f;
        [Tooltip("Higher = faster FOV transitions. Frame-rate independent.")]
        [SerializeField] private float zoomSpeed = 10f;
        [Range(0f, 1f)]
        [SerializeField] private float aimSensitivityMultiplier = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("How much camera shake is reduced while aiming.")]
        [SerializeField] private float aimShakeMultiplier = 0.3f;

        [Header("Wall Collision Settings")]
        [SerializeField] private bool enableWallCollision = true;
        [SerializeField] private LayerMask wallCollisionMask = ~0;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float collisionBuffer = 0.1f;
        [SerializeField] private float collisionSmoothTime = 0.05f;
        [SerializeField] private float minDistanceFromPivot = 0.05f;

        [Header("Headbob Settings")]
        [SerializeField] private bool enableCameraShake = true;
        [Tooltip("Vertical bob amplitude when walking (meters).")]
        [SerializeField] private float walkBobAmount = 0.04f;
        [Tooltip("Vertical bob amplitude when running (meters).")]
        [SerializeField] private float runBobAmount = 0.07f;
        [Tooltip("Steps per second when walking.")]
        [SerializeField] private float walkBobFrequency = 1.8f;
        [Tooltip("Steps per second when running.")]
        [SerializeField] private float runBobFrequency = 2.6f;
        [Tooltip("Horizontal sway amplitude as a fraction of the vertical bob.")]
        [Range(0f, 1f)]
        [SerializeField] private float bobHorizontalRatio = 0.5f;
        [Tooltip("How quickly the bob amplitude fades in/out when starting/stopping.")]
        [SerializeField] private float bobSmoothing = 10f;

        [Header("Landing Shake Settings")]
        [SerializeField] private float landingShakeIntensity = 0.15f;
        [SerializeField] private float landingShakeDuration = 0.3f;
        [SerializeField] private float landingShakeFrequency = 25f;

        // Private variables for mouse look
        private float xRotation = 0f;
        private float yRotation = 0f;
        private Vector2 currentMouseDelta;
        private Vector2 currentMouseDeltaVelocity;
        // Zoom variables
        private bool isAiming = false;
        private float targetFOV;
        private float currentFOV;

        // Camera shake / headbob variables
        private Vector3 cameraShakeOffset;
        private Vector3 baseLocalPosition;
        private float bobPhase;          // accumulated step phase in radians
        private float currentBobAmount;  // smoothed amplitude
        private float currentBobFreq;    // smoothed step frequency
        private float landingShakeTimer;
        private float landingShakeSeed;
        private bool wasGrounded = true;

        // Wall collision variables
        private float currentCollisionDistance;
        private float collisionDistanceVelocity;
        private Collider[] ownColliders;
        void Start()
        {
            // Lock cursor to center of screen and hide it
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Auto-assign references if not set
            if (playerCamera == null)
                playerCamera = GetComponent<Camera>();            // Try to find player body if not assigned
            if (playerBody == null)
            {
                // First try parent
                if (transform.parent != null)
                    playerBody = transform.parent;
                else
                {
                    // Try to find PlayerController in scene
                    PlayerController.PlayerController playerControllerComponent = FindFirstObjectByType<PlayerController.PlayerController>();
                    if (playerControllerComponent != null)
                        playerBody = playerControllerComponent.transform;
                }
            }

            // Try to find PlayerController if not assigned
            if (playerController == null)
            {
                if (playerBody != null)
                    playerController = playerBody.GetComponent<PlayerController.PlayerController>();
                else
                    playerController = FindFirstObjectByType<PlayerController.PlayerController>();
            }
            // Initialize rotation values
            if (playerBody != null)
            {
                yRotation = playerBody.eulerAngles.y;
                Debug.Log("FirstPersonCameraScript: Player Body found and assigned: " + playerBody.name);
            }
            else
            {
                Debug.LogError("FirstPersonCameraScript: No Player Body found! Please assign the player body in the inspector or make sure the camera is a child of the player.");
            }

            // Save the camera's designed local position (e.g. eye height offset set in editor)
            baseLocalPosition = transform.localPosition;
            currentCollisionDistance = baseLocalPosition.magnitude;

            // Cache player's own colliders so wall-collision sphere casts ignore them
            if (playerBody != null)
                ownColliders = playerBody.GetComponentsInChildren<Collider>(true);
            else
                ownColliders = new Collider[0];

            // Initialize zoom values
            if (playerCamera != null)
            {
                normalFOV = playerCamera.fieldOfView;
                currentFOV = normalFOV;
                targetFOV = normalFOV;
            }
        }
        void Update()
        {
            HandleMouseLook();
            HandleZoom();
            HandleCameraShake();
            HandleCursorToggle();
        }
        private void HandleMouseLook()
        {
            // Skip when the cursor is unlocked so menus don't yank the camera around
            if (ignoreInputWhenCursorUnlocked && Cursor.lockState != CursorLockMode.Locked)
                return;

            // Mouse axes are already per-frame deltas; do NOT multiply by Time.deltaTime,
            // otherwise sensitivity becomes frame-rate dependent.
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;

            // Apply aim sensitivity multiplier when aiming
            if (isAiming)
            {
                mouseX *= aimSensitivityMultiplier;
                mouseY *= aimSensitivityMultiplier;
            }

            // Invert Y axis if needed
            if (invertMouseY)
                mouseY = -mouseY;

            // Apply smoothing if enabled
            if (enableSmoothing)
            {
                Vector2 targetMouseDelta = new Vector2(mouseX, mouseY);
                currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, smoothTime);
                mouseX = currentMouseDelta.x;
                mouseY = currentMouseDelta.y;
            }

            // Horizontal rotation (Y axis) - rotates the player body
            yRotation += mouseX;

            // Vertical rotation (X axis) - rotates the camera
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minVerticalAngle, maxVerticalAngle);

            // Apply rotations
            if (playerBody != null)
            {
                playerBody.rotation = Quaternion.Euler(0f, yRotation, 0f);
            }
            else
            {
                // If no player body, rotate the camera itself for horizontal look
                transform.rotation = Quaternion.Euler(xRotation, yRotation, 0f);
                return;
            }
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        private void LateUpdate()
        {
            // Apply camera shake + wall collision to local position
            ApplyCameraPosition();
        }

        private void ApplyCameraPosition()
        {
            if (playerBody == null)
            {
                transform.localPosition = baseLocalPosition + cameraShakeOffset;
                return;
            }

            Vector3 desiredOffset = baseLocalPosition + cameraShakeOffset;
            float desiredDistance = desiredOffset.magnitude;

            if (!enableWallCollision || desiredDistance < Mathf.Epsilon)
            {
                transform.localPosition = desiredOffset;
                currentCollisionDistance = desiredDistance;
                return;
            }

            // Pivot is the player body's position; cast in world space toward the desired camera spot
            Vector3 pivotWorld = playerBody.position;
            Vector3 desiredWorld = playerBody.TransformPoint(desiredOffset);
            Vector3 dir = (desiredWorld - pivotWorld).normalized;

            float targetDistance = desiredDistance;
            RaycastHit[] hits = Physics.SphereCastAll(pivotWorld, collisionRadius, dir,
                desiredDistance, wallCollisionMask, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                if (IsOwnCollider(hits[i].collider)) continue;
                // Ignore zero-distance hits (cast started inside the collider)
                if (hits[i].distance <= 0f) continue;
                if (hits[i].distance < closest) closest = hits[i].distance;
            }
            if (closest < float.PositiveInfinity)
            {
                targetDistance = Mathf.Max(minDistanceFromPivot, closest - collisionBuffer);
            }

            // Smooth pull-in/out so the camera doesn't snap
            currentCollisionDistance = Mathf.SmoothDamp(
                currentCollisionDistance, targetDistance,
                ref collisionDistanceVelocity, collisionSmoothTime);

            // Scale the desired offset to the clamped distance (preserves direction in local space)
            Vector3 finalOffset = desiredOffset.normalized * currentCollisionDistance;
            transform.localPosition = finalOffset;
        }

        private bool IsOwnCollider(Collider c)
        {
            if (c == null || ownColliders == null) return false;
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == c) return true;
            }
            return false;
        }
        private void HandleZoom()
        {
            // Check if aiming
            isAiming = Input.GetKey(aimKey);

            // Check if running (from PlayerController)
            bool isPlayerRunning = playerController != null && playerController.IsRunning();

            // Set target FOV based on aiming state and running state
            if (isAiming)
            {
                targetFOV = zoomedFOV;
            }
            else if (isPlayerRunning)
            {
                targetFOV = runningFOV;
            }
            else
            {
                targetFOV = normalFOV;
            }

            // Frame-rate independent exponential smoothing toward target FOV
            float zoomT = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, zoomT);
            // Apply FOV to camera
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = currentFOV;
            }
        }

        private void HandleCameraShake()
        {
            if (!enableCameraShake || playerController == null)
            {
                cameraShakeOffset = Vector3.MoveTowards(cameraShakeOffset, Vector3.zero, Time.deltaTime);
                return;
            }

            bool isGrounded = playerController.IsGrounded();
            bool isMoving = playerController.IsMoving();
            bool isRunning = playerController.IsRunning();

            // Detect landing
            if (isGrounded && !wasGrounded)
            {
                landingShakeTimer = landingShakeDuration;
                landingShakeSeed = Random.value * 100f;
            }
            wasGrounded = isGrounded;

            // --- Headbob (footstep-driven sine) ---
            // Pick target amplitude/frequency based on movement state
            float targetAmount = 0f;
            float targetFreq = walkBobFrequency;
            if (isMoving && isGrounded)
            {
                targetAmount = isRunning ? runBobAmount : walkBobAmount;
                targetFreq = isRunning ? runBobFrequency : walkBobFrequency;
            }
            if (isAiming)
            {
                targetAmount *= aimShakeMultiplier;
            }

            // Smooth amplitude/frequency so transitions don't pop (frame-rate independent)
            float t = 1f - Mathf.Exp(-bobSmoothing * Time.deltaTime);
            currentBobAmount = Mathf.Lerp(currentBobAmount, targetAmount, t);
            currentBobFreq = Mathf.Lerp(currentBobFreq, targetFreq, t);

            // Advance phase. 2 steps per cycle, so vertical bob frequency = 2 * stepFreq.
            // Only advance while we actually have amplitude, but ease the phase back to 0
            // when stopping so the head settles instead of freezing mid-bob.
            if (currentBobAmount > 0.0001f)
            {
                bobPhase += currentBobFreq * 2f * Mathf.PI * Time.deltaTime;
                if (bobPhase > Mathf.PI * 2f) bobPhase -= Mathf.PI * 2f;
            }
            else
            {
                bobPhase = Mathf.LerpAngle(bobPhase * Mathf.Rad2Deg, 0f, t) * Mathf.Deg2Rad;
            }

            // Vertical bob: full sine. Horizontal sway: half-frequency cosine so it alternates left/right per step.
            float bobY = Mathf.Sin(bobPhase) * currentBobAmount;
            float bobX = Mathf.Cos(bobPhase * 0.5f) * currentBobAmount * bobHorizontalRatio;
            Vector3 bobOffset = new Vector3(bobX, bobY, 0f);

            // --- Landing shake (impulse, decays over duration) ---
            Vector3 landingOffset = Vector3.zero;
            if (landingShakeTimer > 0f)
            {
                landingShakeTimer -= Time.deltaTime;
                float decay = Mathf.Clamp01(landingShakeTimer / landingShakeDuration);
                float amp = decay * decay * landingShakeIntensity; // ease-out
                float n = Time.time * landingShakeFrequency;
                float lx = (Mathf.PerlinNoise(n, landingShakeSeed) - 0.5f) * 2f;
                float ly = (Mathf.PerlinNoise(landingShakeSeed, n) - 0.5f) * 2f;
                landingOffset = new Vector3(lx * amp * 0.5f, ly * amp, 0f);
            }

            cameraShakeOffset = bobOffset + landingOffset;
        }

        private void HandleCursorToggle()
        {
            // Toggle cursor lock with Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        // Public methods for external control
        public void SetMouseSensitivity(float sensitivityX, float sensitivityY)
        {
            mouseSensitivityX = sensitivityX;
            mouseSensitivityY = sensitivityY;
        }

        public void SetVerticalLimits(float minAngle, float maxAngle)
        {
            minVerticalAngle = minAngle;
            maxVerticalAngle = maxAngle;
        }

        public void ResetRotation()
        {
            xRotation = 0f;
            currentMouseDelta = Vector2.zero;
            currentMouseDeltaVelocity = Vector2.zero;
            transform.localRotation = Quaternion.identity;
            if (playerBody != null)
            {
                // Preserve current world yaw instead of snapping to world zero
                yRotation = playerBody.eulerAngles.y;
            }
            else
            {
                yRotation = 0f;
            }
        }

        public void SetInvertMouse(bool invert)
        {
            invertMouseY = invert;
        }

        public void SetSmoothing(bool enable, float time = 0.1f)
        {
            enableSmoothing = enable;
            smoothTime = time;
        }
    }
}
