# Null Saint

A side-view action game project built in **Unity 6000.0.40f1**.

## Visual Direction

Dark fantasy side-view action with a small cloaked hero, glowing violet magic, ruined stone platforms, ghost enemies, and a mist-heavy purple atmosphere.

Local concept renders are kept outside version control in:

```text
Renders/insane renders/
```

## Workspace Layout

```text
Null Saint/                     <- git root
|-- Null Saint/                 <- Unity project; open this folder in Unity Hub
|   |-- Assets/
|   |   |-- Enemy/              <- ghost enemy FBX, material, texture
|   |   |-- Main Character/     <- player rigged FBX, materials, animation clips
|   |   |-- Prefabs/            <- enemy_ghost prefab and environment prefabs
|   |   |-- Scenes/             <- SampleScene
|   |   |-- Scripts/            <- game C# scripts
|   |   `-- Settings/
|   |-- Packages/
|   `-- ProjectSettings/
|-- Blender Sessions/           <- .blend working files
|-- Null Saint game character/  <- character source files
|-- Renders/                    <- output renders
`-- Scripts/                    <- workspace tooling, not Unity game code
```

## Getting Started

1. Install **Unity 6000.0.40f1** via Unity Hub.
2. Clone the repo with **Git LFS** enabled:

   ```bash
   git lfs install
   git clone <repo-url>
   ```

3. In Unity Hub, choose **Add** and select the inner `Null Saint/` folder.
4. Open `Assets/Scenes/SampleScene.unity`.

## Unity Setup Menus

After scripts compile, these menus are available in Unity:

- `Null Saint > Setup Player Animator`
  - Rebuilds/repairs the player animator states and transitions for Idle, Walk, Run, Jump, Crawl, Power, Block, Slash, and Slash Spin.
- `Null Saint > Setup Combat Components`
  - Adds `PlayerCombat` to the player.
  - Adds `EnemyGhostCombat` to scene ghost enemies.
  - Adds `EnemyGhostCombat` to `Assets/Prefabs/enemy_ghost.prefab`.
  - Updates the player slash hit settings to the current defaults.

## Player

Main character rig:

```text
Assets/Main Character/Null_Saint_rigged.fbx
```

Main scripts:

- `Assets/Scripts/PlayerMovement.cs`
  - Side-view movement, jumping, double jump, gravity, ground snapping, facing, camera hookup, animation parameters, and movement/action feedback.
- `Assets/Scripts/PlayerCombat.cs`
  - Slash hit detection, projectile blocking, projectile slashing, fall death, player death behavior, and scene reload after death.

Animation clips:

```text
Idle
Walk
Run
Jump
Crawl
Power
Block_01
Slash_01
Slash_spin
```

## Combat

Player slash damage is handled by `PlayerCombat`, not by a weapon collider.

Current slash logic:

- Left mouse triggers normal slash.
- `Q` triggers slash spin.
- A side-view box hit area is created in front of the player.
- Enemies inside that hit area receive `slashDamage`.
- Enemy projectiles inside that hit area are destroyed.

Important player tuning fields:

- `Slash Range`
- `Slash Depth`
- `Slash Vertical Radius`
- `Slash Height`
- `Slash Damage`
- `Draw Slash Debug`

Enable `Draw Slash Debug` on the selected player to see the slash hitbox gizmo in the Scene view.

## Enemy Ghost

Enemy prefab:

```text
Assets/Prefabs/enemy_ghost.prefab
```

Main script:

```text
Assets/Scripts/EnemyGhostCombat.cs
```

Behavior:

- Has simple health.
- Shoots a power projectile toward the player every few seconds.
- Dies when health reaches zero.
- Uses the collider size authored on the prefab. The script does not auto-resize the enemy collider.

Important enemy tuning fields:

- `Health`
- `Shot Interval`
- `Shot Range`
- `Projectile Speed`
- `Projectile Lifetime`
- `Projectile Radius`
- `Projectile Spawn Offset`

## Projectiles

Enemy power projectiles are created at runtime by `EnemyGhostCombat`.

Projectile rules:

- If the player blocks with `LeftControl` or right mouse, the projectile is destroyed.
- If the player slashes the projectile, it is destroyed.
- If the projectile hits the player while not blocking, the player dies.
- If the projectile hits world geometry, it is destroyed.

Main script:

```text
Assets/Scripts/EnemyPowerProjectile.cs
```

## Death Behavior

Player death is handled by `PlayerCombat`.

Current behavior:

- Plays optional death feedback.
- Optionally triggers an Animator trigger named `Die`.
- Disables player movement.
- Stops enemy combat scripts.
- Clears active enemy projectiles.
- Reloads the current scene after a short delay.

Important fields:

- `Reload Scene On Death`
- `Reload Delay`
- `Stop Enemies On Death`
- `Clear Projectiles On Death`
- `Hide Player On Death`
- `Death Trigger Name`

If the Animator does not have a `Die` trigger, either add one or clear the `Death Trigger Name` field.

## Audio And VFX Feedback

Reusable feedback slots use `GameplayFeedback`:

```text
Assets/Scripts/GameplayFeedback.cs
```

Each feedback slot can hold:

- an `AudioClip`
- a prefab to spawn
- an optional spawn point
- a spawn offset
- volume
- whether the spawned prefab should parent to the spawn point

Player feedback slots live on `PlayerMovement` and `PlayerCombat`:

- Walk
- Run
- Jump
- Slash
- Slash Spin
- Power
- Block Start
- Slash Hit
- Player Death

Enemy/projectile feedback slots live on `EnemyGhostCombat`:

- Enemy Shoot
- Enemy Death
- Projectile Impact
- Projectile Blocked
- Projectile Slashed

## Repo Conventions

- Unity game scripts live in `Null Saint/Assets/Scripts/`.
- Binary assets such as FBX, PNG, WAV, and blend files are tracked with Git LFS; see `.gitattributes`.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`, generated `.sln`, and generated `.csproj` files are ignored.
- The outer `Scripts/` folder is workspace tooling, not Unity runtime code.
