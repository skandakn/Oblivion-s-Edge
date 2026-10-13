using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using OblivionsEdge.Core;

namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Governs the alien boss's combat rules, armor system, weak-point exposure window,
    /// Core-release mechanics, attack damage delivery to the player, Void Pounce enrage integration,
    /// and permanent defeat contract.
    ///
    /// Implements:
    /// - <see cref="IDamageable"/>: Universal damage receiver for Member 3's pulse projectiles.
    /// - <see cref="IBossController"/>: Team baseline boss lifecycle and phase progression contract.
    /// - <see cref="IBossArmorSource"/>: Telemetry source for HUD armor displays.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHealth : MonoBehaviour, IDamageable, IBossController, IBossArmorSource
    {
        #region Inspector Fields

        [Header("Identity")]
        [SerializeField] private string _bossId = "alien-boss";
        [SerializeField] private string _displayName = "Void Apex Predator";

        [Header("Armor Configuration")]
        [Tooltip("Maximum armor capacity. Ordinary bullets deplete armor before the weak-point is exposed.")]
        [SerializeField, Min(1f)] private float _maxArmor = 100f;
        [Tooltip("Starting armor value (clamped between 0 and MaxArmor).")]
        [SerializeField] private float _startingArmor = 100f;
        [Tooltip("Amount of armor replenished when the exposure window expires without a lethal DeathStorm strike.")]
        [SerializeField, Min(1f)] private float _armorRestoreAmount = 100f;

        [Header("Health Configuration")]
        [Tooltip("Maximum boss health pool.")]
        [SerializeField, Min(1f)] private float _maxHealth = 1000f;
        [Tooltip("Starting boss health.")]
        [SerializeField] private float _startingHealth = 1000f;
        [Tooltip("Minimum health ordinary bullets can reduce the boss to. Ordinary bullets cannot permanently kill the boss.")]
        [SerializeField, Min(1f)] private float _minHealthFromOrdinaryBullets = 1f;

        [Header("Exposure & Weak-Point Window")]
        [Tooltip("Duration in seconds that the weak point remains exposed after armor fractures.")]
        [SerializeField, Min(0.5f)] private float _exposedDuration = 5f;
        [Tooltip("Transform marking the weak-point position for aiming and DeathStorm strikes.")]
        [SerializeField] private Transform _weakPointTransform;
        [Tooltip("Collider representing the exposed weak point (enabled only while exposed).")]
        [SerializeField] private Collider2D _weakPointCollider;

        [Header("Core Release")]
        [Tooltip("If true, automatically releases the alien Core upon the first armor fracture.")]
        [SerializeField] private bool _releaseCoreOnFirstArmorBreak = true;
        [Tooltip("Optional prefab of the alien Core item dropped into the arena.")]
        [SerializeField] private GameObject _corePickupPrefab;
        [Tooltip("World spawn point for the released Core item.")]
        [SerializeField] private Transform _coreSpawnPoint;

        [Header("Enrage & Void Pounce")]
        [Tooltip("Internal fallback enrage timer (seconds) if crafting timer signal is delayed.")]
        [SerializeField, Min(10f)] private float _fallbackEnrageTimer = 90f;
        [Tooltip("Attack damage scaling multiplier when the boss is in the ENRAGED state.")]
        [SerializeField, Min(1f)] private float _enragedDamageMultiplier = 1.5f;

        [Header("AI & Script Coordination")]
        [Tooltip("Optional reference to Member 4's AlienBossAI component for shared boss behaviors.")]
        [SerializeField] private MonoBehaviour _alienBossAI;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _armorBrokenColor = new Color(0.4f, 0.85f, 1f, 1f); // Cyro-cyan
        [SerializeField] private Color _exposedColor = new Color(1f, 0.35f, 0.35f, 1f);    // Critical crimson
        [SerializeField] private Color _enragedColor = new Color(0.85f, 0.2f, 0.95f, 1f);   // Void violet
        [SerializeField] private Color _defeatedColor = new Color(0.3f, 0.3f, 0.3f, 0.45f);  // Deceased husk
        [SerializeField] private ParticleSystem _armorBreakVfx;
        [SerializeField] private ParticleSystem _coreReleaseVfx;
        [SerializeField] private ParticleSystem _defeatVfx;

        [Header("Debug & Telemetry")]
        [Tooltip("Enables verbose combat log entries for state and damage transitions.")]
        [SerializeField] private bool _debugLogging = true;

        [Header("Unity Events for Scene Wire-up")]
        public UnityEvent OnArmorDepletedEvent;
        public UnityEvent OnWeakPointExposedEvent;
        public UnityEvent OnWeakPointClosedEvent;
        public UnityEvent OnCoreReleasedEvent;
        public UnityEvent OnEnragedEvent;
        public UnityEvent OnDefeatedEvent;

        #endregion

        #region Runtime State

        private float _currentArmor;
        private float _currentHealth;
        private bool _isAlive = true;
        private bool _isInvulnerable;
        private bool _isExposed;
        private float _exposureTimerRemaining;
        private bool _hasReleasedCore;
        private bool _isEnraged;
        private bool _voidPounceEnabled;
        private float _enrageTimerRemaining;
        private bool _encounterStarted;
        private BossPhase _currentPhase = BossPhase.Dormant;

        private readonly List<IBossPhaseListener> _phaseListeners = new List<IBossPhaseListener>();

        #endregion

        #region Public Properties

        // ── IDamageable ──
        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _isAlive;

        // ── IBossArmorSource ──
        public float CurrentArmor => _currentArmor;
        public float MaxArmor => _maxArmor;

        // ── IBossController ──
        public string BossId => _bossId;
        public string DisplayName => _displayName;
        public BossPhase CurrentPhase => _currentPhase;
        public bool IsInvulnerable => _isInvulnerable;
        public float EnrageTimerRemaining => _enrageTimerRemaining;

        // ── Boss Combat State ──
        public bool IsExposed => _isExposed;
        public float ExposedDuration => _exposedDuration;
        public float ExposureTimerRemaining => _exposureTimerRemaining;
        public bool HasReleasedCore => _hasReleasedCore;
        public bool IsEnraged => _isEnraged || _currentPhase == BossPhase.Enraged;
        public bool VoidPounceEnabled => _voidPounceEnabled;
        public Vector2 WeakPointWorldPosition => _weakPointTransform != null ? (Vector2)_weakPointTransform.position : (Vector2)transform.position;

        #endregion

        #region Public Events

        // ── IDamageable Events ──
        public event Action<DamageInfo> OnDamageTaken;
        public event Action<float, float> OnHealthChanged;
        public event Action<DamageInfo> OnDied;

        // ── IBossArmorSource Events ──
        public event Action<float, float> OnArmorChanged;
        public event Action OnArmorDepleted;

        // ── IBossController Events ──
        public event Action<BossPhase> OnPhaseChanged;
        public event Action OnBossEnraged;
        public event Action OnBossStunned;
        public event Action OnBossDefeated;

        // ── Member 5 Specific Combat Events ──
        public event Action OnArmorExposed;
        public event Action OnWeakPointClosed;
        public event Action OnArmorRestored;
        public event Action OnCoreReleased;
        public event Action<GameObject> OnCoreSpawned;
        public event Action<Vector2> OnVoidPounceTriggered;
        public event Action<GameObject, DamageInfo> OnAttackDelivered;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _currentArmor = Mathf.Clamp(_startingArmor, 0f, _maxArmor);
            _currentHealth = Mathf.Clamp(_startingHealth, 1f, _maxHealth);
            _enrageTimerRemaining = _fallbackEnrageTimer;
            _isAlive = true;

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_weakPointCollider != null)
            {
                _weakPointCollider.enabled = false;
            }

            ResolveAIComponent();
        }

        private void Start()
        {
            BroadcastInitialTelemetry();
        }

        private void Update()
        {
            if (!_encounterStarted || !_isAlive || _currentPhase == BossPhase.Defeated)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            // 1. Exposure window countdown
            if (_isExposed)
            {
                _exposureTimerRemaining -= deltaTime;
                if (_exposureTimerRemaining <= 0f)
                {
                    EndExposureWindow();
                }
            }

            // 2. Fallback Enrage countdown (if not already triggered by crafting timer)
            if (!IsEnraged && !_isExposed)
            {
                _enrageTimerRemaining -= deltaTime;
                if (_enrageTimerRemaining <= 0f)
                {
                    TriggerEnrage();
                }
            }
        }

        #endregion

        #region Encounter Lifecycle (IBossController)

        /// <summary>
        /// Begins the combat encounter, starting telemetry broadcasts and internal timers.
        /// </summary>
        public void StartBossEncounter()
        {
            if (_encounterStarted || !_isAlive)
            {
                return;
            }

            _encounterStarted = true;
            _enrageTimerRemaining = _fallbackEnrageTimer;
            TransitionToPhase(BossPhase.Phase1);
            NotifyAI("StartEncounter");
            Log("[BossHealth] Boss encounter started.");
        }

        /// <summary>
        /// Orchestrates phase transitions in accordance with the IBossController contract.
        /// </summary>
        public void TransitionToPhase(BossPhase newPhase)
        {
            if (!_isAlive && newPhase != BossPhase.Defeated)
            {
                return;
            }

            if (newPhase == _currentPhase)
            {
                return;
            }

            // Defeat transition contract:
            if (newPhase == BossPhase.Defeated)
            {
                if (_isExposed || _currentPhase == BossPhase.VulnerableStun)
                {
                    DamageInfo fatalDamage = new DamageInfo(_currentHealth, DamageType.Physical, null, transform.position, Vector2.zero, 0f, true);
                    ExecuteDefeat(fatalDamage);
                }
                else
                {
                    LogWarning("[BossHealth] TransitionToPhase(Defeated) REJECTED: Boss is protected by armor and not in VulnerableStun/EXPOSED state!");
                }
                return;
            }

            BossPhase previousPhase = _currentPhase;
            _currentPhase = newPhase;

            switch (newPhase)
            {
                case BossPhase.VulnerableStun:
                    ExposeWeakPoint(_exposedDuration);
                    break;
                case BossPhase.Enraged:
                    TriggerEnrage();
                    break;
                case BossPhase.Phase1:
                case BossPhase.Phase2:
                case BossPhase.Phase3:
                    SetVisualColor(_normalColor);
                    break;
            }

            NotifyPhaseListeners(newPhase);
            OnPhaseChanged?.Invoke(newPhase);
            Log($"[BossHealth] Phase transitioned: {previousPhase} -> {newPhase}.");
        }

        public void SetInvulnerable(bool invulnerable)
        {
            _isInvulnerable = invulnerable;
            Log($"[BossHealth] Invulnerability set to: {invulnerable}.");
        }

        public void TriggerStun(float durationSeconds)
        {
            ExposeWeakPoint(durationSeconds);
        }

        public void RegisterPhaseListener(IBossPhaseListener listener)
        {
            if (listener != null && !_phaseListeners.Contains(listener))
            {
                _phaseListeners.Add(listener);
            }
        }

        public void UnregisterPhaseListener(IBossPhaseListener listener)
        {
            if (listener != null)
            {
                _phaseListeners.Remove(listener);
            }
        }

        #endregion

        #region Damage Receiving Pipeline (IDamageable)

        /// <summary>
        /// Primary damage receiver for Member 3's pulse projectiles and other damage sources.
        /// Ordinary bullets deplete armor; while exposed, they damage health down to a minimum of 1 HP.
        /// Ordinary bullets cannot permanently kill the boss.
        /// </summary>
        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!_isAlive || _isInvulnerable || damageInfo.Amount <= 0f || _currentPhase == BossPhase.Defeated)
            {
                return;
            }

            // ── Phase A: Armor Active ──
            if (_currentArmor > 0f)
            {
                float damageApplied = Mathf.Min(_currentArmor, damageInfo.Amount);
                _currentArmor = Mathf.Max(0f, _currentArmor - damageApplied);

                OnArmorChanged?.Invoke(_currentArmor, _maxArmor);
                OnDamageTaken?.Invoke(damageInfo);

                Log($"[BossHealth] Armor hit by {(damageInfo.Source != null ? damageInfo.Source.name : "projectile")} for {damageApplied:F1} ({damageInfo.Type}). Remaining Armor: {_currentArmor:F1}/{_maxArmor:F1}.");

                if (_currentArmor <= 0f)
                {
                    TriggerArmorBreak();
                }
                return;
            }

            // ── Phase B: Armor Depleted & Weak Point Exposed ──
            if (_isExposed || _currentPhase == BossPhase.VulnerableStun)
            {
                // Ordinary bullets chip health but cannot drop below _minHealthFromOrdinaryBullets (1 HP).
                // Only DeathStorm's final-strike contract can transition to Defeated.
                float oldHealth = _currentHealth;
                float clampedHealth = Mathf.Max(_minHealthFromOrdinaryBullets, _currentHealth - damageInfo.Amount);
                float actualDamage = oldHealth - clampedHealth;
                _currentHealth = clampedHealth;

                if (actualDamage > 0f)
                {
                    OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
                    OnDamageTaken?.Invoke(damageInfo);
                    Log($"[BossHealth] Exposed boss struck for {actualDamage:F1} damage. Health: {_currentHealth:F1}/{_maxHealth:F1}. (Protected from permanent death by ordinary weapons).");
                }
                else
                {
                    Log($"[BossHealth] Boss health is at minimum threshold ({_minHealthFromOrdinaryBullets:F1} HP). Awaiting DeathStorm final strike!");
                }
                return;
            }

            // ── Phase C: Armor zero but exposure window closed ──
            Log("[BossHealth] Attack deflected! Armor is regenerating and weak-point is shielded.");
        }

        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            Log($"[BossHealth] Healed for {amount:F1}. Health: {_currentHealth:F1}/{_maxHealth:F1}.");
        }

        #endregion

        #region Armor-Break & Exposure Window Logic

        private void TriggerArmorBreak()
        {
            Log("[BossHealth] ★ ARMOR FRACTURED! Initiating EXPOSED weak-point state.");

            OnArmorDepleted?.Invoke();
            OnArmorDepletedEvent?.Invoke();

            ExposeWeakPoint(_exposedDuration);

            // Core-release on first armor fracture condition
            if (_releaseCoreOnFirstArmorBreak && !_hasReleasedCore)
            {
                ReleaseCore();
            }
        }

        /// <summary>
        /// Opens the weak-point exposure window for the specified duration.
        /// </summary>
        public void ExposeWeakPoint(float duration = -1f)
        {
            if (!_isAlive || _currentPhase == BossPhase.Defeated)
            {
                return;
            }

            float windowDuration = duration > 0f ? duration : _exposedDuration;
            _isExposed = true;
            _exposureTimerRemaining = windowDuration;
            _currentPhase = BossPhase.VulnerableStun;

            if (_weakPointCollider != null)
            {
                _weakPointCollider.enabled = true;
            }

            SetVisualColor(_exposedColor);
            PlayVfx(_armorBreakVfx);

            // Synchronize with Member 4's AlienBossAI
            NotifyAI("NotifyArmorDepleted");

            OnArmorExposed?.Invoke();
            OnBossStunned?.Invoke();
            OnPhaseChanged?.Invoke(BossPhase.VulnerableStun);
            OnWeakPointExposedEvent?.Invoke();
            NotifyPhaseListeners(BossPhase.VulnerableStun);

            Log($"[BossHealth] Weak point EXPOSED for {windowDuration:F1} seconds. Ready for DeathStorm final strike!");
        }

        private void EndExposureWindow()
        {
            if (!_isAlive || !_isExposed || _currentPhase == BossPhase.Defeated)
            {
                return;
            }

            _isExposed = false;
            _exposureTimerRemaining = 0f;

            if (_weakPointCollider != null)
            {
                _weakPointCollider.enabled = false;
            }

            // Restore armor to configured restore threshold
            _currentArmor = Mathf.Clamp(_armorRestoreAmount, 1f, _maxArmor);
            OnArmorRestored?.Invoke();
            OnArmorChanged?.Invoke(_currentArmor, _maxArmor);

            // Resume appropriate combat phase
            BossPhase resumePhase = _isEnraged ? BossPhase.Enraged : BossPhase.Phase1;
            _currentPhase = resumePhase;

            SetVisualColor(_isEnraged ? _enragedColor : _normalColor);

            OnWeakPointClosed?.Invoke();
            OnPhaseChanged?.Invoke(resumePhase);
            OnWeakPointClosedEvent?.Invoke();
            NotifyPhaseListeners(resumePhase);

            Log($"[BossHealth] Exposure window expired. Armor reinforced to {_currentArmor:F1}/{_maxArmor:F1}. Resumed {resumePhase}.");
        }

        #endregion

        #region Core-Release Logic

        /// <summary>
        /// Releases the alien Core item drop into the arena.
        /// Idempotent: Can only trigger once per encounter.
        /// </summary>
        /// <returns>True if the core was newly released; false if already released or boss is deceased.</returns>
        public bool ReleaseCore()
        {
            if (!_isAlive || _hasReleasedCore || _currentPhase == BossPhase.Defeated)
            {
                return false;
            }

            _hasReleasedCore = true;

            GameObject spawnedCore = null;
            Vector2 spawnPos = _coreSpawnPoint != null ? (Vector2)_coreSpawnPoint.position : (Vector2)transform.position;

            if (_corePickupPrefab != null)
            {
                spawnedCore = Instantiate(_corePickupPrefab, spawnPos, Quaternion.identity);
                Log($"[BossHealth] Alien Core prefab '{_corePickupPrefab.name}' instantiated at {spawnPos}.");
            }

            PlayVfx(_coreReleaseVfx);

            // Synchronize with Member 4's AlienBossAI
            NotifyAI("NotifyCoreReleased");

            OnCoreReleased?.Invoke();
            OnCoreReleasedEvent?.Invoke();
            if (spawnedCore != null)
            {
                OnCoreSpawned?.Invoke(spawnedCore);
            }

            Log("[BossHealth] ★ ALIEN CORE RELEASED! Player may now collect Core for legendary weapon crafting.");
            return true;
        }

        #endregion

        #region DeathStorm Final Strike Contract & Defeat State

        /// <summary>
        /// Explicit contract method for Member 10's DeathStorm axe strike to permanently defeat the boss.
        /// Requires the boss to currently be in the EXPOSED / VulnerableStun window.
        /// </summary>
        /// <param name="source">Source GameObject initiating the strike.</param>
        /// <returns>True if the strike permanently defeated the boss.</returns>
        public bool ExecuteDeathStormStrike(GameObject source = null)
        {
            if (!_isAlive)
            {
                return false;
            }

            if (!_isExposed && _currentPhase != BossPhase.VulnerableStun)
            {
                LogWarning("[BossHealth] DeathStorm strike rejected: Boss is protected by armor and not in EXPOSED state!");
                return false;
            }

            Log("[BossHealth] ★ VALID DEATHSTORM FINAL STRIKE CONFIRMED! Boss defeated.");
            DamageInfo fatalDamage = new DamageInfo(
                _currentHealth,
                DamageType.Physical,
                source,
                WeakPointWorldPosition,
                Vector2.zero,
                15f,
                true
            );

            ExecuteDefeat(fatalDamage);
            return true;
        }

        private void ExecuteDefeat(DamageInfo fatalDamage)
        {
            if (!_isAlive)
            {
                return;
            }

            _isAlive = false;
            _currentHealth = 0f;
            _currentPhase = BossPhase.Defeated;
            _isExposed = false;
            _exposureTimerRemaining = 0f;

            // 1. Disable locomotion & physics movement
            StopMovementAndAttacks();

            // 2. Disable weak point collider
            if (_weakPointCollider != null)
            {
                _weakPointCollider.enabled = false;
            }

            // 3. Visual feedback
            SetVisualColor(_defeatedColor);
            PlayVfx(_defeatVfx);

            // 4. Synchronize with Member 4's AlienBossAI
            NotifyAI("NotifyBossDefeated");

            // 5. Broadcast defeat events
            OnHealthChanged?.Invoke(0f, _maxHealth);
            OnBossDefeated?.Invoke();
            OnPhaseChanged?.Invoke(BossPhase.Defeated);
            OnDied?.Invoke(fatalDamage);
            OnDefeatedEvent?.Invoke();
            NotifyPhaseListeners(BossPhase.Defeated);

            Log("[BossHealth] ★ BOSS PERMANENTLY DEFEATED. Attacks and locomotion disabled.");
        }

        private void StopMovementAndAttacks()
        {
            // Zero Rigidbody2D velocity
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;
            }

            // Disable all attack hitboxes on boss
            BossAttackHitbox[] hitboxes = GetComponentsInChildren<BossAttackHitbox>(true);
            for (int i = 0; i < hitboxes.Length; i++)
            {
                hitboxes[i].enabled = false;
                Collider2D col = hitboxes[i].GetComponent<Collider2D>();
                if (col != null)
                {
                    col.enabled = false;
                }
            }
        }

        #endregion

        #region Enrage & Void Pounce Integration

        /// <summary>
        /// Transitions the boss into the ENRAGED state, scaling attack damage and unlocking Void Pounce.
        /// Integrates with crafting countdown expiration or fallback enrage timers.
        /// </summary>
        public void TriggerEnrage()
        {
            if (!_isAlive || _currentPhase == BossPhase.Defeated || _isEnraged)
            {
                return;
            }

            _isEnraged = true;
            _voidPounceEnabled = true;

            if (!_isExposed)
            {
                _currentPhase = BossPhase.Enraged;
                SetVisualColor(_enragedColor);
                OnPhaseChanged?.Invoke(BossPhase.Enraged);
                NotifyPhaseListeners(BossPhase.Enraged);
            }

            // Synchronize with Member 4's AlienBossAI
            NotifyAI("NotifyCraftingTimerElapsed");

            OnBossEnraged?.Invoke();
            OnEnragedEvent?.Invoke();

            Log("[BossHealth] ★ BOSS HAS ENRAGED! Void Pounce unlocked and attacks amplified.");
        }

        /// <summary>
        /// SendMessage-compatible hook matching DeathStorm crafting timer timeout broadcast.
        /// </summary>
        public void EnableVoidPounce()
        {
            Log("[BossHealth] 'EnableVoidPounce' message received.");
            _voidPounceEnabled = true;
            if (!IsEnraged)
            {
                TriggerEnrage();
            }
        }

        /// <summary>
        /// Clean programmatic trigger for Void Pounce attack. Strictly requires the boss to be in the ENRAGED state.
        /// </summary>
        /// <param name="targetPosition">Optional world position target for the pounce. Resolves player if null.</param>
        /// <returns>True if Void Pounce was successfully triggered; false if not enraged or defeated.</returns>
        public bool TriggerVoidPounce(Vector2? targetPosition = null)
        {
            if (!_isAlive || _currentPhase == BossPhase.Defeated)
            {
                LogWarning("[BossHealth] Cannot trigger Void Pounce: Boss is defeated.");
                return false;
            }

            if (!IsEnraged)
            {
                LogWarning("[BossHealth] Cannot trigger Void Pounce: Boss is NOT in the ENRAGED state!");
                return false;
            }

            Vector2 target = targetPosition ?? GetPlayerPosition();
            OnVoidPounceTriggered?.Invoke(target);

            Log($"[BossHealth] ★ VOID POUNCE EXECUTED toward {target}!");
            return true;
        }

        #endregion

        #region Player Attack Damage Delivery (IDamageable Interface)

        /// <summary>
        /// Delivers boss attack damage to a target GameObject through the agreed IDamageable health interface.
        /// Applies the enrage multiplier if the boss is currently enraged.
        /// </summary>
        public bool DeliverAttackDamage(GameObject target, float amount, DamageType type = DamageType.Physical, float knockback = 5f, bool isCritical = false)
        {
            if (!_isAlive || _currentPhase == BossPhase.Defeated || target == null || amount <= 0f)
            {
                return false;
            }

            IDamageable playerDamageable = target.GetComponentInParent<IDamageable>();
            if (playerDamageable == null || !playerDamageable.IsAlive)
            {
                return false;
            }

            float finalDamage = amount;
            if (IsEnraged)
            {
                finalDamage *= _enragedDamageMultiplier;
            }

            Vector2 hitPoint = target.transform.position;
            Vector2 hitDirection = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
            {
                hitDirection = Vector2.right;
            }

            DamageInfo damagePayload = new DamageInfo(
                finalDamage,
                type,
                gameObject, // Boss GameObject as Source
                hitPoint,
                hitDirection,
                knockback,
                isCritical
            );

            playerDamageable.TakeDamage(damagePayload);
            OnAttackDelivered?.Invoke(target, damagePayload);

            Log($"[BossHealth] Delivered {finalDamage:F1} {type} damage to {target.name}. Knockback: {knockback:F1}.");
            return true;
        }

        /// <summary>
        /// Overload delivering attack damage to a hit Collider2D.
        /// </summary>
        public bool DeliverAttackDamage(Collider2D targetCollider, float amount, DamageType type = DamageType.Physical, float knockback = 5f, bool isCritical = false)
        {
            if (targetCollider == null) return false;
            return DeliverAttackDamage(targetCollider.gameObject, amount, type, knockback, isCritical);
        }

        /// <summary>
        /// Convenience method targeting the active Player in the scene.
        /// </summary>
        public bool DealDamageToPlayer(float amount, DamageType type = DamageType.Physical, float knockback = 5f, bool isCritical = false)
        {
            GameObject player = GameObject.FindGameObjectWithTag(GameConstants.TagPlayer);
            if (player == null)
            {
                LogWarning("[BossHealth] DealDamageToPlayer failed: No GameObject with tag 'Player' found.");
                return false;
            }

            return DeliverAttackDamage(player, amount, type, knockback, isCritical);
        }

        #endregion

        #region Internal Helpers & Telemetry

        private void ResolveAIComponent()
        {
            if (_alienBossAI == null)
            {
                // Attempt reflection / name-based resolution so peer compile errors are impossible
                MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null && behaviours[i].GetType().Name == "AlienBossAI")
                    {
                        _alienBossAI = behaviours[i];
                        break;
                    }
                }
            }
        }

        private void NotifyAI(string methodName)
        {
            if (_alienBossAI != null)
            {
                _alienBossAI.SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
            }
            else
            {
                SendMessage(methodName, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void NotifyPhaseListeners(BossPhase phase)
        {
            for (int i = _phaseListeners.Count - 1; i >= 0; i--)
            {
                try
                {
                    _phaseListeners[i]?.OnBossPhaseEntered(phase);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        private Vector2 GetPlayerPosition()
        {
            GameObject player = GameObject.FindGameObjectWithTag(GameConstants.TagPlayer);
            return player != null ? (Vector2)player.transform.position : (Vector2)transform.position;
        }

        private void SetVisualColor(Color color)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }

        private void PlayVfx(ParticleSystem vfx)
        {
            if (vfx != null)
            {
                vfx.Play();
            }
        }

        private void BroadcastInitialTelemetry()
        {
            OnArmorChanged?.Invoke(_currentArmor, _maxArmor);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnPhaseChanged?.Invoke(_currentPhase);
        }

        private void Log(string message)
        {
            if (_debugLogging)
            {
                Debug.Log(message, this);
            }
        }

        private void LogWarning(string message)
        {
            if (_debugLogging)
            {
                Debug.LogWarning(message, this);
            }
        }

        #endregion

        #region Editor Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw weak point visualization in editor
            Vector2 weakPos = WeakPointWorldPosition;
            Gizmos.color = _isExposed ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(weakPos, 0.4f);

            // Draw spawn point visualization
            if (_coreSpawnPoint != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(_coreSpawnPoint.position, new Vector3(0.5f, 0.5f, 0f));
            }
        }

        #endregion
    }
}
