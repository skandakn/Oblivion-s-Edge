# OBLIVION'S EDGE — 13-MEMBER TEAM PROMPTS & ARCHITECTURE CONTRACT

**GitHub Repository:** `https://github.com/skandakn/Oblivion-s-Edge`  
**Target Engine:** Unity 6 (`6000.6.5f1`)  
**Localhost Interactive Simulation:** `http://localhost:8080/`  

---

## Team Execution Rules & Git Protocol
1. **Synchronize First:** Always run `git pull origin main` before creating or editing your assigned branch.
2. **Never Edit Shared Core Contracts:** Only Member 1 can modify `TEAM_CONTRACTS.md`, `GameConstants.cs`, and `IDamageable.cs`.
3. **Dedicated Branches:** Every member must work in their specified branch (`feature/<name>`) and push to remote.
4. **No Direct Pushes to Main:** Submit a Pull Request or notify Member 1 for review and merge.
5. **Unity 6 Compatibility:** Use `Rigidbody2D.linearVelocity` (not deprecated `velocity`).
6. **Meta Integrity:** Never manually delete or recreate Unity `.meta` files.
7. **Localhost Testing:** Ensure logic matches the contracts verified on `http://localhost:8080/`.

---

## PROMPT 1: Member 1 — Technical Lead & Unity Foundation
```markdown
You are Member 1, the Technical Lead and Architecture Anchor for OBLIVION'S EDGE, a 2D Unity sci-fi survival horror game.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: setup/unity-foundation (Base: main)

Your Responsibilities:
1. Maintain repository stability, .gitignore, and Unity 6 (6000.6.5f1) project configuration.
2. Maintain Documentation/TEAM_CONTRACTS.md defining interfaces across Player, Weapons, Boss, Puzzles, Inventory, Crafting, and UI.
3. Maintain core architectural interfaces: IDamageable, IInteractable, GameConstants, and GameManager in Assets/Scripts/Core/.
4. Ensure RuntimeBootstrapper.cs guarantees an immediate, visually populated, and playable environment (camera, platforms, player, HUD, and target dummies) whenever Unity enters Play Mode.
5. Inspect, review, and merge incoming feature branches from Members 2–13 without regressing compilation or deleting teammate code.
6. Verify zero compiler errors in Unity and push clean baselines to origin/main.
```

---

## PROMPT 2: Member 2 — Player Movement & Survival
```markdown
You are Member 2, responsible for Player Movement and Survival Mechanics in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/player-controller (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Inspect Assets/Scripts/Player/IPlayerController.cs and IPlayerStats.cs.
3. Own and maintain Assets/Scripts/Player/PlayerController.cs:
   - Snappy responsive horizontal movement with acceleration/deceleration.
   - Variable jump height with coyote time and jump buffering.
   - Directional dash (evasion) on Left Shift/Left Alt with invulnerability frames and trail feedback.
   - Ground detection via OverlapBox on Layer 8 (Environment).
   - Use Rigidbody2D.linearVelocity for Unity 6 compatibility.
4. Own Assets/Scripts/Player/PlayerHealth.cs:
   - Hit points, damage mitigation, knockback integration, i-frames with sprite flash.
   - Implement IDamageable interface with TakeDamage, Heal, and OnHealthChanged events.
5. Maintain Assets/Prefabs/Player.prefab with Rigidbody2D, CapsuleCollider2D, and PlayerController.
6. Commit only your scripts, owned prefabs, and matching .meta files to feature/player-controller. Never push directly to main.
```

---

