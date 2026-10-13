# Handle Puzzle Setup

`Assets/Prefabs/Puzzles/HandlePuzzle.prefab` is an isolated starting point; it does not belong in the main scene. Drag it into a level or instantiate it from the level's puzzle owner.

## Assembly

1. Create three child objects for the visible rings or symbols and assign their transforms to **Rings** in order: outer, middle, inner.
2. Optionally assign the three `SpriteRenderer` components to **Ring Renderers** for immediate red/green/blue feedback. Placeholder sprites are sufficient.
3. Set **Steps Per Revolution** (the default is four, i.e. 90 degrees) and set each **Target Steps** value. The target is displayed by the ring arrangement you create in the level; matching values are: `0 = up`, `1 = right`, `2 = down`, `3 = left` at the default step count.
4. Set **Initial Steps** to define the known starting position. This makes the puzzle deterministic and solvable by following the target rather than guessing.

## Play and integration

While enabled, keys `1`, `2`, `3` select a ring, `Q` and `E` rotate it by one discrete step, and `R` resets it. Disable **Accept Keyboard Input** when another interaction component drives `SelectRing` and `RotateRing` instead.

The controller compares real modular angles using `Mathf.DeltaAngle`, not animation states. It implements `IPuzzleController` and exposes:

- `OnHandleUnlocked` — subscribe from Member 9's inventory/world bridge to award or enable the Handle component.
- `OnPuzzleSolved` — standard team puzzle-completion event.
- `OnIncorrectAlignment` — immediate C# feedback event for an attempted, non-matching pattern. `OnPuzzleFailed` remains reserved for a level-specific terminal failure condition.
- `HandleUnlocked` — safe status property for one-time integration checks.
- `ForceSolve()` and `ResetPuzzle()` — public testing and recovery methods.

`Assets/Tests/EditMode/HandlePuzzleControllerTests.cs` covers the correct pattern, an incorrect modular rotation, ignored repeated input after completion, and reset/relock behavior. Run it from Unity Test Runner in EditMode.
