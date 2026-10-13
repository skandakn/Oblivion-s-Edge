using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using OblivionsEdge.DeathStorm;
using OblivionsEdge.Boss;
using OblivionsEdge.Core;
using OblivionsEdge.Inventory;

// ─────────────────────────────────────────────────────────────────────────────
//  DeathStormTests.cs — EditMode + PlayMode test suite
//  Owner: Member 10
//
//  Covers:
//    TC-01  Successful crafting (all components present, correct answers)
//    TC-02  Missing components block crafting start
//    TC-03  Timer expiry triggers boss ENRAGED + OnCountdownExpired(true)
//    TC-04  Void Pounce activation message sent after timeout
//    TC-05  Wrong attack direction returns false from TryFire
//    TC-06  Valid DeathStorm final strike transitions boss to Defeated
//    TC-07  Wrong answers consume time but crafting continues
//    TC-08  Crafting resumes after timeout (player survives)
// ─────────────────────────────────────────────────────────────────────────────

namespace OblivionsEdge.DeathStorm.Tests
{
    // ── Minimal stub for IInventory ────────────────────────────────────────

    internal class StubInventory : MonoBehaviour, IInventory
    {
        public HashSet<string> Items = new HashSet<string>();

        public int   SlotCapacity  => 20;
        public float CurrentWeight => 0f;
        public float MaxWeight     => 50f;
        public bool  IsFull        => false;

        public bool AddItem(IItem item)                           => true;
        public bool AddItemById(string id, int qty)               { Items.Add(id); return true; }
        public bool RemoveItem(string id, int qty)                { Items.Remove(id); return true; }
        public bool HasItem(string id, int qty = 1)               => Items.Contains(id);
        public int  GetItemCount(string id)                       => Items.Contains(id) ? qty : 0;
        public IItem GetItemAtSlot(int slotIndex)                 => null;

        private int qty = 1;

        public event Action<int, IItem>    OnSlotChanged;
        public event Action<IItem>         OnItemAdded;
        public event Action<string, int>   OnItemRemoved;
        public event Action<float, float>  OnWeightChanged;
    }

    // ── Minimal stub for IBossController ──────────────────────────────────

    internal class StubBossController : MonoBehaviour, IBossController
    {
        public BossPhase CurrentPhase   { get; private set; } = BossPhase.Phase1;
        public bool  IsInvulnerable     { get; private set; } = false;
        public float EnrageTimerRemaining => 0f;
        public float CurrentHealth      => 100f;
        public float MaxHealth          => 100f;
        public bool  IsAlive            => true;
        public string BossId            => "void_spawn";
        public string DisplayName       => "Void Spawn";

        public bool VoidPounceEnabled   = false;

        public void StartBossEncounter()                            { }
        public void TransitionToPhase(BossPhase p)                  { CurrentPhase = p; OnPhaseChanged?.Invoke(p); if (p == BossPhase.Enraged) OnBossEnraged?.Invoke(); if (p == BossPhase.Defeated) OnBossDefeated?.Invoke(); }
        public void SetInvulnerable(bool inv)                       { IsInvulnerable = inv; }
        public void TriggerStun(float dur)                          { CurrentPhase = BossPhase.VulnerableStun; }
        public void RegisterPhaseListener(IBossPhaseListener l)     { }
        public void UnregisterPhaseListener(IBossPhaseListener l)   { }
        public void TakeDamage(DamageInfo d)                        { }
        public void Heal(float amount)                              { }

        // SendMessage target
        public void EnableVoidPounce()                              { VoidPounceEnabled = true; }

        public event Action<BossPhase>    OnPhaseChanged;
        public event Action               OnBossEnraged;
        public event Action               OnBossStunned;
        public event Action               OnBossDefeated;
        public event Action<DamageInfo>   OnDamageTaken;
        public event Action<float, float> OnHealthChanged;
        public event Action<DamageInfo>   OnDied;
    }

    // ── Factory helpers ────────────────────────────────────────────────────

