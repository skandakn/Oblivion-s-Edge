# OBLIVION'S EDGE — Edge Component Puzzle Setup & Architectural Guide

**Author:** Member 6 (Edge Puzzle & Environmental Mechanics)  
**Target Engine:** Unity 2022.3.40f1 LTS (2D Sci-Fi Survival Horror)  
**Branch:** `feature/edge-puzzle`  
**Namespace:** `OblivionsEdge.Puzzles` & `OblivionsEdge.Core`  

---

## 1. Executive Summary

The **Edge Component Puzzle** guards the critical storyline artifact—the **Edge Component Matrix** (`item_edge_component`)—behind a pneumatic security vault on the derelict orbital research station.

To bypass the vault's lockdown, the player must redirect an optical laser beam emitted from a high-energy optical emitter into a target sensor terminal using a series of player-rotatable specular mirrors.

### Core Architecture & Compliance:
1. **Physical Reflection Mathematics:** Implements the exact specular reflection vector formula:
   $$\vec{r} = \vec{d} - 2(\vec{d} \cdot \vec{n})\vec{n}$$
   where $\vec{d}$ is the incoming unit direction vector, $\vec{n}$ is the normalized surface normal, and $\vec{r}$ is the reflected unit direction.
2. **Team Contract Compliance:** Implements `OblivionsEdge.Puzzles.IPuzzleController`, `OblivionsEdge.Puzzles.IPuzzleElement`, and `OblivionsEdge.Core.IInteractable`.
3. **Decoupled Inventory Pipeline:** Exposes C# events and completion hooks for **Member 9** (`OnEdgeComponentUnlocked`, `OnEdgeComponentClaimed`, and `TryClaimEdgeComponent`) without creating circular dependencies.
4. **Scene Isolation:** Strictly avoids modifying shared master scenes (`Assets/Scenes/MainScene.unity`). All functionality is self-contained in `Assets/Prefabs/Puzzles/EdgePuzzle.prefab`.

---

## 2. Mathematical Ray Reflection Specification

### 2.1 The Specular Law of Reflection
When an optical laser beam hits a mirror surface:
- $\vec{d}$: Normalized incoming ray direction vector.
- $\vec{n}$: Normalized outward surface normal vector at the point of impact.
- $(\vec{d} \cdot \vec{n})$: Scalar projection of the incoming vector onto the surface normal.
- $\vec{r} = \vec{d} - 2(\vec{d} \cdot \vec{n})\vec{n}$: Reflected vector.

Implemented in `Assets/Scripts/Puzzles/LaserReflectionMath.cs`:
```csharp
public static Vector2 Reflect2D(Vector2 incomingDirection, Vector2 surfaceNormal)
{
    Vector2 d = incomingDirection.normalized;
    Vector2 n = surfaceNormal.normalized;
    float dot = Vector2.Dot(d, n);
    return (d - 2f * dot * n).normalized;
}
```

### 2.2 Geometric Proof for 90-Degree Beam Redirection
In the standard puzzle layout:
- **Segment 1:** Beam is projected East ($\vec{d}_1 = (1, 0)$).
- **Mirror 1:** Positioned at $(4, 0)$, angled at $135^\circ$ with surface normal pointing Up-Left ($\vec{n}_1 = (-\frac{1}{\sqrt{2}}, \frac{1}{\sqrt{2}})$).
  $$\vec{d}_1 \cdot \vec{n}_1 = 1 \cdot \left(-\frac{1}{\sqrt{2}}\right) = -\frac{1}{\sqrt{2}}$$
  $$\vec{r}_1 = (1, 0) - 2\left(-\frac{1}{\sqrt{2}}\right)\left(-\frac{1}{\sqrt{2}}, \frac{1}{\sqrt{2}}\right) = (1, 0) - (1, -1) = (0, 1) \quad (\text{North})$$
