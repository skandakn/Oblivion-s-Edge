# Oblivion's Edge Documentation

The authoritative architectural contracts and development guides are maintained in the root `Documentation/` folder:

- [Team Architectural Contracts & Interface Specifications](../../Documentation/TEAM_CONTRACTS.md)
- [Main Repository README](../../README.md)

### Interface Overview
All team contracts are defined in C# under `Assets/Scripts/`:
- `Assets/Scripts/Core` — `GameConstants.cs`, `IDamageable.cs`, `IInteractable.cs`, `GameManager.cs`
- `Assets/Scripts/Player` — `IPlayerController.cs`, `IPlayerStats.cs`
- `Assets/Scripts/Weapons` — `IWeapon.cs`, `IWeaponHandler.cs`, `WeaponType.cs`
- `Assets/Scripts/Boss` — `IBossController.cs`, `IBossPhaseListener.cs`, `BossPhase.cs`
- `Assets/Scripts/Puzzles` — `IPuzzleController.cs`, `IPuzzleElement.cs`, `PuzzleState.cs`
- `Assets/Scripts/Inventory` — `IInventory.cs`, `ItemData.cs`
- `Assets/Scripts/Crafting` — `ICraftingStation.cs`, `CraftingRecipe.cs`
- `Assets/Scripts/UI` — `IUIManager.cs`, `IHUDView.cs`
