using System;
using UnityEngine;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;

namespace OblivionsEdge.DeathStorm
{
    // ─────────────────────────────────────────────────────────────────────────
    //  AxeCombat.cs — DeathStorm melee strike logic
    //  Owner: Member 10
    //
    //  Responsibilities:
    //    · Perform a directional axe attack toward a configurable weak-point.
    //    · Enforce attack cooldown.
    //    · Cast an overlap hitbox in the swing arc; check weak-point area hit.
    //    · Require the boss to be in VulnerableStun phase AND the player to own
    //      the DeathStorm weapon before the finishing strike is valid.
    //    · Trigger IBossController.TransitionToPhase(Defeated) only on a valid
    //      DeathStorm strike against the correct weak-point.
    //    · Implements IWeapon so the existing IWeaponHandler (Member 2) can
    //      equip and fire it through the standard pipeline.
    //
    //  Does NOT duplicate any inventory, boss AI, or UI system.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Directional melee-axe behaviour for the DeathStorm legendary weapon.
    /// Attach to the DeathStorm weapon prefab.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AxeCombat : MonoBehaviour, Weapons.IWeapon
    {
        // ── Inspector ─────────────────────────────────────────────────────
        [Header("Weapon Identity")]
        [SerializeField] private string _weaponId      = "deathstorm_axe";
        [SerializeField] private string _displayName   = "DeathStorm";

        [Header("Combat")]
        [Tooltip("Cooldown in seconds between attacks.")]
        [SerializeField] private float _attackCooldown = 1.2f;

        [Tooltip("Radius of the overlap-circle hitbox for each swing.")]
        [SerializeField] private float _hitboxRadius   = 1.5f;

        [Tooltip("LayerMask that the swing hitbox queries (Boss layer).")]
        [SerializeField] private LayerMask _hitLayers;

        [Tooltip("Angular tolerance (degrees) within which a direction is 'correct'.")]
        [SerializeField] private float _directionTolerance = 45f;

        [Header("Weak-Point")]
        [Tooltip("World-space offset from the boss's transform that defines the weak-point zone.")]
        [SerializeField] private Vector2 _weakPointOffset = new Vector2(0f, 1f);

        [Tooltip("Radius of the weak-point zone (world units).")]
        [SerializeField] private float _weakPointRadius = 0.8f;

        [Header("Dependencies")]
        [Tooltip("Member 3's IBossController implementation.")]
        [SerializeField] private MonoBehaviour _bossControllerMono;

        [Tooltip("The DeathStormCrafting component — used to check HasDeathStorm.")]
        [SerializeField] private DeathStormCrafting _craftingController;

        // ── IWeapon ────────────────────────────────────────────────────────
        public string            WeaponId         => _weaponId;
        public string            DisplayName      => _displayName;
        public Weapons.WeaponType Type            => Weapons.WeaponType.MeleeTool;
        public Weapons.FireMode   Mode            => Weapons.FireMode.SemiAuto;
        public int                CurrentAmmo     => 1;           // Melee: always "loaded"
        public int                MagazineCapacity => 1;
        public float              FireRate        => 1f / _attackCooldown;
        public float              ReloadDuration  => 0f;
        public bool               CanFire         => !_onCooldown;
        public bool               IsReloading     => false;

        public event Action             OnFired;
        public event Action<int, int>   OnAmmoChanged;  // unused for melee; satisfies interface
        public event Action             OnReloadStarted;
        public event Action             OnReloadFinished;
        public event Action             OnDryFire;

        // ── Extra events (consumed by game systems, not part of IWeapon) ───
        /// <summary>Fires when a valid DeathStorm finishing strike defeats the boss.</summary>
        public event Action OnDeathStormStrike;
        /// <summary>Fires when the swing hits the wrong direction or misses the weak-point.</summary>
        public event Action OnInvalidStrike;

        // ── Runtime ────────────────────────────────────────────────────────
        private IBossController _boss;
        private bool            _onCooldown;
        private float           _cooldownTimer;

        // ── Unity lifecycle ────────────────────────────────────────────────
        private void Awake()
        {
            _boss = _bossControllerMono as IBossController;

            if (_boss == null)
                Debug.LogWarning("[AxeCombat] BossController not assigned or does not implement IBossController.");
            if (_craftingController == null)
                Debug.LogWarning("[AxeCombat] DeathStormCrafting reference not set.");
        }

        private void Update()
        {
            if (!_onCooldown) return;

            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _onCooldown    = false;
                _cooldownTimer = 0f;
            }
        }

