# AGENTS.md

## Cursor Cloud specific instructions

### Project overview

Too Fishy is a Godot 4.4 game (GDScript). See `CLAUDE.md` for architecture details and `README.md` for basic setup.

### Running the game

- **Editor/GUI mode:** `DISPLAY=:1 godot --path .` (launches the editor with the project loaded; press F5 to play)
- **Direct play:** `DISPLAY=:1 godot --path . --run` (runs the main scene directly)
- The VM uses software rendering (`llvmpipe`); expect lower performance than a real GPU. The game runs fine for development/testing.

### Headless operations

- **Import/reimport assets:** `godot --headless --import` (run from `/workspace`)
- ALSA audio errors in logs are expected (no sound card in the VM) and do not affect gameplay.

### Lint / testing

- No automated test framework is configured. Validation is manual: run the game and verify behavior.
- `godot --headless --import` serves as a basic project integrity check — it will report GDScript parse errors and missing resources.
- There is a pre-existing warning-as-error in `scripts/destroyable_barier.gd` (Variant type inference); this does not block the game from running.

### Build (export)

- Web export: `godot --headless --export-release "Web" build/web/index.html` (requires export templates installed)
- Export templates are **not** installed in the VM by default. CI uses `barichello/godot-ci:4.4.1` Docker image which bundles them.

### Gotchas

- The `--check-only` flag may hang indefinitely in headless mode; avoid using it. Use `--import` for validation instead.
- GDScript files that reference autoload singletons (e.g., `GameState`) cannot be loaded in isolation via `--script`; they require the full engine context.
