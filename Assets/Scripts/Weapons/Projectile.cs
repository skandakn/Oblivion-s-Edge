using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Weapons
{
    /// <summary>
    /// Rigidbody2D pulse projectile that damages any IDamageable component on the hit collider
    /// or one of its parents, then removes itself on impact or after its lifetime expires.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float _lifetimeSeconds = 3f;
        [SerializeField] private LayerMask _collisionLayers;
        [SerializeField] private ParticleSystem _impactParticlesPrefab;

        private Rigidbody2D _body;
        private Collider2D _projectileCollider;
        private Transform _ownerRoot;
        private Collider2D[] _ownerColliders;
        private GunController _sourceGun;
        private Vector2 _direction;
        private float _damage;
        private float _knockbackForce;
        private bool _initialized;
        private bool _consumed;
        private int _effectiveCollisionMask;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _projectileCollider = GetComponent<Collider2D>();
            _effectiveCollisionMask = _collisionLayers.value;
            if (_effectiveCollisionMask == 0)
            {
                _effectiveCollisionMask = (1 << GameConstants.LayerEnemyIndex) |
                                          (1 << GameConstants.LayerEnvironmentIndex);
            }

            if (_body == null || _projectileCollider == null)
            {
                Debug.LogError("A projectile requires a Rigidbody2D and a Collider2D.", this);
                enabled = false;
            }
        }

        private void Reset()
        {
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            Collider2D projectileCollider = GetComponent<Collider2D>();
            if (projectileCollider != null)
            {
                projectileCollider.isTrigger = true;
            }
        }

        /// <summary>Initializes movement, damage, source attribution, and owner collision filtering.</summary>
        public void Initialize(
            Vector2 direction,
            float speed,
            float damage,
            float knockbackForce,
            Transform ownerRoot,
            Collider2D[] ownerColliders,
            GunController sourceGun = null)
        {
            if (_body == null || _projectileCollider == null)
            {
                return;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                Destroy(gameObject);
                return;
            }

            _direction = direction.normalized;
            _damage = Mathf.Max(0f, damage);
            _knockbackForce = Mathf.Max(0f, knockbackForce);
            _ownerRoot = ownerRoot;
            _ownerColliders = ownerColliders;
            _sourceGun = sourceGun;
            _initialized = true;

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.velocity = _direction * Mathf.Max(0.1f, speed);
            _body.angularVelocity = 0f;
            _body.rotation = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

            if (LayerMask.LayerToName(GameConstants.LayerPlayerProjectileIndex) == GameConstants.LayerNamePlayerProjectile)
            {
                gameObject.layer = GameConstants.LayerPlayerProjectileIndex;
            }

            if (_ownerColliders != null)
            {
                for (int i = 0; i < _ownerColliders.Length; i++)
                {
                    Collider2D ownerCollider = _ownerColliders[i];
                    if (ownerCollider != null && ownerCollider != _projectileCollider)
                    {
                        Physics2D.IgnoreCollision(_projectileCollider, ownerCollider, true);
                    }
                }
            }

            Destroy(gameObject, _lifetimeSeconds);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _consumed || ShouldIgnore(other, out IDamageable damageable))
            {
                return;
            }

            ResolveHit(damageable, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_initialized || _consumed || collision == null)
            {
                return;
            }

            Collider2D other = collision.collider;
            if (ShouldIgnore(other, out IDamageable damageable))
            {
                return;
            }

            Vector2 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : other.ClosestPoint(transform.position);
            ResolveHit(damageable, hitPoint);
        }

        private bool ShouldIgnore(Collider2D other, out IDamageable damageable)
        {
            damageable = null;
            if (other == null || other == _projectileCollider)
            {
                return true;
            }

            Transform hitTransform = other.transform;
            if (_ownerRoot != null && (hitTransform == _ownerRoot || hitTransform.IsChildOf(_ownerRoot)))
            {
                return true;
            }

            int layer = other.gameObject.layer;
            if (layer == GameConstants.LayerPlayerIndex || layer == GameConstants.LayerPlayerProjectileIndex ||
                layer == GameConstants.LayerEnemyProjectileIndex || layer == GameConstants.LayerItemPickupIndex)
            {
                return true;
            }

            damageable = other.GetComponentInParent<IDamageable>();
            bool layerIsAllowed = (_effectiveCollisionMask & (1 << layer)) != 0;
            if (!layerIsAllowed && damageable == null)
            {
                return true;
            }

            if (other.isTrigger && damageable == null)
            {
                return true;
            }

            return false;
        }

        private void ResolveHit(IDamageable damageable, Vector2 hitPoint)
        {
            _consumed = true;
            if (damageable != null && damageable.IsAlive)
            {
                GameObject source = _ownerRoot != null ? _ownerRoot.gameObject : null;
                DamageInfo damageInfo = new DamageInfo(
                    _damage,
                    DamageType.Energy,
                    source,
                    hitPoint,
                    _direction,
                    _knockbackForce);
                damageable.TakeDamage(damageInfo);
                _sourceGun?.NotifyDamageDealt(damageInfo);
            }

            PlayImpactParticles(hitPoint);
            Destroy(gameObject);
        }

        private void PlayImpactParticles(Vector2 hitPoint)
        {
            if (_impactParticlesPrefab == null)
            {
                return;
            }

            ParticleSystem particles = Instantiate(
                _impactParticlesPrefab,
                hitPoint,
                Quaternion.identity);
            ParticleSystem.MainModule main = particles.main;
            float lifetime = main.duration + main.startLifetime.constantMax;
            particles.Play(true);
            Destroy(particles.gameObject, Mathf.Max(0.1f, lifetime));
        }

        private void OnValidate()
        {
            _lifetimeSeconds = Mathf.Max(0.1f, _lifetimeSeconds);
        }
    }
}