        // ── IWeapon.TryFire ────────────────────────────────────────────────

        /// <summary>
        /// Execute the directional axe swing.
        /// </summary>
        /// <param name="origin">Attack origin (player position in world space).</param>
        /// <param name="direction">Normalised 2D vector toward the intended target.</param>
        /// <returns>True if the swing connected with a valid weak-point strike.</returns>
        public bool TryFire(Vector2 origin, Vector2 direction)
        {
            if (_onCooldown)
            {
                OnDryFire?.Invoke();
                return false;
            }

            BeginCooldown();
            OnFired?.Invoke();

            // ── 1. Detect hits within the swing hitbox ──────────────────
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                origin + direction * (_hitboxRadius * 0.5f),
                _hitboxRadius,
                _hitLayers
            );

            bool bossHit = false;
            foreach (Collider2D col in hits)
            {
                if (col.CompareTag(GameConstants.TagBoss))
                {
                    bossHit = true;
                    break;
                }
            }

            if (!bossHit) return false;

            // ── 2. Validate attack preconditions ───────────────────────
            if (!IsBossExposed())
            {
                Debug.Log("[AxeCombat] Boss is not in VulnerableStun — strike ignored.");
                OnInvalidStrike?.Invoke();
                return false;
            }

            if (!HasDeathStormWeapon())
            {
                Debug.Log("[AxeCombat] Player does not have the DeathStorm weapon — strike ignored.");
                OnInvalidStrike?.Invoke();
                return false;
            }

            // ── 3. Calculate direction to the weak-point ──────────────
            Vector2 weakPointWorld = GetWeakPointWorld();
            Vector2 toWeakPoint    = (weakPointWorld - origin).normalized;

            float angleDelta = Vector2.Angle(direction, toWeakPoint);
            if (angleDelta > _directionTolerance)
            {
                Debug.Log($"[AxeCombat] Wrong direction — angle delta {angleDelta:F1}° > tolerance {_directionTolerance}°.");
                OnInvalidStrike?.Invoke();
                return false;
            }

            // ── 4. Confirm hit lands within the weak-point zone ────────
            float distToWeak = Vector2.Distance(origin + direction * _hitboxRadius, weakPointWorld);
            if (distToWeak > _weakPointRadius)
            {
                Debug.Log($"[AxeCombat] Missed weak-point zone — distance {distToWeak:F2} > radius {_weakPointRadius}.");
                OnInvalidStrike?.Invoke();
                return false;
            }

            // ── 5. Valid DeathStorm finishing strike! ──────────────────
            Debug.Log("[AxeCombat] ★ Valid DeathStorm strike — boss permanently defeated!");
            OnDeathStormStrike?.Invoke();
            _boss?.TransitionToPhase(BossPhase.Defeated);
            return true;
        }

        // IWeapon stubs — melee weapon has no reload
        public bool StartReload()  => false;
        public void CancelReload() { }
        public void AddAmmo(int amount) { }

        // ── Private helpers ────────────────────────────────────────────────

        private void BeginCooldown()
        {
            _onCooldown    = true;
            _cooldownTimer = _attackCooldown;
        }

        private bool IsBossExposed()
        {
            if (_boss == null) return false;
            return _boss.CurrentPhase == BossPhase.VulnerableStun;
        }

        private bool HasDeathStormWeapon()
        {
            if (_craftingController == null) return false;
            return _craftingController.HasDeathStorm;
        }

        private Vector2 GetWeakPointWorld()
        {
            if (_boss is MonoBehaviour bm && bm != null)
                return (Vector2)bm.transform.position + _weakPointOffset;

            return (Vector2)transform.position + _weakPointOffset;
        }

        // ── Gizmo for editor visualisation ────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            // Hitbox arc
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _hitboxRadius);

            // Weak-point zone (approximate — boss pivot unknown at edit time)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere((Vector2)transform.position + _weakPointOffset, _weakPointRadius);
        }
    }
}
