# Member 2: Player Controller & Health Test Procedure

**Subsystem:** Player Locomotion, Kinematics, Health & Interaction (`OblivionsEdge.Player`)  
**Assigned Owner:** Member 2  
**Target Engine:** Unity 2022.3 LTS  
**Branch:** `feature/player-controller`  

---

## 1. Overview of Delivered Assets

| File Path | Description |
|---|---|
| `Assets/Scripts/Player/PlayerController.cs` | 2D side-scrolling physics locomotion, ground check, optional jump, mouse-facing, dash/dodge, interactables scan, animation hooks. |
| `Assets/Scripts/Player/PlayerController.cs.meta` | Unity metadata for PlayerController. |
| `Assets/Scripts/Player/PlayerHealth.cs` | Vital health tracking, `IDamageable` implementation, temporary invulnerability (i-frames), sprite flash feedback, death handling. |
| `Assets/Scripts/Player/PlayerHealth.cs.meta` | Unity metadata for PlayerHealth. |
| `Assets/Prefabs/Player.prefab` | Pre-configured Player GameObject ready to drag-and-drop into test or gameplay scenes. |
| `Assets/Prefabs/Player.prefab.meta` | Unity metadata for Player prefab. |

---

## 2. Quick Setup & Integration Instructions

1. **Open Scene / Test Environment**:
   - Open `Assets/Scenes/MainScene.unity` or create an empty test scene with a 2D floor (BoxCollider2D on layer `Environment` or layer `Default`).
2. **Add Player**:
   - Drag `Assets/Prefabs/Player.prefab` from `Assets/Prefabs/` into the scene hierarchy at `(0, 0, 0)`.
3. **Camera Setup**:
   - Ensure the Main Camera is tagged `MainCamera` and positioned at `(0, 0, -10)` with an Orthographic projection.

---

## 3. Step-by-Step Test Procedure

### Test 1: Responsive Side-Scrolling Locomotion
- **Action:** Press `A` / `D` or `Left Arrow` / `Right Arrow`.
- **Expected Result:**
  - Player moves smoothly left/right at configured `Move Speed` (default 7.5).
  - Velocity accelerates and decelerates crisply with zero sluggishness.
- **Sprint Test:** Hold `Left Shift` while moving.
  - Expected: Player speeds up by 1.45x (`_sprintMultiplier`).
- **Crouch Test:** Hold `Left Control` while moving.
  - Expected: Player moves at 0.5x speed (`_crouchMultiplier`).

### Test 2: Ground Detection & Optional Jumping
- **Action:**
  - Press `Space` while grounded on an `Environment` collider.
  - Expected: Player performs an upward leap (`_jumpForce = 12f`).
- **Variable Jump Height:**
  - Tap `Space` briefly vs. hold `Space`.
  - Expected: Releasing early cuts vertical velocity for a lower jump.
- **Coyote Time & Buffer:**
  - Walk off a ledge and press `Space` within 0.15s; or press `Space` 0.15s before landing.
  - Expected: Jump triggers reliably without feeling dropped.
- **Disable Jumping (Level Design Setting):**
  - Uncheck `Enable Jump` in the `PlayerController` Inspector.
  - Expected: `Space` does not trigger a jump (supporting claustrophobic/no-jump levels).

### Test 3: Facing Direction & Mouse-Facing Aim
- **Action:** Move mouse cursor across the screen.
- **Expected Result:**
  - When mouse is to the right of the player, character faces right (`localScale.x > 0`).
  - When mouse is to the left of the player, character flips horizontally (`localScale.x < 0`).
  - If `Aim Pivot` has a child (flashlight or gun), it rotates continuously toward the cursor.
- **Movement-Facing Mode:**
  - Uncheck `Face Mouse Aim` in Inspector.
  - Expected: Character faces moving direction (`A` = face left, `D` = face right).

### Test 4: Dash / Dodge with Cooldown & Feedback
- **Action:** Press `Left Alt` (or configured dash key).
- **Expected Result:**
  - Player dashes in the active movement direction (or facing direction if stationary) across 4.5 units in 0.18s.
  - `TrailRenderer` activates during the dash and emits a glowing blue trail.
  - Rigidbody gravity is suspended during the dash for a crisp horizontal burst.
  - Cooldown timer starts (1.0s); pressing dash again before cooldown finishes is rejected.
  - `OnDashed` event is broadcasted.

### Test 5: Damage, Temporary Invulnerability & Death
- **Action (Taking Damage):**
  - Call `playerHealth.TakeDamage(new DamageInfo(25f, DamageType.Physical, null, transform.position, Vector2.right, 5f))` from a test script or inspector button.
- **Expected Result:**
  - Health drops from 100 to 75.
  - `OnDamageTaken` and `OnHealthChanged` events fire.
  - Directional knockback impulse is applied.
  - Sprite begins flashing at `_flashInterval = 0.08s` with `_flashMinAlpha = 0.25` for 1.0 second.
  - While flashing, subsequent damage payloads are rejected (`IsInvulnerable == true`).
  - Sprite returns to normal full opacity when invulnerability expires.
- **Action (Fatal Damage & Death):**
  - Deal damage exceeding remaining health (e.g., 100 damage).
  - Expected:
    - Health clamps to 0.
    - `OnDied` and `OnDeath` events fire.
    - Player controls are disabled (`DisableControls()`), preventing locomotion.
    - `IsAlive` is set to false.

### Test 6: Interaction Pipeline
- **Action:**
  - Place an object with an `IInteractable` implementation within 2.0 units of the player on layer `Interactable`.
  - Press `E` (or `Interact` button).
- **Expected Result:**
  - `OnInteractableFocusChanged` fires when player moves into range.
  - Pressing `E` executes `interactable.Interact(playerGameObject)`.
