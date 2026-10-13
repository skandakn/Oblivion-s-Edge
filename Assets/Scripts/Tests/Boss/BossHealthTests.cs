using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;

namespace OblivionsEdge.Tests.Boss
{
    /// <summary>
    /// Test suite for Member 5's BossHealth component, testing armor depletion,
    /// exposure window, Core release, DeathStorm final-strike contract, enrage/Void Pounce,
    /// player damage delivery, and defeat states.
    /// </summary>
    [TestFixture]
    public class BossHealthTests
    {
        private GameObject _bossObject;
        private BossHealth _bossHealth;

        [SetUp]
        public void Setup()
        {
            _bossObject = new GameObject("TestAlienBoss");
            _bossObject.tag = GameConstants.TagBoss;
            _bossHealth = _bossObject.AddComponent<BossHealth>();

            // Setup default test values via reflection
            SetField(_bossHealth, "_maxArmor", 100f);
            SetField(_bossHealth, "_startingArmor", 100f);
            SetField(_bossHealth, "_armorRestoreAmount", 100f);
            SetField(_bossHealth, "_maxHealth", 500f);
            SetField(_bossHealth, "_startingHealth", 500f);
            SetField(_bossHealth, "_exposedDuration", 3f);
            SetField(_bossHealth, "_minHealthFromOrdinaryBullets", 1f);
            SetField(_bossHealth, "_releaseCoreOnFirstArmorBreak", true);
            SetField(_bossHealth, "_debugLogging", false);

            InvokeMethod(_bossHealth, "Awake");
            _bossHealth.StartBossEncounter();
        }

        [TearDown]
        public void Teardown()
        {
            if (_bossObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_bossObject);
            }
        }

        // ── TC-01: Armor Depletion from Ordinary Projectiles ──
        [Test]
        public void TC01_OrdinaryBullets_DepleteArmor_AndBroadcastTelemetry()
        {
            float lastCurrentArmor = -1f;
            float lastMaxArmor = -1f;
            _bossHealth.OnArmorChanged += (cur, max) =>
            {
                lastCurrentArmor = cur;
                lastMaxArmor = max;
            };

            DamageInfo bulletDamage = new DamageInfo(40f, DamageType.Energy, null, Vector2.zero, Vector2.right);
            _bossHealth.TakeDamage(bulletDamage);

            Assert.AreEqual(60f, _bossHealth.CurrentArmor, "Armor should be reduced from 100 to 60.");
            Assert.AreEqual(500f, _bossHealth.CurrentHealth, "Health must not be damaged while armor is intact.");
            Assert.AreEqual(60f, lastCurrentArmor, "OnArmorChanged must report updated current armor.");
            Assert.AreEqual(100f, lastMaxArmor, "OnArmorChanged must report max armor.");
            Assert.IsFalse(_bossHealth.IsExposed, "Boss should not be exposed while armor remains.");
        }

        // ── TC-02: Armor Break Triggers EXPOSED State ──
        [Test]
        public void TC02_ArmorBreak_TriggersExposedState_AndVulnerableStunPhase()
        {
            bool armorDepletedFired = false;
            bool armorExposedFired = false;
            _bossHealth.OnArmorDepleted += () => armorDepletedFired = true;
            _bossHealth.OnArmorExposed += () => armorExposedFired = true;

            DamageInfo fatalArmorDamage = new DamageInfo(120f, DamageType.Energy, null, Vector2.zero, Vector2.right);
            _bossHealth.TakeDamage(fatalArmorDamage);

            Assert.AreEqual(0f, _bossHealth.CurrentArmor, "Armor should be 0.");
            Assert.IsTrue(_bossHealth.IsExposed, "Boss must enter IsExposed == true.");
            Assert.AreEqual(BossPhase.VulnerableStun, _bossHealth.CurrentPhase, "Boss phase must transition to VulnerableStun.");
            Assert.IsTrue(armorDepletedFired, "OnArmorDepleted event must fire.");
            Assert.IsTrue(armorExposedFired, "OnArmorExposed event must fire.");
            Assert.Greater(_bossHealth.ExposureTimerRemaining, 0f, "Exposure timer must be active.");
        }

        // ── TC-03: Core Release on First Armor Fracture ──
        [Test]
        public void TC03_CoreRelease_TriggeredOnceOnArmorBreak_AndIsIdempotent()
        {
            int coreReleasedCount = 0;
            _bossHealth.OnCoreReleased += () => coreReleasedCount++;

            Assert.IsFalse(_bossHealth.HasReleasedCore, "Core should not be released at start.");

            // Break armor
            _bossHealth.TakeDamage(new DamageInfo(100f, DamageType.Energy, null, Vector2.zero, Vector2.right));

            Assert.IsTrue(_bossHealth.HasReleasedCore, "HasReleasedCore must be true after armor fracture.");
            Assert.AreEqual(1, coreReleasedCount, "OnCoreReleased must fire exactly once.");

            // Subsequent manual release attempts must return false
            bool secondRelease = _bossHealth.ReleaseCore();
            Assert.IsFalse(secondRelease, "Subsequent ReleaseCore() calls must return false.");
            Assert.AreEqual(1, coreReleasedCount, "OnCoreReleased must not fire a second time.");
        }

