using System;
using UnityEngine;
using UnityEngine.Events;
using OblivionsEdge.Core;

namespace OblivionsEdge.Weapons
{
    /// <summary>
    /// Mouse-directed pulse pistol implementing the shared weapon contract.
    /// Attach it to the equipped weapon and assign a projectile prefab and muzzle transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunController : MonoBehaviour, IWeapon
    {
        [Header("Weapon")]
        [SerializeField] private string _weaponId = "pulse-pistol";
        [SerializeField] private string _displayName = "Pulse Pistol";
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private ParticleSystem _hitFeedback;
        [SerializeField] private Camera _aimCamera;

        [Header("Combat")]
        [SerializeField, Min(0.1f)] private float _projectileSpeed = 24f;
        [SerializeField, Min(0f)] private float _damage = 12f;
        [SerializeField, Min(0.01f)] private float _fireRate = 4f;
        [SerializeField, Min(0f)] private float _knockbackForce;

        [Header("Ammunition")]
        [SerializeField, Min(1)] private int _magazineCapacity = 24;
        [SerializeField, Min(0)] private int _startingMagazineAmmo = 24;
        [SerializeField, Min(0)] private int _startingReserveAmmo = 96;
        [SerializeField, Min(0)] private int _maxReserveAmmo = 240;
        [SerializeField, Min(0f)] private float _reloadDuration = 1.25f;

        [Header("Input")]
        [SerializeField] private bool _readMouseInput = true;
        [SerializeField] private KeyCode _reloadKey = KeyCode.R;

        [Header("Hit Feedback")]
        [SerializeField] private UnityEvent _onSuccessfulHit = new UnityEvent();

        private int _currentAmmo;
        private int _reserveAmmo;
        private float _nextFireTime;
        private float _reloadTimeRemaining;
        private bool _isReloading;
        private bool _dryFireReportedForInput;
        private bool _projectilePrefabIsUsable;
        private Transform _cachedOwnerRoot;
        private Collider2D[] _cachedOwnerColliders = Array.Empty<Collider2D>();

        public string WeaponId => _weaponId;
        public string DisplayName => _displayName;
        public WeaponType Type => WeaponType.Pistol;
        public FireMode Mode => FireMode.FullAuto;
        public int CurrentAmmo => _currentAmmo;
        public int MagazineCapacity => _magazineCapacity;
        public int ReserveAmmo => _reserveAmmo;
        public float FireRate => _fireRate;
        public float ReloadDuration => _reloadDuration;
        public bool IsReloading => _isReloading;
        public bool CanFire => !_isReloading && _currentAmmo > 0 && Time.time >= _nextFireTime &&
                               _projectilePrefabIsUsable && _muzzle != null;

        public event Action OnFired;
        public event Action<int, int> OnAmmoChanged;
        public event Action<int> OnReserveAmmoChanged;
        public event Action OnReloadStarted;
        public event Action OnReloadFinished;
        public event Action OnDryFire;
        public event Action<DamageInfo> OnDamageDealt;

        private void Awake()
        {
            ClampInspectorValues();
            _currentAmmo = Mathf.Clamp(_startingMagazineAmmo, 0, _magazineCapacity);
            _reserveAmmo = Mathf.Clamp(_startingReserveAmmo, 0, _maxReserveAmmo);

            if (_aimCamera == null)
            {
                _aimCamera = Camera.main;
            }

            if (_muzzle == null)
            {
                Transform foundMuzzle = transform.Find("Muzzle");
                if (foundMuzzle != null)
                {
                    _muzzle = foundMuzzle;
                }
                else
                {
                    GameObject muzzleObj = new GameObject("Muzzle");
                    muzzleObj.transform.SetParent(transform, false);
                    muzzleObj.transform.localPosition = new Vector3(0.6f, 0f, 0f);
                    _muzzle = muzzleObj.transform;
                }
            }

            if (_projectilePrefab == null)
            {
                _projectilePrefab = GetOrCreateDefaultProjectile();
            }

            _projectilePrefabIsUsable = _projectilePrefab != null &&
                                        _projectilePrefab.GetComponent<Rigidbody2D>() != null &&
                                        _projectilePrefab.GetComponent<Collider2D>() != null;
            if (_projectilePrefab != null && !_projectilePrefabIsUsable)
            {
                Debug.LogError("The projectile prefab requires both a Rigidbody2D and a Collider2D.", this);
            }
        }

        private static Projectile _defaultRuntimeProjectile;

        private static Projectile GetOrCreateDefaultProjectile()
        {
            if (_defaultRuntimeProjectile != null) return _defaultRuntimeProjectile;

            GameObject boltObj = new GameObject("Runtime_Pulse_Bolt");
            boltObj.layer = GameConstants.LayerPlayerProjectileIndex;
            var rb = boltObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = boltObj.AddComponent<CircleCollider2D>();
            col.radius = 0.2f;
            col.isTrigger = true;

            var sr = boltObj.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.2f, 0.9f, 1f, 1f);
            sr.sortingLayerName = "Environment";
            sr.sortingOrder = 10;

            Texture2D tex = new Texture2D(16, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = (x - 7.5f) / 7.5f;
                    float dy = (y - 3.5f) / 3.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= 1f)
                    {
                        float alpha = Mathf.Clamp01(1f - dist * 0.7f);
                        tex.SetPixel(x, y, new Color(0.3f, 0.9f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 8), new Vector2(0.5f, 0.5f), 16f);

            var proj = boltObj.AddComponent<Projectile>();
            boltObj.SetActive(false);
            boltObj.hideFlags = HideFlags.HideAndDontSave;
            _defaultRuntimeProjectile = proj;
            return proj;
        }

        private void Update()
        {
            if (_isReloading)
            {
                _reloadTimeRemaining -= Time.deltaTime;
                if (_reloadTimeRemaining <= 0f)
                {
                    FinishReload();
                }
            }

            if (!_readMouseInput)
            {
                return;
            }

            if (Input.GetKeyDown(_reloadKey))
            {
                StartReload();
            }

            bool fireHeld = Input.GetMouseButton(0);
            if (!fireHeld)
            {
                _dryFireReportedForInput = false;
                return;
            }

            if (_currentAmmo <= 0)
            {
                if (!_isReloading && _reserveAmmo > 0)
                {
                    StartReload();
                }

                if (!_isReloading && !_dryFireReportedForInput)
                {
                    OnDryFire?.Invoke();
                    _dryFireReportedForInput = true;
                }
                return;
            }

            if (_aimCamera == null || _muzzle == null)
            {
                return;
            }

            Vector3 mousePosition = Input.mousePosition;
            mousePosition.z = Mathf.Abs(_aimCamera.transform.position.z - _muzzle.position.z);
            Vector3 mouseWorldPosition = _aimCamera.ScreenToWorldPoint(mousePosition);
            Vector2 origin = _muzzle.position;
            Vector2 direction = (Vector2)mouseWorldPosition - origin;
            TryFire(origin, direction);
        }

        /// <inheritdoc />
        public bool TryFire(Vector2 origin, Vector2 direction)
        {
            if (_currentAmmo <= 0)
            {
                OnDryFire?.Invoke();
                return false;
            }

            if (_isReloading || Time.time < _nextFireTime || !_projectilePrefabIsUsable || _muzzle == null ||
                direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return false;
            }

            Vector2 normalizedDirection = direction.normalized;
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            Projectile projectile = Instantiate(
                _projectilePrefab,
                origin,
                Quaternion.Euler(0f, 0f, angle));
            projectile.gameObject.SetActive(true);

            Transform ownerRoot = GetOwnerRoot();
            Collider2D[] ownerColliders = GetOwnerColliders(ownerRoot);
            projectile.Initialize(
                normalizedDirection,
                _projectileSpeed,
                _damage,
                _knockbackForce,
                ownerRoot,
                ownerColliders,
                this);

            _currentAmmo--;
            _nextFireTime = Time.time + (1f / _fireRate);
            _dryFireReportedForInput = false;
            PlayMuzzleFlash(angle);
            OnAmmoChanged?.Invoke(_currentAmmo, _magazineCapacity);
            OnFired?.Invoke();

            // Automatically reload after the last round; StartReload preserves the manual R path.
            if (_currentAmmo == 0)
            {
                StartReload();
            }

            return true;
        }

        /// <inheritdoc />
        public bool StartReload()
        {
            if (_isReloading || _currentAmmo >= _magazineCapacity || _reserveAmmo <= 0)
            {
                return false;
            }

            _isReloading = true;
            _reloadTimeRemaining = _reloadDuration;
            OnReloadStarted?.Invoke();

            if (_reloadTimeRemaining <= 0f)
            {
                FinishReload();
            }

            return true;
        }

        /// <inheritdoc />
        public void CancelReload()
        {
            _isReloading = false;
            _reloadTimeRemaining = 0f;
        }

        /// <summary>Adds reserve ammunition, clamped to the configured reserve limit.</summary>
        public void AddAmmo(int amount)
        {
            if (amount <= 0 || _reserveAmmo >= _maxReserveAmmo)
            {
                return;
            }

            int previousAmmo = _reserveAmmo;
            _reserveAmmo = (int)Math.Min((long)_maxReserveAmmo, (long)_reserveAmmo + amount);
            if (_reserveAmmo != previousAmmo)
            {
                OnReserveAmmoChanged?.Invoke(_reserveAmmo);
            }
        }

        internal void NotifyDamageDealt(DamageInfo damageInfo)
        {
            if (_hitFeedback != null)
            {
                _hitFeedback.transform.position = damageInfo.HitPoint;
                _hitFeedback.transform.rotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Atan2(damageInfo.HitDirection.y, damageInfo.HitDirection.x) * Mathf.Rad2Deg);
                _hitFeedback.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _hitFeedback.Play(true);
            }

            OnDamageDealt?.Invoke(damageInfo);
            _onSuccessfulHit?.Invoke();
        }

