extends PanelContainer

@onready var container = $MarginContainer/HUDContainer
@onready var current_stage_label = $MarginContainer/HUDContainer/StageInfo/VBoxContainer/StageValue
@onready var health_bar = $MarginContainer/HUDContainer/HealthInfo/HealthBar
@onready var pressure_headroom_bar = $MarginContainer/HUDContainer/PressureStack/PressureInfo/PressureBar

# Depth indicator
@onready var depth_indicator = $MarginContainer/DepthIndicator
@onready var marker_container = $MarginContainer/DepthIndicator/MarkerContainer
@onready var current_depth_marker = $MarginContainer/DepthIndicator/MarkerContainer/CurrentDepthMarker
@onready var max_depth_marker = $MarginContainer/DepthIndicator/MarkerContainer/MaxDepthMarker
@onready var current_depth_label = $MarginContainer/DepthIndicator/MarkerContainer/CurrentDepthMarker/Label
@onready var max_depth_label = $MarginContainer/DepthIndicator/MarkerContainer/MaxDepthMarker/Label

# Cooldown progress bars - updated to match scene file
@onready var harpoon_cd_bar = $MarginContainer/HUDContainer/CooldownInfo/HarpoonPanel/HarpoonContainer/HarpoonCD
@onready var buoy_cd_bar = $MarginContainer/HUDContainer/CooldownInfo/BuoyPanel/BuoyContainer/BuoyCD
@onready var drone_cd_bar = $MarginContainer/HUDContainer/CooldownInfo/DronePanel/DroneContainer/DroneCD
@onready var ak47_cd_bar = $MarginContainer/HUDContainer/CooldownInfo/AK47Panel/AK47Container/AK47CD

# Label references - updated to match scene file
@onready var harpoon_label = $MarginContainer/HUDContainer/CooldownInfo/HarpoonPanel/HarpoonContainer/HarpoonCD/Label
@onready var buoy_label = $MarginContainer/HUDContainer/CooldownInfo/BuoyPanel/BuoyContainer/BuoyCD/Label
@onready var drone_label = $MarginContainer/HUDContainer/CooldownInfo/DronePanel/DroneContainer/DroneCD/Label
@onready var ak47_label = $MarginContainer/HUDContainer/CooldownInfo/AK47Panel/AK47Container/AK47CD/Label

# Panel references - need to store them to show/hide
@onready var harpoon_panel = $MarginContainer/HUDContainer/CooldownInfo/HarpoonPanel
@onready var buoy_panel = $MarginContainer/HUDContainer/CooldownInfo/BuoyPanel
@onready var drone_panel = $MarginContainer/HUDContainer/CooldownInfo/DronePanel
@onready var ak47_panel = $MarginContainer/HUDContainer/CooldownInfo/AK47Panel

# Store direct references to info panels
# These might not be needed if all styling is via .tscn, but keep for now.
@onready var health_info_panel = $MarginContainer/HUDContainer/HealthInfo
@onready var stage_info_panel = $MarginContainer/HUDContainer/StageInfo
@onready var pressure_info_panel = $MarginContainer/HUDContainer/PressureStack/PressureInfo
@onready var cooldown_info_panel = $MarginContainer/HUDContainer/CooldownInfo
@onready var pause_button = $MarginContainer/HUDContainer/PressureStack/PauseToggle

var _fill_style_override: StyleBoxFlat
var _health_fill_override: StyleBoxFlat
var _harpoon_fill_override: StyleBoxFlat
var _buoy_fill_override: StyleBoxFlat
var _drone_fill_override: StyleBoxFlat
var _ak47_fill_override: StyleBoxFlat
var pause_menu: Control = null

# Variables for smooth marker movement
var current_marker_target_pos: float = 0
var max_marker_target_pos: float = 0
var current_marker_pos: float = 0
var max_marker_pos: float = 0
var marker_smoothing: float = 8.0 # Higher values = faster movement

# Constants for max possible depth and colors
const MAX_POSSIBLE_DEPTH = 600.0  # Adjust this based on your game's maximum depth
const COLOR_COOLDOWN_READY = Color(0, 0.36, 0.83, 0.8) # Blue
const COLOR_COOLDOWN_ACTIVE = Color(0.918, 0.525, 0.212, 0.82) # Orange

# Neutral colors for depth markers
const COLOR_CURRENT_DEPTH = Color(0.8, 0.8, 0.8, 0.8) # Light gray for current depth
const COLOR_MAX_DEPTH = Color(0.6, 0.6, 0.6, 0.8) # Darker gray for max depth
const UPGRADE_SCREEN_BORDER_COLOR = Color(0.2, 0.4, 0.7) # Teal border from upgrade screen
const UPGRADE_SCREEN_TEXT_COLOR = Color(0.2, 0.4, 0.7) # Teal text color

