using System;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Interactive rotatable mirror puzzle element for OBLIVION'S EDGE.
    /// Redirects incoming laser beams based on its orientation using specular reflection:
    /// r = d - 2(d · n)n.
    /// Implements <see cref="IPuzzleElement"/> and <see cref="IInteractable"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Rotatable Mirror")]
    public class RotatableMirror : MonoBehaviour, IPuzzleElement
    {
        #region Inspector Configuration

        [Header("Puzzle Element Identification")]
        [Tooltip("Unique identifier for this mirror element within the puzzle.")]
        [SerializeField] private string _elementId = "mirror_node_01";

        [Header("Orientation Settings")]
        [Tooltip("Starting angle in degrees (0 = normal points (0,1), 90 = normal points (-1,0), etc.).")]
        [SerializeField] [Range(0f, 360f)] private float _initialAngle = 0f;

        [Tooltip("Target solution angle for the puzzle in degrees.")]
        [SerializeField] [Range(0f, 360f)] private float _solutionAngle = 45f;

        [Tooltip("Degrees rotated per player interaction step.")]
        [SerializeField] [Range(5f, 90f)] private float _rotationStep = 45f;

        [Tooltip("Angular tolerance in degrees for matching the solution angle.")]
        [SerializeField] [Range(0.1f, 10f)] private float _angleTolerance = 2.5f;

        [Tooltip("If true, both front and back surfaces reflect the laser beam.")]
        [SerializeField] private bool _isTwoSided = false;

        [Header("Interaction Settings")]
        [Tooltip("Display prompt shown to player when focusing this mirror.")]
        [SerializeField] private string _promptFormat = "Rotate Mirror [{0}°]";

        [Tooltip("Interaction type contract.")]
        [SerializeField] private InteractionType _interactionType = InteractionType.DirectPrompt;

        [Tooltip("Hold duration required if interaction type is HoldPrompt.")]
        [SerializeField] [Range(0f, 3f)] private float _holdDurationRequired = 0f;

        [Tooltip("Can the player interact and rotate when the puzzle has already been solved?")]
        [SerializeField] private bool _canRotateWhenSolved = false;

        [Header("Visual Feedback & Hierarchy")]
        [Tooltip("Transform that rotates to represent the mirror physically. If null, rotates this transform.")]
        [SerializeField] private Transform _rotatingPivot;

        [Tooltip("SpriteRenderer displaying the mirror surface or indicator.")]
        [SerializeField] private SpriteRenderer _mirrorRenderer;

        [Tooltip("Particle system or visual spark played when mirror rotates.")]
        [SerializeField] private ParticleSystem _rotateParticles;

        [Tooltip("Glow sprite or light enabled when the mirror is currently reflecting a laser.")]
        [SerializeField] private GameObject _laserContactGlow;

        [Header("Audio Feedback")]
        [Tooltip("AudioSource component for rotation sound fx.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Audio clip played when mirror rotates.")]
        [SerializeField] private AudioClip _rotateSfx;

        #endregion

        #region Private State

        private float _currentAngle;
        private bool _isActive = true;
        private bool _isLocked = false;
        private bool _isReflectingBeamThisFrame = false;
        private bool _wasReflectingLastFrame = false;

        #endregion

        #region Events

        public event Action<IPuzzleElement> OnElementStateChanged;
        public event Action<GameObject> OnInteracted;
        public event Action<float> OnAngleChanged; // passes new angle

        #endregion

        #region IPuzzleElement Properties

        public string ElementId => _elementId;
        public bool IsActive => _isActive;
        /// <summary>
        /// Discrete integer step value representing current angle index (e.g. 0 to 7 for 45° steps).
        /// </summary>
        public int Value => Mathf.RoundToInt(_currentAngle / Mathf.Max(1f, _rotationStep));

        #endregion

        #region IInteractable Properties

        public string InteractionPromptText => string.Format(_promptFormat, Mathf.RoundToInt(_currentAngle));
        public InteractionType Type => _interactionType;
        public float HoldDurationRequired => _holdDurationRequired;

        #endregion

        #region Public Properties

        /// <summary>
        /// Current rotation angle of the mirror in degrees [0, 360).
        /// </summary>
        public float CurrentAngle => _currentAngle;

        /// <summary>
        /// Ideal target angle that satisfies the puzzle solution.
        /// </summary>
        public float SolutionAngle => _solutionAngle;

        /// <summary>
        /// Checks if the mirror is aligned to the solution angle within tolerance.
        /// </summary>
        public bool IsAtSolutionAngle
        {
            get
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, _solutionAngle));
                if (_isTwoSided)
                {
                    float deltaOpposite = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, _solutionAngle + 180f));
                    return delta <= _angleTolerance || deltaOpposite <= _angleTolerance;
                }
                return delta <= _angleTolerance;
            }
        }

        /// <summary>
        /// Calculated 2D outward surface normal vector based on current rotation.
        /// By convention, a mirror at 0° has normal pointing along Vector2.up (0, 1).
        /// </summary>
        public Vector2 Normal2D => LaserReflectionMath.AngleToNormal2D(_currentAngle);

        /// <summary>
        /// Whether the mirror reflects from both front and back faces.
        /// </summary>
        public bool IsTwoSided => _isTwoSided;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_rotatingPivot == null)
            {
                _rotatingPivot = transform;
            }

            SetAngleInternal(_initialAngle, notify: false);
        }

        private void LateUpdate()
        {
            // Update contact glow based on laser reflection state
            if (_laserContactGlow != null)
            {
                if (_isReflectingBeamThisFrame != _wasReflectingLastFrame)
                {
                    _laserContactGlow.SetActive(_isReflectingBeamThisFrame);
                }
            }

            _wasReflectingLastFrame = _isReflectingBeamThisFrame;
            _isReflectingBeamThisFrame = false; // reset for next frame raycast evaluation
        }

        #endregion

        #region Interaction & Rotation Pipeline

        public bool CanInteract(GameObject interactor)
        {
            if (!_isActive) return false;
            if (_isLocked && !_canRotateWhenSolved) return false;
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            RotateStep(1);
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor)
        {
            // No-op for instantaneous stepped rotation
        }

        /// <summary>
        /// Rotates the mirror by one step in the specified direction (+1 clockwise, -1 counter-clockwise).
        /// </summary>
        public void RotateStep(int stepDirection = 1)
        {
            float newAngle = Mathf.Repeat(_currentAngle + (_rotationStep * Mathf.Sign(stepDirection)), 360f);
            SetAngleInternal(newAngle, notify: true);

            PlayFeedback();
        }

        /// <summary>
        /// Explicitly sets mirror orientation angle in degrees.
        /// </summary>
        public void SetAngle(float angleDegrees)
        {
            SetAngleInternal(Mathf.Repeat(angleDegrees, 360f), notify: true);
        }

        private void SetAngleInternal(float targetAngle, bool notify)
        {
            _currentAngle = targetAngle;

            if (_rotatingPivot != null)
            {
                _rotatingPivot.localEulerAngles = new Vector3(0f, 0f, _currentAngle);
            }

            if (notify)
            {
                OnAngleChanged?.Invoke(_currentAngle);
                OnElementStateChanged?.Invoke(this);
            }
        }

        private void PlayFeedback()
        {
            if (_rotateParticles != null)
            {
                _rotateParticles.Play();
            }

            if (_audioSource != null && _rotateSfx != null)
            {
                _audioSource.PlayOneShot(_rotateSfx);
            }
        }

        #endregion

        #region Laser Reflection API

        /// <summary>
        /// Called by LaserEmitter when a laser ray strikes this mirror.
        /// </summary>
        /// <param name="hitPoint">Point of collision in world space.</param>
        /// <param name="incomingDirection">Incoming beam direction vector.</param>
        /// <param name="surfaceNormalFromHit">Normal provided by RaycastHit2D.</param>
        /// <param name="reflectedDirection">Calculated reflected beam direction.</param>
        /// <returns>True if the beam is reflected; false if absorbed.</returns>
        public bool TryReflectBeam(Vector2 hitPoint, Vector2 incomingDirection, Vector2 surfaceNormalFromHit, out Vector2 reflectedDirection)
        {
            // Determine effective normal
            Vector2 effectiveNormal = surfaceNormalFromHit;
            if (effectiveNormal.sqrMagnitude < 0.001f)
            {
                effectiveNormal = Normal2D;
            }

            // Specular reflection condition: incoming ray must face towards the surface normal (d · n < 0)
            float dot = Vector2.Dot(incomingDirection.normalized, effectiveNormal.normalized);

            if (_isTwoSided && dot > 0.001f)
            {
                // Invert normal for reverse face
                effectiveNormal = -effectiveNormal;
                dot = Vector2.Dot(incomingDirection.normalized, effectiveNormal.normalized);
            }

            if (dot >= 0.001f)
            {
                // Ray struck the non-reflective back face or grazing parallel angle
                reflectedDirection = Vector2.zero;
                return false;
            }

            // Apply mathematical reflection rule: r = d - 2(d · n)n
            reflectedDirection = LaserReflectionMath.Reflect2D(incomingDirection, effectiveNormal);
            _isReflectingBeamThisFrame = true;
            return true;
        }

        #endregion

        #region IPuzzleElement Implementation

        public void SetActive(bool active)
        {
            _isActive = active;
        }

        public void SetValue(int value)
        {
            float targetAngle = Mathf.Repeat(value * _rotationStep, 360f);
            SetAngle(targetAngle);
        }

        public void ResetElement()
        {
            _isLocked = false;
            SetAngleInternal(_initialAngle, notify: true);
        }

        /// <summary>
        /// Locks or unlocks interaction when puzzle status changes.
        /// </summary>
        public void SetLocked(bool isLocked)
        {
            _isLocked = isLocked;
        }

        #endregion

        #region Gizmos & Debugging

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 center = _rotatingPivot != null ? _rotatingPivot.position : transform.position;
            Vector2 normal = Application.isPlaying ? Normal2D : LaserReflectionMath.AngleToNormal2D(_initialAngle);
            Gizmos.DrawRay(center, (Vector3)normal * 1.2f);

            // Draw reflective plane line
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(center - (Vector3)tangent * 0.8f, center + (Vector3)tangent * 0.8f);
        }

        #endregion
    }
}
