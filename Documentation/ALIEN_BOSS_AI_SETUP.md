# Alien Boss AI setup

1. Create a Boss GameObject tagged `Boss` and add a `Rigidbody2D` with a Dynamic body type, zero gravity, and frozen Z rotation.
2. Add `AlienBossAI`. Assign the player Transform in **References**; it will fall back to the `Player` tag when this field is empty.
3. Tune **Movement**, **Standard Charge**, **Armor and Core**, and **Enrage** values in the Inspector for the arena.
4. Call `StartEncounter()` from the encounter trigger. The boss spends the configured awakening time in `Awakening`, then enters `Hunt`.
5. The health/armor component must call `NotifyArmorDepleted()` once when armor reaches zero, `NotifyCoreReleased()` once when it releases the core, and `NotifyBossDefeated()` once when health reaches zero. It can subscribe to `OnArmorExposed`, `OnCoreReleased`, and `OnDefeated` for visual or encounter reactions.
6. The crafting timer integration must call `NotifyCraftingTimerElapsed()` when it completes. This transitions the boss to `Enraged` and enables VOID POUNCE. The internal fallback timer uses **Enrage After Seconds**.
7. Connect telegraph and hitbox visuals to `OnChargeTelegraphed`, `OnChargeStarted`, `OnVoidPounceTelegraphed`, and `OnVoidPounceStarted`. The charge hitbox must call `NotifyChargeHit()` on a successful hit; a charge that reaches its stored target or times out enters `Recover` and raises `OnChargeMissed`.

`ChargeTargetPosition` and the VOID POUNCE target are captured before movement begins. They do not update while the attack is in progress, so the boss does not track a moving player mid-charge.

`TryTransitionTo` rejects repeated or disallowed transitions and raises `OnInvalidTransitionRequested`. `Defeated` stops all movement and attack updates.