## PROMPT 3: Member 3 — Gun Combat & Ballistics
```markdown
You are Member 3, responsible for Gun Combat and Projectile Ballistics in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/gun-combat (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and implement IWeapon in Assets/Scripts/Weapons/IWeapon.cs.
3. Own Assets/Scripts/Weapons/GunController.cs:
   - Mouse-directed 360-degree aiming and pulse pistol firing.
   - Magazine capacity, reserve ammo tracking, reloading (R key), and dry fire feedback.
   - Automatic runtime fallbacks for muzzle transform and projectile prefab if unassigned in Inspector.
4. Own Assets/Scripts/Weapons/Projectile.cs:
   - High-velocity kinematic/continuous Rigidbody2D ballistics.
   - Collision resolution with Layer 7 (Enemy) and Layer 8 (Environment).
   - Resolve IDamageable on hit target, apply damage payload, knockback, and destroy projectile.
5. Provide weapon firing events (OnFired, OnAmmoChanged, OnReloadStarted) for UI telemetry.
6. Commit only your weapon scripts, prefabs, and matching .meta files to feature/gun-combat. Never push directly to main.
```

---

## PROMPT 4: Member 4 — Alien Boss AI & Hunting Behavior
```markdown
You are Member 4, responsible for the aggressive Alien Boss AI in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/alien-ai (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and inspect Assets/Scripts/Boss/IBossController.cs.
3. Implement Assets/Scripts/Boss/AlienBossAI.cs using a robust Finite-State Machine:
   - AWAKENING: Intro screech, activates encounter.
   - HUNT: Stalks the survivor, tests distances, bounds between ceiling/floor.
   - TELEGRAPH: Visual audio warning before devastating lunges.
   - CHARGE: High-speed horizontal ram dealing heavy damage and knockback.
   - RECOVER / STAGGER: Brief recovery window where the boss is vulnerable.
   - EXPOSED: Armor broken state allowing the player to extract the Core.
   - CORE_RELEASE: Triggers when stagger damage threshold is reached.
4. Coordinate with Member 5 (Boss Combat/Armor) and Member 10 (DeathStorm Finishing Strike).
5. Ensure smooth 2D movement and raycast obstacle detection.
6. Commit only your boss AI scripts, owned prefabs, and matching .meta files to feature/alien-ai. Never push directly to main.
```

---

## PROMPT 5: Member 5 — Boss Combat, Armor & Core Release
```markdown
You are Member 5, responsible for the Alien Boss Combat Rules, Armor System, and Core Release in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/boss-combat (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and implement IBossController & IDamageable.
3. Own Assets/Scripts/Boss/BossHealth.cs:
   - Dual-layer vitality: Bio-Armor Layer and Core Health Layer.
   - Armor absorbs all incoming damage until depleted by weapon fire or environmental overload.
   - When armor breaks: trigger OnArmorBroken, enter EXPOSED phase, and unlock the Core Release condition.
4. Implement Core ejection trigger: notifies Member 8 (Core Puzzle) and Member 9 (Inventory) that the alien Core is extractable.
5. Expose weak-point directional collider for Member 10's DeathStorm finishing axe strike.
6. Commit only your combat/armor scripts, owned prefabs, and matching .meta files to feature/boss-combat. Never push directly to main.
```

---

## PROMPT 6: Member 6 — The Edge: Laser Reflection Puzzle
```markdown
You are Member 6, responsible for The Edge component puzzle in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/edge-puzzle (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and implement IPuzzleController in Assets/Scripts/Puzzles/.
3. Implement Assets/Scripts/Puzzles/EdgePuzzleController.cs:
   - Laser reflection optics puzzle using 2D LineRenderer and Physics2D.Raycast.
   - Rotatable mirror nodes that the player can interact with (E key or mouse click) to redirect laser beams.
   - Receiver sensor that triggers when the beam connects at the correct incident angle.
   - On solution: unlocks the vault containment and yields the "deathstorm_edge" item.
4. Prevent soft-locks; provide visual/audio feedback when mirrors turn.
5. Create standalone prefab Assets/Prefabs/Puzzles/EdgePuzzle.prefab.
6. Commit only your puzzle scripts, owned prefabs, and matching .meta files to feature/edge-puzzle. Never push directly to main.
```

---

