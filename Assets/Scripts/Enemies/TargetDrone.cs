using System;
using System.Collections;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Enemies
{
    /// <summary>
    /// Interactive bio-synthetic training drone implementing IDamageable.
    /// Provides immediate combat test feedback for pulse pistols, railguns, and melee weapons.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class TargetDrone : MonoBehaviour, IDamageable
    {
        [Header("Drone Vitals")]
        [SerializeField] private float _maxHealth = 60f;
        [SerializeField] private float _currentHealth = 60f;
        [SerializeField] private float _respawnTime = 3.5f;

        [Header("Floating Motion")]
        [SerializeField] private float _floatFrequency = 2.5f;
        [SerializeField] private float _floatAmplitude = 0.25f;

        private Vector3 _initialPosition;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _col;
        private Rigidbody2D _rb;
        private Color _originalColor;
        private Coroutine _flashRoutine;
        private bool _isAlive = true;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _isAlive;

        public event Action<DamageInfo> OnDamageTaken;
        public event Action<float, float> OnHealthChanged;
        public event Action<DamageInfo> OnDied;

        private void Awake()
        {
            _initialPosition = transform.position;
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            _col = GetComponent<Collider2D>();
            _rb = GetComponent<Rigidbody2D>();

            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
            }

            _currentHealth = _maxHealth;
            _isAlive = true;
        }

        private void Update()
        {
            if (!_isAlive) return;

            // Subtle hovering oscillation
            float newY = _initialPosition.y + Mathf.Sin(Time.time * _floatFrequency) * _floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!_isAlive) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damageInfo.Amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            OnDamageTaken?.Invoke(damageInfo);

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashImpactRoutine());

            if (_rb != null && damageInfo.KnockbackForce > 0f)
            {
                _rb.linearVelocity = Vector2.zero;
                _rb.AddForce(damageInfo.HitDirection.normalized * damageInfo.KnockbackForce, ForceMode2D.Impulse);
            }

            if (_currentHealth <= 0f)
            {
                Die(damageInfo);
            }
        }

        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0f) return;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void Die(DamageInfo damageInfo)
        {
            _isAlive = false;
            OnDied?.Invoke(damageInfo);

            if (_col != null) _col.enabled = false;
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;

            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator FlashImpactRoutine()
        {
            if (_spriteRenderer == null) yield break;

            _spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(0.06f);
            _spriteRenderer.color = new Color(1f, 0.3f, 0.3f, 1f);
            yield return new WaitForSeconds(0.08f);
            _spriteRenderer.color = _originalColor;
        }

        private IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_respawnTime);

            transform.position = _initialPosition;
            _currentHealth = _maxHealth;
            _isAlive = true;

            if (_col != null) _col.enabled = true;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
                _spriteRenderer.color = _originalColor;
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }
    }
}
