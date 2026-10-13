# OBLIVION'S EDGE — Handle Component Puzzle Setup & Architectural Guide

**Author:** Member 7 (Handle Component & Geometric Rotation Puzzle)  
**Target Engine:** Unity 6 (`6000.6.5f1`) / Unity 2022.3.40f1 LTS  
**Branch:** `feature/handle-puzzle` (Base: `main`)  
**Namespace:** `OblivionsEdge.Puzzles` & `OblivionsEdge.Core`  

---

## 1. Executive Summary

The **Handle Component Puzzle** guards the second critical storyline artifact required for the legendary DeathStorm weapon: the **DeathStorm Handle** (`deathstorm_handle`).

The puzzle consists of three concentric geometric dials / rotatable rings (Outer, Middle, Inner) located at the derelict relay junction. To align the mechanism and unlock the handle artifact, the player must rotate each concentric ring by discrete angular steps (45° or 90°) to match the cipher key recovered from the fallen teammate's relay ship log.

### Key Architectural Tenets:
1. **Concentric Ring Geometry:** Three concentric dials evaluated independently with discrete angular steps ($45^\circ$ or $90^\circ$).
2. **Ship Log Cipher Key:** Solution comparison based on real modular angles (`Mathf.DeltaAngle`), matching:
   - **Outer Ring [0]:** $0^\circ$ (North / Cardinal Up)
   - **Middle Ring [1]:** $90^\circ$ (East / Cardinal Right)
   - **Inner Ring [2]:** $180^\circ$ (South / Cardinal Down)
3. **Smooth Slerp / Lerp Visual Transitions:** Visual transforms smoothly interpolate toward discrete targets using `Quaternion.RotateTowards` (spherical linear interpolation) while discrete logic state evaluates immediately.
4. **Completion & Artifact Unlocking:** Solved state raises `OnPuzzleCompleted`, `OnPuzzleSolved`, `OnHandleUnlocked`, and `OnArtifactUnlocked("deathstorm_handle")`.
5. **Discrete State Persistence & Reset:** Full reset mechanism restoring initial configuration, plus deterministic array-based discrete state persistence (`GetDiscreteState`, `SetDiscreteState`, and optional `PlayerPrefs` save/load).
6. **Scene Isolation:** Contained in `Assets/Prefabs/Puzzles/HandlePuzzle.prefab` without modifying shared scenes.

---

## 2. Prefab Assembly (`Assets/Prefabs/Puzzles/HandlePuzzle.prefab`)

```text
HandlePuzzle (Transform, CircleCollider2D [isTrigger], HandlePuzzleController)
├── Ring_Outer_Target_Up (Transform [Scale 1.0], SpriteRenderer)
├── Ring_Middle_Target_Right (Transform [Scale 0.75], SpriteRenderer)
└── Ring_Inner_Target_Down (Transform [Scale 0.5], SpriteRenderer)
```

### Hierarchy Configuration:
1. **Root Object (`HandlePuzzle`):**
   - Layer: `9` (`Interactable`)
   - Tag: `Interactable`
   - `CircleCollider2D`: radius `1.5`, `Is Trigger = true` for player interaction radius detection.
   - `HandlePuzzleController`: Master puzzle script.
2. **Concentric Rings:**
   - **Ring 0 (Outer):** Scale `(1.0, 1.0, 1.0)`, initial `0°`, target `0°` (North).
   - **Ring 1 (Middle):** Scale `(0.75, 0.75, 1.0)`, initial `0°`, target `90°` (East).
   - **Ring 2 (Inner):** Scale `(0.5, 0.5, 1.0)`, initial `0°`, target `180°` (South).

---

## 3. Controls & Interaction

When in range of the puzzle terminal:
- `1`, `2`, `3`: Select Concentric Ring (Outer, Middle, Inner).
- `E` / `RotateClockwiseKey`: Rotate selected ring clockwise by one discrete step ($+1$).
- `Q` / `RotateCounterClockwiseKey`: Rotate selected ring counter-clockwise by one discrete step ($-1$).
- `R` / `ResetKey`: Reset puzzle to initial scrambled configuration.
- `E` (when solved): Claim the unlocked `deathstorm_handle` artifact.

---

## 4. Discrete State Persistence & Reset API

```csharp
// Get current discrete ring step indices (e.g. [0, 1, 2])
int[] discreteSteps = puzzle.GetDiscreteState();

// Restore discrete ring step indices from save system
puzzle.SetDiscreteState(savedSteps);

// Reset puzzle to initial pattern and relock handle
puzzle.ResetPuzzle();

// Reset a specific ring
puzzle.ResetRing(ringIndex);

// Optional PlayerPrefs persistence
puzzle.SaveToPersistence();
puzzle.LoadFromPersistence();
```

---

## 5. Member 9 & 10 Integration API

### Events:
- `OnPuzzleCompleted`: Standard completion event raised when cipher pattern matches.
- `OnPuzzleSolved`: `IPuzzleController` contract event.
- `OnHandleUnlocked`: Raised when handle artifact containment unlocks.
- `OnArtifactUnlocked(string artifactId)`: Emits `"deathstorm_handle"`.
- `OnArtifactClaimed(string artifactId)`: Emits when artifact is claimed by player inventory.
- `OnPuzzleReset`: Raised when puzzle is reset.

### Direct Claim Method:
```csharp
// Member 9 Inventory interaction hook
bool claimed = handlePuzzle.TryClaimHandle(playerGameObject);
```

---

## 6. Automated Unit Tests

Verified via `Assets/Tests/EditMode/HandlePuzzleControllerTests.cs`:
1. `CorrectPattern_UnlocksHandleAndRaisesCompletionOnce`: Rotates rings to cipher target, confirms solution and single completion event.
2. `IncorrectPattern_RemainsInProgressAndUsesModularRotation`: Verifies modular 360° wrapping and in-progress state.
3. `Reset_RestoresInitialPatternAndRelocksHandle`: Validates reset mechanism and state relock.
4. `Solved_EmitsOnPuzzleCompleted_AndUnlocksDeathstormHandleArtifact`: Confirms `OnPuzzleCompleted` event emission and `"deathstorm_handle"` artifact ID.
5. `DiscreteStatePersistence_GetAndSetDiscreteState_RestoresPatternExactly`: Validates discrete state export and restoration.
6. `FortyFiveDegreeMode_OperatesWithEightDiscreteSteps`: Confirms 8-step (45°) angular mode.
7. `CipherKey_MatchesShipLogConfiguration`: Confirms alignment with ship log cipher descriptions.
8. `ResetRing_ResetsSingleRingBackToInitialStep`: Verifies individual ring reset.