- **Segment 2:** Beam travels North ($\vec{d}_2 = (0, 1)$).
- **Mirror 2:** Positioned at $(4, 4)$, angled at $225^\circ$ with surface normal pointing Down-Left ($\vec{n}_2 = (-\frac{1}{\sqrt{2}}, -\frac{1}{\sqrt{2}})$).
  $$\vec{d}_2 \cdot \vec{n}_2 = 1 \cdot \left(-\frac{1}{\sqrt{2}}\right) = -\frac{1}{\sqrt{2}}$$
  $$\vec{r}_2 = (0, 1) - 2\left(-\frac{1}{\sqrt{2}}\right)\left(-\frac{1}{\sqrt{2}}, -\frac{1}{\sqrt{2}}\right) = (0, 1) - (1, 1) = (-1, 0) \quad (\text{West})$$
- **Receiver:** Positioned at $(0, 4)$ facing East. The arriving beam ($\vec{r}_2 = (-1, 0)$) enters directly into the receiver aperture ($\vec{n}_{\text{aperture}} = (1, 0)$), satisfying acceptance angle $\theta \le 45^\circ$.

---

## 3. Subsystem Scripts Overview

All scripts reside in `Assets/Scripts/Puzzles/`:

| Script File | Primary Role | Interfaces Implemented |
|---|---|---|
| `LaserReflectionMath.cs` | Pure math utility for reflection vector calculations | Static Class |
| `LaserEmitter.cs` | Raycasts beam with bounces, renders path via `LineRenderer`, updates color telemetry | MonoBehaviour |
| `RotatableMirror.cs` | Interactive mirror reflecting incoming lasers, stepped rotation upon player interact | `IPuzzleElement`, `IInteractable` |
| `LaserReceiver.cs` | Sensor aperture detecting beam arrival within acceptance cone, triggers status lights | MonoBehaviour |
| `VaultDoor.cs` | Pneumatic bulkhead barrier that retracts and unblocks player passage when puzzle is solved | MonoBehaviour |
| `EdgeComponentReward.cs`| In-world relic artifact unlocked inside the vault, interactable pickup hook for Member 9 | `IInteractable` |
| `EdgePuzzleController.cs` | Master orchestrator coordinating emitter, mirrors, receiver, vault, and reward | `IPuzzleController` |
| `EdgePuzzleVerification.cs`| Automated simulation harness validating math, correct solutions, and error arrangements | MonoBehaviour |

---

## 4. Prefab Hierarchy (`Assets/Prefabs/Puzzles/`)

The composite prefab `EdgePuzzle.prefab` contains all components pre-wired:

```text
EdgePuzzle (EdgePuzzleController, EdgePuzzleVerification)
├── LaserEmitter (Transform, LineRenderer, LaserEmitter)
├── Mirror_01 (Transform, BoxCollider2D, RotatableMirror [initial: 0°, target: 135°])
├── Mirror_02 (Transform, BoxCollider2D, RotatableMirror [initial: 0°, target: 225°])
├── LaserReceiver (Transform, BoxCollider2D, LaserReceiver [facing East])
├── VaultDoor (Transform, BoxCollider2D, VaultDoor)
│   └── DoorSlab (Sliding bulkhead)
├── EdgeComponentReward (Transform, CircleCollider2D, EdgeComponentReward)
├── SuccessFeedbackBeacon (Active upon puzzle solution)
└── StandbyHazardBeacon (Active while unsolved)
```

---

## 5. Scene Setup Guide

To instantiate and use the Edge Puzzle in any testing or level scene:

1. **Open your level or sandbox scene** (e.g., `SandboxScene.unity`). **Never modify `MainScene.unity` directly**.
2. **Drag `Assets/Prefabs/Puzzles/EdgePuzzle.prefab`** into the Scene Hierarchy.
3. **Position the root GameObject** at your desired room coordinates.
4. **Collision Layer Setup:** Ensure `ProjectSettings/TagManager.asset` layers match the team contracts:
   - Mirrors are on Layer `Interactable` (Layer 9).
   - Receiver & Walls are on Layer `Environment` (Layer 8) or `Default` (Layer 0).
   - Player is on Layer `Player` (Layer 6).
5. **Adjust Solution Tolerances (Inspector):**
   - In `EdgePuzzleController`:
     - `Solve Confirmation Time`: `0.25s` (avoids transient solve flicker).
     - `Lock On Solve`: `true` (keeps vault open once solved).
     - `Lock Mirrors On Solve`: `true` (prevents altering solved mirrors).

