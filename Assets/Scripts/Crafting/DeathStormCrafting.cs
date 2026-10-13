using System;
using System.Collections.Generic;
using UnityEngine;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;
using OblivionsEdge.Inventory;

namespace OblivionsEdge.DeathStorm
{
    // ─────────────────────────────────────────────────────────────────────────
    //  DeathStormCrafting.cs — Legendary weapon crafting controller
    //  Owner: Member 10
    //
    //  Responsibilities:
    //    · Validate that Edge, Handle and Core are all present in the inventory.
    //    · Run a strict 60-second synthesis countdown once crafting commences.
    //    · Drive three sequential puzzle stages (Core alignment → Edge sync →
    //      Energy stabilise).
    //    · Wrong answers deduct time penalty without soft-locking the mini-game.
    //    · On timeout, trigger boss ENRAGED phase and enable VOID POUNCE.
    //    · On completion: forge the legendary weapon DeathStorm and notify inventory.
    //    · Allow crafting to restart after timeout if the player survives.
    //
    //  Members 5 (IInventory), 8 (Core Puzzle), and 5/3 (IBossController) are
    //  coordinated via decoupled interfaces and resilient messaging.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Concrete implementation of the DeathStorm crafting mini-game.
    /// Attach to any persistent GameObject in the boss arena scene.
    /// Wire <see cref="BossController"/> and <see cref="PlayerInventory"/> in the Inspector.
    /// </summary>
    public class DeathStormCrafting : MonoBehaviour, IDeathStormCrafting
    {
        // ── Inspector-configurable values ──────────────────────────────────
        [Header("Item IDs (matches Member 9 InventorySystem)")]
        [SerializeField] private string _edgeItemId   = "deathstorm_edge";
        [SerializeField] private string _handleItemId = "deathstorm_handle";
        [SerializeField] private string _coreItemId   = "deathstorm_core";

        [Header("Countdown")]
        [Tooltip("Total crafting window in seconds (strict 60s synthesis timer).")]
        [SerializeField] private float _countdownTotal = 60f;

        [Header("Wrong-Answer Penalty")]
        [Tooltip("Seconds deducted from the timer per wrong answer without soft-locking.")]
        [SerializeField] private float _wrongAnswerPenalty = 5f;

        [Header("Stage 1 — Core Alignment (4-direction)")]
        [Tooltip("Correct cardinal direction index: 0=North 1=East 2=South 3=West (South per Kira's note)")]
        [SerializeField] private int _coreAlignmentAnswer = 2;

        [Header("Stage 2 — Edge Sync (angle buckets 0-5)")]
        [Tooltip("Correct angle bucket index: 0-5 → 60° increments starting at 0° (Bucket 3 = 180° per Kira's note)")]
        [SerializeField] private int _edgeSyncAnswer = 3;

        [Header("Stage 3 — Energy Stabilise (directional sequence length 3)")]
        [Tooltip("Three-step directional channel sequence: 0=N 1=E 2=S 3=W (East, West, North = 1, 3, 0)")]
        [SerializeField] private List<int> _stabiliseSequence = new List<int> { 1, 3, 0 };

        [Header("Dependencies (injected by scene, DI, or auto-resolved)")]
        [Tooltip("Member 3/5 IBossController implementation.")]
        [SerializeField] private MonoBehaviour _bossControllerMono;
        [Tooltip("Member 9 IInventory implementation attached to the player.")]
        [SerializeField] private MonoBehaviour _playerInventoryMono;

        // ── Runtime state ──────────────────────────────────────────────────
        private IBossController _boss;
        private IInventory      _inventory;

        private CraftingStage _currentStage       = CraftingStage.Idle;
        private float         _countdownRemaining = 0f;
        private bool          _countdownRunning   = false;
        private bool          _hasDeathStorm      = false;
        private bool          _firstTimeout       = true;

        // Stage 3 sub-step index
        private int _stabiliseStep = 0;

        // Stage interrupted on timeout
        private CraftingStage _resumeStage = CraftingStage.CoreAlignment;

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
            // Resolve interfaces from serialized MonoBehaviours (avoids hard deps)
            _boss      = _bossControllerMono  as IBossController;
            _inventory = _playerInventoryMono as IInventory;

            // Auto-resolve fallbacks if not assigned in Inspector
            if (_inventory == null)
            {
                var player = GameObject.FindGameObjectWithTag(GameConstants.TagPlayer);
                if (player != null)
                {
                    _inventory = player.GetComponentInParent<IInventory>();
                    if (_inventory == null) _inventory = player.GetComponentInChildren<IInventory>();
                }
                if (_inventory == null)
                {
                    _inventory = FindFirstObjectByType<InventorySystem>() as IInventory;
                }
            }

