# OBLIVION'S EDGE

[![Unity Version](https://img.shields.io/badge/Unity-2022.3.40f1%20LTS-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-PC%20Windows%20%7C%20Mac%20%7C%20Linux-brightgreen.svg)]()
[![Branch Model](https://img.shields.io/badge/Git%20Flow-Feature%20Branches-orange.svg)]()

> **OBLIVION'S EDGE** is a 2D top-down sci-fi survival horror game set aboard a derelict orbital research station consumed by bio-synthetic corruption and psychological dread.

---

## 1. Engine & Environment Specification

- **Unity Target Version:** `Unity 2022.3.40f1 LTS`
- **Render Pipeline:** 2D Renderer / Built-in 2D with Sprite & Tilemap packages
- **Scripting Backend:** Mono / .NET Standard 2.1
- **Physics Engine:** 2D Physics (Rigidbody2D / BoxCollider2D / CircleCollider2D)

> **Workstation Note:** On the initialization machine, local standalone Unity Editor executable was not pre-installed in default paths. The complete project foundation, package manifest (`Packages/manifest.json`), ProjectSettings (`ProjectVersion.txt`, `TagManager.asset`, `Physics2DSettings.asset`, `EditorBuildSettings.asset`), deterministic `.meta` files, C# architecture contracts, and launch scene (`Assets/Scenes/MainScene.unity`) have been initialized to ensure 100% plug-and-play opening upon cloning with Unity 2022.3.40f1 LTS.

---

## 2. Quickstart & Setup Instructions

Follow these steps to set up and run the project locally:

### Prerequisites
1. **Unity Hub**: Download and install [Unity Hub](https://unity.com/download).
2. **Unity 2022.3.40f1 LTS**: Install the editor via Unity Hub (`Install Editor` -> `2022.3.40f1`). Ensure the 2D development module is checked.
3. **Git**: Ensure Git 2.20+ is installed on your system.
4. **IDE**: Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit.

### Project Setup
1. **Clone the repository:**
   ```bash
   git clone https://github.com/skandakn/Oblivion-s-Edge.git
   cd Oblivion-s-Edge
   ```
2. **Verify active branch:**
   Ensure you are on the latest `main` branch before branching off:
   ```bash
   git checkout main
   git pull origin main
   ```
3. **Add to Unity Hub:**
   - Launch **Unity Hub**.
   - Click the **Add** button (or `Add project from disk`).
   - Select the repository root folder (`Oblivion-s-Edge`).
   - Select Unity version `2022.3.40f1`.
4. **Open and Compile:**
   - Click on `Oblivion's Edge` in Unity Hub.
   - Wait for Unity to import packages and generate local library cache files (`Library/` and `Temp/`).
   - Open the primary scene at `Assets/Scenes/MainScene.unity`.
   - Press **Play** in the Unity Editor to confirm clean compilation and execution.

---

## 3. Controls & Input Mapping

Configured in `ProjectSettings/InputManager.asset` and defined in `OblivionsEdge.Core.GameConstants`:

| Action | Primary Key / Mouse | Secondary / Gamepad |
|---|---|---|
| **Move Up / Down** | `W` / `S` | `Up` / `Down` Arrow / Left Stick |
| **Move Left / Right** | `A` / `D` | `Left` / `Right` Arrow / Left Stick |
| **Aim Direction** | Mouse Cursor | Right Analog Stick |
| **Primary Fire / Attack** | Left Mouse Button | `Left Ctrl` / Right Trigger |
| **Secondary Aim / Alt Fire**| Right Mouse Button | `Left Alt` / Left Trigger |
| **Interact / Examine** | `E` | Gamepad Button South (`A` / `Cross`) |
| **Reload Weapon** | `R` | Gamepad Button West (`X` / `Square`) |
| **Open Inventory** | `Tab` or `I` | Gamepad Button North (`Y` / `Triangle`) |
| **Open Crafting** | `C` | Gamepad Button East (`B` / `Circle`) |
| **Sprint / Evade** | `Left Shift` | Left Stick Click (`L3`) |
| **Pause Game** | `Escape` or `P` | `Start` Button |

---

## 4. Repository Structure

The project root directly houses the Unity project files without nested subfolders:

```
Oblivion-s-Edge/
├── Assets/
│   ├── Art/                 # Sprites, animations, and materials
│   │   ├── Animations/
│   │   ├── Materials/
│   │   └── Sprites/
│   ├── Audio/               # SFX, ambient soundscapes, dynamic BGM
│   │   ├── BGM/
│   │   └── SFX/
│   ├── Documentation/       # In-editor references to architectural contracts
│   ├── Prefabs/             # Game prefabs (Player, Weapons, Enemies, Boss, Props)
│   ├── Scenes/              # Game scenes
│   │   └── MainScene.unity  # Primary launchable bootstrap scene
│   ├── Scripts/             # Modular C# codebase
│   │   ├── Boss/            # Boss AI, phase listener, encounter triggers
│   │   ├── Core/            # GameManager, constants, IDamageable, IInteractable
│   │   ├── Crafting/        # Workbench station, recipes, resource consumption
│   │   ├── Inventory/       # Item instances, slot storage, encumbrance limits
│   │   ├── Player/          # Player movement, stamina, sanity, oxygen
│   │   ├── Puzzles/         # Environmental locks, power grids, terminals
│   │   ├── UI/              # Heads-Up Display (HUD), modal manager, menus
│   │   └── Weapons/         # Weapon state machine, ammo logic, firing modes
│   └── UI/                  # UI fonts, icons, HUD textures, canvas prefabs
├── Documentation/
│   └── TEAM_CONTRACTS.md    # Comprehensive system contracts & API specs
├── Packages/
│   └── manifest.json        # Unity 2D package dependencies
├── ProjectSettings/         # ProjectVersion, TagManager, InputManager, Physics2D
├── .gitattributes           # Unity YAML and binary asset handling
├── .gitignore               # Strict exclusion of Library, Temp, Obj, Logs, IDEs
└── README.md                # Project documentation and team onboarding
```

---

## 5. Architectural Contracts & Team Ownership

Each subsystem is encapsulated within its own namespace and governed by an authoritative contract:

| Subsystem | Namespace | Primary Interfaces | Responsible Contributor |
|---|---|---|---|
| **Core Architecture** | `OblivionsEdge.Core` | `IDamageable`, `IInteractable`, `GameConstants`, `GameManager` | Member 1 (Tech Lead) |
| **Player Locomotion & Vitals** | `OblivionsEdge.Player` | `IPlayerController`, `IPlayerStats` | Member 2 |
| **Weapons & Combat** | `OblivionsEdge.Weapons` | `IWeapon`, `IWeaponHandler`, `DamageInfo`, `WeaponType` | Member 2 |
| **Boss & Enemy AI** | `OblivionsEdge.Boss` | `IBossController`, `IBossPhaseListener`, `BossPhase` | Member 3 |
| **Puzzles & Environment** | `OblivionsEdge.Puzzles` | `IPuzzleController`, `IPuzzleElement`, `PuzzleState` | Member 4 |
| **Inventory & Items** | `OblivionsEdge.Inventory` | `IInventory`, `IItem`, `ItemData`, `ItemCategory` | Member 5 |
| **Crafting & Upgrades** | `OblivionsEdge.Crafting` | `ICraftingStation`, `CraftingRecipe` | Member 5 |
| **UI & Audio Presentation** | `OblivionsEdge.UI` | `IUIManager`, `IHUDView`, `UIPanelType` | Member 6 |

For complete code contracts, method signatures, sequence diagrams, and collision matrices, consult [Documentation/TEAM_CONTRACTS.md](Documentation/TEAM_CONTRACTS.md).

---

## 6. Git Workflow & Collaboration Protocol

To maintain project stability across all contributors:

### 1. Synchronize Before Creating Branches
All team members **must clone or pull the latest `main` baseline** before creating their feature branches:
```bash
git checkout main
git pull origin main
git checkout -b feature/<member>-<feature-name>
```

### 2. Branch Naming Rules
- Feature branches: `feature/m2-movement`, `feature/m3-boss-phase1`, etc.
- Bugfix branches: `fix/m5-inventory-stack`, `fix/m6-hud-ammo-refresh`, etc.
- Infrastructure: `setup/<name>`

### 3. Golden Rule of Unity `.meta` Files
- **Every asset file must have a `.meta` file.**
- **Never manually delete or regenerate existing `.meta` files.**
- When renaming or moving assets, do so inside the Unity Editor or update both the asset and `.meta` file simultaneously.
- Always check `git status` before committing to verify that all `.meta` files are staged.

### 4. Integration & Pull Request Guidelines
- Do not implement systems belonging to other members. Implement your assigned contracts.
- Ensure your code builds without warnings or errors before opening a Pull Request.
- PRs must be reviewed by Member 1 (Technical Lead) before merging into `main`.
