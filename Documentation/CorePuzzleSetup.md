# Core Extraction Puzzle Setup & Integration Guide (Member 8)

`Assets/Prefabs/Puzzles/CorePuzzle.prefab` and `Assets/Scripts/Puzzles/CorePuzzleController.cs` implement the four-direction gravity-alignment puzzle that gates DeathStorm Core extraction. Do not modify `Assets/Scenes/MainScene.unity` directly when testing in isolation; drag the prefab into a dedicated test or arena scene.

---

## 1. Prefab & Inspector Setup

1. Place `Assets/Prefabs/Puzzles/CorePuzzle.prefab` into the Boss Arena / Containment Chamber scene.
2. Verify child transforms on `CorePuzzleController`:
   - **Force Arrow Transform (`_forceArrowTransform`)**: Points in the currently selected `Vector2` gravity direction (`0° = Up (0, 1)`, `270° = Right (1, 0)`, `180° = Down (0, -1)`, `90° = Left (-1, 0)`).
   - **Socket Indicator Transform (`_socketIndicatorTransform`)**: Points in the required socket `Vector2` direction (`_requiredSocketDirection`, default `Down (0, -1)`).
   - Optionally assign `SpriteRenderer` components to **Force Arrow Renderer** and **Socket Indicator Renderer** for color-coded status telemetry (`_lockedColor`, `_incorrectColor`, `_correctColor`, `_solvedColor`).
3. Configure **Gravity Vector Configuration**:
   - **Initial Gravity Direction (`_initialGravityDirection`)**: Starting cardinal `Vector2` (default `(0, 1)` Up).
   - **Required Socket Direction (`_requiredSocketDirection`)**: Target socket `Vector2` (default `(0, -1)` Down).
4. Configure **Boss Condition Gate (`_requireBossCondition`)**:
   - Keep **Require Boss Condition** enabled (`true`) in the arena so Core extraction is blocked until the Alien Boss armor is depleted / Core is exposed.
   - Assign the Boss `MonoBehaviour` (`AlienBossAI` or an `IBossController` implementation) to **Boss Source (`_bossSource`)**.

---

## 2. Controls & Mathematical Model

- **Four Cardinal Directions (`Vector2`)**:
  - `Up`: `(0, 1)` (`0°`)
  - `Right`: `(1, 0)` (`270°`)
  - `Down`: `(0, -1)` (`180°`)
  - `Left`: `(-1, 0)` (`90°`)
- **90-Degree Rotation**:
  - `Q` / `RotateCounterClockwise()` rotates the `Vector2` gravity vector $90^\circ$ counter-clockwise: $(x, y) \mapsto (-y, x)$.
  - `E` / `RotateClockwise()` rotates the `Vector2` gravity vector $90^\circ$ clockwise: $(x, y) \mapsto (y, -x)$.
  - Keys `1`–`4` (or Arrow keys) call `SelectDirection(Vector2)` for direct selection of `Up`, `Right`, `Down`, or `Left`.
  - `R` calls `ResetPuzzle()` to restore the initial `Vector2` direction.
- **Alignment Comparison**:
  - `EvaluateCondition()` computes `Vector2.Dot(CurrentGravityDirection.normalized, RequiredSocketDirection.normalized)` and `Mathf.DeltaAngle(GetForceArrowAngleDegrees(), GetSocketAngleDegrees())`.
  - Incorrect alignments transition `State` to `PuzzleState.InProgress`, update `LastFeedbackMessage`, tint the force arrow `_incorrectColor`, and raise `OnIncorrectAlignment` **without soft-locking** the puzzle.

---

## 3. Cross-Member Integration Contracts

### Member 5 (Alien Boss AI — Core-Release Condition)
- `CorePuzzleController` prevents Core extraction while `IsBossConditionMet` is `false` (raising `OnExtractionBlockedByBoss` and displaying locked feedback if attempted early).
- **Automatic Wiring**: Assign Member 5's `AlienBossAI` (or any `IBossController`) to `_bossSource`:
  - Subscribes to `AlienBossAI.OnArmorExposed` (and checks `IsArmorExposed`), or `IBossController.OnBossStunned` / `OnBossDefeated` / `BossPhase.VulnerableStun`.
  - Automatically calls `NotifyCoreReleased()` on `_bossSource` when Core extraction unlocks/completes.
- **Manual / Event Wiring**: You can also call `NotifyArmorExposed()`, `NotifyBossConditionMet()`, or `SetBossConditionMet(true)` directly from the Boss armor/health component.

### Member 9 (Inventory & World Interactions — Granting the Core)
- `CorePuzzleController` implements `IPuzzleController` and `IInteractable`:
  - **Gate Unlocking**: Assign `CorePuzzleController` to Member 9's `PuzzleCompletionPickupGate._puzzleSource`, or assign Member 9's `ComponentPickup` (`deathstorm_core`) to `_corePickupTarget`. When aligned after the boss condition is met, `OnPuzzleSolved` and `OnCoreExtractionUnlocked` fire and `NotifyPuzzleCompleted()` is sent to `_corePickupTarget`.
  - **Direct Core Extraction API**: Subscribe to `OnCoreExtractionUnlocked` or `OnCoreExtracted(GameObject interactor)`, or call `TryExtractCore(interactor)` / `CanExtractCore()`.

### Member 11 / 12 (UI & QA Telemetry)
- Assign `CorePuzzleController` to `OblivionUIController._puzzleSource` (`IPuzzleController`). It broadcasts `OnPuzzleStateChanged`, `OnPuzzleSolved`, `OnIncorrectAlignment`, and `OnFeedbackChanged(string)`.

---

## 4. Verification Procedure

Run `Assets/Tests/EditMode/CorePuzzleControllerTests.cs` in Unity Test Runner (EditMode) or verify in Play Mode:
1. **Four Directions & 90° Rotations**: Confirm `RotateClockwise()` steps through `(0, 1) -> (1, 0) -> (0, -1) -> (-1, 0) -> (0, 1)` and `RotateCounterClockwise()` steps in reverse, updating the force arrow transform rotation at each step.
2. **Locked-State Behavior**: With `IsBossConditionMet == false`, align the force arrow with `_requiredSocketDirection`. Confirm `IsSolved` and `CoreExtractionUnlocked` remain `false`, `TryExtractCore()` returns `false`, and locked feedback is shown.
3. **Incorrect Alignment (No Soft-Lock)**: With `IsBossConditionMet == true`, select a non-matching direction. Confirm `State == PuzzleState.InProgress`, `OnIncorrectAlignment` fires, and subsequent inputs still work.
4. **Correct Alignment & Extraction**: Select `_requiredSocketDirection` while `IsBossConditionMet == true`. Confirm `IsSolved == true`, `CoreExtractionUnlocked == true`, `OnCoreExtractionUnlocked` and `OnPuzzleSolved` fire once, and `TryExtractCore()` succeeds once.
