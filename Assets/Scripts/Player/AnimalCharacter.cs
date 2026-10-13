using System;
using System.Collections;
using UnityEngine;
using OblivionsEdge.Core;

namespace OblivionsEdge.Player
{
    /// <summary>
    /// Autonomous and player-controllable combat beast/companion character.
    /// Scaled larger than standard humanoid characters, featuring high-mobility
    /// quadruped movement, ground checks, pounce attacks, and health tracking.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    [AddComponentMenu("OblivionsEdge/Player/Animal Character")]
    public class AnimalCharacter : MonoBehaviour, IDamageable
    {
        #region Inspector Settings

        [Header("Animal Identification & Scale")]
        [SerializeField] private string _characterName = "Fenrir-09 Cyber-Hound";
        [Tooltip("Size multiplier to make the animal visibly larger than normal humanoid characters.")]
        [SerializeField] [Range(1.2f, 2.5f)] private float _bodyScale = 1.65f;

        [Header("Survival & Health")]
        [SerializeField] private float _maxHealth = 250f;
        [SerializeField] private float _currentHealth;

        [Header("Locomotion")]
        [SerializeField] private float _moveSpeed = 8.5f;
        [SerializeField] private float _jumpForce = 13.5f;
        [SerializeField] private float _followDistance = 3.2f;
        [SerializeField] private float _stopDistance = 1.8f;
        [SerializeField] private LayerMask _groundLayerMask;

        [Header("Combat & Pounce")]
        [SerializeField] private float _attackRadius = 6.5f;
        [SerializeField] private float _pounceForce = 16f;
        [SerializeField] private float _pounceDamage = 40f;
        [SerializeField] private float _attackCooldown = 1.4f;

        [Header("Target & Control")]
        [SerializeField] private Transform _targetPlayer;
        [SerializeField] private bool _isDirectlyControlled = false;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _flashColor = new Color(1f, 0.3f, 0.3f, 1f);

        #endregion

        #region Runtime State

        private Rigidbody2D _rb;
        private Collider2D _mainCollider;
        private bool _isGrounded;
        private float _lastAttackTime;
        private bool _isPouncing;
        private bool _isAlive = true;
        private Transform _currentTargetEnemy;
        private Color _originalColor = Color.white;

        #endregion

        #region IDamageable Properties & Events

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsAlive => _isAlive;

        public event Action<DamageInfo> OnDamageTaken;
        public event Action<float, float> OnHealthChanged;
        public event Action<DamageInfo> OnDied;

        #endregion

        public string CharacterName => _characterName;
        public bool IsDirectlyControlled => _isDirectlyControlled;
        public Transform TargetPlayer { get => _targetPlayer; set => _targetPlayer = value; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _currentHealth = _maxHealth;

            // Apply enlarged body scale
            transform.localScale = new Vector3(_bodyScale, _bodyScale, 1f);

            // Configure physics
            _rb.mass = 2.5f;
            _rb.gravityScale = 3f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Ensure collider
            _mainCollider = GetComponent<Collider2D>();
            if (_mainCollider == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.8f, 1.1f);
                box.offset = new Vector2(0f, 0.05f);
                _mainCollider = box;
            }

            // Ensure sprite renderer
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (_spriteRenderer == null)
                {
                    var vis = new GameObject("Visuals");
                    vis.transform.SetParent(transform, false);
                    _spriteRenderer = vis.AddComponent<SpriteRenderer>();
                }
            }

            if (_spriteRenderer != null)
            {
                _originalColor = _spriteRenderer.color;
                _spriteRenderer.sortingLayerName = "Characters";
                _spriteRenderer.sortingOrder = 3;
            }

