using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Boss
{
    /// <summary>
    /// Delivers boss attack damage (standard claw, charge impact, or Void Pounce) to the player
    /// upon contact using the agreed IDamageable health interface.
    /// Automatically disables damage delivery when the boss is defeated.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class BossAttackHitbox : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("Base damage dealt to the player on contact.")]
        [SerializeField, Min(1f)] private float _damageAmount = 25f;
        [Tooltip("Damage type passed in the DamageInfo payload.")]
        [SerializeField] private DamageType _damageType = DamageType.Physical;
        [Tooltip("Knockback impulse force applied to the player.")]
        [SerializeField, Min(0f)] private float _knockbackForce = 6f;
        [Tooltip("Minimum time in seconds between hits on the same target.")]
        [SerializeField, Min(0.1f)] private float _hitCooldown = 0.5f;

        [Header("Attack Classification")]
        [Tooltip("If true, this hitbox represents a charge attack and notifies AlienBossAI upon a successful hit.")]
        [SerializeField] private bool _isChargeAttack;
        [Tooltip("If true, this hitbox represents Void Pounce with boosted damage.")]
        [SerializeField] private bool _isVoidPounce;
        [Tooltip("Damage multiplier applied when Void Pounce is active.")]
        [SerializeField, Min(1f)] private float _voidPounceMultiplier = 2f;

        [Header("References")]
        [Tooltip("BossHealth component owning this hitbox. Automatically resolved in parent if null.")]
        [SerializeField] private BossHealth _bossHealth;

        private Collider2D _hitboxCollider;
        private float _lastHitTime = -100f;

        private void Awake()
        {
            _hitboxCollider = GetComponent<Collider2D>();
            if (_bossHealth == null)
            {
                _bossHealth = GetComponentInParent<BossHealth>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHitTarget(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryHitTarget(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null)
            {
                TryHitTarget(collision.collider);
            }
        }

        private void TryHitTarget(Collider2D other)
        {
            if (other == null || !enabled)
            {
                return;
            }

            // Immediately disarm if boss is defeated
            if (_bossHealth != null && !_bossHealth.IsAlive)
            {
                enabled = false;
                if (_hitboxCollider != null)
                {
                    _hitboxCollider.enabled = false;
                }
                return;
            }

            // Cooldown check
            if (Time.time - _lastHitTime < _hitCooldown)
            {
                return;
            }

            // Target check: Player tag or layer
            bool isPlayerTarget = other.CompareTag(GameConstants.TagPlayer) ||
                                 other.gameObject.layer == GameConstants.LayerPlayerIndex;

            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null && !isPlayerTarget)
            {
                return;
            }

            if (damageable != null && damageable.IsAlive)
            {
                float damage = _damageAmount;
                if (_isVoidPounce)
                {
                    damage *= _voidPounceMultiplier;
                }
                else if (_bossHealth != null && _bossHealth.IsEnraged)
                {
                    damage *= 1.5f;
                }

                bool hitDelivered = false;
                if (_bossHealth != null)
                {
                    hitDelivered = _bossHealth.DeliverAttackDamage(other, damage, _damageType, _knockbackForce);
                }
                else
                {
                    // Fallback direct delivery
                    Vector2 hitPoint = other.ClosestPoint(transform.position);
                    Vector2 hitDirection = ((Vector2)other.transform.position - (Vector2)transform.position).normalized;
                    if (hitDirection.sqrMagnitude < 0.001f) hitDirection = Vector2.right;

                    DamageInfo payload = new DamageInfo(damage, _damageType, gameObject, hitPoint, hitDirection, _knockbackForce);
                    damageable.TakeDamage(payload);
                    hitDelivered = true;
                }

                if (hitDelivered)
                {
                    _lastHitTime = Time.time;

                    // If charge attack, report hit to Member 4's AlienBossAI to finish charge without registering a miss
                    if (_isChargeAttack)
                    {
                        SendMessageUpwards("NotifyChargeHit", SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        }
    }
}
