using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Puzzles
{
    /// <summary>
    /// Master orchestration controller for the Edge Component laser puzzle in OBLIVION'S EDGE.
    /// Manages laser emission, specular mirror reflections (r = d - 2(d · n)n), receptor detection,
    /// pneumatic vault unsealing, and relic unlocking for Member 9's inventory pipeline.
    /// Implements <see cref="IPuzzleController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Puzzles/Edge Puzzle Controller")]
    public class EdgePuzzleController : MonoBehaviour, IPuzzleController
    {
        #region Inspector Configuration

        [Header("Puzzle Metadata")]
        [Tooltip("Authoritative puzzle identifier.")]
        [SerializeField] private string _puzzleId = "puzzle_edge_component";

        [Tooltip("Archetype classification under team puzzle contracts.")]
        [SerializeField] private PuzzleType _puzzleType = PuzzleType.PowerGridReroute;

        [Header("Puzzle Subsystem References")]
        [Tooltip("The optical laser emitter projecting the primary beam.")]
        [SerializeField] private LaserEmitter _emitter;

        [Tooltip("Array of player-rotatable specular mirrors participating in the circuit.")]
        [SerializeField] private List<RotatableMirror> _mirrors = new List<RotatableMirror>();

        [Tooltip("The sensor terminal that validates beam arrival.")]
        [SerializeField] private LaserReceiver _receiver;

        [Tooltip("The vault bulkhead protecting the Edge Component.")]
        [SerializeField] private VaultDoor _vaultDoor;

        [Tooltip("The Edge component artifact reward.")]
        [SerializeField] private EdgeComponentReward _edgeComponent;

        [Header("Validation & Timing Settings")]
        [Tooltip("Minimum continuous duration (seconds) laser must hit receiver to confirm solution.")]
        [SerializeField] [Range(0.05f, 2f)] private float _solveConfirmationTime = 0.25f;

        [Tooltip("If true, once solved the puzzle locks permanently and vault stays open.")]
        [SerializeField] private bool _lockOnSolve = true;

        [Tooltip("If true, mirrors are locked in place once solved so player cannot disturb the solution.")]
        [SerializeField] private bool _lockMirrorsOnSolve = true;

        [Header("Feedback Indicators (Success / Failure)")]
        [Tooltip("GameObjects enabled when puzzle is solved (e.g. green status beacons, holo displays).")]
        [SerializeField] private List<GameObject> _successVisualObjects = new List<GameObject>();

        [Tooltip("GameObjects enabled while puzzle is unsolved / failed (e.g. amber hazard strobes).")]
        [SerializeField] private List<GameObject> _failureVisualObjects = new List<GameObject>();

        [Header("Audio Telemetry")]
        [Tooltip("Audio source for puzzle announcement fx.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Audio fanfare played when the puzzle is solved and vault opens.")]
        [SerializeField] private AudioClip _puzzleSolvedSfx;

        [Tooltip("Audio clip played if puzzle reset or misaligned.")]
        [SerializeField] private AudioClip _puzzleFailedSfx;

        #endregion

        #region Private State

        private PuzzleState _state = PuzzleState.Unsolved;
        private bool _isSolved = false;
        private float _continuousHitTimer = 0f;

        #endregion

        #region IPuzzleController Events

        public event Action<PuzzleState> OnPuzzleStateChanged;
        public event Action OnPuzzleSolved;
        public event Action OnPuzzleFailed;

        #endregion

        #region Member 9 Inventory Integration Events

        /// <summary>
        /// Fired when the Edge component artifact is unlocked upon puzzle solution.
        /// Member 9 can subscribe to this event to prepare inventory reward delivery.
        /// </summary>
        public event Action<string> OnEdgeComponentUnlocked; // (itemId)

        /// <summary>
        /// Fired when the player interacts with and claims the unlocked Edge component relic.
        /// Member 9's inventory system should connect here to grant the item:
        /// e.g. <c>inventory.AddItemById(itemId, 1)</c>.
        /// </summary>
        public event Action<string> OnEdgeComponentClaimed; // (itemId)

        #endregion

        #region IPuzzleController Properties

        public string PuzzleId => _puzzleId;
        public PuzzleType Type => _puzzleType;
        public PuzzleState State => _state;
        public bool IsSolved => _isSolved;

        #endregion

        #region Public Subsystem Accessors

        public LaserEmitter Emitter => _emitter;
        public IReadOnlyList<RotatableMirror> Mirrors => _mirrors;
        public LaserReceiver Receiver => _receiver;
        public VaultDoor Vault => _vaultDoor;
        public EdgeComponentReward EdgeComponent => _edgeComponent;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            AutoBindSubsystemsIfNull();
            RegisterSubsystemListeners();
            UpdateFeedbackVisuals(false);
        }

        private void Start()
        {
            // Initial condition evaluation
            EvaluateInitialState();
        }

        private void Update()
        {
            if (_isSolved && _lockOnSolve) return;

            EvaluateContinuousSolution();
        }

        private void OnDestroy()
        {
            UnregisterSubsystemListeners();
        }

        #endregion

        #region Subsystem Binding & Observation

        private void AutoBindSubsystemsIfNull()
        {
            if (_emitter == null) _emitter = GetComponentInChildren<LaserEmitter>();
            if (_receiver == null) _receiver = GetComponentInChildren<LaserReceiver>();
            if (_vaultDoor == null) _vaultDoor = GetComponentInChildren<VaultDoor>();
            if (_edgeComponent == null) _edgeComponent = GetComponentInChildren<EdgeComponentReward>();

            if (_mirrors == null || _mirrors.Count == 0)
            {
                _mirrors = new List<RotatableMirror>(GetComponentsInChildren<RotatableMirror>());
            }
        }

        private void RegisterSubsystemListeners()
        {
            if (_receiver != null)
            {
                _receiver.OnBeamReceived += HandleBeamReceived;
                _receiver.OnBeamLost += HandleBeamLost;
            }

            if (_mirrors != null)
            {
                foreach (var mirror in _mirrors)
                {
                    if (mirror != null)
                    {
                        mirror.OnAngleChanged += HandleMirrorRotated;
                    }
                }
            }

            if (_edgeComponent != null)
            {
                _edgeComponent.OnComponentClaimed += HandleComponentClaimed;
            }
        }

        private void UnregisterSubsystemListeners()
        {
            if (_receiver != null)
            {
                _receiver.OnBeamReceived -= HandleBeamReceived;
                _receiver.OnBeamLost -= HandleBeamLost;
            }

            if (_mirrors != null)
            {
                foreach (var mirror in _mirrors)
                {
                    if (mirror != null)
                    {
                        mirror.OnAngleChanged -= HandleMirrorRotated;
                    }
                }
            }

            if (_edgeComponent != null)
            {
                _edgeComponent.OnComponentClaimed -= HandleComponentClaimed;
            }
        }

        #endregion

        #region Puzzle State Evaluation

        private void EvaluateInitialState()
        {
            if (EvaluateCondition())
            {
                ExecuteSolveSequence();
            }
            else
            {
                SetState(PuzzleState.Unsolved);
            }
        }

        private void EvaluateContinuousSolution()
        {
            if (_receiver != null && _receiver.IsReceivingBeam)
            {
                _continuousHitTimer += Time.deltaTime;
                if (_continuousHitTimer >= _solveConfirmationTime)
                {
                    if (!_isSolved)
                    {
                        ExecuteSolveSequence();
                    }
                }
            }
            else
            {
                _continuousHitTimer = 0f;

                if (_isSolved && !_lockOnSolve)
                {
                    ExecuteUnsolveSequence();
                }
            }
        }

        private void HandleBeamReceived()
        {
            if (_state == PuzzleState.Unsolved)
            {
                SetState(PuzzleState.InProgress);
            }
        }

        private void HandleBeamLost()
        {
            if (!_isSolved && _state == PuzzleState.InProgress)
            {
                SetState(PuzzleState.Unsolved);
                OnPuzzleFailed?.Invoke();
            }
        }

        private void HandleMirrorRotated(float newAngle)
        {
            if (!_isSolved && _state == PuzzleState.Unsolved)
            {
                SetState(PuzzleState.InProgress);
            }
        }

        private void HandleComponentClaimed(string itemId)
        {
            OnEdgeComponentClaimed?.Invoke(itemId);
        }

        private void SetState(PuzzleState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                OnPuzzleStateChanged?.Invoke(_state);
            }
        }

        #endregion

        #region Solution Execution

        private void ExecuteSolveSequence()
        {
            _isSolved = true;
            SetState(PuzzleState.Solved);

            // Open vault door
            if (_vaultDoor != null)
            {
                _vaultDoor.OpenVault();
            }

            // Unlock edge component
            if (_edgeComponent != null)
            {
                _edgeComponent.Unlock();
                OnEdgeComponentUnlocked?.Invoke(_edgeComponent.ItemId);
            }

            // Lock mirrors if desired
            if (_lockMirrorsOnSolve && _mirrors != null)
            {
                foreach (var mirror in _mirrors)
                {
                    if (mirror != null) mirror.SetLocked(true);
                }
            }

            // Audio & Visual Telemetry
            UpdateFeedbackVisuals(true);
            if (_audioSource != null && _puzzleSolvedSfx != null)
            {
                _audioSource.PlayOneShot(_puzzleSolvedSfx);
            }

            OnPuzzleSolved?.Invoke();
        }

        private void ExecuteUnsolveSequence()
        {
            _isSolved = false;
            SetState(PuzzleState.InProgress);

            if (_vaultDoor != null)
            {
                _vaultDoor.CloseVault();
            }

            if (_edgeComponent != null)
            {
                _edgeComponent.ResetState();
            }

            UpdateFeedbackVisuals(false);

            if (_audioSource != null && _puzzleFailedSfx != null)
            {
                _audioSource.PlayOneShot(_puzzleFailedSfx);
            }

            OnPuzzleFailed?.Invoke();
        }

        private void UpdateFeedbackVisuals(bool solved)
        {
            foreach (var go in _successVisualObjects)
            {
                if (go != null) go.SetActive(solved);
            }

            foreach (var go in _failureVisualObjects)
            {
                if (go != null) go.SetActive(!solved);
            }
        }

        #endregion

        #region IPuzzleController Implementation

        /// <summary>
        /// Evaluates whether current mirror arrangement directs the laser into the receiver.
        /// </summary>
        public bool EvaluateCondition()
        {
            if (_receiver == null) return false;
            return _receiver.IsReceivingBeam;
        }

        /// <summary>
        /// Forces puzzle resolution: snaps mirrors to solution angles and unlocks vault.
        /// Useful for debug cheats, speedruns, and automated testing suites.
        /// </summary>
        public void ForceSolve()
        {
            if (_mirrors != null)
            {
                foreach (var mirror in _mirrors)
                {
                    if (mirror != null)
                    {
                        mirror.SetAngle(mirror.SolutionAngle);
                    }
                }
            }

            if (_emitter != null)
            {
                _emitter.TraceAndRenderBeam();
            }

            ExecuteSolveSequence();
        }

        /// <summary>
        /// Resets the puzzle to pristine initial state: resets mirrors, reseals vault, and locks component.
        /// </summary>
        public void ResetPuzzle()
        {
            _continuousHitTimer = 0f;
            _isSolved = false;

            if (_mirrors != null)
            {
                foreach (var mirror in _mirrors)
                {
                    if (mirror != null)
                    {
                        mirror.ResetElement();
                        mirror.SetLocked(false);
                    }
                }
            }

            if (_receiver != null)
            {
                _receiver.ResetReceiver();
            }

            if (_vaultDoor != null)
            {
                _vaultDoor.ResetVault();
            }

            if (_edgeComponent != null)
            {
                _edgeComponent.ResetState();
            }

            if (_emitter != null)
            {
                _emitter.TraceAndRenderBeam();
            }

            UpdateFeedbackVisuals(false);
            SetState(PuzzleState.Unsolved);
        }

        #endregion

        #region Member 9 Integration Hooks

        /// <summary>
        /// Public completion method for Member 9, dialogue scripts, or quest triggers to complete puzzle.
        /// </summary>
        public void CompletePuzzle()
        {
            ForceSolve();
        }

        /// <summary>
        /// Allows Member 9 or player interactor to claim the Edge component directly.
        /// </summary>
        public bool TryClaimEdgeComponent(GameObject interactor)
        {
            if (_edgeComponent != null && _edgeComponent.CanInteract(interactor))
            {
                _edgeComponent.Interact(interactor);
                return true;
            }
            return false;
        }

        #endregion
    }
}