        // ── TC-04: Ordinary Bullets Cannot Permanently Kill Boss ──
        [Test]
        public void TC04_OrdinaryBullets_CannotPermanentlyKillBoss()
        {
            // Break armor to expose boss
            _bossHealth.TakeDamage(new DamageInfo(100f, DamageType.Energy, null, Vector2.zero, Vector2.right));
            Assert.IsTrue(_bossHealth.IsExposed);

            // Deal massive ordinary damage (999999)
            DamageInfo massiveDamage = new DamageInfo(999999f, DamageType.Energy, null, Vector2.zero, Vector2.right);
            _bossHealth.TakeDamage(massiveDamage);

            Assert.IsTrue(_bossHealth.IsAlive, "Boss must remain alive; ordinary bullets cannot permanently kill the boss.");
            Assert.AreEqual(1f, _bossHealth.CurrentHealth, "Boss health must clamp to minimum 1 HP from ordinary weapons.");
            Assert.AreNotEqual(BossPhase.Defeated, _bossHealth.CurrentPhase, "Boss must not be in Defeated phase.");
        }

        // ── TC-05: Valid DeathStorm Final Strike Defeats Exposed Boss ──
        [Test]
        public void TC05_ValidDeathStormStrike_PermanentlyDefeatsExposedBoss()
        {
            // Break armor
            _bossHealth.TakeDamage(new DamageInfo(100f, DamageType.Energy, null, Vector2.zero, Vector2.right));
            Assert.AreEqual(BossPhase.VulnerableStun, _bossHealth.CurrentPhase);

            bool bossDefeatedFired = false;
            _bossHealth.OnBossDefeated += () => bossDefeatedFired = true;

            // Execute DeathStorm final strike
            bool strikeResult = _bossHealth.ExecuteDeathStormStrike();

            Assert.IsTrue(strikeResult, "ExecuteDeathStormStrike should return true when boss is exposed.");
            Assert.IsFalse(_bossHealth.IsAlive, "Boss IsAlive must become false.");
            Assert.AreEqual(0f, _bossHealth.CurrentHealth, "Boss health must reach 0.");
            Assert.AreEqual(BossPhase.Defeated, _bossHealth.CurrentPhase, "CurrentPhase must be Defeated.");
            Assert.IsTrue(bossDefeatedFired, "OnBossDefeated event must fire.");
        }

        // ── TC-06: TransitionToPhase(Defeated) Respects Exposure Contract ──
        [Test]
        public void TC06_TransitionToDefeated_RejectedWhenNotExposed()
        {
            // Armor is still intact (100)
            Assert.Greater(_bossHealth.CurrentArmor, 0f);
            Assert.AreNotEqual(BossPhase.VulnerableStun, _bossHealth.CurrentPhase);

            _bossHealth.TransitionToPhase(BossPhase.Defeated);

            Assert.IsTrue(_bossHealth.IsAlive, "Boss must remain alive when defeat transition is called without exposure.");
            Assert.AreNotEqual(BossPhase.Defeated, _bossHealth.CurrentPhase, "Phase must NOT transition to Defeated without exposure.");
        }

        // ── TC-07: Defeated State Disables Movement and Attacks ──
        [Test]
        public void TC07_DefeatedState_DisablesMovementAndAttackHitboxes()
        {
            // Add a Rigidbody2D and BossAttackHitbox to the boss
            Rigidbody2D rb = _bossObject.AddComponent<Rigidbody2D>();
            rb.velocity = new Vector2(5f, 2f);

            GameObject hitboxObj = new GameObject("Hitbox");
            hitboxObj.transform.SetParent(_bossObject.transform);
            hitboxObj.AddComponent<CircleCollider2D>();
            BossAttackHitbox hitbox = hitboxObj.AddComponent<BossAttackHitbox>();
            SetField(hitbox, "_bossHealth", _bossHealth);

            // Expose and defeat
            _bossHealth.TakeDamage(new DamageInfo(100f, DamageType.Energy, null, Vector2.zero, Vector2.right));
            _bossHealth.ExecuteDeathStormStrike();

            Assert.AreEqual(BossPhase.Defeated, _bossHealth.CurrentPhase);
            Assert.AreEqual(Vector2.zero, rb.velocity, "Velocity must be zeroed upon defeat.");
            Assert.IsFalse(hitbox.enabled, "BossAttackHitbox must be disabled upon defeat.");
        }

