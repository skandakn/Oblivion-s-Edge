# OBLIVION'S EDGE — 90-Second Judge Demo Script
**Member 12 (UI, QA & Judge Demonstration)**  
**Branch:** `feature/ui-qa`  
**Version:** 1.2 — 2026-10-13  

---

> **Pre-flight checklist (before the clock starts):**
> 1. Unity Editor open on `MainScene` in Play Mode.
> 2. `OblivionUIController` Inspector references wired (or auto-discovery active).
> 3. Screen capture or projector mirroring active.
> 4. Stopwatch ready. Total runtime: **~90 seconds**.

---

## Segment 1 — Main Menu & Game Start `[0:00 – 0:10]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 0:00 | Scene loads in Play Mode | **OBLIVION'S EDGE** Main Menu panel visible; HUD hidden |
| 0:04 | Click **START** | Main Menu fades; HUD appears — health bar, ammo, component indicators |
| 0:08 | Point at Controls button, click | Controls overlay opens showing keybindings |
| 0:10 | Click **CLOSE** | Controls overlay closes; player control resumes |

**Judge talking point:** *"Event-driven UI — no polling. The Main Menu panel is just a Canvas group toggled by the GameState event from Member 1's GameManager."*

---

## Segment 2 — Survival HUD: Health & Weapon `[0:10 – 0:22]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 0:10 | Player spawns in Sector 1 — Crew Quarters | `VITALS  100 / 100` — green bar full |
| 0:13 | Walk into alien patrol trigger (or console-inject damage) | Health drains; text updates numerically each frame |
| 0:17 | Reduce player to <25 % health | Bar turns **danger red** |
| 0:19 | Pick up Pulse Pistol from weapon station | Weapon text changes to `PULSE PISTOL`; ammo shows `AMMO  12 / 48` |
| 0:22 | Fire 3 shots | Ammo reads `AMMO  9 / 48` |

**Judge talking point:** *"Health is owned by Member 2. We subscribe to IDamageable.OnHealthChanged — zero coupling to the concrete PlayerHealth class. Same for ammo via Member 3's IWeapon."*

---

## Segment 3 — Component Collection & Inventory Indicators `[0:22 – 0:36]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 0:22 | Show HUD — all three components read `□ MISSING` | Indicators clearly visible in top-right corner |
| 0:25 | Move to Sector 2 (Optics Lab), solve Edge laser puzzle | `EDGE  [■ ONLINE]` lights up instantly |
| 0:30 | Move to Sector 3 (Engineering Bay), solve Handle rotation puzzle | `HANDLE  [■ ONLINE]` |
| 0:34 | Move to Boss Arena entry, approach Core Altar (post boss phase) | `CORE  [■ ONLINE]` |

**Judge talking point:** *"Each indicator fires from Member 9's IInventory.OnItemAdded event. The item IDs are configurable in the Inspector — deathstorm_edge, deathstorm_handle, deathstorm_core."*

---

## Segment 4 — Boss Encounter `[0:36 – 0:55]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 0:36 | Enter Sector 4 — Bio-Containment Nest | Boss health bar slides in: `ALIEN`, full armor + health fills |
| 0:39 | Boss transitions to Phase 1 | Notification banner: **PHASE I — PREDATOR** |
| 0:43 | Shoot boss armor twice | Armor fill drains; armor text updates (`ARMOR  150 / 200`) |
| 0:48 | Armor fully depleted | Notification: **ARMOR FRACTURED — CORE EXPOSED** |
| 0:50 | Boss transitions to VulnerableStun | Phase label: **EXPOSED — STRIKE NOW!** |
| 0:53 | Boss triggers Enrage (let timer run) | Notification: **⚠  BOSS ENRAGED — VOID POUNCE ENABLED!** |

**Judge talking point:** *"All 8 boss phases — Dormant through Defeated — map to distinct UI labels. Boss armor is tracked via Member 5's IBossArmorSource interface."*

