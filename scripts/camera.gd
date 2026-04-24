extends Camera3D

@export var directional_light: DirectionalLight3D

# Reference resolution (optimized for 1920x1080)
const REFERENCE_WIDTH = 1920
const REFERENCE_HEIGHT = 1080
const REFERENCE_ASPECT = float(REFERENCE_WIDTH) / float(REFERENCE_HEIGHT)

# Camera parameters
var base_fov = 75.0
var min_fov = 65.0
var max_fov = 85.0

var environment_color_map = {
	GameState.Stage.SURFACE: Color.from_rgba8(30, 110, 163, 255),
	GameState.Stage.DEEP: Color.from_rgba8(30, 110, 163, 255),
	GameState.Stage.DEEPER: Color.from_rgba8(12, 59, 94, 255),
	GameState.Stage.SUPERDEEP: Color.from_rgba8(12, 59, 94, 255),
	GameState.Stage.HOT: Color.from_rgba8(217, 103, 4, 255),
	GameState.Stage.LAVA: Color.from_rgba8(217, 103, 4, 255),
	GameState.Stage.VOID: Color.from_rgba8(10, 10, 10, 255)
}

var environment_light_map = {
	GameState.Stage.SURFACE: 1.0,
	GameState.Stage.DEEP: 0.8,
	GameState.Stage.DEEPER: 0.5,
	GameState.Stage.SUPERDEEP: 0.1,
	GameState.Stage.HOT: 0.2,
	GameState.Stage.LAVA: 1.0,
	GameState.Stage.VOID: 0.1,
}

func _ready():
	# Initialize with SURFACE stage instead of integer 0
	environment.fog_light_color = environment_color_map[GameState.Stage.SURFACE]
	directional_light = get_node("/root/Node3D/DirectionalLight3D")
	
	# Connect to window resize signal to adjust camera parameters
	get_viewport().size_changed.connect(_adjust_camera_for_aspect_ratio)
	
	# Initial adjustment
	_adjust_camera_for_aspect_ratio()

func _adjust_camera_for_aspect_ratio():
	var window_size = DisplayServer.window_get_size()
	var current_aspect = float(window_size.x) / float(window_size.y)
	
	# Adjust FOV based on aspect ratio difference
	if current_aspect > REFERENCE_ASPECT:
		# Wider screen - increase FOV to see more horizontally
		var aspect_difference = current_aspect / REFERENCE_ASPECT
		var new_fov = base_fov * min(1.2, aspect_difference)
		fov = clamp(new_fov, min_fov, max_fov)
	elif current_aspect < REFERENCE_ASPECT:
		# Taller screen - might need to adjust FOV differently
		var aspect_difference = REFERENCE_ASPECT / current_aspect
		var new_fov = base_fov * max(0.9, 2.0 - aspect_difference)
		fov = clamp(new_fov, min_fov, max_fov)
	else:
		# Same aspect ratio as reference
		fov = base_fov
	
	if GameState.DEBUG_PRINTS:
		print("Window size: ", window_size, " - Aspect ratio: ", current_aspect, " - FOV: ", fov)

func change_section_environment(sectionType):
	# Validate that the section type exists in our maps
	if not environment_color_map.has(sectionType):
		print("Warning: Unknown section type ", sectionType, " - using SURFACE default")
		sectionType = GameState.Stage.SURFACE
	
	var tween = self.create_tween()
	tween.tween_property(environment, "fog_light_color", environment_color_map[sectionType], 1)
	if directional_light and environment_light_map.has(sectionType):
		tween.tween_property(directional_light, "light_energy", environment_light_map[sectionType], 3)
		
func _process(_delta: float) -> void:
	if global_position.y <= -0.2:
		environment.fog_enabled = true
	else:
		environment.fog_enabled = false
