using System;
using System.Collections.Generic;
using UnityEngine;
using OblivionsEdge.Boss;
using OblivionsEdge.Inventory;

namespace OblivionsEdge.DeathStorm
{
    // ─────────────────────────────────────────────────────────────────────────
    //  DeathStormCrafting.cs — Legendary weapon crafting controller
    //  Owner: Member 10
    //
    //  Responsibilities:
    //    · Validate that Edge, Handle and Core are all present in the inventory.
    //    · Start a shared 60-second countdown the moment the Core is collected.
    //    · Drive three sequential puzzle stages (Core alignment → Edge sync →
    //      Energy stabilise).
    //    · Wrong answers cost time, but crafting may continue unless clock hits 0.
    //    · On timeout fire OnCountdownExpired (firstTimeOut=true) → boss ENRAGED,
    //      VOID POUNCE enabled.  Subsequent timeouts reuse firstTimeOut=false.
    //    · On success set HasDeathStorm and fire OnCraftingSuccess.
    //    · Allow crafting to restart after a timeout if the player survives.
    //
    //  Members 5 (IInventory) and 9 (IBossController) are referenced only through
    //  their interfaces — no concrete-class coupling.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Concrete implementation of the DeathStorm crafting mini-game.
    /// Attach to any persistent GameObject in the boss arena scene.
    /// Wire <see cref="BossController"/> and <see cref="PlayerInventory"/> in the Inspector.
    /// </summary>
    public class DeathStormCrafting : MonoBehaviour, IDeathStormCrafting
    {
        // ── Inspector-configurable values ──────────────────────────────────
        [Header("Item IDs (must match Member 5 ItemData)")]
        [SerializeField] private string _edgeItemId   = "deathstorm_edge";
        [SerializeField] private string _handleItemId = "deathstorm_handle";
        [SerializeField] private string _coreItemId   = "deathstorm_core";

        [Header("Countdown")]
        [Tooltip("Total crafting window in seconds.")]
        [SerializeField] private float _countdownTotal = 60f;

        [Header("Wrong-Answer Penalty")]
        [Tooltip("Seconds deducted from the timer per wrong answer.")]
        [SerializeField] private float _wrongAnswerPenalty = 5f;

        [Header("Stage 1 — Core Alignment (4-direction)")]
        [Tooltip("Correct cardinal direction index: 0=North 1=East 2=South 3=West")]
        [SerializeField] private int _coreAlignmentAnswer = 2;   // South by default

        [Header("Stage 2 — Edge Sync (angle buckets 0-5)")]
        [Tooltip("Correct angle bucket index (0-5 → 60° increments starting at 0°)")]
        [SerializeField] private int _edgeSyncAnswer = 3;

        [Header("Stage 3 — Energy Stabilise (directional sequence length 3)")]
        [Tooltip("Three-step directional channel sequence: 0=N 1=E 2=S 3=W")]
        [SerializeField] private List<int> _stabiliseSequence = new List<int> { 1, 3, 0 };

        [Header("Dependencies (injected by scene or DI)")]
        [Tooltip("Member 3's IBossController implementation.")]
        [SerializeField] private MonoBehaviour _bossControllerMono;
        [Tooltip("Member 5's IInventory implementation attached to the player.")]
        [SerializeField] private MonoBehaviour _playerInventoryMono;

        // ── Runtime state ──────────────────────────────────────────────────
        private IBossController _boss;
        private IInventory      _inventory;

        private CraftingStage _currentStage      = CraftingStage.Idle;
        private float         _countdownRemaining = 0f;
        private bool          _countdownRunning   = false;
        private bool          _hasDeathStorm      = false;
        private bool          _firstTimeout       = true;

        // Stage 3 sub-step index
        private int _stabiliseStep = 0;

        // ── IDeathStormCrafting ────────────────────────────────────────────
        public CraftingStage CurrentStage       => _currentStage;
        public float         CountdownRemaining => _countdownRemaining;
        public float         CountdownTotal     => _countdownTotal;
        public bool          HasDeathStorm      => _hasDeathStorm;
        public bool          IsCountdownRunning => _countdownRunning;

