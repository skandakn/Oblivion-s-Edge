using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Combat states used by <see cref="AlienBossAI"/>. State changes only occur through
    /// TryTransitionTo so encounter code cannot accidentally start overlapping attacks.
    /// </summary>
    public enum AlienBossState
    {
        Awakening,
        Hunt,
        Telegraph,
        Charge,
        Recover,
        Exposed,
        CoreReleased,
        Enraged,
        Defeated
    }

    /// <summary>
    /// Drives the alien boss's movement and attacks. A separate health/armor component owns
    /// damage calculation and calls the public notification methods below when its thresholds change.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class AlienBossAI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private Transform _player;

        [Header("Identity")]
        [SerializeField] private string _bossId = "alien-boss";

        [Header("Movement")]
        [SerializeField, Min(0f)] private float _huntSpeed = 3.5f;
        [SerializeField, Min(0f)] private float _chargeSpeed = 12f;
        [SerializeField, Min(0f)] private float _attackRange = 3f;
        [SerializeField, Min(0f)] private float _chargeStopDistance = 0.1f;

        [Header("Standard Charge")]
        [SerializeField, Min(0f)] private float _awakeningDuration = 1.5f;
        [SerializeField, Min(0f)] private float _telegraphDuration = 0.8f;
        [SerializeField, Min(0f)] private float _chargeDuration = 0.7f;
        [SerializeField, Min(0f)] private float _recoverDuration = 1.25f;
        [SerializeField, Min(0f)] private float _chargeCooldown = 2f;

        [Header("Armor and Core")]
        [SerializeField, Min(0f)] private float _exposedDuration = 4f;

        [Header("Enrage")]
        [SerializeField, Min(0f)] private float _enrageAfterSeconds = 90f;
        [SerializeField, Min(0f)] private float _voidPounceRange = 6f;
        [SerializeField, Min(0f)] private float _voidPounceSpeed = 16f;
        [SerializeField, Min(0f)] private float _voidPounceTelegraphDuration = 0.5f;
        [SerializeField, Min(0f)] private float _voidPounceDuration = 0.45f;
        [SerializeField, Min(0f)] private float _voidPounceCooldown = 3f;

        public string BossId => _bossId;
        public AlienBossState CurrentState { get; private set; } = AlienBossState.Awakening;
        public bool IsDefeated => CurrentState == AlienBossState.Defeated;
        public bool IsEnraged => _hasEnraged;
        public bool IsArmorExposed => CurrentState == AlienBossState.Exposed;
        public Vector2 ChargeTargetPosition => _chargeTargetPosition;

        public event Action<AlienBossState> OnStateChanged;
        public event Action<AlienBossState, AlienBossState> OnInvalidTransitionRequested;
        public event Action<Vector2> OnChargeTelegraphed;
        public event Action<Vector2> OnChargeStarted;
        public event Action OnChargeMissed;
        public event Action OnArmorExposed;
        public event Action OnCoreReleased;
        public event Action OnEnraged;
        public event Action<Vector2> OnVoidPounceTelegraphed;
        public event Action<Vector2> OnVoidPounceStarted;
        public event Action OnDefeated;

        private readonly HashSet<AlienBossState> _terminalStates = new HashSet<AlienBossState>
        {
            AlienBossState.CoreReleased,
            AlienBossState.Defeated
        };

        private float _stateTimeRemaining;
        private float _chargeCooldownRemaining;
        private float _voidPounceCooldownRemaining;
        private float _enrageTimerRemaining;
        private Vector2 _chargeTargetPosition;
        private Vector2 _movementDirection;
        private float _movementSpeed;
        private bool _encounterStarted;
        private bool _hasEnraged;
        private bool _voidPounceTelegraphing;
        private bool _voidPounceActive;
        private float _voidPounceTimeRemaining;
        private Vector2 _voidPounceTargetPosition;

        private void Reset()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void Awake()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody2D>();
            }
        }

        private void Start()
        {
            ResolvePlayer();
        }

        private void Update()
        {
            if (!_encounterStarted || IsDefeated)
            {
                StopMovement();
                return;
            }

            float deltaTime = Time.deltaTime;
            _chargeCooldownRemaining = Mathf.Max(0f, _chargeCooldownRemaining - deltaTime);
            _voidPounceCooldownRemaining = Mathf.Max(0f, _voidPounceCooldownRemaining - deltaTime);

            if (!IsEnraged && !_terminalStates.Contains(CurrentState))
            {
                _enrageTimerRemaining = Mathf.Max(0f, _enrageTimerRemaining - deltaTime);
                if (_enrageTimerRemaining <= 0f)
                {
                    TriggerEnrage();
                }
            }

            UpdateState(deltaTime);
        }

        private void FixedUpdate()
        {
            if (_movementSpeed <= 0f || _movementDirection.sqrMagnitude <= 0f || IsDefeated)
            {
                return;
            }

            _rigidbody.MovePosition(_rigidbody.position + _movementDirection * _movementSpeed * Time.fixedDeltaTime);
        }

        /// <summary>Begins the AI encounter and its awakening sequence.</summary>
        public void StartEncounter()
        {
            if (_encounterStarted || IsDefeated)
            {
                return;
            }

            _encounterStarted = true;
            _enrageTimerRemaining = _enrageAfterSeconds;
            EnterState(AlienBossState.Awakening);
        }

        /// <summary>
        /// Explicit integration point for a health/armor component. It is idempotent so a depleted
        /// armor event cannot restart the exposed window or interrupt a defeated boss.
        /// </summary>
        public void NotifyArmorDepleted()
        {
            if (!_encounterStarted || IsDefeated || CurrentState == AlienBossState.CoreReleased || CurrentState == AlienBossState.Exposed)
            {
                return;
            }

            TryTransitionTo(AlienBossState.Exposed);
        }

        /// <summary>Explicit integration point for the core/health component after it releases the core.</summary>
        public void NotifyCoreReleased()
        {
            if (!_encounterStarted || IsDefeated || CurrentState == AlienBossState.CoreReleased)
            {
                return;
            }

            TryTransitionTo(AlienBossState.CoreReleased);
        }

        /// <summary>Explicit integration point for the crafting timer completing.</summary>
        public void NotifyCraftingTimerElapsed()
        {
            TriggerEnrage();
        }

        /// <summary>Explicit integration point for the health component when health reaches zero.</summary>
        public void NotifyBossDefeated()
        {
            if (IsDefeated)
            {
                return;
            }

            TryTransitionTo(AlienBossState.Defeated);
        }

        /// <summary>
        /// Lets a hitbox report a successful charge impact. A successful impact ends the charge
        /// without incorrectly broadcasting a miss.
        /// </summary>
        public void NotifyChargeHit()
        {
            if (CurrentState == AlienBossState.Charge)
            {
                TryTransitionTo(IsEnraged ? AlienBossState.Enraged : AlienBossState.Hunt);
            }
        }

        public bool TryTransitionTo(AlienBossState nextState)
        {
            if (nextState == CurrentState)
            {
                return false;
            }

            if (!CanTransitionTo(nextState))
            {
                OnInvalidTransitionRequested?.Invoke(CurrentState, nextState);
                return false;
            }

            EnterState(nextState);
            return true;
        }

        private bool CanTransitionTo(AlienBossState nextState)
        {
            if (IsDefeated)
            {
                return false;
            }

            if (nextState == AlienBossState.Defeated || nextState == AlienBossState.Enraged)
            {
                return true;
            }

            if (CurrentState == AlienBossState.CoreReleased)
            {
                return false;
            }

            switch (CurrentState)
            {
                case AlienBossState.Awakening:
                    return nextState == AlienBossState.Hunt || nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Hunt:
                    return nextState == AlienBossState.Telegraph || nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Telegraph:
                    return nextState == AlienBossState.Charge || nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Charge:
                    return nextState == AlienBossState.Recover || nextState == AlienBossState.Hunt || nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Recover:
                    return nextState == AlienBossState.Hunt || nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Exposed:
                    return nextState == AlienBossState.Hunt || nextState == AlienBossState.CoreReleased;
                case AlienBossState.Enraged:
                    return nextState == AlienBossState.Exposed || nextState == AlienBossState.CoreReleased;
                default:
                    return false;
            }
        }

        private void EnterState(AlienBossState nextState)
        {
            CurrentState = nextState;
            StopMovement();

            switch (nextState)
            {
                case AlienBossState.Awakening:
                    _stateTimeRemaining = _awakeningDuration;
                    break;
                case AlienBossState.Telegraph:
                    _stateTimeRemaining = _telegraphDuration;
                    _chargeTargetPosition = GetPlayerPosition();
                    OnChargeTelegraphed?.Invoke(_chargeTargetPosition);
                    break;
                case AlienBossState.Charge:
                    _stateTimeRemaining = _chargeDuration;
                    _chargeCooldownRemaining = _chargeCooldown;
                    OnChargeStarted?.Invoke(_chargeTargetPosition);
                    break;
                case AlienBossState.Recover:
                    _stateTimeRemaining = _recoverDuration;
                    OnChargeMissed?.Invoke();
                    break;
                case AlienBossState.Exposed:
                    _stateTimeRemaining = _exposedDuration;
                    OnArmorExposed?.Invoke();
                    break;
                case AlienBossState.CoreReleased:
                    OnCoreReleased?.Invoke();
                    break;
                case AlienBossState.Enraged:
                    CancelVoidPounce();
                    OnEnraged?.Invoke();
                    break;
                case AlienBossState.Defeated:
                    CancelVoidPounce();
                    OnDefeated?.Invoke();
                    break;
            }

            OnStateChanged?.Invoke(nextState);
        }

        private void UpdateState(float deltaTime)
        {
            switch (CurrentState)
            {
                case AlienBossState.Awakening:
                    CountDownTo(deltaTime, AlienBossState.Hunt);
                    break;
                case AlienBossState.Hunt:
                    HuntPlayer();
                    break;
                case AlienBossState.Telegraph:
                    CountDownTo(deltaTime, AlienBossState.Charge);
                    break;
                case AlienBossState.Charge:
                    ChargeToStoredPosition(deltaTime);
                    break;
                case AlienBossState.Recover:
                    CountDownTo(deltaTime, AlienBossState.Hunt);
                    break;
                case AlienBossState.Exposed:
                    CountDownTo(deltaTime, _hasEnraged ? AlienBossState.Enraged : AlienBossState.Hunt);
                    break;
                case AlienBossState.Enraged:
                    UpdateEnraged(deltaTime);
                    break;
                case AlienBossState.CoreReleased:
                case AlienBossState.Defeated:
                    StopMovement();
                    break;
            }
        }

        private void HuntPlayer()
        {
            Vector2 toPlayer = GetPlayerPosition() - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude <= _attackRange * _attackRange && _chargeCooldownRemaining <= 0f)
            {
                TryTransitionTo(AlienBossState.Telegraph);
                return;
            }

            SetMovement(toPlayer, _huntSpeed);
        }

        private void ChargeToStoredPosition(float deltaTime)
        {
            Vector2 toChargeTarget = _chargeTargetPosition - (Vector2)transform.position;
            SetMovement(toChargeTarget, _chargeSpeed);
            _stateTimeRemaining -= deltaTime;

            if (toChargeTarget.sqrMagnitude <= _chargeStopDistance * _chargeStopDistance || _stateTimeRemaining <= 0f)
            {
                TryTransitionTo(AlienBossState.Recover);
            }
        }

        private void UpdateEnraged(float deltaTime)
        {
            if (_voidPounceTelegraphing)
            {
                _stateTimeRemaining -= deltaTime;
                if (_stateTimeRemaining <= 0f)
                {
                    _voidPounceTelegraphing = false;
                    _voidPounceActive = true;
                    _voidPounceTimeRemaining = _voidPounceDuration;
                    _voidPounceCooldownRemaining = _voidPounceCooldown;
                    OnVoidPounceStarted?.Invoke(_voidPounceTargetPosition);
                }
                return;
            }

            if (_voidPounceActive)
            {
                Vector2 toPounceTarget = _voidPounceTargetPosition - (Vector2)transform.position;
                SetMovement(toPounceTarget, _voidPounceSpeed);
                _voidPounceTimeRemaining -= deltaTime;
                if (_voidPounceTimeRemaining <= 0f || toPounceTarget.sqrMagnitude <= _chargeStopDistance * _chargeStopDistance)
                {
                    _voidPounceActive = false;
                    StopMovement();
                }
                return;
            }

            Vector2 toPlayer = GetPlayerPosition() - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude <= _voidPounceRange * _voidPounceRange && _voidPounceCooldownRemaining <= 0f)
            {
                _voidPounceTargetPosition = GetPlayerPosition();
                _voidPounceTelegraphing = true;
                _stateTimeRemaining = _voidPounceTelegraphDuration;
                StopMovement();
                OnVoidPounceTelegraphed?.Invoke(_voidPounceTargetPosition);
                return;
            }

            SetMovement(toPlayer, _huntSpeed);
        }

        private void CountDownTo(float deltaTime, AlienBossState nextState)
        {
            _stateTimeRemaining -= deltaTime;
            if (_stateTimeRemaining <= 0f)
            {
                TryTransitionTo(nextState);
            }
        }

        private void TriggerEnrage()
        {
            if (!_encounterStarted || IsDefeated || IsEnraged || CurrentState == AlienBossState.CoreReleased)
            {
                return;
            }

            _hasEnraged = true;
            TryTransitionTo(AlienBossState.Enraged);
        }

        private void ResolvePlayer()
        {
            if (_player != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag(OblivionsEdge.Core.GameConstants.TagPlayer);
            if (playerObject != null)
            {
                _player = playerObject.transform;
            }
        }

        private Vector2 GetPlayerPosition()
        {
            ResolvePlayer();
            return _player == null ? (Vector2)transform.position : _player.position;
        }

        private void SetMovement(Vector2 direction, float speed)
        {
            _movementDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.zero;
            _movementSpeed = _movementDirection.sqrMagnitude > 0f ? speed : 0f;
        }

        private void StopMovement()
        {
            _movementDirection = Vector2.zero;
            _movementSpeed = 0f;
        }

        private void CancelVoidPounce()
        {
            _voidPounceTelegraphing = false;
            _voidPounceActive = false;
            _voidPounceTimeRemaining = 0f;
        }
    }
}
