extends Area3D
class_name MindControlZone

# Mind control zone properties
@export var zone_radius: float = 0.8
@export var pull_strength: float = 11
@export var escape_threshold: float = 16  # Required upgrade level to escape
@export var max_pull_distance: float = 15.0
@export var effect_intensity: float = 1.0

# Zone state
var is_active: bool = true
var players_in_zone: Array[Node3D] = []
var zone_center: Vector3

# Visual components
@onready var visual_effect: MeshInstance3D = $VisualEffect
@onready var collision_shape: CollisionShape3D = $CollisionShape3D
@onready var pull_timer: Timer = $PullTimer
@onready var audio_player: AudioStreamPlayer3D = $AudioStreamPlayer3D

# Shader material reference
var shader_material: ShaderMaterial

func _ready():
	# Set up the zone
	zone_center = global_position
	setup_collision_shape()
	setup_visual_effect()
	setup_audio()
	
	# Connect signals
	body_entered.connect(_on_body_entered)
	body_exited.connect(_on_body_exited)
	pull_timer.timeout.connect(_on_pull_timer_timeout)
	
	print("Mind Control Zone initialized at: ", zone_center)

func setup_collision_shape():
	# Create a sphere collision shape
	var sphere_shape = SphereShape3D.new()
	sphere_shape.radius = zone_radius
	collision_shape.shape = sphere_shape

func setup_visual_effect():
	# Create a sphere mesh for the visual effect
	var sphere_mesh = SphereMesh.new()
	sphere_mesh.radius = zone_radius
	sphere_mesh.height = zone_radius * 2
	sphere_mesh.radial_segments = 32
	sphere_mesh.rings = 16
	
	visual_effect.mesh = sphere_mesh
	
	# Get the shader material - try multiple ways to access it
	shader_material = visual_effect.material_override
	if not shader_material:
		shader_material = visual_effect.material_overlay
	if not shader_material:
		shader_material = visual_effect.get_surface_override_material(0)
	
	# If still no material, create one from the scene's material
	if not shader_material:
		print("Creating new shader material for mind control zone")
		var shader_res = preload("res://Shaders/mind_control_effect.gdshader")
		shader_material = ShaderMaterial.new()
		shader_material.shader = shader_res
		visual_effect.material_override = shader_material
	
	# Set initial shader parameters
	if shader_material:
		update_shader_parameters()
	else:
		print("ERROR: Could not set up shader material for mind control zone")

func setup_audio():
	# TODO: Add mind control sound effect
	pass

func update_shader_parameters():
	if shader_material:
		shader_material.set_shader_parameter("pull_strength", effect_intensity)
		shader_material.set_shader_parameter("wobble_frequency", 2.0 + effect_intensity)
		shader_material.set_shader_parameter("wobble_amplitude", 0.1 + effect_intensity * 0.05)
		shader_material.set_shader_parameter("distortion_scale", 0.15 + effect_intensity * 0.1)
		shader_material.set_shader_parameter("time_speed", 1.0)
		shader_material.set_shader_parameter("custom_time", 0.0)  # Initialize custom time
		shader_material.set_shader_parameter("color_tint", Color(0.8, 0.3, 0.9, 0.6))
		print("Shader parameters set successfully")
	else:
		print("ERROR: shader_material is null in update_shader_parameters")

func _process(delta):
	if not is_active:
		return
	
	# Update shader time parameter for controlled animation
	if shader_material:
		var current_time = shader_material.get_shader_parameter("custom_time")
		if current_time == null:
			current_time = 0.0
		shader_material.set_shader_parameter("custom_time", current_time + delta)
	
	# Apply continuous pull effect to players in zone
	for player in players_in_zone:
		if is_instance_valid(player):
			apply_mind_control_effect(player, delta)

func _on_body_entered(body):
	if body.has_method("add_trauma"):  # Check if it's the player
		print("Player entered mind control zone")
		players_in_zone.append(body)
		
		# Add initial trauma effect
		body.add_trauma(0.3)
		
		# Start audio effect
		if audio_player:
			audio_player.play()

func _on_body_exited(body):
	if body in players_in_zone:
		print("Player exited mind control zone")
		players_in_zone.erase(body)
		
		# Stop audio if no players in zone
		if players_in_zone.is_empty() and audio_player:
			audio_player.stop()

func _on_pull_timer_timeout():
	# Increase effect intensity over time for players in zone
	if not players_in_zone.is_empty():
		effect_intensity = min(effect_intensity + 0.1, 3.0)
		update_shader_parameters()

func apply_mind_control_effect(player, delta):
	# Calculate distance from zone center
	var distance_to_center = player.global_position.distance_to(zone_center)
	
	# Check if player can escape based on upgrades
	var can_escape = check_escape_capability(player)
	
	if not can_escape or distance_to_center < max_pull_distance:
		# Calculate pull direction (towards center)
		var pull_direction = (zone_center - player.global_position).normalized()
		
		# Calculate pull force based on distance (stronger when closer)
		var distance_factor = 1.0 - (distance_to_center / max_pull_distance)
		distance_factor = max(distance_factor, 0.0)
		
		var pull_force = pull_direction * pull_strength * distance_factor * effect_intensity * delta
		
		# Apply the pull force to player's velocity
		if player.has_method("apply_external_force"):
			player.apply_external_force(pull_force)
		else:
			# Fallback: directly modify position
			player.global_position += pull_force * 0.5
		
		# Add continuous trauma while in the zone
		if player.has_method("add_trauma"):
			player.add_trauma(0.02 * effect_intensity * distance_factor)

func check_escape_capability(player) -> bool:
	# Check if player has sufficient upgrades to escape
	# This could be based on engine power, special equipment, etc.
	
	# Access GameState to check upgrades
	var total_speed_upgrades = GameState.upgrades.get(GameState.Upgrade.HOR_SPEED, 0) + GameState.upgrades.get(GameState.Upgrade.VERT_SPEED, 0)
	var depth_resistance = GameState.upgrades.get(GameState.Upgrade.DEPTH_RESISTANCE, 0)
	
	# Player needs a combination of speed and depth resistance to escape
	var escape_power = total_speed_upgrades + depth_resistance
	
	return escape_power >= escape_threshold

func activate_zone():
	"""Activate the mind control zone"""
	is_active = true
	visible = true
	monitoring = true
	effect_intensity = 1.0
	update_shader_parameters()
	print("Mind control zone activated")

func deactivate_zone():
	"""Deactivate the mind control zone"""
	is_active = false
	visible = false
	monitoring = false
	
	# Clear players and stop effects
	for player in players_in_zone:
		_on_body_exited(player)
	players_in_zone.clear()
	
	if audio_player:
		audio_player.stop()
	
	print("Mind control zone deactivated")

func set_zone_radius(new_radius: float):
	"""Dynamically change the zone radius"""
	zone_radius = new_radius
	setup_collision_shape()
	setup_visual_effect()

func set_pull_strength(new_strength: float):
	"""Dynamically change the pull strength"""
	pull_strength = new_strength

func apply_external_force(player, force: Vector3):
	"""Helper method to apply force to player if they support it"""
	if player.has_method("apply_external_force"):
		player.apply_external_force(force)
	elif "velocity" in player:
		player.velocity += force
	elif "target_velocity" in player:
		player.target_velocity += force