        // ── TC-08: Enrage State and Clean Void Pounce Trigger ──
        [Test]
        public void TC08_EnrageState_EnablesVoidPounce_AndRejectsPounceIfNotEnraged()
        {
            // Before enrage, Void Pounce trigger must be rejected
            bool pounceResult = _bossHealth.TriggerVoidPounce(Vector2.up);
            Assert.IsFalse(pounceResult, "TriggerVoidPounce must return false when not enraged.");

            bool enragedEventFired = false;
            _bossHealth.OnBossEnraged += () => enragedEventFired = true;

            // Trigger enrage (e.g. from crafting timer timeout or signal)
            _bossHealth.TriggerEnrage();

            Assert.IsTrue(_bossHealth.IsEnraged, "Boss must report IsEnraged == true.");
            Assert.IsTrue(_bossHealth.VoidPounceEnabled, "VoidPounceEnabled must be true.");
            Assert.IsTrue(enragedEventFired, "OnBossEnraged event must fire.");

            // Now Void Pounce trigger must succeed
            bool pounceSucceeded = _bossHealth.TriggerVoidPounce(new Vector2(10f, 0f));
            Assert.IsTrue(pounceSucceeded, "TriggerVoidPounce must succeed when enraged.");
        }

        // ── TC-09: Boss Attack Damage Delivery to Player via IDamageable ──
        [Test]
        public void TC09_BossAttack_DeliversDamageToPlayer_ViaIDamageable()
        {
            GameObject playerObj = new GameObject("PlayerMock");
            playerObj.tag = GameConstants.TagPlayer;
            playerObj.layer = GameConstants.LayerPlayerIndex;
            playerObj.transform.position = new Vector3(2f, 0f, 0f);
            StubPlayerDamageable playerHealth = playerObj.AddComponent<StubPlayerDamageable>();

            bool delivered = _bossHealth.DeliverAttackDamage(playerObj, 30f, DamageType.Physical, 8f);

            Assert.IsTrue(delivered, "Attack damage delivery must return true.");
            Assert.AreEqual(70f, playerHealth.CurrentHealth, "Player health must decrease from 100 to 70.");
            Assert.AreEqual(1, playerHealth.DamageInfoHistory.Count, "Player must receive one DamageInfo payload.");
            Assert.AreEqual(_bossObject, playerHealth.DamageInfoHistory[0].Source, "Damage source must be the Boss GameObject.");

            UnityEngine.Object.DestroyImmediate(playerObj);
        }

        // ── TC-10: Exposure Window End Restores Armor ──
        [Test]
        public void TC10_ExposureWindow_ExpirationRestoresArmor()
        {
            // Set short exposure duration
            SetField(_bossHealth, "_exposedDuration", 0.2f);

            // Deplete armor
            _bossHealth.TakeDamage(new DamageInfo(100f, DamageType.Energy, null, Vector2.zero, Vector2.right));
            Assert.IsTrue(_bossHealth.IsExposed);

            // Trigger exposure expiration
            InvokeMethod(_bossHealth, "EndExposureWindow");

            Assert.IsFalse(_bossHealth.IsExposed, "Boss must no longer be exposed after timer expiration.");
            Assert.AreEqual(100f, _bossHealth.CurrentArmor, "Armor must be replenished when exposure ends.");
            Assert.AreEqual(BossPhase.Phase1, _bossHealth.CurrentPhase, "Boss must return to combat phase.");
        }

        #region Reflection Helpers

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(target, value);
        }

        private static void InvokeMethod(object target, string methodName)
        {
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(target, null);
        }

        #endregion

        #region Test Stubs

        private class StubPlayerDamageable : MonoBehaviour, IDamageable
        {
            public float CurrentHealth { get; private set; } = 100f;
            public float MaxHealth => 100f;
            public bool IsAlive => CurrentHealth > 0f;
            public System.Collections.Generic.List<DamageInfo> DamageInfoHistory = new System.Collections.Generic.List<DamageInfo>();

            public event Action<DamageInfo> OnDamageTaken;
            public event Action<float, float> OnHealthChanged;
            public event Action<DamageInfo> OnDied;

            public void TakeDamage(DamageInfo damageInfo)
            {
                DamageInfoHistory.Add(damageInfo);
                CurrentHealth = Mathf.Max(0f, CurrentHealth - damageInfo.Amount);
                OnDamageTaken?.Invoke(damageInfo);
                OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
                if (CurrentHealth <= 0f) OnDied?.Invoke(damageInfo);
            }

            public void Heal(float amount)
            {
                CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            }
        }

        #endregion
    }
}
