using System;
using System.Collections.Generic;
using System.Reflection;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;
using UnityEngine;
using UnityEngine.Events;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Discrete cardinal gravity directions mapped to unit <see cref="Vector2"/> values.
    /// </summary>
    public enum GravityDirection
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    /// <summary>
    /// Member 8 — Four-direction gravity-alignment puzzle that gates DeathStorm Core extraction.
    /// Represents gravity and socket orientations as <see cref="Vector2"/> unit vectors, applies
    /// 90-degree discrete vector rotations, compares alignment via vector dot product and modular
    /// angle delta, and prevents Core extraction until the Member 5 Boss Core-release condition is met.
    /// Exposes <see cref="OnCoreExtractionUnlocked"/>, <see cref="OnCoreExtracted"/>, and
    /// <see cref="TryExtractCore"/> for Member 9 to unlock or grant the Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorePuzzleController : MonoBehaviour, IPuzzleController, IInteractable, IBossPhaseListener
    {
        public const float AlignmentDotThreshold = 0.99f;
        public const float AlignmentAngleToleranceDegrees = 0.01f;

        /// <summary>
        /// Authoritative four-direction lookup table indexed by <see cref="GravityDirection"/>:
        /// 0 = Up (0, 1), 1 = Right (1, 0), 2 = Down (0, -1), 3 = Left (-1, 0).
        /// </summary>
        public static readonly IReadOnlyList<Vector2> CardinalDirections = new[]
        {
            Vector2.up,
            Vector2.right,
            Vector2.down,
            Vector2.left
        };

        [Header("Identity")]
        [SerializeField] private string _puzzleId = "core-gravity-alignment";
        [SerializeField] private string _lockedPromptText = "Core locked — deplete Boss armor to expose the Core";
        [SerializeField] private string _activePromptText = "Align gravity vector with socket (Q/E rotate 90°, 1-4 select, R reset)";
        [SerializeField] private string _extractPromptText = "Core aligned — press E to extract Core";

        [Header("Gravity Vector Configuration")]
        [Tooltip("Initial selected gravity direction as a unit Vector2 (snapped to nearest cardinal direction).")]
        [SerializeField] private Vector2 _initialGravityDirection = Vector2.up;
        [Tooltip("Required socket gravity direction as a unit Vector2 (snapped to nearest cardinal direction).")]
        [SerializeField] private Vector2 _requiredSocketDirection = Vector2.down;

        [Header("Boss Condition Gate (Member 5 Coordination)")]
        [Tooltip("When true, Core extraction cannot unlock until the Boss armor/exposure condition is met.")]
        [SerializeField] private bool _requireBossCondition = true;
        [Tooltip("Runtime/Inspector state indicating whether the Boss Core-release condition has been satisfied.")]
        [SerializeField] private bool _bossConditionMet;
        [Tooltip("Optional Boss component (IBossController or Member 5 AlienBossAI).")]
        [SerializeField] private MonoBehaviour _bossSource;

        [Header("Core Pickup Bridge (Member 9 Coordination)")]
        [Tooltip("Optional pickup or gate component that receives NotifyPuzzleCompleted() when unlocked.")]
        [SerializeField] private MonoBehaviour _corePickupTarget;

        [Header("Interaction Controls")]
        [SerializeField] private bool _acceptKeyboardInput = true;
        [SerializeField] private KeyCode _rotateCounterClockwiseKey = KeyCode.Q;
        [SerializeField] private KeyCode _rotateClockwiseKey = KeyCode.E;
        [SerializeField] private KeyCode _resetKey = KeyCode.R;

        [Header("Visual Indication (Force Arrow & Socket)")]
        [SerializeField] private Transform _forceArrowTransform;
        [SerializeField] private SpriteRenderer _forceArrowRenderer;
        [SerializeField] private Transform _socketIndicatorTransform;
        [SerializeField] private SpriteRenderer _socketIndicatorRenderer;
        [SerializeField] private Color _lockedColor = new Color(0.55f, 0.45f, 0.65f, 1f);
        [SerializeField] private Color _incorrectColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color _correctColor = new Color(0.2f, 1f, 0.65f, 1f);
        [SerializeField] private Color _solvedColor = new Color(0.25f, 0.85f, 1f, 1f);

        [Header("Unity Events")]
        [SerializeField] private UnityEvent _onCorrectAlignment;
        [SerializeField] private UnityEvent _onIncorrectAlignment;
        [SerializeField] private UnityEvent _onLockedFeedback;
        [SerializeField] private UnityEvent _onCoreExtractionUnlocked;
        [SerializeField] private UnityEvent _onCoreExtracted;

        private Vector2 _currentGravityDirection = Vector2.up;
        private PuzzleState _state = PuzzleState.Unsolved;
        private bool _coreExtractionUnlocked;
        private bool _coreExtracted;
        private string _lastFeedbackMessage = string.Empty;

        private IBossController _bossController;
        private EventInfo _alienArmorExposedEvent;
        private Delegate _alienArmorExposedHandler;

        public string PuzzleId => _puzzleId;
        public PuzzleType Type => PuzzleType.PowerGridReroute;
        public PuzzleState State => _state;
        public bool IsSolved => _state == PuzzleState.Solved;

        /// <summary>True when the puzzle has been aligned while the boss condition is met.</summary>
        public bool CoreExtractionUnlocked => _coreExtractionUnlocked;
        public bool IsCoreExtractionUnlocked => _coreExtractionUnlocked;

        /// <summary>True after Member 9 or the player has extracted the unlocked Core.</summary>
        public bool IsCoreExtracted => _coreExtracted;

        /// <summary>True when either boss gating is disabled or the boss exposure condition has been satisfied.</summary>
        public bool IsBossConditionMet => !_requireBossCondition || _bossConditionMet;

        /// <summary>Selected gravity direction represented as a cardinal unit <see cref="Vector2"/>.</summary>
        public Vector2 CurrentGravityDirection => _currentGravityDirection;

        /// <summary>Required socket direction represented as a cardinal unit <see cref="Vector2"/>.</summary>
        public Vector2 RequiredSocketDirection => _requiredSocketDirection;

        /// <summary>Configured starting gravity direction represented as a cardinal unit <see cref="Vector2"/>.</summary>
        public Vector2 InitialGravityDirection => _initialGravityDirection;

        /// <summary>True when <see cref="CurrentGravityDirection"/> matches <see cref="RequiredSocketDirection"/> mathematically.</summary>
        public bool IsDirectionAligned =>
            GetAlignmentDot() >= AlignmentDotThreshold &&
            GetAlignmentAngleDegrees() < AlignmentAngleToleranceDegrees;

        /// <summary>Human-readable feedback string describing the latest alignment or lock evaluation.</summary>
        public string LastFeedbackMessage => _lastFeedbackMessage;

        public string InteractionPromptText
        {
            get
            {
                if (_coreExtracted) return string.Empty;
                if (!IsBossConditionMet) return _lockedPromptText;
                return _coreExtractionUnlocked ? _extractPromptText : _activePromptText;
            }
        }

        InteractionType IInteractable.Type => InteractionType.PuzzleTerminal;
        public float HoldDurationRequired => 0f;

        public event Action<PuzzleState> OnPuzzleStateChanged;
        public event Action OnPuzzleSolved;
        public event Action OnPuzzleFailed;
        public event Action<Vector2> OnGravityDirectionChanged;
        public event Action OnIncorrectAlignment;
        public event Action OnExtractionBlockedByBoss;
        public event Action OnCoreExtractionUnlocked;
        public event Action<GameObject> OnCoreExtracted;
        public event Action<string> OnFeedbackChanged;
        public event Action<GameObject> OnInteracted;

        private void Awake()
        {
            EnsureConfiguration();
            InitializePuzzleState();
        }

        private void OnEnable()
        {
            BindBossSource();
        }

        private void OnDisable()
        {
            UnbindBossSource();
        }

        private void Update()
        {
            if (!_acceptKeyboardInput || IsSolved)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.UpArrow))
                SelectDirection(Vector2.up);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.RightArrow))
                SelectDirection(Vector2.right);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.DownArrow))
                SelectDirection(Vector2.down);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.LeftArrow))
                SelectDirection(Vector2.left);
            else if (Input.GetKeyDown(_rotateCounterClockwiseKey))
                RotateCounterClockwise();
            else if (Input.GetKeyDown(_rotateClockwiseKey))
                RotateClockwise();
            else if (Input.GetKeyDown(_resetKey))
                ResetPuzzle();
        }

        public bool CanInteract(GameObject interactor) => enabled && !_coreExtracted;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
                return;

            OnInteracted?.Invoke(interactor);

            if (!IsBossConditionMet)
            {
                EvaluateCondition();
                return;
            }

            if (_coreExtractionUnlocked)
            {
                TryExtractCore(interactor);
            }
        }

        public void CancelInteraction(GameObject interactor) { }

        /// <summary>
        /// Rotates the current gravity direction by <paramref name="quarterTurnsClockwise"/> 90-degree increments.
        /// Positive values rotate clockwise (Up -> Right -> Down -> Left); negative values rotate counter-clockwise.
        /// </summary>
        public bool RotateGravity(int quarterTurnsClockwise = 1)
        {
            if (IsSolved || quarterTurnsClockwise == 0)
                return false;

            _currentGravityDirection = RotateVector90Clockwise(_currentGravityDirection, quarterTurnsClockwise);
            ApplyForceArrowVisual();
            OnGravityDirectionChanged?.Invoke(_currentGravityDirection);
            EvaluateCondition();
            return true;
        }

        /// <summary>Rotates the gravity direction 90 degrees clockwise.</summary>
        public bool RotateClockwise() => RotateGravity(1);

        /// <summary>Rotates the gravity direction 90 degrees counter-clockwise.</summary>
        public bool RotateCounterClockwise() => RotateGravity(-1);

        /// <summary>
        /// Selects a gravity direction using a <see cref="Vector2"/> value, snapping it to the nearest
        /// cardinal unit vector and evaluating alignment against the socket direction.
        /// </summary>
        public bool SelectDirection(Vector2 direction)
        {
            if (IsSolved || direction.sqrMagnitude < 0.0001f)
                return false;

            _currentGravityDirection = SnapToCardinal(direction);
            ApplyForceArrowVisual();
            OnGravityDirectionChanged?.Invoke(_currentGravityDirection);
            EvaluateCondition();
            return true;
        }

        /// <summary>Selects one of the four discrete <see cref="GravityDirection"/> values.</summary>
        public bool SelectDirection(GravityDirection direction) =>
            SelectDirection(DirectionToVector(direction));

        /// <summary>Selects a cardinal direction by index (0=Up, 1=Right, 2=Down, 3=Left).</summary>
        public bool SelectDirectionIndex(int directionIndex) =>
            SelectDirection(CardinalDirections[Mod(directionIndex, CardinalDirections.Count)]);

        /// <summary>
        /// Sets the required socket direction as a cardinal unit <see cref="Vector2"/> and updates the socket visual.
        /// </summary>
        public void SetRequiredSocketDirection(Vector2 socketDirection)
        {
            if (socketDirection.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Socket direction must be a non-zero vector.", nameof(socketDirection));

            _requiredSocketDirection = SnapToCardinal(socketDirection);
            ApplySocketVisual();
        }

        /// <summary>
        /// Sets the initial gravity direction as a cardinal unit <see cref="Vector2"/>.
        /// </summary>
        public void SetInitialGravityDirection(Vector2 initialDirection)
        {
            if (initialDirection.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Initial direction must be a non-zero vector.", nameof(initialDirection));

            _initialGravityDirection = SnapToCardinal(initialDirection);
        }

        /// <summary>
        /// Assigns the force arrow and optional socket indicator transforms and immediately updates their rotations.
        /// </summary>
        public void SetVisualTransforms(Transform forceArrowTransform, Transform socketIndicatorTransform = null)
        {
            _forceArrowTransform = forceArrowTransform;
            _socketIndicatorTransform = socketIndicatorTransform;
            ApplyForceArrowVisual();
            ApplySocketVisual();
        }

        /// <summary>
        /// Updates whether the Member 5 Boss Core-release condition (armor depleted / core exposed) has been met.
        /// </summary>
        public void SetBossConditionMet(bool isMet)
        {
            _bossConditionMet = isMet;
            if (!IsSolved)
            {
                SetForceArrowColor(IsBossConditionMet ? _incorrectColor : _lockedColor);
                SetFeedback(IsBossConditionMet
                    ? $"BOSS CORE EXPOSED: Align gravity arrow with socket {_requiredSocketDirection}."
                    : "CORE LOCKED: Deplete Alien Boss armor to expose the Core before extraction.");
            }
        }

        /// <summary>Explicit integration hook for Member 5 when Boss armor is depleted / Core is exposed.</summary>
        public void NotifyBossConditionMet() => SetBossConditionMet(true);

        /// <summary>Alias matching Member 5's <c>AlienBossAI.OnArmorExposed</c> event naming.</summary>
        public void NotifyArmorExposed() => SetBossConditionMet(true);

        /// <summary>
        /// Implementation of <see cref="IBossPhaseListener"/> for Member 3/5 <see cref="IBossController"/> integration.
        /// Unlocks the boss gate when the boss enters <see cref="BossPhase.VulnerableStun"/> or <see cref="BossPhase.Defeated"/>.
        /// </summary>
        public void OnBossPhaseEntered(BossPhase phase)
        {
            if (phase == BossPhase.VulnerableStun || phase == BossPhase.Defeated)
            {
                SetBossConditionMet(true);
            }
        }

        /// <summary>Returns the dot product between the normalized selected direction and required socket direction.</summary>
        public float GetAlignmentDot()
        {
            Vector2 selected = _currentGravityDirection.sqrMagnitude > 0f ? _currentGravityDirection.normalized : Vector2.up;
            Vector2 required = _requiredSocketDirection.sqrMagnitude > 0f ? _requiredSocketDirection.normalized : Vector2.down;
            return Vector2.Dot(selected, required);
        }

        /// <summary>Returns the absolute shortest angular difference (0 to 180 degrees) between selected and socket vectors.</summary>
        public float GetAlignmentAngleDegrees() =>
            Mathf.Abs(Mathf.DeltaAngle(GetForceArrowAngleDegrees(), GetSocketAngleDegrees()));

        /// <summary>Returns the current Z-axis rotation in degrees [0, 360) for the force arrow.</summary>
        public float GetForceArrowAngleDegrees()
        {
            if (_forceArrowTransform != null)
                return Mathf.Repeat(_forceArrowTransform.localEulerAngles.z, 360f);
            return GetDirectionAngleDegrees(_currentGravityDirection);
        }

        /// <summary>Returns the target Z-axis rotation in degrees [0, 360) for the required socket direction.</summary>
        public float GetSocketAngleDegrees()
        {
            if (_socketIndicatorTransform != null)
                return Mathf.Repeat(_socketIndicatorTransform.localEulerAngles.z, 360f);
            return GetDirectionAngleDegrees(_requiredSocketDirection);
        }

        /// <summary>
        /// Evaluates whether the boss condition is met and the selected <see cref="Vector2"/> direction
        /// aligns with the required socket direction. Never soft-locks on wrong input or locked state.
        /// </summary>
        public bool EvaluateCondition()
        {
            if (IsSolved)
                return true;

            if (!IsBossConditionMet)
            {
                _coreExtractionUnlocked = false;
                SetForceArrowColor(_lockedColor);
                SetFeedback("CORE LOCKED: Deplete Alien Boss armor to expose the Core before extraction.");
                _onLockedFeedback?.Invoke();
                OnExtractionBlockedByBoss?.Invoke();
                return false;
            }

            if (IsDirectionAligned)
            {
                CompletePuzzle();
                return true;
            }

            _coreExtractionUnlocked = false;
            SetState(PuzzleState.InProgress);
            SetForceArrowColor(_incorrectColor);
            SetFeedback($"MISALIGNED: Gravity {_currentGravityDirection} does not match socket {_requiredSocketDirection}.");
            _onIncorrectAlignment?.Invoke();
            OnIncorrectAlignment?.Invoke();
            return false;
        }

        /// <summary>Returns true if the Core is unlocked by the puzzle, boss condition is met, and not yet extracted.</summary>
        public bool CanExtractCore() => _coreExtractionUnlocked && !_coreExtracted && IsBossConditionMet;

        /// <summary>
        /// Public extraction method for Member 9 (or direct player interaction) to claim the Core once unlocked.
        /// Prevents extraction before the boss condition and alignment puzzle are both satisfied.
        /// </summary>
        public bool TryExtractCore(GameObject interactor = null)
        {
            if (!IsBossConditionMet)
            {
                SetForceArrowColor(_lockedColor);
                SetFeedback("CORE LOCKED: Cannot extract Core before Boss condition is met.");
                _onLockedFeedback?.Invoke();
                OnExtractionBlockedByBoss?.Invoke();
                return false;
            }

            if (!_coreExtractionUnlocked && !EvaluateCondition())
                return false;

            if (_coreExtracted)
                return false;

            _coreExtracted = true;
            SetFeedback("CORE EXTRACTED: Containment released.");
            NotifyBossCoreReleased();
            _onCoreExtracted?.Invoke();
            OnCoreExtracted?.Invoke(interactor);
            return true;
        }

        /// <summary>
        /// Forces the puzzle into its solved state (sets boss condition met and aligns gravity with the socket).
        /// Intended for debug/testing via <see cref="IPuzzleController.ForceSolve"/>.
        /// </summary>
        public void ForceSolve()
        {
            if (IsSolved)
                return;

            _bossConditionMet = true;
            _currentGravityDirection = _requiredSocketDirection;
            ApplyForceArrowVisual();
            OnGravityDirectionChanged?.Invoke(_currentGravityDirection);
            CompletePuzzle();
        }

        /// <summary>
        /// Resets the gravity direction to <see cref="InitialGravityDirection"/> and relocks Core extraction
        /// without soft-locking or discarding the current boss gate state.
        /// </summary>
        public void ResetPuzzle()
        {
            EnsureConfiguration();
            InitializePuzzleState();
        }

        /// <summary>
        /// Rotates a 2D vector by <paramref name="quarterTurnsClockwise"/> discrete 90-degree turns
        /// using the exact 2D orthogonal rotation transformation <c>(x, y) -&gt; (y, -x)</c> per clockwise step,
        /// then snaps to the nearest cardinal unit <see cref="Vector2"/>.
        /// </summary>
        public static Vector2 RotateVector90Clockwise(Vector2 direction, int quarterTurnsClockwise = 1)
        {
            Vector2 current = SnapToCardinal(direction);
            int steps = Mod(quarterTurnsClockwise, 4);
            for (int i = 0; i < steps; i++)
            {
                current = new Vector2(current.y, -current.x);
            }
            return SnapToCardinal(current);
        }

        /// <summary>
        /// Projects any non-zero <see cref="Vector2"/> onto the four cardinal unit vectors using dot products
        /// and returns the exact matching cardinal <see cref="Vector2"/>.
        /// </summary>
        public static Vector2 SnapToCardinal(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return Vector2.up;

            Vector2 normalized = direction.normalized;
            Vector2 bestCardinal = CardinalDirections[0];
            float bestDot = Vector2.Dot(normalized, bestCardinal);

            for (int i = 1; i < CardinalDirections.Count; i++)
            {
                float dot = Vector2.Dot(normalized, CardinalDirections[i]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestCardinal = CardinalDirections[i];
                }
            }

            return bestCardinal;
        }

        /// <summary>
        /// Converts a <see cref="GravityDirection"/> enum value to its unit <see cref="Vector2"/> representation.
        /// </summary>
        public static Vector2 DirectionToVector(GravityDirection direction) =>
            CardinalDirections[Mod((int)direction, CardinalDirections.Count)];

        /// <summary>
        /// Converts a cardinal <see cref="Vector2"/> into its corresponding <see cref="GravityDirection"/> enum value.
        /// </summary>
        public static GravityDirection VectorToDirection(Vector2 direction)
        {
            Vector2 cardinal = SnapToCardinal(direction);
            for (int i = 0; i < CardinalDirections.Count; i++)
            {
                if (Vector2.Dot(cardinal, CardinalDirections[i]) >= AlignmentDotThreshold)
                    return (GravityDirection)i;
            }
            return GravityDirection.Up;
        }

        /// <summary>
        /// Converts a 2D direction vector into a modular Z-axis angle in degrees [0, 360) where
        /// <see cref="Vector2.up"/> is 0°, <see cref="Vector2.left"/> is 90°, <see cref="Vector2.down"/> is 180°,
        /// and <see cref="Vector2.right"/> is 270°.
        /// </summary>
        public static float GetDirectionAngleDegrees(Vector2 direction)
        {
            Vector2 cardinal = SnapToCardinal(direction);
            float signedAngle = Vector2.SignedAngle(Vector2.up, cardinal);
            return Mathf.Repeat(signedAngle, 360f);
        }

        private void InitializePuzzleState()
        {
            _coreExtractionUnlocked = false;
            _coreExtracted = false;
            _currentGravityDirection = _initialGravityDirection;
            ApplyForceArrowVisual();
            ApplySocketVisual();
            SetForceArrowColor(IsBossConditionMet ? _incorrectColor : _lockedColor);
            SetFeedback(IsBossConditionMet
                ? $"ALIGN GRAVITY: Rotate arrow to match socket {_requiredSocketDirection}."
                : "CORE LOCKED: Deplete Alien Boss armor to expose the Core before extraction.");
            SetState(PuzzleState.Unsolved);
        }

        private void CompletePuzzle()
        {
            _coreExtractionUnlocked = true;
            SetForceArrowColor(_solvedColor);
            SetSocketColor(_correctColor);
            SetFeedback("CORE ALIGNED: Gravity vector locked to socket. Core extraction unlocked.");
            SetState(PuzzleState.Solved);

            NotifyCorePickupTarget();
            NotifyBossCoreReleased();

            _onCorrectAlignment?.Invoke();
            _onCoreExtractionUnlocked?.Invoke();
            OnPuzzleSolved?.Invoke();
            OnCoreExtractionUnlocked?.Invoke();
        }

        private void ApplyForceArrowVisual()
        {
            if (_forceArrowTransform != null)
            {
                _forceArrowTransform.localRotation = Quaternion.Euler(0f, 0f, GetDirectionAngleDegrees(_currentGravityDirection));
            }
        }

        private void ApplySocketVisual()
        {
            if (_socketIndicatorTransform != null)
            {
                _socketIndicatorTransform.localRotation = Quaternion.Euler(0f, 0f, GetDirectionAngleDegrees(_requiredSocketDirection));
            }
        }

        private void SetForceArrowColor(Color color)
        {
            if (_forceArrowRenderer != null)
            {
                _forceArrowRenderer.color = color;
            }
        }

        private void SetSocketColor(Color color)
        {
            if (_socketIndicatorRenderer != null)
            {
                _socketIndicatorRenderer.color = color;
            }
        }

        private void SetFeedback(string message)
        {
            if (_lastFeedbackMessage == message)
                return;

            _lastFeedbackMessage = message;
            OnFeedbackChanged?.Invoke(_lastFeedbackMessage);
        }

        private void SetState(PuzzleState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            OnPuzzleStateChanged?.Invoke(_state);
        }

        private void EnsureConfiguration()
        {
            _initialGravityDirection = SnapToCardinal(_initialGravityDirection);
            _requiredSocketDirection = SnapToCardinal(_requiredSocketDirection);
        }

        private void BindBossSource()
        {
            if (_bossSource == null)
                return;

            if (_bossSource is IBossController bossController)
            {
                _bossController = bossController;
                _bossController.RegisterPhaseListener(this);
                _bossController.OnPhaseChanged += OnBossPhaseEntered;
                _bossController.OnBossStunned += NotifyBossConditionMet;
                _bossController.OnBossDefeated += NotifyBossConditionMet;

                if (_bossController.CurrentPhase == BossPhase.VulnerableStun ||
                    _bossController.CurrentPhase == BossPhase.Defeated)
                {
                    SetBossConditionMet(true);
                }
                return;
            }

            // Reflection bridge for Member 5's AlienBossAI (OnArmorExposed / IsArmorExposed) without concrete coupling.
            Type bossType = _bossSource.GetType();
            PropertyInfo exposedProperty = bossType.GetProperty("IsArmorExposed", BindingFlags.Instance | BindingFlags.Public);
            if (exposedProperty != null && exposedProperty.PropertyType == typeof(bool))
            {
                if ((bool)exposedProperty.GetValue(_bossSource))
                {
                    SetBossConditionMet(true);
                }
            }

            _alienArmorExposedEvent = bossType.GetEvent("OnArmorExposed", BindingFlags.Instance | BindingFlags.Public);
            if (_alienArmorExposedEvent != null && _alienArmorExposedEvent.EventHandlerType == typeof(Action))
            {
                _alienArmorExposedHandler = new Action(NotifyArmorExposed);
                _alienArmorExposedEvent.AddEventHandler(_bossSource, _alienArmorExposedHandler);
            }
        }

        private void UnbindBossSource()
        {
            if (_bossController != null)
            {
                _bossController.UnregisterPhaseListener(this);
                _bossController.OnPhaseChanged -= OnBossPhaseEntered;
                _bossController.OnBossStunned -= NotifyBossConditionMet;
                _bossController.OnBossDefeated -= NotifyBossConditionMet;
                _bossController = null;
            }

            if (_bossSource != null && _alienArmorExposedEvent != null && _alienArmorExposedHandler != null)
            {
                _alienArmorExposedEvent.RemoveEventHandler(_bossSource, _alienArmorExposedHandler);
            }

            _alienArmorExposedEvent = null;
            _alienArmorExposedHandler = null;
        }

        private void NotifyCorePickupTarget()
        {
            if (_corePickupTarget == null)
                return;

            _corePickupTarget.SendMessage("NotifyPuzzleCompleted", SendMessageOptions.DontRequireReceiver);
        }

        private void NotifyBossCoreReleased()
        {
            if (_bossSource == null)
                return;

            _bossSource.SendMessage("NotifyCoreReleased", SendMessageOptions.DontRequireReceiver);
        }

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            Vector2 currentDir = SnapToCardinal(_currentGravityDirection);
            Vector2 socketDir = SnapToCardinal(_requiredSocketDirection);

            Gizmos.color = _coreExtractionUnlocked ? _solvedColor : (IsBossConditionMet ? _incorrectColor : _lockedColor);
            Gizmos.DrawLine(origin, origin + (Vector3)(currentDir * 1.2f));
            Gizmos.DrawWireSphere(origin + (Vector3)(currentDir * 1.2f), 0.12f);

            Gizmos.color = _correctColor;
            Gizmos.DrawWireSphere(origin + (Vector3)(socketDir * 1.5f), 0.18f);
        }
    }
}
