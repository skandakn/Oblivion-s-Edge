# Inventory and Interaction API

## Component identifiers and progression

`OblivionsEdge.Inventory.InventorySystem` implements `IInventory` and exposes the story state as `HasEdge`, `HasHandle`, `HasCore`, `HasDeathStorm`, and `HasDiscoveredNote`.

| Component | Item ID | Collection condition |
|---|---|---|
| Edge | `deathstorm_edge` | Read the teammate's note |
| Handle | `deathstorm_handle` | Read the teammate's note |
| Core | `deathstorm_core` | Read the note, collect Edge and Handle, and solve its configured puzzle (the Core always requires a puzzle completion event) |

Unique components are only added through an eligible `ComponentPickup`. `AddItemById` and `AddItem` reject these IDs, which prevents generic inventory or UI calls from bypassing story order. Duplicate collection is rejected. A puzzle event can unlock a pickup through `PuzzleCompletionPickupGate`; it never grants the item.

## UI integration

Subscribe to `IInventory.OnItemAdded`, `OnItemRemoved`, and `OnSlotChanged` to update ordinary inventory UI. The component indicators can use `GetItemCount` with the IDs above, matching the current HUD contract. For progression-specific views, subscribe to `InventorySystem.OnComponentsChanged(bool hasEdge, bool hasHandle, bool hasCore, bool hasDeathStorm)` and `OnNoteDiscovered`.

`InteractionController` uses E by default, detects the closest `IInteractable` within its configured 2D range/layer mask, and calls `IHUDView.ShowInteractionPrompt` / `HideInteractionPrompt`. Assign a HUD component to `_hudSource` or place the HUD implementation on the same GameObject.

## DeathStorm crafting integration

The crafting controller should receive the same `InventorySystem` through `IInventory`. Its ingredient checks can use `HasItem` for all three component IDs. `ComponentPickup.OnInteracted` can be wired to Member 10's `NotifyCoreCollected` when the pickup's item ID is `deathstorm_core`; the method is reached only after the pickup passes note, component, and configured puzzle gates. Subscribe to `IDeathStormCrafting.OnCraftingSuccess` and call `InventorySystem.NotifyDeathStormCrafted()` to update the shared `HasDeathStorm` state. That method rejects incomplete component sets and repeated notifications. Generic item addition rejects the DeathStorm output ID, so a recipe or UI grant cannot bypass the three crafting puzzles.

Puzzle owners can assign their `IPuzzleController` to `PuzzleCompletionPickupGate` in the Inspector, or call `ComponentPickup.NotifyPuzzleCompleted()` / `ComponentLockedDoor.NotifyPuzzleCompleted()` from their existing solve callback. The Core pickup always requires this notification; other pickups can opt in. Puzzle completion only unlocks the target; the player must still interact to collect a component or open the door.

## Story objects

- `StoryNote` reveals the dead teammate's message, fires `OnNoteDiscovered` through `InventorySystem`, and unlocks component collection.
- `TeammateTransmissionTrigger` is a one-shot interactable. An optional `IStoryMessageSink` displays its text; without one, the text is written to the Unity log.
- `ComponentLockedDoor` can require any combination of Edge, Handle, Core, and a puzzle solve. It disables the configured visual and collider only after a valid E interaction.

## Verification checklist

Run in a Unity scene with 2D colliders on the interactables and the player:

1. Try Edge and Handle before reading the note: both remain unavailable and inventory counts stay zero.
2. Press E on the note: its message appears, `HasDiscoveredNote` becomes true, and `OnNoteDiscovered` fires once.
3. Collect Edge and Handle: each count and flag changes once; interact with either pickup again and confirm no duplicate item/event.
4. Try Core before Edge and Handle: collection fails. Collect Core afterward, gated by the containment puzzle if configured.
5. Check a door with missing requirements and with a required puzzle unsolved: it stays closed and reports its locked prompt. Satisfy the conditions and press E: it opens.
6. Stand beyond `_interactionRange`: no target prompt appears and E does not interact. Move inside range and confirm the nearest target prompt appears and E activates it.
7. Call `NotifyDeathStormCrafted` before all components: it returns false. After all three, it returns true once and sets `HasDeathStorm`.
