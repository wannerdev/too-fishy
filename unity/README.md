# Too Fishy — Unity Port

A Unity remake of the Ludum Dare 57 game **Too Fishy**: dive deeper, catch exotic fish, sell them at the surface dock, upgrade your submarine, and push into the abyss.

The original Godot 4 project remains at the repository root. This folder is a self-contained Unity project.

## Requirements

- **Unity 6.3 LTS (6000.3.28f1)** with the built-in render pipeline
- Modules (Unity Hub): Android Build Support (with OpenJDK and Android SDK & NDK Tools) and Web Build Support as needed

## Open & Play

1. Install Unity Hub and Unity 6000.3.28f1 (Hub offers the version in `ProjectSettings/ProjectVersion.txt`).
2. **Add** → select this `unity/` folder.
3. Open the project. The first import copies the Godot art (see Notes) and takes a few minutes.
4. Open `Assets/Scenes/Main.unity`.
5. Press **Play**.

`GameBootstrap` builds the world, submarine, fish, UI, and systems at runtime—no prefab wiring required.

## Controls

| Input | Action |
|-------|--------|
| WASD / Arrows | Move submarine |
| Left Mouse | Fire harpoon |
| E / Tab | Upgrades (while docked at surface) |
| B | Surface buoy (after upgrade) |
| Q | Selling drone (after upgrade) |
| Space | Swing pickaxe (after upgrade) |
| Esc | Pause |

## Gameplay (ported from Godot)

- **Depth stages**: Surface → Deep → Deeper → SuperDeep → Hot → Lava → Void (every 100 m)
- **Pressure damage** when deeper than `(DepthResistance + 1) × 100` m
- **Dock** (near surface, x > −7): auto-sell fish, heal, buy upgrades
- **Fish** spawn per section with stage-based rates, weights, shiny (×3 value) variants
- **Boss** blobfish appears after reaching 500 m+; harpoon deals 10 damage

## Project layout

```
Assets/
  Scenes/Main.unity          # Entry scene (GameBootstrap)
  Shaders/                   # Ports of the Godot shaders (fish swim, background fade, water, lava)
  Scripts/
    Core/                    # GameState, bootstrap, camera fog, Godot asset loader, audio
    Player/                  # Submarine controller
    Fish/                    # Fish behaviour + spawn config
    Inventory/               # Weight-limited cargo + smart replace
    Items/                   # Harpoon projectile
    Level/                   # Procedural sections, barriers, lava
    Boss/                    # Blobfish boss
    UI/                      # Ports of the Godot UI: HUD, cargo panel, achievements, shop, inventory,
                             # pause/settings/save menus, story dialogs, death screen, touch controls
```

## Notes

- Art and audio are the original Godot files. `Assets/Editor/GodotAssetSync.cs` copies them from the
  repository root (`../meshes`, `../textures`, `../music`, `../sounds`) into `Assets/Resources/Godot`
  (git-ignored) and rebuilds the Godot materials there. It runs when the editor opens, before every
  command-line build, and from **Too Fishy → Sync Godot Assets**. The Unity project therefore has to
  stay inside the too-fishy repository.
- Coordinates: Godot (x, y, z) is Unity (x, y, −z). Scene transforms are copied verbatim from the
  `.tscn` files through `GodotSpace.Apply`, which also handles the X mirror of Unity's model importer.
- UI text uses Open Sans SemiBold (Godot's default font, `Assets/Resources/Fonts`, SIL OFL).
- Godot bugs that were deliberately not copied: the intro replaying on every launch, the missing
  control tutorials after the intro, crate barriers at twice the section depth, inverted vertical
  touch input, the dock popup saying "Drone sold…", the silent shiny-catch sound, the unreachable
  mind-control escape, the boss section overlapping the next sections, and released fish re-rolling
  their weight (±20 %), which let a fish change value by being released and caught again.
- The boss section's large rock (`gestein_v001.obj`, 54 MB / 344k vertices) is the original on desktop
  builds. Android and WebGL builds use the smaller `gestein_v003.obj` scaled to the same size (the sync
  only copies v001 for non-mobile/non-WebGL targets, so it never ends up in phone builds).
- The Godot rock meshes (`gestein_*.obj`) arrive inside-out through Unity's OBJ importer and would be
  culled to thin slivers; `GodotAssetImportSettings.OnPostprocessModel` reverses their winding.- Gravity is disabled in Physics settings (underwater 2.5D movement).

## Original

Godot source and Ludum Dare entry: see root `README.md`.

## Screenshots and Windows build

- Windows player: `Unity.exe -batchmode -nographics -quit -projectPath unity -buildTarget StandaloneWindows64 -executeMethod TooFishy.EditorTools.BuildScripts.BuildWindows` (output `build/windows/TooFishy.exe`).
- `TooFishy.exe -tfshots <dir>` runs `ScreenshotDirector`: it plays through dock, upgrades, diving, catching, barriers, every stage and the boss, saves a JPG per step and quits (`-tfintro` records the intro mission instead). Without the flag it does nothing.
- The Godot counterpart is `docs/screenshots/godot_shot_director.gd` (autoload in a throw-away checkout, start with `-- <dir>`).
