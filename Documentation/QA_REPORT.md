# UI QA report

Date: 2026-10-13  
Branch: `feature/ui-qa`

## Executed checks

| Area | Result | Evidence |
| --- | --- | --- |
| UI source integrity | Pass | Event subscriptions are paired with unsubscriptions; the controller contains no `Update` or object-search loop. |
| Inspector resilience | Pass | Critical missing UI and source fields log named warnings in `Awake`; null UI fields are guarded. |
| UI state coverage | Pass by code review | Main menu, controls, health, ammo, components, boss armor/health, crafting countdown, interaction prompt, puzzle feedback, death/retry, victory, restart, and scene load handlers are present. |
| Unity runtime regression | Blocked | No Unity editor executable or assembled gameplay scene is available in this workspace. |

## Blocking bugs / integration gaps

1. **No runnable integrated build.** `main` contains contracts and an empty scene only. Opening sequence, weapon note, component pickups, puzzles, alien attacks, armor depletion, Core retrieval, crafting paths, and final DeathStorm strike cannot be exercised until gameplay branches are merged into a playable scene.
2. **DeathStorm UI adapter pending Member 10.** Member 10's branch exposes `IDeathStormCrafting`, but it is not published on `main`, and its event names do not yet directly satisfy `ICraftingCountdownSource`. The required adapter mapping is in `Documentation/UI_SETUP.md`.
3. **Boss armor event contract pending boss owner.** The baseline `IBossController` has health and phase events but no armor telemetry. The boss armor component must implement `IBossArmorSource`.
4. **Menu scene flow needs an entry scene.** Only `MainScene` is in the baseline build settings. Create a menu scene or show the Main Menu panel before gameplay starts.

## Runtime QA checklist for integration build

- Start game: confirm main menu, controls panel, and scene transition.
- Read weapon note; acquire Edge, Handle, then Core; confirm each indicator changes to `ONLINE`.
- Complete every puzzle and intentionally fail one; confirm instruction plus feedback.
- Fire, reload, and empty the gun; confirm ammunition telemetry.
- Take alien damage, deplete armor, retrieve Core; confirm health, armor, and exposed-core feedback.
- Complete the 60-second crafting sequence; repeat it until timeout and confirm enrage/VOID POUNCE messaging.
- Deliver DeathStorm's final weak-point strike; confirm victory screen.
- Die, restart, reload the gameplay scene, and remove each critical Inspector source once; confirm graceful UI warnings and no exception.