            // Ground layer fallback
            if (_groundLayerMask.value == 0)
            {
                _groundLayerMask = 1 << GameConstants.LayerEnvironmentIndex;
            }
        }

        private void Start()
        {
            if (_targetPlayer == null)
            {
                GameObject player = GameObject.FindWithTag(GameConstants.TagPlayer);
                if (player != null)
                {
                    _targetPlayer = player.transform;
                }
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private void Update()
        {
            if (!_isAlive) return;

            CheckGrounded();

            // Toggle direct control with 'T' key
            if (Input.GetKeyDown(KeyCode.T))
            {
                _isDirectlyControlled = !_isDirectlyControlled;
                Debug.Log($"[AnimalCharacter] Direct control mode toggled: {_isDirectlyControlled}");
            }

            if (_isDirectlyControlled)
            {
                HandleManualControl();
            }
            else
            {
                HandleAutonomousCompanionAI();
            }

            UpdateFacingDirection();
        }

        private void CheckGrounded()
        {
            Vector2 checkPos = (Vector2)transform.position + Vector2.down * 0.6f;
            _isGrounded = Physics2D.OverlapBox(checkPos, new Vector2(1.4f, 0.3f), 0f, _groundLayerMask) != null;
        }

        #region Locomotion & AI

        private void HandleManualControl()
        {
            float horizontal = Input.GetAxisRaw(GameConstants.InputHorizontal);
            Vector2 vel = _rb.linearVelocity;
            vel.x = horizontal * _moveSpeed;

            if (Input.GetButtonDown("Jump") && _isGrounded)
            {
                vel.y = _jumpForce;
            }

            // Pounce attack with Fire1
            if (Input.GetButtonDown(GameConstants.InputFirePrimary) && Time.time >= _lastAttackTime + _attackCooldown)
            {
                Vector2 aimDir = _spriteRenderer != null && _spriteRenderer.flipX ? Vector2.left : Vector2.right;
                TriggerPounce(aimDir);
            }

            _rb.linearVelocity = vel;
        }

        private void HandleAutonomousCompanionAI()
        {
            ScanForHostileTargets();

            // Combat engagement takes priority
            if (_currentTargetEnemy != null && _currentTargetEnemy.gameObject.activeInHierarchy)
            {
                float distToEnemy = Vector2.Distance(transform.position, _currentTargetEnemy.position);
                Vector2 dirToEnemy = (_currentTargetEnemy.position - transform.position).normalized;

                if (distToEnemy <= _attackRadius && Time.time >= _lastAttackTime + _attackCooldown)
                {
                    TriggerPounce(dirToEnemy);
                    return;
                }

                if (!_isPouncing)
                {
                    MoveTowards(_currentTargetEnemy.position.x, 1.2f);
                }
                return;
            }

            // Follow player companion behavior
            if (_targetPlayer != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, _targetPlayer.position);
                if (distToPlayer > _followDistance && !_isPouncing)
                {
                    MoveTowards(_targetPlayer.position.x, _stopDistance);
                }
                else if (!_isPouncing && _isGrounded)
                {
                    // Gentle deceleration when near player
                    Vector2 v = _rb.linearVelocity;
                    v.x = Mathf.MoveTowards(v.x, 0f, 25f * Time.deltaTime);
                    _rb.linearVelocity = v;
                }
            }
        }

        private void MoveTowards(float targetX, float threshold)
        {
            float deltaX = targetX - transform.position.x;
            if (Mathf.Abs(deltaX) > threshold)
            {
                float moveDir = Mathf.Sign(deltaX);
                Vector2 vel = _rb.linearVelocity;
                vel.x = moveDir * _moveSpeed;

                // Jump over small obstacles or ledges
                if (_isGrounded && NeedsJump(moveDir))
                {
                    vel.y = _jumpForce;
                }

                _rb.linearVelocity = vel;
            }
        }

        private bool NeedsJump(float direction)
        {
            // Raycast forward to check for obstruction
            Vector2 origin = (Vector2)transform.position + new Vector2(direction * 0.8f, -0.2f);
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, 1.2f, _groundLayerMask);
            return hit.collider != null;
        }

        private void ScanForHostileTargets()
        {
            // Scan for enemies or boss
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, _attackRadius * 1.5f);
            float nearestDist = float.MaxValue;
            Transform bestTarget = null;

            foreach (var col in hits)
            {
                if (col.CompareTag(GameConstants.TagEnemy) || col.CompareTag(GameConstants.TagBoss))
                {
                    float d = Vector2.Distance(transform.position, col.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        bestTarget = col.transform;
                    }
                }
            }

            _currentTargetEnemy = bestTarget;
        }

        private void TriggerPounce(Vector2 direction)
        {
            _lastAttackTime = Time.time;
            _isPouncing = true;

            Vector2 impulse = new Vector2(direction.x * _pounceForce, Mathf.Max(direction.y * _pounceForce, 5f));
            _rb.linearVelocity = impulse;

            StartCoroutine(PounceCooldownRoutine());
            Debug.Log($"<color=#00e5ff>[AnimalCharacter]</color> {_characterName} performed a combat pounce!");
        }

        private IEnumerator PounceCooldownRoutine()
        {
            yield return new WaitForSeconds(0.45f);
            _isPouncing = false;
        }

        private void UpdateFacingDirection()
        {
            if (_spriteRenderer == null) return;

            if (Mathf.Abs(_rb.linearVelocity.x) > 0.15f)
            {
                _spriteRenderer.flipX = _rb.linearVelocity.x < 0;
            }
        }

        #endregion

        #region Collision & Damage

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_isPouncing)
            {
                var damageable = collision.gameObject.GetComponent<IDamageable>();
                if (damageable != null && (collision.gameObject.CompareTag(GameConstants.TagEnemy) || collision.gameObject.CompareTag(GameConstants.TagBoss)))
                {
                    DamageInfo payload = new DamageInfo(
                        _pounceDamage,
                        DamageType.Physical,
                        gameObject,
                        collision.GetContact(0).point,
                        _rb.linearVelocity.normalized,
                        8f,
                        true
                    );

                    damageable.TakeDamage(payload);
                    Debug.Log($"[AnimalCharacter] Pounce impact struck {collision.gameObject.name} for {_pounceDamage} DMG!");
                }
            }
        }

        public void TakeDamage(DamageInfo damageInfo)
        {
            if (!_isAlive) return;

            _currentHealth = Mathf.Max(0f, _currentHealth - damageInfo.Amount);
            OnDamageTaken?.Invoke(damageInfo);
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            StartCoroutine(DamageFlashRoutine());

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

        private void Die(DamageInfo fatalInfo)
        {
            _isAlive = false;
            OnDied?.Invoke(fatalInfo);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            }

            Debug.Log($"[AnimalCharacter] {_characterName} has fallen in combat.");
        }

        private IEnumerator DamageFlashRoutine()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _flashColor;
                yield return new WaitForSeconds(0.12f);
                _spriteRenderer.color = _originalColor;
            }
        }

        #endregion
    }
}