func _ready():
	# Hide the entire cooldown info panel since we're using floating rings
	cooldown_info_panel.visible = false
	
	# Initially hide upgrade-dependent panels
	buoy_panel.visible = false
	drone_panel.visible = false
	ak47_panel.visible = false
	# Harpoon is always available by default

	# Create style overrides for pressure bar
	if is_instance_valid(pressure_headroom_bar):
		_fill_style_override = StyleBoxFlat.new()
		_fill_style_override.corner_radius_top_left = 4
		_fill_style_override.corner_radius_top_right = 4
		_fill_style_override.corner_radius_bottom_right = 4
		_fill_style_override.corner_radius_bottom_left = 4
		pressure_headroom_bar.add_theme_stylebox_override("fill", _fill_style_override)
   
	# Create style override for health bar
	if is_instance_valid(health_bar):
		_health_fill_override = StyleBoxFlat.new()
		_health_fill_override.bg_color = Color(0, 0.36, 0.83, 0.8)
		_health_fill_override.corner_radius_top_left = 4
		_health_fill_override.corner_radius_top_right = 4
		_health_fill_override.corner_radius_bottom_right = 4
		_health_fill_override.corner_radius_bottom_left = 4
		health_bar.add_theme_stylebox_override("fill", _health_fill_override)
	
	# Create style overrides for cooldown bars
	if is_instance_valid(harpoon_cd_bar):
		_harpoon_fill_override = create_cooldown_style()
		harpoon_cd_bar.add_theme_stylebox_override("fill", _harpoon_fill_override)
		
	if is_instance_valid(buoy_cd_bar):
		_buoy_fill_override = create_cooldown_style()
		buoy_cd_bar.add_theme_stylebox_override("fill", _buoy_fill_override)
		
	if is_instance_valid(drone_cd_bar):
		_drone_fill_override = create_cooldown_style()
		drone_cd_bar.add_theme_stylebox_override("fill", _drone_fill_override)
		
	if is_instance_valid(ak47_cd_bar):
		_ak47_fill_override = create_cooldown_style()
		ak47_cd_bar.add_theme_stylebox_override("fill", _ak47_fill_override)

	# Apply margins - keep these constant
	$MarginContainer.add_theme_constant_override("margin_left", 15)
	$MarginContainer.add_theme_constant_override("margin_right", 15)
	$MarginContainer.add_theme_constant_override("margin_top", 15)
	$MarginContainer.add_theme_constant_override("margin_bottom", 15)
	_setup_pause_control()

func create_cooldown_style() -> StyleBoxFlat:
	var style = StyleBoxFlat.new()
	style.corner_radius_top_left = 4
	style.corner_radius_top_right = 4
	style.corner_radius_bottom_right = 4
	style.corner_radius_bottom_left = 4
	style.bg_color = COLOR_COOLDOWN_READY
	return style

func _process(delta: float) -> void:
	# Update depth indicator
	update_depth_indicator(delta)
	
	current_stage_label.text = "%s" % Strings.stageNames[GameState.playerInStage]
	
	# Update health bar value
	health_bar.value = GameState.health
	
	# Change health bar color based on health percentage
	if is_instance_valid(health_bar) and _health_fill_override:
		if GameState.health <= 15: # 15% or less - red
			_health_fill_override.bg_color = Color(0.918, 0.212, 0.212, 0.82) # Red
		elif GameState.health <= 30: # Between 15% and 30% - orange
			_health_fill_override.bg_color = Color(0.918, 0.525, 0.212, 0.82) # Orange
		else: # Above 30% - default blue
			_health_fill_override.bg_color = Color(0, 0.36, 0.83, 0.8) # Default blue
	
	# Update pressure headroom bar value
	var headroom_value = GameState.headroom / (GameState.upgrades[GameState.Upgrade.DEPTH_RESISTANCE] + 1)
	pressure_headroom_bar.value = headroom_value
	
	# Change pressure bar color based on headroom percentage
	if not is_instance_valid(pressure_headroom_bar) or not _fill_style_override:
		push_warning("ProgressBar or its fill_style_box_override is not properly initialized.")
		return
		
	if headroom_value <= 15: # 15% or less - red
		_fill_style_override.bg_color = Color(0.918, 0.212, 0.212, 0.82) # Red
	elif headroom_value <= 30: # Between 15% and 30% - orange
		_fill_style_override.bg_color = Color(0.918, 0.525, 0.212, 0.82) # Orange
	else: # Above 30% - default blue
		_fill_style_override.bg_color = Color(0, 0.36, 0.83, 0.8) # Default blue

	if GameState.player_node:
		# Update cooldown bars
		if is_instance_valid(harpoon_cd_bar):
			var harpoon_timer = GameState.player_node.get_node("HarpoonCD")
			if harpoon_timer:
				var time_left = harpoon_timer.time_left
				harpoon_cd_bar.value = harpoon_timer.wait_time - time_left
				_harpoon_fill_override.bg_color = COLOR_COOLDOWN_ACTIVE if time_left > 0 else COLOR_COOLDOWN_READY
		
		if is_instance_valid(buoy_cd_bar):
			var buoy_timer = GameState.player_node.get_node("BuoyCD")
			if buoy_timer:
				var time_left = buoy_timer.time_left
				buoy_cd_bar.value = buoy_timer.wait_time - time_left
				# Show or hide buoy panel based on upgrade status
				buoy_panel.visible = GameState.upgrades[GameState.Upgrade.SURFACE_BUOY] > 0
				_buoy_fill_override.bg_color = COLOR_COOLDOWN_ACTIVE if time_left > 0 else COLOR_COOLDOWN_READY
		
		if is_instance_valid(drone_cd_bar):
			var drone_timer = GameState.player_node.get_node("DroneCD")
			if drone_timer:
				var time_left = drone_timer.time_left
				drone_cd_bar.value = drone_timer.wait_time - time_left
				# Show or hide drone panel based on upgrade status
				drone_panel.visible = GameState.upgrades[GameState.Upgrade.DRONE_SELLING] > 0
				_drone_fill_override.bg_color = COLOR_COOLDOWN_ACTIVE if time_left > 0 else COLOR_COOLDOWN_READY
				
		if is_instance_valid(ak47_cd_bar) and is_instance_valid(ak47_label):
			var ak47 = GameState.player_node.get_node("Pivot/SmFishSubmarine/ak47_0406195124_texture")
			if ak47:
				# Only show panel if AK47 upgrade is purchased
				ak47_panel.visible = GameState.upgrades[GameState.Upgrade.AK47] > 0
				
				if GameState.upgrades[GameState.Upgrade.AK47] > 0:
					var max_ammo = ak47.get_max_ammo()
					if ak47.is_currently_reloading():
						ak47_cd_bar.value = ak47.get_reload_progress()
						_ak47_fill_override.bg_color = COLOR_COOLDOWN_ACTIVE
						ak47_label.text = "AK47\n(%d/%d)\nReloading" % [ak47.get_current_ammo(), max_ammo]
					else:
						ak47_cd_bar.value = 1.0
						_ak47_fill_override.bg_color = COLOR_COOLDOWN_READY
						ak47_label.text = "AK47\n(%d/%d)" % [ak47.get_current_ammo(), max_ammo]

	if is_instance_valid(pause_button):
		pause_button.disabled = GameState.isDocked
		if GameState.isDocked and pause_button.button_pressed:
			pause_button.set_pressed_no_signal(false)

