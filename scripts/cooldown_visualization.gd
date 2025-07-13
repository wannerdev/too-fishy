extends CanvasLayer

var top_rings_container: HBoxContainer
var front_rings_container: HBoxContainer
var control_node: Control

var cooldown_rings = {}
var player_node: CharacterBody3D

# Ring positioning - fixed 2D positions
var ring_spacing: float = 5.0

func _ready():
	# Set layer to be below UI elements (UI is typically on layer 1)
	layer = -1
	
	# Create control node for positioning - full screen
	var control = Control.new()
	control.name = "Control"
	control.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(control)
	
	# Get viewport size and center
	var viewport_size = get_viewport().get_visible_rect().size
	var screen_center = viewport_size / 2
	
	# Create container for top rings (buoy, drone)
	var top_container = HBoxContainer.new()
	top_container.name = "TopRingsContainer"
	top_container.alignment = BoxContainer.ALIGNMENT_CENTER
	top_container.add_theme_constant_override("separation", int(ring_spacing))
	# Position relative to screen center: center horizontally, 200px above center
	top_container.position = Vector2(screen_center.x , screen_center.y +15)  # -100 to center the 200px wide container
	top_container.size = Vector2(200, 60)
	control.add_child(top_container)
	
	# Create container for front rings (harpoon, ak47)
	var front_container = HBoxContainer.new()
	front_container.name = "FrontRingsContainer"
	front_container.alignment = BoxContainer.ALIGNMENT_CENTER
	front_container.add_theme_constant_override("separation", int(ring_spacing))
	# Position relative to screen center: center horizontally, 200px below center
	front_container.position = Vector2(screen_center.x , screen_center.y + 150)  # -100 to center the 200px wide container
	front_container.size = Vector2(200, 60)
	control.add_child(front_container)
	
	top_rings_container = top_container
	front_rings_container = front_container
	control_node = control
	
	# Find player and camera
	call_deferred("setup_references")

func setup_references():
	# Find player node
	player_node = get_node("../")
	if not player_node:
		push_error("Could not find player node")
		return
	
	# Create rings for each item type
	create_cooldown_ring("harpoon")
	create_cooldown_ring("ak47")
	create_cooldown_ring("buoy")
	create_cooldown_ring("drone")

func create_cooldown_ring(item_name: String):
	var ring_script = preload("res://scripts/cooldown_ring.gd")
	var ring = Control.new()
	ring.set_script(ring_script)
	ring.setup(item_name)
	ring.name = item_name + "_ring"
	
	# Add to appropriate container based on item type
	if item_name in ["harpoon", "ak47"]:
		front_rings_container.add_child(ring)
	else:  # buoy, drone
		top_rings_container.add_child(ring)
	
	cooldown_rings[item_name] = ring

func _process(delta):
	if not player_node:
		return
	
	# Update cooldown data
	update_cooldown_data()

func update_cooldown_data():
	if not player_node:
		return
	
	# Update harpoon cooldown
	if cooldown_rings.has("harpoon"):
		var harpoon_timer = player_node.get_node("HarpoonCD")
		if harpoon_timer:
			var time_left = harpoon_timer.time_left
			var progress = (harpoon_timer.wait_time - time_left) / harpoon_timer.wait_time
			cooldown_rings["harpoon"].update_cooldown(progress, true)
	
	# Update AK47 cooldown
	if cooldown_rings.has("ak47"):
		var ak47 = player_node.get_node("Pivot/SmFishSubmarine/ak47_0406195124_texture")
		if ak47 and GameState.upgrades[GameState.Upgrade.AK47] > 0:
			var progress = 1.0
			if ak47.has_method("is_currently_reloading") and ak47.is_currently_reloading():
				progress = ak47.get_reload_progress()
			cooldown_rings["ak47"].update_cooldown(progress, true)
		else:
			cooldown_rings["ak47"].update_cooldown(0.0, false)
	
	# Update buoy cooldown
	if cooldown_rings.has("buoy"):
		var buoy_timer = player_node.get_node("BuoyCD")
		if buoy_timer and GameState.upgrades[GameState.Upgrade.SURFACE_BUOY] > 0:
			var time_left = buoy_timer.time_left
			var progress = (buoy_timer.wait_time - time_left) / buoy_timer.wait_time
			cooldown_rings["buoy"].update_cooldown(progress, true)
		else:
			cooldown_rings["buoy"].update_cooldown(0.0, false)
	
	# Update drone cooldown
	if cooldown_rings.has("drone"):
		var drone_timer = player_node.get_node("DroneCD")
		if drone_timer and GameState.upgrades[GameState.Upgrade.DRONE_SELLING] > 0:
			var time_left = drone_timer.time_left
			var progress = (drone_timer.wait_time - time_left) / drone_timer.wait_time
			cooldown_rings["drone"].update_cooldown(progress, true)
		else:
			cooldown_rings["drone"].update_cooldown(0.0, false) 
