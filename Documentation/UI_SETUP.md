# UI prefab setup

Create a screen-space overlay Canvas named `OblivionUI` and add `OblivionUIController`. Use TextMeshProUGUI for every text field. Set the Canvas sorting layer to `UI`.

Use a near-black background (`#070913`), off-white text (`#E8EDF8`), desaturated blue labels (`#79A8E8`), and violet alerts (`#9E7CFF`). Keep 16 px or larger body text and reserve glow effects for active objectives, the boss bar, and failure states.

Create and assign these child panels: Main Menu, Controls, HUD, Crafting, Inventory, Puzzle, Death, Victory, Boss Bar, and Interaction Prompt. Add Buttons and wire their OnClick handlers to `StartGame`, `OpenControls`, `CloseControls`, `QuitGame`, `RestartScene`, and `LoadGameplayScene` on the controller. Assign filled Images to health, boss health, armor, and countdown fields.

The controller verifies the critical Inspector references in `Awake` and issues a named warning for each missing reference. It uses no `Find` calls and does not poll gameplay state in `Update`.

## Event wiring

| Owner | Source contract | UI behavior |
| --- | --- | --- |
| Member 1 | `GameManager.OnGameStateChanged` | Shows Game Over or Victory panels. |
| Player owner | `IDamageable.OnHealthChanged`, `OnDied` | Updates vitals and opens the death/retry panel. |
| Weapon owner | `IWeapon.OnAmmoChanged`, `IWeaponHandler.OnWeaponEquipped` | Updates magazine readout and equipped weapon name. |
| Inventory owner | `IInventory.OnItemAdded`, `OnItemRemoved`, `OnSlotChanged` | Refreshes Edge, Handle, and Core indicators. |
| Boss owner | `IDamageable.OnHealthChanged`, `IBossController.OnBossDefeated` | Updates boss health and opens the DeathStorm victory screen. |
| Boss armor owner | `IBossArmorSource.OnArmorChanged`, `OnArmorDepleted` | Updates armor and announces exposed core. |
| Puzzle owner | `IPuzzleController.OnPuzzleStateChanged`, `OnPuzzleSolved`, `OnPuzzleFailed` | Shows instructions and success/failure feedback. |

## Member 10 crafting adapter

`OblivionUIController` accepts a `MonoBehaviour` implementing `ICraftingCountdownSource`. Member 10's `IDeathStormCrafting` already supplies the timer and completion/timeout events; its component or an adapter should expose the following UI-owned surface:

```csharp
float CountdownRemaining { get; }
float CountdownTotal { get; }
string CurrentInstruction { get; }
event Action<float, float> OnTimerTick;
event Action<string> OnInstructionChanged;
event Action OnCraftingSucceeded;
event Action<bool> OnCountdownExpired;
```

Map `IDeathStormCrafting.OnCraftingSuccess` to `OnCraftingSucceeded`, `OnStageChanged` to `OnInstructionChanged(CurrentInstruction)`, and forward the timer and timeout events unchanged. This keeps the UI dependent on a small public event contract and avoids a dependency on Member 10's concrete `DeathStormCrafting` class.
