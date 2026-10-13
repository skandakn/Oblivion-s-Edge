using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Player
{
    /// <summary>
    /// Core 2D side-scrolling player controller for OBLIVION'S EDGE.
    /// Manages responsive kinematic locomotion, ground detection, optional jumping,
    /// mouse/movement-based facing, dash/dodge mechanics, interaction detection,
    /// and decoupled animation hooks.
    /// Implements <see cref="IPlayerController"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Player/Player Controller")]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        #region Inspector Configuration

        [Header("Locomotion Settings")]
        [Tooltip("Base horizontal movement speed in units/sec.")]
        [SerializeField] [Range(2f, 20f)] private float _moveSpeed = 7.5f;

        [Tooltip("Horizontal acceleration rate when moving on the ground.")]
        [SerializeField] [Range(10f, 100f)] private float _groundAcceleration = 55f;

        [Tooltip("Horizontal deceleration rate when coming to a stop on the ground.")]
        [SerializeField] [Range(10f, 100f)] private float _groundDeceleration = 65f;

        [Tooltip("Horizontal acceleration rate when airborne.")]
        [SerializeField] [Range(5f, 60f)] private float _airAcceleration = 35f;

        [Tooltip("Horizontal deceleration rate when airborne with no directional input.")]
        [SerializeField] [Range(5f, 60f)] private float _airDeceleration = 20f;

        [Tooltip("Speed multiplier applied when sprinting.")]
        [SerializeField] [Range(1.1f, 2.5f)] private float _sprintMultiplier = 1.45f;

        [Tooltip("Speed multiplier applied when crouching.")]
        [SerializeField] [Range(0.2f, 0.9f)] private float _crouchMultiplier = 0.5f;

        [Header("Ground Detection")]
        [Tooltip("Transform representing the ground check anchor (typically at the player's feet).")]
        [SerializeField] private Transform _groundCheckPoint;

        [Tooltip("Size of the ground check overlap box.")]
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.6f, 0.15f);

        [Tooltip("Layer mask designating walkable geometry.")]
        [SerializeField] private LayerMask _groundLayerMask;

        [Header("Jumping (Level Design Optional)")]
        [Tooltip("Whether jumping is enabled for the current level design.")]
        [SerializeField] private bool _enableJump = true;

        [Tooltip("Initial vertical impulse velocity applied upon jumping.")]
        [SerializeField] [Range(5f, 25f)] private float _jumpForce = 12f;

        [Tooltip("Gravity multiplier applied when falling to give snappy, responsive jump arcs.")]
        [SerializeField] [Range(1f, 5f)] private float _fallGravityMultiplier = 2.5f;

        [Tooltip("Gravity multiplier applied when releasing jump button early (variable jump height).")]
        [SerializeField] [Range(1f, 5f)] private float _lowJumpGravityMultiplier = 2.0f;

        [Tooltip("Grace period (in seconds) allowing the player to jump briefly after leaving a ledge.")]
        [SerializeField] [Range(0.05f, 0.3f)] private float _coyoteDuration = 0.15f;

        [Tooltip("Grace period (in seconds) queuing a jump input pressed just before touching ground.")]
        [SerializeField] [Range(0.05f, 0.3f)] private float _jumpBufferDuration = 0.15f;

        [Header("Facing & Aiming")]
        [Tooltip("If true, the character faces toward the mouse aim position. If false, faces locomotion direction.")]
        [SerializeField] private bool _faceMouseAim = true;

        [Tooltip("Visual transform to mirror/flip horizontally. If unassigned, defaults to this transform.")]
        [SerializeField] private Transform _visualRoot;

        [Tooltip("Optional aim pivot (e.g., weapon or flashlight mount) rotated toward the aim vector.")]
        [SerializeField] private Transform _aimPivot;

        [Header("Dash / Dodge Settings")]
        [Tooltip("Target distance traversed during a dash in world units.")]
        [SerializeField] [Range(2f, 10f)] private float _dashDistance = 4.5f;

        [Tooltip("Duration of the active dash in seconds.")]
        [SerializeField] [Range(0.1f, 0.5f)] private float _dashDuration = 0.18f;

        [Tooltip("Cooldown period between consecutive dashes in seconds.")]
        [SerializeField] [Range(0.2f, 3f)] private float _dashCooldown = 1.0f;

        [Tooltip("Grants temporary invulnerability (i-frames) during dash if PlayerHealth is present.")]
        [SerializeField] private bool _dashGrantsInvulnerability = true;

        [Header("Interaction Settings")]
        [Tooltip("Maximum interaction radius to detect IInteractable objects.")]
        [SerializeField] [Range(0.5f, 5f)] private float _interactionRadius = 2.0f;

        [Tooltip("Layer mask for interactable objects in the environment.")]
        [SerializeField] private LayerMask _interactableLayerMask;

        [Header("Input Keys (Fallback & Direct Binding)")]
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode _dashKey = KeyCode.LeftAlt;
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode _crouchKey = KeyCode.LeftControl;
        [SerializeField] private KeyCode _interactKey = KeyCode.E;

        [Header("Feedback & Animation Hooks")]
        [Tooltip("Animator component for state transitions (safe fallback if null or missing parameters).")]
        [SerializeField] private Animator _animator;

        [Tooltip("Trail renderer activated during dash.")]
        [SerializeField] private TrailRenderer _dashTrail;

        [Tooltip("Particle system triggered upon initiating a dash.")]
        [SerializeField] private ParticleSystem _dashParticles;

        [Tooltip("Audio source for footstep and action feedback.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Sound played upon dashing.")]
        [SerializeField] private AudioClip _dashSfx;

        [Tooltip("Sound played upon jumping.")]
        [SerializeField] private AudioClip _jumpSfx;

        #endregion

        #region Internal State & Components

        private Rigidbody2D _rb;
        private Collider2D _mainCollider;
        private PlayerHealth _playerHealth;

        private Vector2 _moveInput;
        private Vector2 _aimTargetPosition;
        private Vector2 _dashDirection;

        private float _currentHorizontalVelocity;
        private float _initialGravityScale;
        private Vector3 _initialVisualScale = Vector3.one;

        private bool _controlsEnabled = true;
        private bool _isGrounded;
        private bool _wasGroundedLastFrame;
        private bool _isDashing;
        private bool _isSprinting;
        private bool _isCrouching;
        private bool _isFacingRight = true;

        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private float _dashTimer;
        private float _dashCooldownTimer;

        private IInteractable _focusedInteractable;
        private readonly Collider2D[] _interactionHits = new Collider2D[16];

        // Cached Animator Parameter Hashes
        private static readonly int AnimHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimHashVerticalVelocity = Animator.StringToHash("VerticalVelocity");
        private static readonly int AnimHashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimHashIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int AnimHashIsDashing = Animator.StringToHash("IsDashing");
        private static readonly int AnimHashIsCrouching = Animator.StringToHash("IsCrouching");
        private static readonly int AnimHashIsSprinting = Animator.StringToHash("IsSprinting");
        private static readonly int AnimHashJump = Animator.StringToHash("Jump");
        private static readonly int AnimHashDash = Animator.StringToHash("Dash");

        #endregion

        #region Properties (IPlayerController)

        public Vector2 MoveDirection => _moveInput;
        public Vector2 AimDirection { get; private set; } = Vector2.right;
        public bool IsGrounded => _isGrounded;
        public bool IsMoving => Mathf.Abs(_currentHorizontalVelocity) > 0.1f && Mathf.Abs(_moveInput.x) > 0.05f;
        public bool IsSprinting => _isSprinting && IsMoving;
        public bool IsCrouching => _isCrouching;
        public bool IsDashing => _isDashing;
        public bool IsFacingRight => _isFacingRight;
        public int FacingSign => _isFacingRight ? 1 : -1;
        public bool ControlsEnabled => _controlsEnabled;

        public float MoveSpeed
        {
            get => _moveSpeed;
            set => _moveSpeed = Mathf.Max(0f, value);
        }

        public float DashDistance
        {
            get => _dashDistance;
            set => _dashDistance = Mathf.Max(0.5f, value);
        }

        public float DashCooldown
        {
            get => _dashCooldown;
            set => _dashCooldown = Mathf.Max(0.1f, value);
        }

        public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownTimer);
        public bool CanDash => !_isDashing && _dashCooldownTimer <= 0f && _controlsEnabled;

        public IInteractable FocusedInteractable => _focusedInteractable;

        #endregion

        #region Events (IPlayerController & Extended)

        public event Action<Vector2> OnMoved;
        public event Action OnDashed;
        public event Action OnJumped;
        public event Action<bool> OnGroundedChanged;
        public event Action<bool> OnFacingDirectionChanged;
        public event Action<bool> OnSprintingChanged;
        public event Action<bool> OnCrouchedChanged;
        public event Action<float, float> OnDashCooldownChanged; // (remaining, duration)
        public event Action<IInteractable> OnInteractableFocusChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _mainCollider = GetComponent<Collider2D>();
            _playerHealth = GetComponent<PlayerHealth>();

            _initialGravityScale = _rb.gravityScale;

            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }
            _initialVisualScale = _visualRoot.localScale;

            // Default ground layer to Environment or Default if unset
            if (_groundLayerMask.value == 0)
            {
                _groundLayerMask = LayerMask.GetMask(
                    GameConstants.LayerNameEnvironment,
                    GameConstants.LayerNameDefault
                );
                if (_groundLayerMask.value == 0)
                {
                    _groundLayerMask = 1 << GameConstants.LayerEnvironmentIndex;
                }
            }

            // Default interactable layer if unset
            if (_interactableLayerMask.value == 0)
            {
                _interactableLayerMask = LayerMask.GetMask(
                    GameConstants.LayerNameInteractable,
                    GameConstants.LayerNameDefault
                );
                if (_interactableLayerMask.value == 0)
                {
                    _interactableLayerMask = ~0; // all layers fallback
                }
            }

            if (_dashTrail != null)
            {
                _dashTrail.emitting = false;
            }
        }

        private void Update()
        {
            HandleTimers(Time.deltaTime);

            if (_controlsEnabled)
            {
                GatherInput();
                UpdateAiming();
                UpdateFacing();
                ScanForInteractables();
            }

            UpdateAnimationHooks();
        }

        private void FixedUpdate()
        {
            EvaluateGroundStatus();

            if (_isDashing)
            {
                ExecuteDashStep();
                return;
            }

            if (_controlsEnabled)
            {
                ApplyHorizontalMovement();
                ApplyJumpPhysics();
            }
            else
            {
                // Smoothly decelerate to zero horizontal velocity when disabled
                _currentHorizontalVelocity = Mathf.MoveTowards(
                    _rb.linearVelocity.x,
                    0f,
                    _groundDeceleration * Time.fixedDeltaTime
                );
                _rb.linearVelocity = new Vector2(_currentHorizontalVelocity, _rb.linearVelocity.y);
            }
        }

        #endregion

        #region Input Handling

        private void GatherInput()
        {
            // Horizontal locomotion input from Input axes or key fallback
            float horizontal = 0f;
            try
            {
                horizontal = Input.GetAxisRaw(GameConstants.InputHorizontal);
            }
            catch
            {
                // Fallback to standard AD / Arrow keys if Axis not yet bound
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            }

            _moveInput = new Vector2(horizontal, 0f);

            // Sprint input
            bool sprintHeld = Input.GetKey(_sprintKey);
            SetSprint(sprintHeld);

            // Crouch input
            bool crouchHeld = Input.GetKey(_crouchKey);
            SetCrouch(crouchHeld);

            // Jump input
            if (_enableJump)
            {
                if (Input.GetKeyDown(_jumpKey))
                {
                    _jumpBufferTimer = _jumpBufferDuration;
                }
            }

            // Dash input
            if (Input.GetKeyDown(_dashKey))
            {
                TriggerDash();
            }

            // Interaction input
            bool interactPressed = false;
            try
            {
                interactPressed = Input.GetButtonDown(GameConstants.InputInteract);
            }
            catch
            {
                interactPressed = Input.GetKeyDown(_interactKey);
            }

            if (interactPressed)
            {
                TryInteract();
            }
        }

        private void UpdateAiming()
        {
            // Determine aim target in world space
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 mouseScreen = Input.mousePosition;
                mouseScreen.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
                _aimTargetPosition = cam.ScreenToWorldPoint(mouseScreen);
            }

            Vector2 origin = _aimPivot != null ? (Vector2)_aimPivot.position : (Vector2)transform.position;
            Vector2 aimDelta = _aimTargetPosition - origin;
            if (aimDelta.sqrMagnitude > 0.0001f)
            {
                AimDirection = aimDelta.normalized;
            }

            // Aim pivot rotation
            if (_aimPivot != null)
            {
                float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
                if (!_isFacingRight)
                {
                    angle += 180f;
                }
                _aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void UpdateFacing()
        {
            bool shouldFaceRight = _isFacingRight;

            if (_faceMouseAim)
            {
                float aimDeltaX = _aimTargetPosition.x - transform.position.x;
                if (Mathf.Abs(aimDeltaX) > 0.15f)
                {
                    shouldFaceRight = aimDeltaX > 0f;
                }
            }
            else if (Mathf.Abs(_moveInput.x) > 0.05f)
            {
                shouldFaceRight = _moveInput.x > 0f;
            }

            if (shouldFaceRight != _isFacingRight)
            {
                _isFacingRight = shouldFaceRight;
                ApplyFacingVisual();
                OnFacingDirectionChanged?.Invoke(_isFacingRight);
            }
        }

        private void ApplyFacingVisual()
        {
            if (_visualRoot == null) return;

            Vector3 scale = _initialVisualScale;
            scale.x = Mathf.Abs(scale.x) * (_isFacingRight ? 1f : -1f);
            _visualRoot.localScale = scale;
        }

        #endregion

        #region Locomotion & Physics

        private void EvaluateGroundStatus()
        {
            Vector2 checkPosition = _groundCheckPoint != null
                ? (Vector2)_groundCheckPoint.position
                : GetDefaultGroundCheckPosition();

            bool groundedNow = Physics2D.OverlapBox(
                checkPosition,
                _groundCheckSize,
                0f,
                _groundLayerMask
            ) != null;

            if (groundedNow != _isGrounded)
            {
                _isGrounded = groundedNow;
                OnGroundedChanged?.Invoke(_isGrounded);
            }

            if (_isGrounded)
            {
                _coyoteTimer = _coyoteDuration;
            }

            _wasGroundedLastFrame = _isGrounded;
        }

        private Vector2 GetDefaultGroundCheckPosition()
        {
            if (_mainCollider != null)
            {
                Bounds bounds = _mainCollider.bounds;
                return new Vector2(bounds.center.x, bounds.min.y);
            }
            return (Vector2)transform.position + Vector2.down * 0.9f;
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = _moveSpeed;

            if (_isCrouching)
            {
                targetSpeed *= _crouchMultiplier;
            }
            else if (_isSprinting)
            {
                targetSpeed *= _sprintMultiplier;
            }

            float targetVelocityX = _moveInput.x * targetSpeed;

            float accel = _isGrounded
                ? (Mathf.Abs(targetVelocityX) > 0.01f ? _groundAcceleration : _groundDeceleration)
                : (Mathf.Abs(targetVelocityX) > 0.01f ? _airAcceleration : _airDeceleration);

            _currentHorizontalVelocity = Mathf.MoveTowards(
                _rb.linearVelocity.x,
                targetVelocityX,
                accel * Time.fixedDeltaTime
            );

            _rb.linearVelocity = new Vector2(_currentHorizontalVelocity, _rb.linearVelocity.y);

            if (Mathf.Abs(_currentHorizontalVelocity) > 0.05f)
            {
                OnMoved?.Invoke(_rb.linearVelocity);
            }
        }

        private void ApplyJumpPhysics()
        {
            if (!_enableJump) return;

            // Execute buffered jump if coyote time is valid
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                ExecuteJump();
            }

            // Snappy jump arc modifications
            if (_rb.linearVelocity.y < 0f)
            {
                // Falling faster
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
            }
            else if (_rb.linearVelocity.y > 0f && !Input.GetKey(_jumpKey))
            {
                // Variable jump height cut when release early
                _rb.linearVelocity += Vector2.up * (Physics2D.gravity.y * (_lowJumpGravityMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private void ExecuteJump()
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _jumpForce);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;

            if (_audioSource != null && _jumpSfx != null)
            {
                _audioSource.PlayOneShot(_jumpSfx);
            }

            SafeSetTrigger(AnimHashJump);
            OnJumped?.Invoke();
        }

        #endregion

        #region Dash / Dodge Mechanics

        public void TriggerDash()
        {
            if (!CanDash) return;

            _isDashing = true;
            _dashTimer = _dashDuration;
            _dashCooldownTimer = _dashCooldown;

            // Determine dash direction (movement input if moving, otherwise facing direction)
            if (Mathf.Abs(_moveInput.x) > 0.05f)
            {
                _dashDirection = new Vector2(Mathf.Sign(_moveInput.x), 0f);
            }
            else
            {
                _dashDirection = new Vector2(_isFacingRight ? 1f : -1f, 0f);
            }

            // Temporarily suspend gravity during dash
            _rb.gravityScale = 0f;

            // Enable visual feedback
            if (_dashTrail != null)
            {
                _dashTrail.emitting = true;
            }
            if (_dashParticles != null)
            {
                _dashParticles.Play();
            }
            if (_audioSource != null && _dashSfx != null)
            {
                _audioSource.PlayOneShot(_dashSfx);
            }

            // Optional i-frame invulnerability
            if (_dashGrantsInvulnerability && _playerHealth != null)
            {
                _playerHealth.TriggerInvulnerability(_dashDuration);
            }

            SafeSetTrigger(AnimHashDash);
            SafeSetBool(AnimHashIsDashing, true);

            OnDashed?.Invoke();
        }

        private void ExecuteDashStep()
        {
            float speed = _dashDistance / _dashDuration;
            _rb.linearVelocity = _dashDirection * speed;
        }

        private void EndDash()
        {
            _isDashing = false;
            _rb.gravityScale = _initialGravityScale;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x * 0.5f, 0f);

            if (_dashTrail != null)
            {
                _dashTrail.emitting = false;
            }

            SafeSetBool(AnimHashIsDashing, false);
        }

        private void HandleTimers(float deltaTime)
        {
            if (_coyoteTimer > 0f) _coyoteTimer -= deltaTime;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= deltaTime;

            if (_isDashing)
            {
                _dashTimer -= deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
            }

            if (_dashCooldownTimer > 0f)
            {
                _dashCooldownTimer -= deltaTime;
                OnDashCooldownChanged?.Invoke(Mathf.Max(0f, _dashCooldownTimer), _dashCooldown);
            }
        }

        #endregion

        #region Interaction Pipeline

        private void ScanForInteractables()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _interactionRadius,
                _interactionHits,
                _interactableLayerMask
            );

            IInteractable bestCandidate = null;
            float closestDistanceSqr = float.MaxValue;
            Vector2 currentPos = transform.position;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = _interactionHits[i];
                if (col == null || col.gameObject == gameObject) continue;

                IInteractable interactable = col.GetComponent<IInteractable>() ?? col.GetComponentInParent<IInteractable>();
                if (interactable != null && interactable.CanInteract(gameObject))
                {
                    float distSqr = ((Vector2)col.transform.position - currentPos).sqrMagnitude;
                    if (distSqr < closestDistanceSqr)
                    {
                        closestDistanceSqr = distSqr;
                        bestCandidate = interactable;
                    }
                }
            }

            if (bestCandidate != _focusedInteractable)
            {
                _focusedInteractable = bestCandidate;
                OnInteractableFocusChanged?.Invoke(_focusedInteractable);
            }
        }

        /// <summary>
        /// Attempts interaction with the currently focused interactable.
        /// </summary>
        public bool TryInteract()
        {
            if (!_controlsEnabled || _focusedInteractable == null)
            {
                return false;
            }

            if (_focusedInteractable.CanInteract(gameObject))
            {
                _focusedInteractable.Interact(gameObject);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Explicitly triggers interaction with an interactable instance.
        /// </summary>
        public bool TriggerInteractionWith(IInteractable target)
        {
            if (target != null && target.CanInteract(gameObject))
            {
                target.Interact(gameObject);
                return true;
            }
            return false;
        }

        #endregion

        #region Public Control Overrides (IPlayerController)

        public void SetMoveInput(Vector2 direction)
        {
            _moveInput = new Vector2(Mathf.Clamp(direction.x, -1f, 1f), 0f);
        }

        public void SetAimTarget(Vector2 worldPosition)
        {
            _aimTargetPosition = worldPosition;
        }

        public void SetSprint(bool isSprinting)
        {
            if (_isSprinting != isSprinting)
            {
                _isSprinting = isSprinting;
                SafeSetBool(AnimHashIsSprinting, _isSprinting);
                OnSprintingChanged?.Invoke(_isSprinting);
            }
        }

        public void SetCrouch(bool isCrouching)
        {
            if (_isCrouching != isCrouching)
            {
                _isCrouching = isCrouching;
                SafeSetBool(AnimHashIsCrouching, _isCrouching);
                OnCrouchedChanged?.Invoke(_isCrouching);
            }
        }

        public void DisableControls()
        {
            _controlsEnabled = false;
            _moveInput = Vector2.zero;
            SetSprint(false);
            SetCrouch(false);

            if (_isDashing)
            {
                EndDash();
            }
        }

        public void EnableControls()
        {
            _controlsEnabled = true;
        }

        #endregion

        #region Animation Hooks Helper

        private void UpdateAnimationHooks()
        {
            if (_animator == null) return;

            SafeSetFloat(AnimHashSpeed, Mathf.Abs(_currentHorizontalVelocity));
            SafeSetFloat(AnimHashVerticalVelocity, _rb.linearVelocity.y);
            SafeSetBool(AnimHashIsGrounded, _isGrounded);
            SafeSetBool(AnimHashIsMoving, IsMoving);
        }

        private void SafeSetFloat(int hash, float value)
        {
            if (_animator != null && _animator.isInitialized)
            {
                try { _animator.SetFloat(hash, value); } catch { /* graceful fallback */ }
            }
        }

        private void SafeSetBool(int hash, bool value)
        {
            if (_animator != null && _animator.isInitialized)
            {
                try { _animator.SetBool(hash, value); } catch { /* graceful fallback */ }
            }
        }

        private void SafeSetTrigger(int hash)
        {
            if (_animator != null && _animator.isInitialized)
            {
                try { _animator.SetTrigger(hash); } catch { /* graceful fallback */ }
            }
        }

        #endregion

        #region Debug & Gizmos

        private void OnDrawGizmosSelected()
        {
            // Ground check visual
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Vector2 checkPosition = _groundCheckPoint != null
                ? (Vector2)_groundCheckPoint.position
                : GetDefaultGroundCheckPosition();
            Gizmos.DrawWireCube(checkPosition, _groundCheckSize);

            // Interaction radius visual
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRadius);

            // Aim ray visual
            Gizmos.color = Color.cyan;
            Vector3 origin = _aimPivot != null ? _aimPivot.position : transform.position;
            Gizmos.DrawRay(origin, AimDirection * 2f);
        }

        #endregion
    }
}
