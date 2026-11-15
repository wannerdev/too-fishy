# Godot vs Unity parity audit (2026-11-15)

Scope: the Unity port on `unity-faithful-port-local` (5cceb06) against the Godot original in this repo.

How it was tested:
- **Code audit:** 9 readers, one per subsystem (player, weapons, fish, inventory, progression, level, boss, UI/touch, Unity robustness), compared Godot and Unity line by line. Every finding then went to an independent skeptic told to refute it: 83 confirmed, 19 rejected.
- **Runtime:** both games ran the same scripted playthrough (`ScreenshotDirector -tfshots`, godot-ref `shot_director.gd`) at 1600x900, and the screenshots were compared side by side. The touch test (`-tftouchtest`) and a WebGL smoke test were also run.
- **Second opinion:** Nappy (Grok Bot) reviewed the fix diff. It found four small issues, all fixed.

Not tested on a real Android device. The only BlueStacks instance is 32-bit and cannot run the ARM64 build.

## Fixed

| Area | Problem in the port | Now |
|---|---|---|
| Lamp | Headlamp at intensity 4 blew the rock beside the sub out to white at every depth | Intensity 2, measured against Godot with the lamp on (see `screenshots/parity-audit/lamp_sweep_godot_vs_unity.jpg`). Lamp is ForcePixel; Medium quality has 4 pixel lights, Android uses Medium |
| Numbers | Locale formatting: `$4,00`, `4,0 / 200,0` on German devices | Invariant culture like Godot |
| Sub collision | Upright 1.0 x 1.2 capsule, so bow and stern sank 0.9 m into rock | Real horizontal 2.8 m hull swept against walls (`PlayerController.SweepHull`) |
| Boss | Moved through rocks, walls and the sub | Swept slide like `move_and_slide` |
| Lava | Cylinder areas became huge spheres (non-uniform scale); overlap tested the upright capsule | Godot cylinders; overlap tests the hull |
| Crate rows | At y+10 (150/250/350 m) | At Godot's actual 2*y+10 (310/510 m) |
| Boss section | 25 m deeper than Godot | Same depth as Godot |
| Section streaming | Sections came back at full fish count; damaged crates healed | Stable section identity, refill 1 fish / 15 s like Godot; fish cull distance raised to 250 m |
| Crates | No 5% special reward (speed boost or jackpot); no proximity hint; boss crates had 10 HP | Special reward and hint added (4 s popup, touch wording); boss crates 1 HP |
| Fish | Price came from the truncated weight; ids restarted every launch (some fish uncatchable after a load); fish turned and hurt the player while paused; direction changes depended on frame rate; released fish kept their weight | All as Godot |
| Bullets | No sweep (tunnelled through small fish at low fps); 2x speed, range and size | Swept capsule cast; Godot speed, range and size |
| Input | Firing worked with the inventory open; touch presses made while paused fired later; joystick stuck after the app lost focus | Blocked, dropped and reset |
| Touch | With Rotatable Harpoon nothing left of the sub could be aimed at | A quick left-half tap fires at the tapped point (only with that upgrade); joystick dead zone so a tap doesn't nudge the sub |
| Touch UI | Small buttons; keyboard wording in tutorials and Controls; no quick save on touch | Bigger buttons, touch wording, SAVE button |
| Friend sub (intro) | Fired invisible AK47s | Guns shown like Godot |
| Saving | Saving in the intro stored the friend's maxed sub and $10,000; story flags not saved; drone money lost or doubled around save and load | Intro saves refused; story flags saved (old saves still load); drone money handled |
| WebGL | Saves gone after a reload | `autoSyncPersistentDataPath` on, plus an explicit flush |
| Mobile | No autosave; screen dimmed | Autosave on pause/quit (never in the intro, the death screen or the boss intro, and never over a save this run didn't load); screen kept on |
| Small parity fixes | Surface sink, buoy momentum, sun tween order, crack fade, popups while paused, boss bar label, upgrade pips, inventory panel after Close, Sell Drone button during cooldown, false "Inventory full!" on double hits, friend re-touch after WIN | As Godot |

## Kept on purpose (Godot behaviour looks like a bug)

- Pressure death shows one death screen. Godot shows two and skips the wipe.
- Unity doesn't damage the boss outside its 100 m leash. Godot does.
- The Android back button opens the pause menu. Godot quits the app.
- The "Unlock 2nd Gun" card appears right after buying the AK47. Godot only shows it from the next session.
- The pickaxe returns to its rest angle.
- SFX follow the volume slider and play centred.
- Save timestamps are in local time.
- The intro LavaWall is removed after the intro.
- The intro plays once, not on every launch.
- The boss intro dialog is not replaced after 0.5 s.
- Fish carried off by the drone can't be harpooned again.
- Crates are destroyed instantly.
- Dock sale popup wording.
- Red damage numbers on boss hits.
- 60 fps cap.

## Left open

- The lava-stage veins have no seam fade. This needs a new shader and material in `GodotAssetSync`.
- Pause and settings menu buttons are still below 9 mm on small phones. The panels would need scroll containers.
