# Pulse pistol integration

`GunController` implements the shared `IWeapon` contract. It reads left mouse button and `R` by default, aims at the mouse through the assigned camera, and can also be driven by a player or weapon handler through `TryFire`, `StartReload`, and `CancelReload`. It does not implement player movement, boss logic, or modify `MainScene`.

## Project layers and tags

The shared project already defines the contract layers in `ProjectSettings/TagManager.asset`:

| Object | Tag | Layer |
| --- | --- | --- |
| Player root | `Player` | `Player` (6) |
| Normal enemy and boss hurtboxes | `Enemy` / `Boss` | `Enemy` (7) |
| Solid walls and bulkheads | `Untagged` or project-specific | `Environment` (8) |
| Gun | `Weapon` | `Default` or `Weapon` if a Weapon layer is added |
| Pulse projectile | `Projectile` | `PlayerProjectile` (10) |

The contract has no separate Boss physics layer, so put boss hurtboxes on `Enemy` and retain the `Boss` tag. In **Project Settings > Physics 2D**, set the `PlayerProjectile` row to collide with `Enemy` and `Environment`; disable its collisions with `Player`, `PlayerProjectile`, `EnemyProjectile`, `ItemPickup`, `Interactable`, and `TriggerZone`. The runtime projectile also rejects player and non-target layers and calls `Physics2D.IgnoreCollision` for all colliders under its firing root, so the shooter remains safe even if the matrix is not yet configured.

## Prefabs and component setup

1. Create a pulse projectile prefab with a visible `SpriteRenderer` (or `TrailRenderer`), `Rigidbody2D`, `CircleCollider2D`, and `Projectile` on the same root. Set the root tag to `Projectile` and layer to `PlayerProjectile`. Use a Dynamic body, gravity scale `0`, Continuous collision detection, and a trigger circle collider. The `Collision Layers` field defaults to `Enemy` and `Environment`; set it explicitly if the target layers differ. The script also supports a non-trigger collider when the Physics 2D matrix is configured. Set its lifetime and optional impact-particle prefab in the Inspector.
2. Add `GunController` to the equipped pistol object. Give it a child `Muzzle` transform positioned at the barrel tip. Assign the projectile prefab, muzzle, and the scene camera. The weapon object should be parented beneath the player so its root identifies the firing source and owner colliders.
3. Assign a reusable muzzle-flash `ParticleSystem` to `Muzzle Flash` and an optional reusable hit-feedback `ParticleSystem` to `Hit Feedback`. Successful damage also invokes `OnDamageDealt(DamageInfo)` and the Inspector `On Successful Hit` event. Impact particles are spawned only when an impact occurs.
4. Tune `Projectile Speed`, `Damage`, `Fire Rate` (shots per second), `Knockback Force`, magazine/reserve values, and reload duration on the gun. `AddAmmo(int)` adds reserve ammunition; read `ReserveAmmo` or subscribe to `OnReserveAmmoChanged` for reserve-ammo UI. A weapon handler can disable `Read Mouse Input` and call the `IWeapon` methods instead.

Any enemy or boss hurtbox must have a `MonoBehaviour` implementing `OblivionsEdge.Core.IDamageable` on the collider object or one of its parents. The projectile sends an Energy `DamageInfo` containing amount, source root, hit point, direction, and knockback force. No concrete enemy or boss class is required.

## Runtime verification checklist

In a temporary test scene (leave `MainScene` unchanged), use a player with a collider on `Player`, a camera, the pistol as a child, an `Enemy` layer target implementing `IDamageable`, and an `Environment` wall:

- Hold left mouse and confirm shots travel from the muzzle toward the cursor at the configured rate; the magazine decrements once per shot and the muzzle flash plays.
- Fire at the target and confirm `TakeDamage` receives one Energy `DamageInfo` with the player as `Source`; confirm hit feedback fires. Fire at the wall and confirm the projectile is consumed without damage.
- Hold fire until the magazine is empty and confirm firing stops and `OnDryFire` fires once per mouse hold. Press `R`; confirm reload blocks firing, transfers reserve ammo after the configured delay, and raises the reload/ammo events.
- Fire from inside the player collider and confirm no self-hit; leave a projectile unobstructed and confirm it is destroyed when its lifetime expires.

The project checkout used for this branch does not include a Unity Editor executable, so those scene-based checks must be run in Unity 2022.3.40f1 after importing the branch.