        public string CurrentInstruction => BuildInstruction();

        public event Action<float, float> OnTimerTick;
        public event Action<CraftingStage> OnStageChanged;
        public event Action<float>         OnWrongAnswer;
        public event Action                OnCraftingSuccess;
        public event Action<bool>          OnCountdownExpired;

        // ── Unity lifecycle ────────────────────────────────────────────────
        private void Awake()
        {
            // Resolve interfaces from serialised MonoBehaviours (avoids hard deps)
            _boss      = _bossControllerMono  as IBossController;
            _inventory = _playerInventoryMono as IInventory;

            if (_boss == null)
                Debug.LogWarning("[DeathStormCrafting] BossController not assigned or does not implement IBossController.");
            if (_inventory == null)
                Debug.LogWarning("[DeathStormCrafting] PlayerInventory not assigned or does not implement IInventory.");
        }

        private void Update()
        {
            if (!_countdownRunning) return;

            _countdownRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(_countdownRemaining, _countdownTotal);

            if (_countdownRemaining <= 0f)
            {
                _countdownRemaining = 0f;
                HandleTimeout();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Called by the Core pickup (ItemPickup handler) the moment the Core
        /// enters the player's inventory.  Validates all three components are
        /// present before starting the countdown and entering Stage 1.
        /// </summary>
        public void NotifyCoreCollected()
        {
            if (_hasDeathStorm || _currentStage == CraftingStage.Complete) return;

            if (!ValidateComponents(out string reason))
            {
                Debug.LogWarning($"[DeathStormCrafting] Cannot start — {reason}");
                return;
            }

            StartCountdown();
            TransitionTo(CraftingStage.CoreAlignment);
        }

        /// <summary>
        /// Resume crafting after timeout if the player survived the ENRAGED phase.
        /// Resets the countdown to give a fresh window.
        /// </summary>
        public void ResumeAfterTimeout()
        {
            if (_currentStage != CraftingStage.TimedOut) return;
            if (_hasDeathStorm) return;

            // Restart the timer; stages continue from where they left off
            _countdownRemaining = _countdownTotal;
            _countdownRunning   = true;

            // Re-enter whichever stage was active when the clock hit zero
            // (stages are preserved — _currentStage is set back to TimedOut only
            //  in HandleTimeout, so we need to remember the interrupted stage)
            TransitionTo(_resumeStage);
        }

        /// <inheritdoc />
        public bool SubmitAnswer(int answer)
        {
            if (!_countdownRunning || _hasDeathStorm) return false;

            switch (_currentStage)
            {
                case CraftingStage.CoreAlignment:
                    return HandleCoreAlignment(answer);

                case CraftingStage.EdgeSync:
                    return HandleEdgeSync(answer);

                case CraftingStage.EnergyStabilise:
                    return HandleEnergyStabilise(answer);

                default:
                    return false;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────

        private bool ValidateComponents(out string reason)
        {
            if (_inventory == null) { reason = "IInventory not available."; return false; }
            if (!_inventory.HasItem(_edgeItemId))   { reason = "Missing Edge component.";   return false; }
            if (!_inventory.HasItem(_handleItemId)) { reason = "Missing Handle component."; return false; }
            if (!_inventory.HasItem(_coreItemId))   { reason = "Missing Core component.";   return false; }
            reason = string.Empty;
            return true;
        }

        private void StartCountdown()
        {
            _countdownRemaining = _countdownTotal;
            _countdownRunning   = true;
        }

        // Stage that was interrupted when the timer expired
        private CraftingStage _resumeStage = CraftingStage.CoreAlignment;

        private void TransitionTo(CraftingStage stage)
        {
            _currentStage = stage;
            _stabiliseStep = 0; // reset sub-step; no-op for other stages
            OnStageChanged?.Invoke(stage);
            Debug.Log($"[DeathStormCrafting] → Stage: {stage}  |  Instruction: {CurrentInstruction}");
        }

        private void HandleTimeout()
        {
            _countdownRunning = false;
            _resumeStage      = _currentStage;
            _currentStage     = CraftingStage.TimedOut;
            OnStageChanged?.Invoke(CraftingStage.TimedOut);

            bool isFirst = _firstTimeout;
            if (_firstTimeout)
            {
                _firstTimeout = false;
                // Signal boss to ENRAGE and enable VOID POUNCE
                _boss?.TransitionToPhase(BossPhase.Enraged);
                EnableVoidPounce();
            }

            OnCountdownExpired?.Invoke(isFirst);
        }

        private void EnableVoidPounce()
        {
            // Broadcast a Unity message so the boss prefab's VoidPounce component
            // can self-enable without creating a hard dependency here.
            if (_boss is MonoBehaviour bm)
                bm.SendMessage("EnableVoidPounce", SendMessageOptions.DontRequireReceiver);

            Debug.Log("[DeathStormCrafting] VOID POUNCE enabled on boss.");
        }

        // ── Stage handlers ─────────────────────────────────────────────────

        private bool HandleCoreAlignment(int answer)
        {
            if (answer == _coreAlignmentAnswer)
            {
                TransitionTo(CraftingStage.EdgeSync);
                return true;
            }
            ApplyWrongAnswerPenalty();
            return false;
        }

        private bool HandleEdgeSync(int answer)
        {
            if (answer == _edgeSyncAnswer)
            {
                TransitionTo(CraftingStage.EnergyStabilise);
                return true;
            }
            ApplyWrongAnswerPenalty();
            return false;
        }

        private bool HandleEnergyStabilise(int answer)
        {
            if (_stabiliseStep >= _stabiliseSequence.Count)
            {
                // Sequence already completed (defensive guard)
                return false;
            }

            if (answer == _stabiliseSequence[_stabiliseStep])
            {
                _stabiliseStep++;
                if (_stabiliseStep >= _stabiliseSequence.Count)
                {
                    // All steps correct → success!
                    CompleteCrafting();
                    return true;
                }
                // Correct but more steps remain — fire stage-changed to update UI prompt
                OnStageChanged?.Invoke(CraftingStage.EnergyStabilise);
                return true;
            }

            // Wrong step: reset sequence back to start but keep timer penalty
            _stabiliseStep = 0;
            ApplyWrongAnswerPenalty();
            return false;
        }

        private void ApplyWrongAnswerPenalty()
        {
            _countdownRemaining = Mathf.Max(0f, _countdownRemaining - _wrongAnswerPenalty);
            OnWrongAnswer?.Invoke(_wrongAnswerPenalty);
            Debug.Log($"[DeathStormCrafting] Wrong answer — {_wrongAnswerPenalty}s penalty. Remaining: {_countdownRemaining:F1}s");
        }

        private void CompleteCrafting()
        {
            _countdownRunning = false;
            _hasDeathStorm    = true;
            _currentStage     = CraftingStage.Complete;
            OnStageChanged?.Invoke(CraftingStage.Complete);
            OnCraftingSuccess?.Invoke();
            Debug.Log("[DeathStormCrafting] ★ DeathStorm forged! hasDeathStorm = true");
        }

        private string BuildInstruction()
        {
            switch (_currentStage)
            {
                case CraftingStage.Idle:
                    return "Collect all three DeathStorm components to begin forging.";
                case CraftingStage.CoreAlignment:
                    return "Stage 1 — Core Alignment: channel the Core's energy. Select the correct cardinal direction (North / East / South / West).";
                case CraftingStage.EdgeSync:
                    return $"Stage 2 — Edge Synchronisation: align the angular resonance field. Select angle sector (0–5) matching the Edge frequency.";
                case CraftingStage.EnergyStabilise:
                    return $"Stage 3 — Energy Stabilisation [{_stabiliseStep + 1}/{_stabiliseSequence.Count}]: direct the energy pulse. Select the required channel (N/E/S/W).";
                case CraftingStage.Complete:
                    return "DeathStorm forged! The legendary weapon is yours.";
                case CraftingStage.TimedOut:
                    return "TIME EXPIRED — Survive the ENRAGED boss. You may resume crafting if you endure.";
                default:
                    return string.Empty;
            }
        }
    }
}