---

## 6. Test Procedures & Verification

### Test 1: Mathematical Reflection Formula
- **Execution:** Open context menu on `EdgePuzzleVerification` component -> click **"Run All Puzzle Tests"**.
- **Expected Result:**
  - Case A (East hitting West normal) -> Reflected East to West $(-1, 0)$.
  - Case B (East hitting $45^\circ$ normal) -> Reflected East to North $(0, 1)$.
  - Case C (North hitting $45^\circ$ normal) -> Reflected North to West $(-1, 0)$.
  - Console prints: `[Test 1] PASSED`.

### Test 2: Correct Solution Arrangement
- **Execution:** Rotate `Mirror_01` to $135^\circ$ and `Mirror_02` to $225^\circ$.
- **Expected Result:**
  1. Laser propagates: Emitter $(0,0) \to$ Mirror 1 $(4,0) \to$ Mirror 2 $(4,4) \to$ Receiver $(0,4)$.
  2. Laser beam turns from cyan (`#1ABEFF`) to neon emerald (`#26FF73`).
  3. `LaserReceiver` indicator turns from red to green.
  4. `VaultDoor` unseals, slides open $+3$ units vertically, and disables its barrier collider.
  5. `EdgeComponentReward` unlocks its interaction collider and enables its relic glow aura.
  6. `EdgePuzzleController.OnPuzzleSolved` fires.

### Test 3: Incorrect Misalignment (Failure Mode)
- **Execution:** Set `Mirror_01` to $0^\circ$ or $90^\circ$ (default initial state).
- **Expected Result:**
  1. Laser either reflects parallel to the mirror plane or strikes the non-reflective back face.
  2. Beam terminates without reaching `Mirror_02`.
  3. `LaserReceiver` remains unpowered (red indicator).
  4. `VaultDoor` remains tightly sealed with active collision barrier.
  5. `EdgeComponentReward` remains locked and non-interactable.
  6. `EdgePuzzleController.IsSolved` remains `false`.

### Test 4: In-Game Interactive Stepping (Player 'E' Input)
- **Execution:** Approach `Mirror_01` with Player avatar and press `E` (or `GameConstants.InputInteract`).
- **Expected Result:**
  - Mirror rotates $45^\circ$ per press: $0^\circ \to 45^\circ \to 90^\circ \to 135^\circ$.
  - Upon reaching $135^\circ$, beam instantly hits `Mirror_02`.
  - Next, approach `Mirror_02` and press `E`: rotates $0^\circ \to 45^\circ \to \dots \to 225^\circ$.
  - Upon reaching $225^\circ$, beam connects to receiver and solves the puzzle.

---

## 7. Member 9 Inventory Integration Contract

Member 9 (Inventory & Items owner) should connect their inventory granting logic using either the event or the direct claim hook:

### Option A: Subscribing to Unlocked or Claimed Events (Recommended)
```csharp
using UnityEngine;
using OblivionsEdge.Puzzles;
using OblivionsEdge.Inventory;

public class Member9InventoryBridge : MonoBehaviour
{
    [SerializeField] private EdgePuzzleController _edgePuzzle;
    [SerializeField] private IInventory _playerInventory;

    private void OnEnable()
    {
        if (_edgePuzzle != null)
        {
            // Triggered when player extracts the relic from the vault:
            _edgePuzzle.OnEdgeComponentClaimed += HandleComponentClaimed;
        }
    }

    private void OnDisable()
    {
        if (_edgePuzzle != null)
        {
            _edgePuzzle.OnEdgeComponentClaimed -= HandleComponentClaimed;
        }
    }

    private void HandleComponentClaimed(string itemId)
    {
        Debug.Log($"[Member 9] Awarding unlocked quest relic: {itemId}");
        _playerInventory?.AddItemById(itemId, 1);
    }
}
```

### Option B: Direct Verification Query
```csharp
if (edgePuzzleController.IsSolved)
{
    // Puzzle completed; safe to inspect or award quest progression
}
```
