using System;
using System.Collections;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Player
{
    /// <summary>
    /// Player vital health and damage resolution system for OBLIVION'S EDGE.
    /// Manages hit points, damage mitigation, knockback integration,
    /// temporary invulnerability (i-frames) with visual feedback, and death sequence events.
    /// Implements <see cref="IDamageable"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Player/Player Health")]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        #region Inspector Configuration

        [Header("Health Settings")]
        [Tooltip("Maximum health capacity for the player.")]
        [SerializeField] [Range(10f, 500f)] private float _maxHealth = 100f;

        [Tooltip("Initial health when spawned.")]
        [SerializeField] [Range(10f, 500f)] private float _startingHealth = 100f;

        [Header("Invulnerability (i-Frames)")]
        [Tooltip("Duration of temporary invulnerability triggered after taking damage.")]
        [SerializeField] [Range(0.1f, 3f)] private float _invulnerabilityDuration = 1.0f;

        [Tooltip("Whether taking damage automatically triggers temporary invulnerability.")]
        [SerializeField] private bool _invulnerableOnHit = true;

        [Tooltip("Rate at which the sprite flashes during invulnerability in seconds.")]
        [SerializeField] [Range(0.02f, 0.3f)] private float _flashInterval = 0.08f;

        [Tooltip("Minimum alpha value during invulnerability flash.")]
        [SerializeField] [Range(0.1f, 0.8f)] private float _flashMinAlpha = 0.25f;

        [Header("Feedback & Audio")]
        [Tooltip("SpriteRenderer used for invulnerability flashing feedback.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Tooltip("Animator for Hurt and Die trigger hooks.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Audio source for audio feedback.")]
        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Sound clip played upon receiving damage.")]
        [SerializeField] private AudioClip _hurtSfx;

        [Tooltip("Sound clip played upon player death.")]
        [SerializeField] private AudioClip _deathSfx;

        [Tooltip("Sound clip played when healed.")]
        [SerializeField] private AudioClip _healSfx;

        #endregion

        #region Internal State

        private float _currentHealth;
        private bool _isAlive = true;
        private bool _isInvulnerable;
        private float _invulnerabilityTimer;

        private Rigidbody2D _rb;
        private Coroutine _flashCoroutine;
        private Color _originalSpriteColor = Color.white;

        // Cached Animator hashes
        private static readonly int AnimHashHurt = Animator.StringToHash("Hurt");
        private static readonly int AnimHashDie = Animator.StringToHash("Die");
        private static readonly int AnimHashIsDead = Animator.StringToHash("IsDead");

        #endregion

        #region Properties (IDamageable)

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _isAlive;
        public bool IsInvulnerable => _isInvulnerable;
        public float InvulnerabilityDuration => _invulnerabilityDuration;
        public float InvulnerabilityTimerRemaining => Mathf.Max(0f, _invulnerabilityTimer);

        #endregion

        #region Events (IDamageable & Extended)

        public event Action<DamageInfo> OnDamageTaken;
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<DamageInfo> OnDied;
        public event Action OnDeath;
        public event Action<float> OnHealed;
        public event Action<bool> OnInvulnerabilityChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (_spriteRenderer != null)
            {
                _originalSpriteColor = _spriteRenderer.color;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }

            _currentHealth = Mathf.Clamp(_startingHealth, 1f, _maxHealth);
            _isAlive = true;
        }

        private void Start()
        {
            // Initial telemetry broadcast
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void Update()
        {
            if (_isInvulnerable)
            {
                _invulnerabilityTimer -= Time.deltaTime;
                if (_invulnerabilityTimer <= 0f)
                {
                    EndInvulnerability();
                }
            }
        }

        #endregion

        #region Damage & Healing Pipeline (IDamageable)

        /// <summary>
        /// Processes an incoming damage payload against current player status.
        /// Ignores damage if player is deceased or currently invulnerable.
        /// </summary>
        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!_isAlive || _isInvulnerable || damageInfo.Amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damageInfo.Amount);

            // Apply directional knockback impulse if present
            if (damageInfo.KnockbackForce > 0f && _rb != null)
            {
                Vector2 knockbackDir = damageInfo.HitDirection;
                if (knockbackDir.sqrMagnitude < 0.001f)
                {
                    knockbackDir = (transform.position.x - damageInfo.HitPoint.x >= 0f)
                        ? Vector2.right
                        : Vector2.left;
                }
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _rb.AddForce(knockbackDir.normalized * damageInfo.KnockbackForce, ForceMode2D.Impulse);
            }

            // Audio & Animation feedback
            if (_audioSource != null && _hurtSfx != null)
            {
                _audioSource.PlayOneShot(_hurtSfx);
            }
            SafeSetTrigger(AnimHashHurt);

            // Broadcast damage telemetry
            OnDamageTaken?.Invoke(damageInfo);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            // Trigger post-damage temporary invulnerability
            if (_invulnerableOnHit && _currentHealth > 0f)
            {
                TriggerInvulnerability(_invulnerabilityDuration);
            }

            // Check lethal threshold
            if (_currentHealth <= 0f)
            {
                HandleDeath(damageInfo);
            }
        }

        /// <summary>
        /// Restores player health up to MaxHealth.
        /// </summary>
        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0f)
            {
                return;
            }

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            float actualHealed = _currentHealth - previousHealth;

            if (actualHealed > 0f)
            {
                if (_audioSource != null && _healSfx != null)
                {
                    _audioSource.PlayOneShot(_healSfx);
                }

                OnHealed?.Invoke(actualHealed);
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            }
        }

        #endregion

        #region Invulnerability

        /// <summary>
        /// Manually grants temporary invulnerability for the specified duration (e.g. for dash/dodge i-frames).
        /// </summary>
        public void TriggerInvulnerability(float duration)
        {
            if (!_isAlive || duration <= 0f) return;

            _invulnerabilityTimer = Mathf.Max(_invulnerabilityTimer, duration);

            if (!_isInvulnerable)
            {
                _isInvulnerable = true;
                OnInvulnerabilityChanged?.Invoke(true);

                if (_spriteRenderer != null)
                {
                    if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                    _flashCoroutine = StartCoroutine(InvulnerabilityFlashRoutine());
                }
            }
        }

        public void SetInvulnerable(bool invulnerable)
        {
            if (_isInvulnerable == invulnerable) return;

            _isInvulnerable = invulnerable;
            _invulnerabilityTimer = invulnerable ? float.MaxValue : 0f;

            if (!invulnerable)
            {
                EndInvulnerability();
            }
            else
            {
                OnInvulnerabilityChanged?.Invoke(true);
            }
        }

        private void EndInvulnerability()
        {
            _isInvulnerable = false;
            _invulnerabilityTimer = 0f;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalSpriteColor;
            }

            OnInvulnerabilityChanged?.Invoke(false);
        }

        private IEnumerator InvulnerabilityFlashRoutine()
        {
            bool isFaded = false;
            while (_isInvulnerable)
            {
                if (_spriteRenderer != null)
                {
                    Color c = _originalSpriteColor;
                    c.a = isFaded ? 1f : _flashMinAlpha;
                    _spriteRenderer.color = c;
                    isFaded = !isFaded;
                }
                yield return new WaitForSeconds(_flashInterval);
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _originalSpriteColor;
            }
        }

        #endregion

        #region Death Sequence

        private void HandleDeath(DamageInfo fatalDamage)
        {
            if (!_isAlive) return;

            _isAlive = false;
            EndInvulnerability();

            // Disable player locomotion
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.DisableControls();
            }

            // Audio & Animation
            if (_audioSource != null && _deathSfx != null)
            {
                _audioSource.PlayOneShot(_deathSfx);
            }

            SafeSetBool(AnimHashIsDead, true);
            SafeSetTrigger(AnimHashDie);

            // Broadcast death events
            OnDied?.Invoke(fatalDamage);
            OnDeath?.Invoke();
        }

        /// <summary>
        /// Revives or resets player health to starting value.
        /// </summary>
        public void ResetHealth()
        {
            _isAlive = true;
            _currentHealth = _maxHealth;
            EndInvulnerability();

            SafeSetBool(AnimHashIsDead, false);

            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.EnableControls();
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        #endregion

        #region Animation Helper

        private void SafeSetTrigger(int hash)
        {
            if (_animator != null && _animator.isInitialized)
            {
                try { _animator.SetTrigger(hash); } catch { /* graceful fallback */ }
            }
        }

        private void SafeSetBool(int hash, bool value)
        {
            if (_animator != null && _animator.isInitialized)
            {
                try { _animator.SetBool(hash, value); } catch { /* graceful fallback */ }
            }
        }

        #endregion
    }
}
