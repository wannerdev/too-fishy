# Too Fishy — Unity Port

A Unity remake of the Ludum Dare 57 game **Too Fishy**: dive deeper, catch exotic fish, sell them at the surface dock, upgrade your submarine, and push into the abyss.

The original Godot 4 project remains at the repository root. This folder is a self-contained Unity project.

## Requirements

- **Unity 2022.3 LTS** (or Unity 6) with the built-in render pipeline
- Modules: Windows/Mac/Linux Build Support as needed

## Open & Play

1. Install Unity Hub and Unity 2022.3 LTS (or let Hub download the version in `ProjectSettings/ProjectVersion.txt`).
2. **Add** → select this `unity/` folder.
3. Open the project (first import may take a minute).
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
  Scripts/
    Core/                    # GameState, bootstrap, camera fog
    Player/                  # Submarine controller
    Fish/                    # Fish behaviour + spawn config
    Inventory/               # Weight-limited cargo + smart replace
    Items/                   # Harpoon projectile
    Level/                   # Procedural sections, barriers, lava
    Boss/                    # Blobfish boss
    UI/                      # HUD, upgrades, death screen, popups
```

## Notes

- Visuals use procedural primitives (colored meshes) so the game is playable without importing Godot FBX assets. You can later swap in models from `../meshes/`.
- AK-47 / dual guns and full intro-mission cinematic are stubbed lighter than the Godot version; core dive → catch → sell → upgrade → boss loop is fully playable.
- Gravity is disabled in Physics settings (underwater 2.5D movement).

## Original

Godot source and Ludum Dare entry: see root `README.md`.