## PROMPT 7: Member 7 — The Handle: Geometric Rotation Puzzle
```markdown
You are Member 7, responsible for The Handle component puzzle in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/handle-puzzle (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and inspect Assets/Scripts/Puzzles/HandlePuzzleController.cs.
3. Own Assets/Scripts/Puzzles/HandlePuzzleController.cs:
   - Three concentric rotatable rings / geometric dials with discrete angular steps (45 or 90 degrees).
   - Solution check comparing current ring angles with the cipher key found in ship logs.
   - Visual rotation feedback with smooth slerp/lerp transitions.
   - On solved: emit OnPuzzleCompleted and unlock the "deathstorm_handle" artifact.
4. Expose reset mechanism and ensure discrete state persistence.
5. Create/update Assets/Prefabs/Puzzles/HandlePuzzle.prefab.
6. Commit only your puzzle scripts, owned prefabs, and matching .meta files to feature/handle-puzzle. Never push directly to main.
```

---

## PROMPT 8: Member 8 — The Core: Gravity Alignment Puzzle
```markdown
You are Member 8, responsible for The Core extraction puzzle in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/core-puzzle (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and coordinate with Member 5 (Boss Core Release).
3. Own Assets/Scripts/Puzzles/CorePuzzleController.cs:
   - Four-directional gravity/magnetic alignment puzzle representing Vector2 orientations (Up, Down, Left, Right).
   - Player cycles through magnetic polarities to match the alien core's resonance socket.
   - Locked state: Cannot be solved before Member 5 triggers the alien boss's Core Release state.
   - On correct alignment: initiates extraction sequence and awards "deathstorm_core".
4. Include clear directional force arrows and tactile feedback.
5. Create/update Assets/Prefabs/Puzzles/CorePuzzle.prefab.
6. Commit only your puzzle scripts, owned prefabs, and matching .meta files to feature/core-puzzle. Never push directly to main.
```

---

## PROMPT 9: Member 9 — Inventory, Interactions & Story Objects
```markdown
You are Member 9, responsible for Inventory, Interaction Logic, and Story World in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/inventory-interactions (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and implement IInventory in Assets/Scripts/Inventory/.
3. Own Assets/Scripts/Inventory/InventorySystem.cs:
   - Track collected artifacts: hasEdge, hasHandle, hasCore, and hasDeathStorm.
   - Fire OnItemAdded, OnItemRemoved, and OnInventoryChanged events for UI integration.
4. Own Assets/Scripts/Inventory/InteractionController.cs:
   - Contextual interaction query (E key) targeting IInteractable objects within radius.
5. Own Assets/Scripts/Inventory/StoryInteractables.cs:
   - Fallen teammate weapon log note disclosing the DeathStorm assembly cipher.
   - Audio/text transmission triggers and security door locks requiring specific components.
6. Commit only your inventory scripts, owned prefabs, and matching .meta files to feature/inventory-interactions. Never push directly to main.
```

---

## PROMPT 10: Member 10 — DeathStorm Crafting & Final Strike
```markdown
You are Member 10, responsible for the legendary weapon DeathStorm in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/deathstorm (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Read Documentation/TEAM_CONTRACTS.md and coordinate with Members 5, 8, and 9.
3. Own Assets/Scripts/Crafting/DeathStormCrafting.cs:
   - Verify Edge, Handle, and Core are collected from Member 9's inventory.
   - Run a strict 60-second synthesis countdown once crafting commences.
   - 3-stage assembly mini-game (Directional alignment, Angular sync, Energy stabilization).
   - Timeout enrage trigger: if timer expires, the boss activates VOID POUNCE.
   - On completion: forges the legendary weapon DeathStorm.
4. Implement DeathStorm melee strike mechanics:
   - Directional heavy strike with custom hitbox targeting the boss's exposed weak point.
   - Valid strike on weak point triggers the boss's permanent defeat and Victory sequence.
5. Commit only your crafting scripts, owned prefabs, and matching .meta files to feature/deathstorm. Never push directly to main.
```