    internal static class TestFactory
    {
        /// <summary>Create a fully wired DeathStormCrafting GameObject.</summary>
        public static (DeathStormCrafting crafter, StubInventory inv, StubBossController boss)
            BuildCrafter(bool withAllItems = true, float countdown = 60f)
        {
            var go  = new GameObject("DSCrafter");
            var inv  = go.AddComponent<StubInventory>();
            var boss = go.AddComponent<StubBossController>();

            if (withAllItems)
            {
                inv.Items.Add("deathstorm_edge");
                inv.Items.Add("deathstorm_handle");
                inv.Items.Add("deathstorm_core");
            }

            // Use reflection / SerializeField approach: set via serialized field
            var crafter = go.AddComponent<DeathStormCrafting>();

            // Wire via the public-facing internal fields using Unity serialisation helper
            SetPrivateField(crafter, "_bossControllerMono",  boss);
            SetPrivateField(crafter, "_playerInventoryMono", inv);
            SetPrivateField(crafter, "_countdownTotal",       countdown);
            // Set stage answers to defaults matching the class defaults
            SetPrivateField(crafter, "_coreAlignmentAnswer", 2);
            SetPrivateField(crafter, "_edgeSyncAnswer",       3);
            SetPrivateField(crafter, "_stabiliseSequence",    new List<int> { 1, 3, 0 });

            // Call Awake manually (we're in EditMode, no scene lifecycle)
            InvokePrivate(crafter, "Awake");

            return (crafter, inv, boss);
        }