            if (_boss == null)
            {
                var bossGo = GameObject.FindGameObjectWithTag(GameConstants.TagBoss);
                if (bossGo != null)
                {
                    _boss = bossGo.GetComponentInParent<IBossController>();
                    if (_boss == null) _boss = bossGo.GetComponentInChildren<IBossController>();
                }
            }
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
        /// Explicit starting trigger for crafting synthesis. Validates all three components
        /// (Edge, Handle, Core) are present before commencing the 60-second countdown.
        /// </summary>
        public bool StartCrafting()
        {
            if (_hasDeathStorm || _currentStage == CraftingStage.Complete) return false;

            if (!ValidateComponents(out string reason))
            {
                Debug.LogWarning($"[DeathStormCrafting] Cannot start — {reason}");
                return false;
            }

            StartCountdown();
            TransitionTo(CraftingStage.CoreAlignment);
            return true;
        }

        /// <summary>
        /// Called by Member 8/9 Core pickup the moment the Core is collected from the containment puzzle.
        /// </summary>
        public void NotifyCoreCollected()
        {
            StartCrafting();
        }

        /// <summary>
        /// Resume crafting after timeout if the player survived the ENRAGED boss phase.
        /// Resets the countdown to give a fresh 60s window.
        /// </summary>
        public void ResumeAfterTimeout()
        {
            if (_currentStage != CraftingStage.TimedOut) return;
            if (_hasDeathStorm) return;

            // Restart the timer; stages continue from where they left off
            _countdownRemaining = _countdownTotal;
            _countdownRunning   = true;

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
            if (_inventory == null)
            {
                reason = "IInventory not available.";
                return false;
            }

            if (!_inventory.HasItem(_edgeItemId))
            {
                reason = "Missing Edge component (deathstorm_edge).";
                return false;
            }

            if (!_inventory.HasItem(_handleItemId))
            {
                reason = "Missing Handle component (deathstorm_handle).";
                return false;
            }

            if (!_inventory.HasItem(_coreItemId))
            {
                reason = "Missing Core component (deathstorm_core).";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void StartCountdown()
        {
            _countdownRemaining = _countdownTotal;
            _countdownRunning   = true;
        }

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
            // Broadcast messages so the boss AI / VoidPounce component can react
            if (_boss is MonoBehaviour bm)
            {
                bm.SendMessage("EnableVoidPounce", SendMessageOptions.DontRequireReceiver);
                bm.SendMessage("NotifyCraftingTimerElapsed", SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                var bossGo = GameObject.FindGameObjectWithTag(GameConstants.TagBoss);
                if (bossGo != null)
                {
                    bossGo.SendMessage("EnableVoidPounce", SendMessageOptions.DontRequireReceiver);
                    bossGo.SendMessage("NotifyCraftingTimerElapsed", SendMessageOptions.DontRequireReceiver);
                }
            }

            Debug.Log("[DeathStormCrafting] VOID POUNCE and Enrage enabled on boss.");
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
                return false;
            }

            if (answer == _stabiliseSequence[_stabiliseStep])
            {
                _stabiliseStep++;
                if (_stabiliseStep >= _stabiliseSequence.Count)
                {
                    CompleteCrafting();
                    return true;
                }
                OnStageChanged?.Invoke(CraftingStage.EnergyStabilise);
                return true;
            }

            // Wrong step: reset sequence back to start of stage 3 but keep timer penalty
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

            // Notify Member 9 InventorySystem
            if (_inventory is InventorySystem invSys)
            {
                invSys.NotifyDeathStormCrafted();
            }
            else if (_inventory is MonoBehaviour mb)
            {
                mb.SendMessage("NotifyDeathStormCrafted", SendMessageOptions.DontRequireReceiver);
            }

            OnStageChanged?.Invoke(CraftingStage.Complete);
            OnCraftingSuccess?.Invoke();
            Debug.Log("[DeathStormCrafting] ★ DeathStorm forged! hasDeathStorm = true");
        }

        private string BuildInstruction()
        {
            switch (_currentStage)
            {
                case CraftingStage.Idle:
                    return "Collect all three DeathStorm components (Edge, Handle, Core) to begin forging.";
                case CraftingStage.CoreAlignment:
                    return "Stage 1 — Core Alignment: channel the Core's polarity. Select cardinal direction (0=North, 1=East, 2=South, 3=West).";
                case CraftingStage.EdgeSync:
                    return "Stage 2 — Edge Synchronisation: align resonance field. Select angle sector (0–5) matching Edge frequency.";
                case CraftingStage.EnergyStabilise:
                    return $"Stage 3 — Energy Stabilisation [{_stabiliseStep + 1}/{_stabiliseSequence.Count}]: direct pulse channel. Select channel (0=N, 1=E, 2=S, 3=W).";
                case CraftingStage.Complete:
                    return "DeathStorm forged! The legendary weapon is ready for the final strike.";
                case CraftingStage.TimedOut:
                    return "SYNTHESIS TIMEOUT — Survive the ENRAGED boss. Resume crafting once the window reopens.";
                default:
                    return string.Empty;
            }
        }
    }
}