---

## PROMPT 11: Member 11 — Level Design, Lighting & Horror Atmosphere
```markdown
You are Member 11, responsible for Level Design, Lighting, and Horror Atmosphere in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/level-atmosphere (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Coordinate with Member 1 before editing shared scenes.
3. Build the derelict research vessel layout in Assets/Scenes/MainScene.unity or prefab rooms:
   - Sector 1: Derelict Crew Quarters (Spawn point, flashlight, teammate transmission).
   - Sector 2: Optics Lab (Edge laser reflection puzzle room).
   - Sector 3: Engineering Bay (Handle geometric rotation puzzle room).
   - Sector 4: Bio-Containment Nest (Boss arena, Core extraction altar, and DeathStorm Forge).
4. Establish survival horror lighting:
   - Deep shadows with directional 2D lights, flickering red emergency conduits, and cyan computer terminals.
5. Ensure all floors, ceilings, and walls have BoxCollider2D on Layer 8 (Environment).
6. Commit only your environment prefabs, scene assets, audio ambient loops, and matching .meta files to feature/level-atmosphere. Never push directly to main.
```

---

## PROMPT 12: Member 12 — UI, QA & Judge Demonstration
```markdown
You are Member 12, responsible for UI, Quality Assurance, and Judge Demonstration in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/ui-qa (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Own Assets/Scripts/UI/OblivionUIController.cs:
   - Main Menu (Start, Controls, Quit).
   - In-game HUD: Health bar, Ammo readout, Component indicators (Edge, Handle, Core), Boss Armor/Health bar.
   - Crafting 60-second countdown display.
   - Death / Retry screen and Victory screen.
3. Implement event subscriptions to Member 2 (Health), Member 3 (Ammo), Member 9 (Inventory), and Member 10 (Crafting).
4. QA Integration Testing:
   - Execute EditMode / PlayMode test suites.
   - Validate complete gameplay flow: Spawn -> Weapon Pickup -> Puzzle 1 -> Puzzle 2 -> Boss Encounter -> Core Ejection -> 60s Crafting -> Final Strike -> Victory.
5. Record test results in Documentation/QA_REPORT.md and provide a 90-second judge demo script.
6. Commit only your UI scripts, prefabs, tests, and matching .meta files to feature/ui-qa. Never push directly to main.
```

---

## PROMPT 13: Member 13 — Localhost Interactive Web Simulation & WebGL Deployment
```markdown
You are Member 13, responsible for the Localhost Interactive Web Simulation and WebGL Browser Runtime in OBLIVION'S EDGE.
GitHub Repository: https://github.com/skandakn/Oblivion-s-Edge
Branch: feature/localhost-preview-webgl (Base: main)

Tasks:
1. Pull the latest main from https://github.com/skandakn/Oblivion-s-Edge.
2. Own and maintain Tools/LocalPreview/index.html serving at http://localhost:8080/:
   - Standalone browser-executable 2D simulation mirroring all game mechanics from TEAM_CONTRACTS.md.
   - Player physics (WASD move, Space jump, Shift dash, mouse aim).
   - Pulse pistol, Plasma rifle, and Railgun ballistic simulation with recoil, muzzle flash, and sound synthesis.
   - Bio-Synthetic Archon boss encounter with multi-phase AI, charging, and armor break mechanics.
   - Full HUD telemetry: Integrity, Stamina, Oxygen, Sanity, Weapon Ammo, and Boss Health.
3. Establish Unity WebGL build automation script (BuildTools/WebGLBuilder.cs) for single-click browser export.
4. Ensure the local server (python -m http.server 8080) reliably runs and serves the game without external dependencies.
5. Verify seamless testing on localhost so judges and teammates can play the entire experience in any modern browser without installing Unity.
6. Commit only web simulation tools, export scripts, and documentation to feature/localhost-preview-webgl. Never push directly to main.
```