---

## Segment 5 — DeathStorm 60-Second Crafting `[0:55 – 1:18]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 0:55 | Interact with DeathStorm Forge (all 3 components online) | Crafting panel opens; countdown begins at `DEATHSTORM SYNTHESIS  60s` in **green** |
| 0:58 | Wait / make progress — time drops below 30 s | Countdown bar transitions to **amber** |
| 1:02 | Stage instruction updates to "ALIGN CORE — ENTER DIRECTION" | Stage text visible below countdown |
| 1:06 | Submit wrong answer | Timer decreases (penalty); crafting continues — no permanent break |
| 1:10 | Complete Stage 1 → instruction changes to Edge Sync | Instruction text updates |
| 1:14 | Time drops to <15 s | Bar turns **danger red** |
| 1:18 | Complete Stage 3 (Energy Stabilisation) | `DEATHSTORM FORGED` + **★  DEATHSTORM ONLINE — FINISH THE HUNT!** notification |

**Judge talking point:** *"The countdown is a live 60-second shared timer from Member 10's IDeathStormCrafting. Colour grades from green → amber → red are purely presentational — no game state owned here."*

---

## Segment 6 — Final Strike & Victory `[1:18 – 1:28]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 1:18 | Boss re-enters VulnerableStun phase | **EXPOSED — STRIKE NOW!** label |
| 1:21 | Swing DeathStorm axe toward weak point | Axe swing animation; boss hit |
| 1:23 | Valid finishing strike lands | Victory panel appears: **DEATHSTORM STRIKE CONFIRMED — THE VOID HAS FALLEN SILENT.** |
| 1:25 | Boss health bar disappears | Boss bar hidden via `HideBossHealthBar()` |
| 1:28 | Demo complete | Victory screen holds |

---

## Segment 7 — Death / Retry Path (Optional, +15 s) `[1:28 – 1:43]`

| Time | Action | Expected UI Response |
|------|--------|----------------------|
| 1:28 | Use console to set player HP to 0 | Death panel: **TRAUMA — HULL INTEGRITY LOST** (Physical damage) |
| 1:33 | Click **RETRY** | Scene reloads; HUD reinitialises at full health |
| 1:38 | Use console to inject Toxic damage | Death panel: **BIOLOGICAL CONTAMINATION** |
| 1:43 | End demo | — |

---

## Key Architecture Points for Judges

```
OblivionUIController (Member 12)
  │
  ├── IDamageable         ──→ Member 2 (Player Health)
  ├── IWeapon             ──→ Member 3 (Gun Combat)
  ├── IWeaponHandler      ──→ Member 3 (Weapon Swap)
  ├── IInventory          ──→ Member 9 (Component Tracking)
  ├── IBossController     ──→ Members 4/5 (Boss Phases)
  ├── IBossArmorSource    ──→ Member 5 (Armor Layer)
  ├── ICraftingCountdown  ──→ Member 10 (60s DeathStorm Timer)
  └── GameManager         ──→ Member 1 (Session State)
```

- **Zero direct coupling** — all connections via interfaces.
- **Auto-discovery fallback** — controller finds runtime sources by Unity tag if Inspector is not wired.
- **Event leak prevention** — all subscriptions removed in `OnDisable`.
- **20 EditMode tests** verify every UI logic branch without needing a running Unity instance.

---

## Timeout / Enrage Bonus Demo (Optional, +20 s)

1. Start crafting.
2. Let the 60-second timer expire without completing all stages.
3. Confirm: `SYNTHESIS TIMEOUT — ENRAGE DETECTED` and `⚠  VOID POUNCE ENABLED` notification.
4. Boss enters Enraged phase — `ENRAGED — VOID POUNCE ACTIVE!` banner.
5. Player survives and re-enters crafting — crafting continues (design intent: per-timeout enrage only).

---

*End of Judge Demo Script — Member 12 (UI, QA & Judge Demonstration)*