# Update the depth indicator display with smooth movement
func update_depth_indicator(delta: float) -> void:
	if !is_instance_valid(marker_container) or !is_instance_valid(current_depth_marker) or !is_instance_valid(max_depth_marker):
		return
		
	# Update the text labels
	if is_instance_valid(current_depth_label):
		current_depth_label.text = "%sm" % int(GameState.depth)
	
	if is_instance_valid(max_depth_label):
		max_depth_label.text = "%sm" % int(GameState.maxDepthReached)
	
	# Calculate normalized positions (0.0 to 1.0) for the markers
	var current_normalized = clamp(GameState.depth / MAX_POSSIBLE_DEPTH, 0.0, 1.0)
	var max_normalized = clamp(GameState.maxDepthReached / MAX_POSSIBLE_DEPTH, 0.0, 1.0)
	
	# Calculate target positions
	var container_width = marker_container.size.x
	current_marker_target_pos = current_normalized * container_width - 1 # -1 for half width of 2px marker
	max_marker_target_pos = max_normalized * container_width - 1
	
	# Smoothly interpolate current positions toward target positions
	current_marker_pos = lerp(current_marker_pos, current_marker_target_pos, delta * marker_smoothing)
	max_marker_pos = lerp(max_marker_pos, max_marker_target_pos, delta * marker_smoothing)
	
	# Apply smoothed positions to markers
	current_depth_marker.position.x = current_marker_pos
	max_depth_marker.position.x = max_marker_pos

func _setup_pause_control() -> void:
	var ui_root := get_parent()
	if ui_root and ui_root.has_node("CenterContainer/PauseMenu"):
		pause_menu = ui_root.get_node("CenterContainer/PauseMenu")
	if is_instance_valid(pause_button):
		pause_button.toggled.connect(_on_pause_button_toggled)
	if pause_menu and pause_menu.has_signal("pause_state_changed"):
		pause_menu.pause_state_changed.connect(_on_pause_state_changed)
	if pause_menu and is_instance_valid(pause_button):
		pause_button.set_pressed_no_signal(pause_menu.is_paused)

func _on_pause_button_toggled(pressed: bool) -> void:
	if GameState.isDocked:
		if is_instance_valid(pause_button):
			pause_button.set_pressed_no_signal(false)
		return
	if pause_menu:
		if pressed and pause_menu.has_method("pause_game"):
			pause_menu.pause_game()
		elif not pressed and pause_menu.has_method("resume_game"):
			pause_menu.resume_game()
	else:
		Input.action_press("esc")
		Input.action_release("esc")

func _on_pause_state_changed(paused: bool) -> void:
	if is_instance_valid(pause_button):
		pause_button.set_pressed_no_signal(paused)