        private void FinishReload()
        {
            int ammoNeeded = _magazineCapacity - _currentAmmo;
            int ammoLoaded = Mathf.Min(ammoNeeded, _reserveAmmo);
            _currentAmmo += ammoLoaded;
            _reserveAmmo -= ammoLoaded;
            _isReloading = false;
            _reloadTimeRemaining = 0f;

            OnAmmoChanged?.Invoke(_currentAmmo, _magazineCapacity);
            OnReserveAmmoChanged?.Invoke(_reserveAmmo);
            OnReloadFinished?.Invoke();
        }

        private void PlayMuzzleFlash(float angle)
        {
            if (_muzzleFlash == null)
            {
                return;
            }

            _muzzleFlash.transform.position = _muzzle.position;
            _muzzleFlash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _muzzleFlash.Play(true);
        }

        private Transform GetOwnerRoot()
        {
            return transform.root;
        }

        private Collider2D[] GetOwnerColliders(Transform ownerRoot)
        {
            if (_cachedOwnerRoot != ownerRoot)
            {
                _cachedOwnerRoot = ownerRoot;
                _cachedOwnerColliders = ownerRoot.GetComponentsInChildren<Collider2D>(true);
            }

            return _cachedOwnerColliders;
        }

        private void OnValidate()
        {
            ClampInspectorValues();
        }

        private void ClampInspectorValues()
        {
            _magazineCapacity = Mathf.Max(1, _magazineCapacity);
            _startingMagazineAmmo = Mathf.Clamp(_startingMagazineAmmo, 0, _magazineCapacity);
            _maxReserveAmmo = Mathf.Max(0, _maxReserveAmmo);
            _startingReserveAmmo = Mathf.Clamp(_startingReserveAmmo, 0, _maxReserveAmmo);
            _projectileSpeed = Mathf.Max(0.1f, _projectileSpeed);
            _damage = Mathf.Max(0f, _damage);
            _fireRate = Mathf.Max(0.01f, _fireRate);
            _knockbackForce = Mathf.Max(0f, _knockbackForce);
            _reloadDuration = Mathf.Max(0f, _reloadDuration);
        }
    }
}