        public static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        public static void InvokePrivate(object target, string name)
        {
            var method = target.GetType().GetMethod(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(target, null);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-01 Successful crafting
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC01_SuccessfulCrafting
    {
        [Test]
        public void AllCorrectAnswers_SetsHasDeathStormAndFiresEvent()
        {
            var (crafter, _, _) = TestFactory.BuildCrafter();

            bool successFired = false;
            crafter.OnCraftingSuccess += () => successFired = true;

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.CoreAlignment, crafter.CurrentStage);

            // Stage 1 — correct cardinal (South=2)
            bool r1 = crafter.SubmitAnswer(2);
            Assert.IsTrue(r1, "Stage 1 correct answer should return true.");
            Assert.AreEqual(CraftingStage.EdgeSync, crafter.CurrentStage);

            // Stage 2 — correct angle bucket (3)
            bool r2 = crafter.SubmitAnswer(3);
            Assert.IsTrue(r2, "Stage 2 correct answer should return true.");
            Assert.AreEqual(CraftingStage.EnergyStabilise, crafter.CurrentStage);

            // Stage 3 — correct sequence: 1, 3, 0
            crafter.SubmitAnswer(1);
            crafter.SubmitAnswer(3);
            bool r3 = crafter.SubmitAnswer(0);
            Assert.IsTrue(r3, "Stage 3 final answer should return true.");

            Assert.AreEqual(CraftingStage.Complete, crafter.CurrentStage);
            Assert.IsTrue(crafter.HasDeathStorm,    "HasDeathStorm must be true after success.");
            Assert.IsTrue(successFired,             "OnCraftingSuccess event must fire.");

            Object.DestroyImmediate(crafter.gameObject);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-02 Missing components block crafting
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC02_MissingComponents
    {
        [Test]
        public void MissingEdge_CraftingStaysIdle()
        {
            var (crafter, inv, _) = TestFactory.BuildCrafter(withAllItems: false);
            inv.Items.Add("deathstorm_handle");
            inv.Items.Add("deathstorm_core");
            // Edge is absent

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.Idle, crafter.CurrentStage,
                "Without Edge, crafting must not start.");

            Object.DestroyImmediate(crafter.gameObject);
        }

        [Test]
        public void MissingHandle_CraftingStaysIdle()
        {
            var (crafter, inv, _) = TestFactory.BuildCrafter(withAllItems: false);
            inv.Items.Add("deathstorm_edge");
            inv.Items.Add("deathstorm_core");

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.Idle, crafter.CurrentStage);

            Object.DestroyImmediate(crafter.gameObject);
        }

        [Test]
        public void MissingCore_CraftingStaysIdle()
        {
            var (crafter, inv, _) = TestFactory.BuildCrafter(withAllItems: false);
            inv.Items.Add("deathstorm_edge");
            inv.Items.Add("deathstorm_handle");

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.Idle, crafter.CurrentStage);

            Object.DestroyImmediate(crafter.gameObject);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-03 Timer expiry triggers boss ENRAGED and event
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC03_TimerExpiry
    {
        [Test]
        public void TimerExpiry_TransitionsBossToEnragedAndFiresEvent()
        {
            var (crafter, _, boss) = TestFactory.BuildCrafter(countdown: 0.2f);

            bool firstExpiry = false;
            crafter.OnCountdownExpired += (isFirst) => firstExpiry = isFirst;

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.CoreAlignment, crafter.CurrentStage);

            // Simulate timer expiry directly
            TestFactory.SetPrivateField(crafter, "_countdownRemaining", 0f);
            TestFactory.InvokePrivate(crafter, "HandleTimeout");

            Assert.AreEqual(CraftingStage.TimedOut, crafter.CurrentStage);
            Assert.AreEqual(BossPhase.Enraged,      boss.CurrentPhase, "Boss must be Enraged after timeout.");
            Assert.IsTrue(firstExpiry, "OnCountdownExpired must fire with isFirst=true for the first timeout.");

            Object.DestroyImmediate(crafter.gameObject);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-04 Void Pounce activation
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC04_VoidPounceActivation
    {
        [Test]
        public void OnTimeout_VoidPounceIsEnabled()
        {
            var (crafter, _, boss) = TestFactory.BuildCrafter(countdown: 0.2f);

            crafter.NotifyCoreCollected();
            TestFactory.SetPrivateField(crafter, "_countdownRemaining", 0f);
            TestFactory.InvokePrivate(crafter, "HandleTimeout");

            Assert.IsTrue(boss.VoidPounceEnabled,
                "StubBossController.EnableVoidPounce() must be called via SendMessage after timeout.");

            Object.DestroyImmediate(crafter.gameObject);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-05 Wrong attack direction
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC05_WrongAttackDirection
    {
        [Test]
        public void WrongDirection_ReturnsFalse_BossNotDefeated()
        {
            // Build AxeCombat rig
            var go        = new GameObject("AxeRig");
            var bossGo    = new GameObject("Boss");
            bossGo.tag    = "Boss";

            // Collider for the swing hitbox to detect
            var bossCol   = bossGo.AddComponent<CircleCollider2D>();
            bossGo.layer  = LayerMask.NameToLayer("Default"); // use Default since physics matrix isn't set

            var boss      = bossGo.AddComponent<StubBossController>();
            boss.TriggerStun(10f); // Put boss into VulnerableStun

            // DeathStorm crafting with hasDeathStorm = true via reflection
            var craftGo   = new GameObject("Craft");
            var crafter   = craftGo.AddComponent<DeathStormCrafting>();
            SetField(crafter, "_hasDeathStorm", true);

            var axe       = go.AddComponent<AxeCombat>();
            var col       = go.GetComponent<Collider2D>();
            SetField(axe, "_bossControllerMono",  boss);
            SetField(axe, "_craftingController",  crafter);
            SetField(axe, "_hitboxRadius",        50f);   // huge to guarantee hit
            SetField(axe, "_directionTolerance",  20f);
            SetField(axe, "_weakPointOffset",     new Vector2(0f, 0f));
            SetField(axe, "_hitLayers",           ~0);    // all layers
            InvokeMethod(axe, "Awake");

            bool invalidFired = false;
            axe.OnInvalidStrike += () => invalidFired = true;

            Vector2 origin        = Vector2.zero;
            Vector2 wrongDir      = Vector2.left;   // 180° from forward (0,1) → well outside 20° tol

            // Place boss on the right so weak-point is to the right
            bossGo.transform.position = new Vector3(5f, 0f, 0f);

            bool hit = axe.TryFire(origin, wrongDir);

            Assert.IsFalse(hit, "Wrong direction should not register a valid strike.");
            Assert.AreNotEqual(BossPhase.Defeated, boss.CurrentPhase,
                "Boss must not be defeated by a wrong-direction strike.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(bossGo);
            Object.DestroyImmediate(craftGo);
        }

        private static void SetField(object t, string n, object v) =>
            t.GetType().GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(t, v);

        private static void InvokeMethod(object t, string n) =>
            t.GetType().GetMethod(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(t, null);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-06 Valid DeathStorm final strike defeats boss
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC06_ValidFinalStrike
    {
        [Test]
        public void CorrectDirection_WithDeathStorm_ExposedBoss_TransitionsToDefeated()
        {
            var go     = new GameObject("AxeRig2");
            var bossGo = new GameObject("Boss2");
            bossGo.tag = "Boss";
            bossGo.AddComponent<CircleCollider2D>();

            var boss   = bossGo.AddComponent<StubBossController>();
            boss.TriggerStun(10f); // VulnerableStun

            var craftGo = new GameObject("Craft2");
            var crafter = craftGo.AddComponent<DeathStormCrafting>();
            SetField(crafter, "_hasDeathStorm", true);

            var axe    = go.AddComponent<AxeCombat>();
            SetField(axe, "_bossControllerMono",  boss);
            SetField(axe, "_craftingController",  crafter);
            SetField(axe, "_hitboxRadius",        50f);
            SetField(axe, "_directionTolerance",  90f);   // wide tolerance for test
            SetField(axe, "_weakPointRadius",     50f);   // wide zone for test
            SetField(axe, "_weakPointOffset",     new Vector2(0f, 0f));
            SetField(axe, "_hitLayers",           ~0);
            InvokeMethod(axe, "Awake");

            bool strikeFired = false;
            axe.OnDeathStormStrike += () => strikeFired = true;

            // Place boss directly in front
            bossGo.transform.position = new Vector3(0f, 1f, 0f);
            Vector2 origin  = Vector2.zero;
            Vector2 forward = Vector2.up;

            bool hit = axe.TryFire(origin, forward);

            Assert.IsTrue(hit, "Valid DeathStorm strike should return true.");
            Assert.IsTrue(strikeFired, "OnDeathStormStrike event must fire.");
            Assert.AreEqual(BossPhase.Defeated, boss.CurrentPhase,
                "Boss must transition to Defeated after valid DeathStorm strike.");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(bossGo);
            Object.DestroyImmediate(craftGo);
        }

        private static void SetField(object t, string n, object v) =>
            t.GetType().GetField(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(t, v);

        private static void InvokeMethod(object t, string n) =>
            t.GetType().GetMethod(n, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(t, null);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-07 Wrong answers consume time but do not lock crafting
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC07_WrongAnswersPenalty
    {
        [Test]
        public void WrongAnswer_ReducesTimerButAllowsContinue()
        {
            var (crafter, _, _) = TestFactory.BuildCrafter(countdown: 60f);

            float penaltyReceived = 0f;
            crafter.OnWrongAnswer += (p) => penaltyReceived = p;

            crafter.NotifyCoreCollected();
            float before = crafter.CountdownRemaining;

            // Wrong answer for Stage 1 (correct is 2, send 0)
            bool wrong = crafter.SubmitAnswer(0);
            Assert.IsFalse(wrong, "Wrong answer must return false.");
            Assert.Greater(penaltyReceived, 0f, "A time penalty must be applied.");
            Assert.Less(crafter.CountdownRemaining, before, "Timer must decrease after wrong answer.");

            // Still in Stage 1 (not locked out)
            Assert.AreEqual(CraftingStage.CoreAlignment, crafter.CurrentStage,
                "Stage must not advance on a wrong answer.");

            // Now send the correct answer — crafting should still work
            crafter.SubmitAnswer(2);
            Assert.AreEqual(CraftingStage.EdgeSync, crafter.CurrentStage,
                "Crafting must continue after wrong answer — should advance on correct answer.");

            Object.DestroyImmediate(crafter.gameObject);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TC-08 Crafting continues after timeout (player survives)
    // ══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TC08_ResumeAfterTimeout
    {
        [Test]
        public void AfterTimeout_ResumeRestartsCountdownAndAllowsCompletion()
        {
            var (crafter, _, boss) = TestFactory.BuildCrafter(countdown: 0.2f);

            crafter.NotifyCoreCollected();
            Assert.AreEqual(CraftingStage.CoreAlignment, crafter.CurrentStage);

            // Let timer expire
            TestFactory.SetPrivateField(crafter, "_countdownRemaining", 0f);
            TestFactory.InvokePrivate(crafter, "HandleTimeout");
            Assert.AreEqual(CraftingStage.TimedOut, crafter.CurrentStage);

            // Simulate player surviving: resume crafting
            crafter.ResumeAfterTimeout();
            Assert.AreNotEqual(CraftingStage.TimedOut, crafter.CurrentStage,
                "Stage must not be TimedOut after ResumeAfterTimeout.");
            Assert.IsTrue(crafter.IsCountdownRunning, "Countdown must restart after resume.");

            // Complete the crafting from Stage 1
            crafter.SubmitAnswer(2); // Stage 1 correct
            crafter.SubmitAnswer(3); // Stage 2 correct
            crafter.SubmitAnswer(1); // Stage 3 step 1
            crafter.SubmitAnswer(3); // Stage 3 step 2
            crafter.SubmitAnswer(0); // Stage 3 step 3 (final)

            Assert.IsTrue(crafter.HasDeathStorm,           "HasDeathStorm must be true after post-timeout completion.");
            Assert.AreEqual(CraftingStage.Complete, crafter.CurrentStage);

            Object.DestroyImmediate(crafter.gameObject);
        }
    }
}
