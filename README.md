# Casual Top Down — HFGE1181 SA1

**Student:** Phiwokuhle Masina  
**Student number:** 08HA2603634  
**Module:** HFGE1181 — Summative Assessment 1  
**Engine:** Unity 6000.0.63f1 · Universal Render Pipeline (2D) · Input System 1.16

## About the game

A casual top-down "eat or be eaten" arena. The player starts at size **1.00**.
Eating food and smaller donuts fills a satiation meter; every full meter raises the
player's size by **0.25** (up to **2.50**). Food that is smaller than the player is
pulled into the player's centre and eaten. Larger enemies pull the player in and
eat them — game over. Everything that is smaller than the player is a snack;
everything bigger is a threat.

## Controls

| Action | Desktop | Mobile |
| --- | --- | --- |
| Move | WASD / arrow keys | Left on-screen joystick |
| Aim | Mouse cursor | Right on-screen joystick |
| Pause | `II` button | `II` button |

The on-screen joysticks also work with a mouse, which makes the mobile controls
easy to test in the editor.

## Assignment requirements

### Level design

- **Layered tilemaps:** a `Floor` tilemap and a separate `Walls` tilemap inside one
  `Grid` (cell size 2 × 2 units). Each tilemap is on its own sorting layer
  (`Floor`, `Walls`) so the layers are clearly separated.
- **Collision:** the walls tilemap uses `TilemapCollider2D` + `CompositeCollider2D`
  on a static `Rigidbody2D`, and the tile asset uses `ColliderType.Grid`, so collision
  matches the drawn tiles exactly.
- **Buildings and apertures:** four hollow buildings plus a perimeter wall. Door
  openings differ in size (2, 3 and 4 cells wide) and two buildings have a
  second, single-cell doorway on the side.
- The level is 24 × 18 cells (48 × 36 units). Textures are generated from the
  provided tileset so walls and floor keep the pack's art style.

### Gameplay (vertical slice)

- Player starts at size 1.00 and grows by 0.25 per full satiation meter.
- Food is spawned at random sizes relative to the player and can only be eaten
  when it is smaller than the player.
- Smaller units are pulled to the centre of the larger unit and consumed there;
  larger enemies pull the player in and kill them.
- Enemies use random patrol AI, spawned at runtime between size 0.45 and 1.50.
- Runtime spawning and despawning: enemies and food below 25% of the player size
  are removed; enemies above 1.5, food above 0.6 are not spawned.
- `GameStateManager` drives Playing / Paused / Game Over and freezes the game
  with `Time.timeScale`; `SpawnManager`, `UIManager` and `CameraHandler` are all
  wired into the scenes.

### Feedback

- Eat, enemy-bite, grow, death and UI-click sounds through `SoundManager`.
- Background music while the player is alive; stopped on game over.
- Crumb particle bursts on eating (`SpawnParticleEvent` / `CrumbBurst`).
- Animation controllers for idle, move, bite, grow and death; enemies have idle,
  move and bite.
- Animated camera that zooms out as the player grows.

### UI

- HUD with current size, `mm:ss` survival timer and pause button.
- Arrows around the player point at nearby enemies.
- Main menu with Play / Exit and animated donut decoration.
- Pause menu with Resume / Restart / Quit.
- Game Over screen with survival time, final size, Retry and Main Menu.
- Fade-out transitions between scenes (`SceneFader`).

### Mobile and version control

- Dual virtual joysticks (move + aim) that work with touch input.
- Android build (IL2CPP, ARM64, landscape) is committed at
  `Builds/Android/CasualTopDown.apk`.
- The project is versioned with Git; commit history shows the work in stages.

## Project structure

```
Assets/
  Art/            Provided character, level and UI art (plus generated tiles)
  Audio/          Music and sound effects
  Animations/     Player and enemy animation clips + controllers
  Editor/         SA1ProjectBuilder — regenerates every asset, prefab and scene
  Input/          PlayerInputActions (move / aim bindings)
  Prefabs/        Player, Enemy, Food, CrumbBurst, TargetIndicator
  Scenes/         MainMenu, Game
  Scripts/        Core, Player, Units, UI, Feedback, Audio
  Tiles/          Wall and floor tile assets
Builds/Android/   CasualTopDown.apk
Screenshots/      Level overview and preview renders
```

## Running the project

1. Open the folder in **Unity 6000.0.63f1** (Unity Hub → Add → select this folder).
2. Open `Assets/Scenes/MainMenu.unity` and press Play.
3. To rebuild every generated asset from the source art, use
   **Tools → SA1 → Build Project Content**. This regenerates sprite import
   settings, tiles, prefabs, animation controllers, both scenes and the build
   settings, so the project can be reproduced from the provided asset pack.

APK build: **File → Build Profiles → Android → Build**, or run
`SA1ProjectBuilder.BuildAndroidBatch` in batch mode. The delivered APK was built
with the settings already configured in the project (IL2CPP, ARM64, min SDK 23,
landscape only).

## Credits

- Character, food and level art: provided HFGE1181 SA1 asset pack.
- Sound effects and music: provided pack (Zapsplat content as supplied).
- TextMeshPro: Unity package built-in resources.

