# Floating Cooldown Visualization

## Overview
The cooldown visualization system displays circular progress rings that float above the player's submarine, showing the cooldown status for various items and abilities.

## Features

### Visual Elements
- **Circular Progress Rings**: Each item gets a circular ring that fills clockwise as the cooldown progresses
- **Color Coding**: 
  - 🔵 **Blue** (`COLOR_COOLDOWN_READY`): Item is ready to use
  - 🟠 **Orange** (`COLOR_COOLDOWN_ACTIVE`): Item is on cooldown
- **Text Labels**: Item names are displayed in the center of each ring
- **Responsive Visibility**: Rings only appear when the corresponding upgrade is unlocked

### Supported Items
1. **Harpoon** - Always available
2. **AK47** - Visible when AK47 upgrade is purchased
3. **Buoy** - Visible when Surface Buoy upgrade is purchased  
4. **Drone** - Visible when Drone Selling upgrade is purchased

## Implementation Details

### File Structure
- `scripts/cooldown_ring.gd` - Individual ring component
- `scripts/cooldown_visualization.gd` - Main visualization manager
- `scenes/cooldown_visualization.tscn` - Scene definition

### Positioning
- Rings appear **80 pixels above** the submarine
- **50 pixel spacing** between rings
- Automatically follows submarine movement
- Uses screen space positioning with 3D-to-2D projection

### Integration
- Added to `scenes/player.tscn` as a child node
- HUD cooldown panels are hidden (`cooldown_info_panel.visible = false`)
- Reuses existing cooldown logic from `scripts/hud.gd`

## Usage
The system automatically activates when the game runs. No additional configuration required.

## Customization
Adjust these values in `scripts/cooldown_visualization.gd`:
- `ring_spacing`: Distance between rings
- `rings_height_offset`: Vertical offset from submarine
- `ring_radius` & `ring_thickness`: Visual appearance in `cooldown_ring.gd` 