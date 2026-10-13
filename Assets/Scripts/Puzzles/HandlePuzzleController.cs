using System;
using OblivionsEdge.Core;
using UnityEngine;
using UnityEngine.Events;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// A three-ring, step-based alignment puzzle. Rotate the selected ring with the configured
    /// controls, or call RotateRing from an interaction component. Completing the pattern raises
    /// OnHandleUnlocked for inventory or world-object integration without depending on either.
    /// </summary>
    public sealed class HandlePuzzleController : MonoBehaviour, IPuzzleController, IInteractable
    {
        private const int RingCount = 3;

        [Header("Identity")]
        [SerializeField] private string _puzzleId = "handle-alignment";
        [SerializeField] private string _interactionPromptText = "Align the three handle rings (Q/E rotate, 1-3 select, R reset)";

        [Header("Rings and target pattern")]
        [SerializeField] private Transform[] _rings = new Transform[RingCount];
        [SerializeField, Range(1, 36)] private int _stepsPerRevolution = 4;
        [SerializeField] private int[] _targetSteps = { 0, 1, 2 };
        [SerializeField] private int[] _initialSteps = { 0, 0, 0 };
        [SerializeField] private int _selectedRing;

        [Header("Interaction controls")]
        [SerializeField] private bool _acceptKeyboardInput = true;
        [SerializeField] private KeyCode _rotateCounterClockwiseKey = KeyCode.Q;
        [SerializeField] private KeyCode _rotateClockwiseKey = KeyCode.E;
        [SerializeField] private KeyCode _resetKey = KeyCode.R;

        [Header("Visual feedback")]
        [SerializeField] private SpriteRenderer[] _ringRenderers = new SpriteRenderer[RingCount];
        [SerializeField] private Color _correctColor = new Color(0.2f, 1f, 0.65f, 1f);
        [SerializeField] private Color _incorrectColor = new Color(1f, 0.35f, 0.2f, 1f);
        [SerializeField] private Color _solvedColor = new Color(0.25f, 0.8f, 1f, 1f);
        [SerializeField] private UnityEvent _onCorrectAlignment;
        [SerializeField] private UnityEvent _onIncorrectAlignment;
        [SerializeField] private UnityEvent _onHandleUnlocked;

        private readonly int[] _currentSteps = new int[RingCount];
        private PuzzleState _state = PuzzleState.Unsolved;
        private bool _handleUnlocked;

        public string PuzzleId => _puzzleId;
        public PuzzleType Type => PuzzleType.FrequencyTuner;
        public PuzzleState State => _state;
        public bool IsSolved => _state == PuzzleState.Solved;
        public bool HandleUnlocked => _handleUnlocked;
        public string InteractionPromptText => _interactionPromptText;
        InteractionType IInteractable.Type => InteractionType.PuzzleTerminal;
        public float HoldDurationRequired => 0f;
        public int SelectedRing => _selectedRing;

        public event Action<PuzzleState> OnPuzzleStateChanged;
        public event Action OnPuzzleSolved;
        public event Action OnPuzzleFailed;
        public event Action<int, float> OnRingRotated;
        public event Action OnIncorrectAlignment;
        public event Action OnHandleUnlocked;
        public event Action<GameObject> OnInteracted;

        private void Awake()
        {
            EnsureConfiguration();
            ResetPuzzle();
        }

        private void Update()
        {
            if (!_acceptKeyboardInput || IsSolved)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectRing(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectRing(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectRing(2);
            if (Input.GetKeyDown(_rotateCounterClockwiseKey)) RotateSelectedRing(-1);
            if (Input.GetKeyDown(_rotateClockwiseKey)) RotateSelectedRing(1);
            if (Input.GetKeyDown(_resetKey)) ResetPuzzle();
        }

        public bool CanInteract(GameObject interactor) => enabled && !_handleUnlocked;

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            OnInteracted?.Invoke(interactor);
        }

        public void CancelInteraction(GameObject interactor) { }

        public void SelectRing(int ringIndex)
        {
            if (ringIndex < 0 || ringIndex >= RingCount)
                throw new ArgumentOutOfRangeException(nameof(ringIndex), "The handle puzzle has three rings, indexed 0 through 2.");
            _selectedRing = ringIndex;
        }

        /// <summary>Rotates a ring by discrete configured steps and immediately re-evaluates the pattern.</summary>
        public bool RotateRing(int ringIndex, int stepDelta = 1)
        {
            if (IsSolved || ringIndex < 0 || ringIndex >= RingCount || stepDelta == 0)
                return false;

            _currentSteps[ringIndex] = Mod(_currentSteps[ringIndex] + stepDelta, _stepsPerRevolution);
            ApplyRingRotation(ringIndex);
            OnRingRotated?.Invoke(ringIndex, GetCurrentRotationDegrees(ringIndex));
            EvaluateCondition();
            return true;
        }

        public bool RotateSelectedRing(int stepDelta = 1) => RotateRing(_selectedRing, stepDelta);

        /// <summary>Returns the actual modular orientation represented by the ring, in degrees.</summary>
        public float GetCurrentRotationDegrees(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            if (_rings[ringIndex] != null)
                return Mathf.Repeat(_rings[ringIndex].localEulerAngles.z, 360f);
            return _currentSteps[ringIndex] * StepAngle;
        }

        public float GetTargetRotationDegrees(int ringIndex)
        {
            ValidateRingIndex(ringIndex);
            return Mod(_targetSteps[ringIndex], _stepsPerRevolution) * StepAngle;
        }

        /// <summary>
        /// Compares normalized angles using DeltaAngle. This is resilient to 0/360 wrapping and
        /// intentionally does not rely on animation state.
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
        }

        /// <summary>Restores the configured starting pattern and relocks the handle.</summary>
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
        }

        private float StepAngle => 360f / _stepsPerRevolution;

        private void CompletePuzzle()
        {
            _handleUnlocked = true;
            for (int i = 0; i < RingCount; i++) SetRingFeedback(i, _solvedColor);
            SetState(PuzzleState.Solved);
            _onCorrectAlignment?.Invoke();
            _onHandleUnlocked?.Invoke();
            OnPuzzleSolved?.Invoke();
            OnHandleUnlocked?.Invoke();
        }

        private void ApplyRingRotation(int ringIndex)
        {
            Transform ring = _rings[ringIndex];
            if (ring != null) ring.localRotation = Quaternion.Euler(0f, 0f, GetCurrentRotationDegrees(ringIndex));
        }

        private void SetRingFeedback(int ringIndex, Color color)
        {
            if (_ringRenderers[ringIndex] != null) _ringRenderers[ringIndex].color = color;
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
            if (ringIndex < 0 || ringIndex >= RingCount) throw new ArgumentOutOfRangeException(nameof(ringIndex));
        }

        private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
    }
}
