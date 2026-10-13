using System;
using OblivionsEdge.Core;
using UnityEngine;
using UnityEngine.Events;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Preset options for discrete angular step sizes on the geometric dials.
    /// </summary>
    public enum AngularStepPreset
    {
        /// <summary>Four 90-degree steps per revolution (0°, 90°, 180°, 270°).</summary>
        NinetyDegrees = 4,

        /// <summary>Eight 45-degree steps per revolution (0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°).</summary>
        FortyFiveDegrees = 8
    }

    /// <summary>
    /// Member 7 — The Handle component puzzle in OBLIVION'S EDGE.
    /// Manages three concentric rotatable rings / geometric dials with discrete angular steps (45° or 90°),
    /// compares ring orientations with the relay cipher key from ship logs, renders smooth slerp/lerp
    /// visual transitions, persists discrete state, and on solve emits <see cref="OnPuzzleCompleted"/>
    /// and unlocks the "deathstorm_handle" artifact for Member 9's inventory.
    /// Implements <see cref="IPuzzleController"/> and <see cref="IInteractable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Handle Puzzle Controller")]
    public sealed class HandlePuzzleController : MonoBehaviour, IPuzzleController, IInteractable
    {
        public const int RingCount = 3;
        public const string HandleArtifactId = "deathstorm_handle";
        public const string DefaultPuzzleId = "handle-alignment";

        [Header("Identity & Artifact")]
        [Tooltip("Unique puzzle identifier conforming to team standards.")]
        [SerializeField] private string _puzzleId = DefaultPuzzleId;

        [Tooltip("The unique storyline artifact unlocked upon puzzle completion.")]
        [SerializeField] private string _unlockedArtifactId = HandleArtifactId;

        [Tooltip("Storyline cipher key description found in the fallen teammate's relay ship log.")]
        [SerializeField] private string _cipherKeyDescription =
            "Relay Ship Log Cipher: Outer Ring -> 0° (North), Middle Ring -> 90° (East), Inner Ring -> 180° (South)";

        [Tooltip("Interaction prompt displayed to the player when in interaction radius.")]
        [SerializeField] private string _interactionPromptText =
            "Align the three handle rings (Q/E rotate, 1-3 select, R reset)";

        [Tooltip("Prompt displayed after the puzzle is solved and handle unlocked.")]
        [SerializeField] private string _solvedPromptText = "Handle relay aligned — deathstorm_handle unlocked";

        [Header("Concentric Rings & Discrete Steps")]
        [Tooltip("Concentric ring transforms in order: [0] Outer Ring, [1] Middle Ring, [2] Inner Ring.")]
        [SerializeField] private Transform[] _rings = new Transform[RingCount];

        [Tooltip("Number of discrete angular steps per 360-degree revolution (4 = 90°, 8 = 45°).")]
        [SerializeField, Range(1, 36)] private int _stepsPerRevolution = 4;

        [Tooltip("Cipher key target step indices for [0] Outer, [1] Middle, [2] Inner rings.")]
        [SerializeField] private int[] _targetSteps = { 0, 1, 2 };

        [Tooltip("Initial scrambled step indices for [0] Outer, [1] Middle, [2] Inner rings.")]
        [SerializeField] private int[] _initialSteps = { 0, 0, 0 };

        [Tooltip("Currently selected concentric ring index (0 = Outer, 1 = Middle, 2 = Inner).")]
        [SerializeField] private int _selectedRing = 0;

        [Header("Visual Feedback & Smooth Transitions")]
        [Tooltip("When enabled, ring transforms smoothly interpolate toward target rotations using spherical linear interpolation.")]
        [SerializeField] private bool _smoothTransitions = true;

        [Tooltip("Angular interpolation speed in degrees per second.")]
        [SerializeField, Range(60f, 1440f)] private float _rotationSpeed = 360f;

        [Tooltip("Sprite renderers on the three rings for colored visual feedback.")]
        [SerializeField] private SpriteRenderer[] _ringRenderers = new SpriteRenderer[RingCount];

        [Tooltip("Color applied to a ring when its current angle matches the cipher key.")]
        [SerializeField] private Color _correctColor = new Color(0.2f, 1f, 0.65f, 1f);

        [Tooltip("Color applied to a ring when misaligned with the cipher key.")]
        [SerializeField] private Color _incorrectColor = new Color(1f, 0.35f, 0.2f, 1f);

        [Tooltip("Color applied to all rings when the puzzle is completely solved.")]
        [SerializeField] private Color _solvedColor = new Color(0.25f, 0.8f, 1f, 1f);

        [Header("Discrete State Persistence")]
        [Tooltip("Automatically save and load discrete ring steps using PlayerPrefs across sessions.")]
        [SerializeField] private bool _autoPersistState = false;

        [Tooltip("PlayerPrefs key used when auto-persistence is enabled.")]
        [SerializeField] private string _persistenceKey = "OblivionsEdge_HandlePuzzle_State";

        [Header("Interaction Controls")]
        [Tooltip("Whether direct keyboard inputs (1-3, Q, E, R) are accepted.")]
        [SerializeField] private bool _acceptKeyboardInput = true;

        [SerializeField] private KeyCode _rotateCounterClockwiseKey = KeyCode.Q;
        [SerializeField] private KeyCode _rotateClockwiseKey = KeyCode.E;
        [SerializeField] private KeyCode _resetKey = KeyCode.R;

        [Header("Unity Events")]
        [SerializeField] private UnityEvent _onCorrectAlignment;
        [SerializeField] private UnityEvent _onIncorrectAlignment;
        [SerializeField] private UnityEvent _onHandleUnlocked;
        [SerializeField] private UnityEvent _onPuzzleCompleted;
        [SerializeField] private UnityEvent _onPuzzleReset;

        // Discrete step tracking: each entry in range [0, _stepsPerRevolution - 1]
        private readonly int[] _currentSteps = new int[RingCount];
        private PuzzleState _state = PuzzleState.Unsolved;
        private bool _handleUnlocked;

        #region Public Properties

        public string PuzzleId => _puzzleId;
        public string UnlockedArtifactId => _unlockedArtifactId;
        public string CipherKeyDescription => _cipherKeyDescription;
        public PuzzleType Type => PuzzleType.FrequencyTuner;
        public PuzzleState State => _state;
        public bool IsSolved => _state == PuzzleState.Solved;
        public bool HandleUnlocked => _handleUnlocked;
        public bool IsHandleUnlocked => _handleUnlocked;
        public string InteractionPromptText => IsSolved ? _solvedPromptText : _interactionPromptText;
        InteractionType IInteractable.Type => InteractionType.PuzzleTerminal;
        public float HoldDurationRequired => 0f;
        public int SelectedRing => _selectedRing;
        public int StepsPerRevolution => _stepsPerRevolution;
        public float StepAngle => 360f / Mathf.Max(1, _stepsPerRevolution);
        public bool SmoothTransitions => _smoothTransitions;

        /// <summary>Returns true if all ring transforms have arrived within 0.1° of their discrete target angles.</summary>
        public bool IsVisualTransitionComplete
        {
            get
            {
                for (int i = 0; i < RingCount; i++)
                {
                    if (_rings[i] != null)
                    {
                        float current = Mathf.Repeat(_rings[i].localEulerAngles.z, 360f);
                        float target = GetCurrentRotationDegrees(i);
                        if (Mathf.Abs(Mathf.DeltaAngle(current, target)) > 0.1f)
                            return false;
                    }
                }
                return true;
            }
        }

        #endregion

        #region Events

        public event Action<PuzzleState> OnPuzzleStateChanged;
        public event Action OnPuzzleSolved;
        public event Action OnPuzzleFailed;
        public event Action OnPuzzleCompleted;
        public event Action OnHandleUnlocked;
        public event Action<string> OnArtifactUnlocked;
        public event Action<string> OnArtifactClaimed;
        public event Action<int, float> OnRingRotated;
        public event Action OnIncorrectAlignment;
        public event Action OnPuzzleReset;
        public event Action<GameObject> OnInteracted;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureConfiguration();
            ResetPuzzle();

            if (_autoPersistState && Application.isPlaying)
            {
                LoadFromPersistence();
            }
        }

        private void Update()
        {
            UpdateVisualTransitions();

            if (!_acceptKeyboardInput || IsSolved)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRing(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectRing(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectRing(2);
            if (Input.GetKeyDown(_rotateCounterClockwiseKey)) RotateSelectedRing(-1);
            if (Input.GetKeyDown(_rotateClockwiseKey)) RotateSelectedRing(1);
            if (Input.GetKeyDown(_resetKey)) ResetPuzzle();
        }

        #endregion

        #region Visual Transitions (Slerp / Lerp)

        /// <summary>
        /// Performs per-frame smooth spherical linear rotation of ring transforms toward their discrete target angles.
        /// </summary>
        private void UpdateVisualTransitions()
        {
            if (!_smoothTransitions || !Application.isPlaying)
                return;

            float stepDelta = _rotationSpeed * Time.deltaTime;
            for (int i = 0; i < RingCount; i++)
            {
                Transform ring = _rings[i];
                if (ring != null)
                {
                    float targetAngle = GetCurrentRotationDegrees(i);
                    Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
                    ring.localRotation = Quaternion.RotateTowards(ring.localRotation, targetRotation, stepDelta);
                }
            }
        }

        /// <summary>
        /// Instantly snaps all concentric ring transforms to their discrete target angles without interpolation.
        /// </summary>
        public void SnapToTargetRotations()
        {
            for (int i = 0; i < RingCount; i++)
            {
                ApplyRingRotation(i);
            }
        }

        private void ApplyRingRotation(int ringIndex)
        {
            Transform ring = _rings[ringIndex];
            if (ring != null)
            {
                ring.localRotation = Quaternion.Euler(0f, 0f, GetCurrentRotationDegrees(ringIndex));
            }
        }

        #endregion

        #region IInteractable Implementation

        public bool CanInteract(GameObject interactor) => enabled;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;

            OnInteracted?.Invoke(interactor);

            if (_handleUnlocked && interactor != null)
            {
                TryClaimHandle(interactor);
            }
        }

        public void CancelInteraction(GameObject interactor) { }

        #endregion

        #region Ring Selection & Rotation

        public void SelectRing(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= RingCount)
                throw new ArgumentOutOfRangeException(nameof(ringIndex), "The handle puzzle has three concentric rings, indexed 0 through 2.");
            _selectedRing = ringIndex;
        }

        /// <summary>
        /// Rotates the specified ring by discrete angular steps and immediately re-evaluates the cipher pattern.
        /// </summary>
        /// <param name="ringIndex">Concentric ring index (0 = Outer, 1 = Middle, 2 = Inner).</param>
        /// <param name="stepDelta">Discrete step delta (positive = clockwise, negative = counter-clockwise).</param>
        /// <returns>True if the ring was rotated, false if puzzle is already solved or inputs are invalid.</returns>
        public bool RotateRing(int ringIndex, int stepDelta = 1)
        {
            if (IsSolved || ringIndex < 0 || ringIndex >= RingCount || stepDelta == 0)
                return false;

            _currentSteps[ringIndex] = Mod(_currentSteps[ringIndex] + stepDelta, _stepsPerRevolution);

            if (!_smoothTransitions || !Application.isPlaying)
            {
                ApplyRingRotation(ringIndex);
            }

            OnRingRotated?.Invoke(ringIndex, GetCurrentRotationDegrees(ringIndex));
            EvaluateCondition();

            if (_autoPersistState && Application.isPlaying)
            {
                SaveToPersistence();
            }

            return true;
        }

        public bool RotateSelectedRing(int stepDelta = 1) => RotateRing(_selectedRing, stepDelta);

        /// <summary>
        /// Returns the discrete logical orientation represented by the ring, in degrees [0, 360).
        /// </summary>
        public float GetCurrentRotationDegrees(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            return Mod(_currentSteps[ringIndex], _stepsPerRevolution) * StepAngle;
        }

        /// <summary>
        /// Returns the target cipher key orientation for the specified ring, in degrees [0, 360).
        /// </summary>
        public float GetTargetRotationDegrees(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            return Mod(_targetSteps[ringIndex], _stepsPerRevolution) * StepAngle;
        }

        /// <summary>
        /// Returns the actual current visual Transform angle for the specified ring, in degrees [0, 360).
        /// </summary>
        public float GetVisualRotationDegrees(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            if (_rings[ringIndex] != null)
                return Mathf.Repeat(_rings[ringIndex].localEulerAngles.z, 360f);
            return GetCurrentRotationDegrees(ringIndex);
        }

        /// <summary>
        /// Configures the discrete angular step mode (e.g. 4 for 90°, 8 for 45°).
        /// </summary>
        public void SetAngularStepMode(AngularStepPreset preset)
        {
            SetStepsPerRevolution((int)preset);
        }

        public void SetStepsPerRevolution(int steps)
        {
            _stepsPerRevolution = Mathf.Max(1, steps);
            SnapToTargetRotations();
            EvaluateCondition();
        }

        #endregion

        #region Solution Evaluation & Completion

        /// <summary>
        /// Evaluates ring angles against the ship log cipher key using DeltaAngle comparison.
        /// </summary>
        public bool EvaluateCondition()
        {
            if (IsSolved) return true;

            bool aligned = true;
            for (int i = 0; i < RingCount; i++)
            {
                float angularDifference = Mathf.Abs(Mathf.DeltaAngle(GetCurrentRotationDegrees(i), GetTargetRotationDegrees(i)));
                bool ringAligned = angularDifference < 0.01f;
                SetRingFeedback(i, ringAligned ? _correctColor : _incorrectColor);
                aligned &= ringAligned;
            }

            if (aligned)
            {
                CompletePuzzle();
                return true;
            }

            SetState(PuzzleState.InProgress);
            _onIncorrectAlignment?.Invoke();
            OnIncorrectAlignment?.Invoke();
            return false;
        }

        public void ForceSolve()
        {
            if (IsSolved) return;
            for (int i = 0; i < RingCount; i++)
            {
                _currentSteps[i] = Mod(_targetSteps[i], _stepsPerRevolution);
                ApplyRingRotation(i);
            }
            CompletePuzzle();

            if (_autoPersistState && Application.isPlaying)
            {
                SaveToPersistence();
            }
        }

        private void CompletePuzzle()
        {
            _handleUnlocked = true;
            for (int i = 0; i < RingCount; i++)
            {
                SetRingFeedback(i, _solvedColor);
            }

            SetState(PuzzleState.Solved);

            _onCorrectAlignment?.Invoke();
            _onHandleUnlocked?.Invoke();
            _onPuzzleCompleted?.Invoke();

            OnPuzzleSolved?.Invoke();
            OnHandleUnlocked?.Invoke();
            OnPuzzleCompleted?.Invoke();
            OnArtifactUnlocked?.Invoke(_unlockedArtifactId);

            Debug.Log($"[HandlePuzzleController] Puzzle solved! Artifact '{_unlockedArtifactId}' unlocked.");
        }

        #endregion

        #region Reset Mechanism

        /// <summary>
        /// Restores the configured initial pattern, relocks the handle, and resets visual telemetry.
        /// </summary>
        [ContextMenu("Reset Puzzle")]
        public void ResetPuzzle()
        {
            EnsureConfiguration();
            _handleUnlocked = false;
            _selectedRing = Mathf.Clamp(_selectedRing, 0, RingCount - 1);

            for (int i = 0; i < RingCount; i++)
            {
                _currentSteps[i] = Mod(_initialSteps[i], _stepsPerRevolution);
                ApplyRingRotation(i);
                SetRingFeedback(i, _incorrectColor);
            }

            SetState(PuzzleState.Unsolved);

            _onPuzzleReset?.Invoke();
            OnPuzzleReset?.Invoke();

            if (_autoPersistState && Application.isPlaying)
            {
                SaveToPersistence();
            }
        }

        /// <summary>
        /// Resets a single ring back to its initial step configuration.
        /// </summary>
        public void ResetRing(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            if (IsSolved) return;

            _currentSteps[ringIndex] = Mod(_initialSteps[ringIndex], _stepsPerRevolution);
            ApplyRingRotation(ringIndex);
            EvaluateCondition();
        }

        #endregion

        #region Discrete State Persistence

        /// <summary>
        /// Returns a snapshot copy of the current discrete ring step indices.
        /// </summary>
        public int[] GetDiscreteState() => (int[])_currentSteps.Clone();

        /// <summary>
        /// Restores discrete ring step indices and immediately updates orientation and feedback.
        /// </summary>
        public void SetDiscreteState(int[] savedSteps)
        {
            if (savedSteps == null || savedSteps.Length != RingCount)
                throw new ArgumentException($"Saved steps array must contain exactly {RingCount} elements.", nameof(savedSteps));

            for (int i = 0; i < RingCount; i++)
            {
                _currentSteps[i] = Mod(savedSteps[i], _stepsPerRevolution);
                ApplyRingRotation(i);
            }

            EvaluateCondition();
        }

        /// <summary>
        /// Saves the current discrete state to PlayerPrefs.
        /// </summary>
        public void SaveToPersistence(string customKey = null)
        {
            string key = string.IsNullOrEmpty(customKey) ? _persistenceKey : customKey;
            string data = $"{_currentSteps[0]},{_currentSteps[1]},{_currentSteps[2]};{(_isSolved ? 1 : 0)}";
            PlayerPrefs.SetString(key, data);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads and restores the discrete state from PlayerPrefs if present.
        /// </summary>
        public bool LoadFromPersistence(string customKey = null)
        {
            string key = string.IsNullOrEmpty(customKey) ? _persistenceKey : customKey;
            if (!PlayerPrefs.HasKey(key)) return false;

            string data = PlayerPrefs.GetString(key);
            try
            {
                string[] parts = data.Split(';');
                if (parts.Length > 0)
                {
                    string[] stepTokens = parts[0].Split(',');
                    if (stepTokens.Length == RingCount)
                    {
                        for (int i = 0; i < RingCount; i++)
                        {
                            if (int.TryParse(stepTokens[i], out int stepVal))
                            {
                                _currentSteps[i] = Mod(stepVal, _stepsPerRevolution);
                                ApplyRingRotation(i);
                            }
                        }
                    }
                }

                if (parts.Length > 1 && parts[1] == "1")
                {
                    CompletePuzzle();
                }
                else
                {
                    EvaluateCondition();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HandlePuzzleController] Failed to parse persisted state '{data}': {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Member 9 Inventory Integration

        /// <summary>
        /// Claims the unlocked "deathstorm_handle" artifact and grants it to the interactor's inventory if present.
        /// </summary>
        public bool TryClaimHandle(GameObject interactor)
        {
            if (!_handleUnlocked || interactor == null)
                return false;

            // Attempt direct bridge to Member 9's InventorySystem
            var inventory = interactor.GetComponentInParent<OblivionsEdge.Inventory.InventorySystem>();
            if (inventory != null)
            {
                inventory.AddItemById(_unlockedArtifactId, 1);
                OnArtifactClaimed?.Invoke(_unlockedArtifactId);
                return true;
            }

            OnArtifactClaimed?.Invoke(_unlockedArtifactId);
            return true;
        }

        #endregion

        #region Internal Utilities

        private bool _isSolved => _state == PuzzleState.Solved;

        private void SetRingFeedback(int ringIndex, Color color)
        {
            if (ringIndex >= 0 && ringIndex < _ringRenderers.Length && _ringRenderers[ringIndex] != null)
            {
                _ringRenderers[ringIndex].color = color;
            }
        }

        private void SetState(PuzzleState newState)
        {
            if (_state == newState) return;
            _state = newState;
            OnPuzzleStateChanged?.Invoke(_state);
        }

        private void EnsureConfiguration()
        {
            _stepsPerRevolution = Mathf.Max(1, _stepsPerRevolution);
            _rings = EnsureLength(_rings);
            _ringRenderers = EnsureLength(_ringRenderers);
            _targetSteps = EnsureLength(_targetSteps);
            _initialSteps = EnsureLength(_initialSteps);
        }

        private static T[] EnsureLength<T>(T[] values)
        {
            if (values != null && values.Length == RingCount) return values;
            T[] result = new T[RingCount];
            if (values != null) Array.Copy(values, result, Mathf.Min(values.Length, RingCount));
            return result;
        }

        private void ValidateRingIndex(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= RingCount)
                throw new ArgumentOutOfRangeException(nameof(ringIndex), $"Ring index {ringIndex} is out of range [0, {RingCount - 1}].");
        }

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;

        #endregion
    }
}
