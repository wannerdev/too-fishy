extends CharacterBody3D

@export var min_speed = 1
@export var max_speed = 2.5
@export var min_angle = -30
@export var max_angle = 30
@export var difficulty = 0
@export var rotation_cooldown = .1
@export var weight = 1
@export var price = 1
@export var is_shiny = false
@export var shiny_particles: GPUParticles3D

# Debug flag - set to true to enable detailed orientation debugging
@export var debug_orientation = false
# Debug flag for shader animation - set to true to track animation rate issues
@export var debug_shader_animation = false

@export var mesh_instance_path: NodePath
var mesh_instance: MeshInstance3D

var rotation_cooldown_left = 0
var speed = 0
var home: int
var type: int
var accumulated_shader_time: float = 0.0 # Custom time accumulator for shader
var debug_birth_time: float = 0.0 # Track when this fish was created for debugging
var debug_last_accum_time: float = 0.0 # Track previous accumulated time for rate calculation
var debug_last_real_time: float = 0.0 # Track real time for rate calculation

# Collision damage tuning (submarine ramming)
@export var collision_damage_cooldown: float = 0.25
@export var collision_damage_speed_boost: float = 0.4
@export var collision_speed_cap_multiplier: float = 1.8
@export var collision_health_base: float = 1.0
@export var collision_health_weight_factor: float = 0.2
var collision_health: float = 1.0
var collision_last_hit_time: float = -1000.0

# --- Shader Animation Speed Control (names describe rate of accumulation) ---
@export var base_anim_rate: float = 0.5 # Base rate for accumulated_shader_time
@export var speed_to_anim_rate_factor: float = 1.25 # Increased from 0.75
@export var min_effective_anim_rate: float = 0.2 # Min rate at which accumulated_shader_time advances
@export var max_effective_anim_rate: float = 1.6 # Max rate at which accumulated_shader_time advances
# --- End Shader Animation Speed Control ---

# WebGL Performance optimizations
var shader_update_timer: float = 0.0
var shader_update_interval: float = 0.033 # Update shader ~30fps instead of every frame
var is_webgl_build: bool = false

func _ready():
	# Detect WebGL build for performance optimizations
	is_webgl_build = OS.get_name() == "Web"
	
	# Reduce shader update frequency on WebGL
	if is_webgl_build:
		shader_update_interval = 0.05 # 20fps for shader updates on WebGL
	
	if type == 0 or type == 1: # Only update animation for fish_a (0) and fish_b (1)
		if mesh_instance_path:
			mesh_instance = get_node_or_null(mesh_instance_path)
			if !mesh_instance:
				printerr("  ERROR: Fish script could not find MeshInstance3D at path: ", mesh_instance_path, " for node: ", name)
		else:
			printerr("  ERROR: mesh_instance_path is not set for node: ", name)
	
	accumulated_shader_time = randf() * 2.0 * PI
	debug_birth_time = Time.get_time_dict_from_system()["second"] + Time.get_time_dict_from_system()["minute"] * 60 + Time.get_time_dict_from_system()["hour"] * 3600
	debug_last_accum_time = accumulated_shader_time
	debug_last_real_time = debug_birth_time
	
	# Disable shiny particles on WebGL for performance
	if is_webgl_build and shiny_particles:
		shiny_particles.visible = false
		shiny_particles.emitting = false

func removeFish():
	var fish_data = {
		"price": price,
		"weight": weight,
		"id": get_instance_id()
	}
	var my_fish = InvItem.new()
	my_fish.type = str(self.type)
	my_fish.weight = fish_data.weight
	my_fish.price = fish_data.price
	my_fish.id = fish_data.id
	my_fish.shiny = self.is_shiny
	queue_free()
	return my_fish
	
func _physics_process(delta: float) -> void:
	# print("Fish ", name, ": _physics_process, delta: ", delta) # Uncomment for very verbose logging
	if rotation_cooldown_left > 0:
		rotation_cooldown_left -= delta
	
	if get_slide_collision_count() > 0 && rotation_cooldown_left <= 0:
		rotation_cooldown_left = rotation_cooldown
		# Flip direction first
		rotate_y(deg_to_rad(180))
		# Normalize rotation to prevent accumulation errors
		normalize_y_rotation()
		# Then set new velocity with random angle
		var deg = randf_range(min_angle, max_angle)
		set_z_rotation_and_velocity(deg)
		# Extra validation after collision handling
		validate_orientation_velocity_match()
	
	if global_position.y >= -0.5:
		record_surface_achievement()
		queue_free()
		return
	elif global_position.y >= -0.75:
		if is_looking_up():
			var deg = randf_range(min_angle, min_angle / 2.0)
			set_z_rotation_and_velocity(deg)
	elif global_position.y <= GameState.fishes_lower_boarder:
		if !is_looking_up():
			var deg = randf_range(max_angle / 2.0, max_angle)
			set_z_rotation_and_velocity(deg)
	elif randf() < .004:
		set_z_rotation_and_velocity(randf_range(min_angle, max_angle))
			
	move_and_slide()
	
	# Validate orientation matches velocity (only check occasionally for performance)
	if randf() < 0.005: # Check ~0.5% of frames to reduce spam
		validate_orientation_velocity_match()
	
	# Update shader animation every frame (removed timer optimization)
	# The timer-based optimization was causing inconsistent animation rates
	if type == 0 or type == 1: # Only update animation for fish_a (0) and fish_b (1)
		update_shader_animation(delta)

func update_shader_animation(delta_time: float):
	if !mesh_instance: return
		# print("Fish ", name, ": mesh_instance is null in _physics_process. Skipping animation update.") # Uncomment for verbose logging
		
	var material_override = mesh_instance.get_surface_override_material(0)
	if !material_override: return
		# print("Fish ", name, ": material_override is null. Skipping animation update.") # Uncomment for verbose logging

	if !(material_override is ShaderMaterial): return
		# return
		# print("Fish ", name, ": material_override is not ShaderMaterial (Type: ", typeof(material_override), "). Skipping animation update.") # Uncomment for verbose logging
	
	# If we reach here, mesh_instance and material are valid
	var material: ShaderMaterial = material_override
	
	var current_speed: float = velocity.length()
	var effective_anim_rate = base_anim_rate + (current_speed * speed_to_anim_rate_factor)
	effective_anim_rate = clamp(effective_anim_rate, min_effective_anim_rate, max_effective_anim_rate)
	
	# Add the accumulated time since last update, scaled by animation rate
	accumulated_shader_time += delta_time * effective_anim_rate
	
	# Keep the accumulated time in a reasonable range to prevent floating point precision issues
	# Wrap around every ~6.28 seconds (2π) since most shader animations are cyclical
	var old_accum_time = accumulated_shader_time
	var two_pi = 2.0 * PI
	accumulated_shader_time = fmod(accumulated_shader_time, two_pi)
	
	# Debug: Track animation rate changes if enabled
	if debug_shader_animation and randf() < 0.05: # Increased sampling rate for better accuracy
		var current_time = Time.get_time_dict_from_system()["second"] + Time.get_time_dict_from_system()["minute"] * 60 + Time.get_time_dict_from_system()["hour"] * 3600
		var fish_age = current_time - debug_birth_time
		var time_increment = delta_time * effective_anim_rate
		
		# Calculate actual animation rate
		var real_time_elapsed = current_time - debug_last_real_time
		var accum_time_change = accumulated_shader_time - debug_last_accum_time
		# Handle wrapping
		if accum_time_change < -3.0: # Wrapped backwards
			accum_time_change += 2 * PI
		elif accum_time_change > 3.0: # Wrapped forwards
			accum_time_change -= 2 * PI
		var actual_rate = accum_time_change / real_time_elapsed if real_time_elapsed > 0 else 0
		
		print("Fish ", name, ": age=", fish_age, "s anim_rate=", effective_anim_rate, " actual_rate=", actual_rate, " accum_time=", accumulated_shader_time, " delta=", delta_time)
		
		debug_last_accum_time = accumulated_shader_time
		debug_last_real_time = current_time
		
		if delta_time > 0.05: # Flag unusually large delta times
			print("  -> WARNING: Large delta_time: ", delta_time)
		if abs(old_accum_time - accumulated_shader_time) > 0.1:
			print("  -> Wrapped from ", old_accum_time, " to ", accumulated_shader_time)
	material.set_shader_parameter("animation_time_input", accumulated_shader_time)

func initialize(mStart_position, mHome, mMin_speed, mMax_speed,
mDifficulty, mMin_weight, mMax_weight, price_weight_multiplier, mType, weight_multiplier, mIs_shiny = false):
	self.home = mHome
	self.min_speed = mMin_speed
	self.max_speed = mMax_speed
	self.difficulty = mDifficulty
	self.weight = clamp(randf_range(mMin_weight, mMax_weight) * weight_multiplier,
							mMin_weight, mMax_weight)
	self.price = round(weight * price_weight_multiplier)
	self.type = mType
	self.is_shiny = mIs_shiny
	collision_health = max(0.5, collision_health_base + (self.weight * collision_health_weight_factor))
	
	scale = get_scale_for_weight(mMax_weight, mMin_weight, weight)
	
	if self.is_shiny:
		price = price * 3
	speed = randf_range(min_speed, max_speed)
	position = mStart_position
	add_to_group("fishes")
	
	set_z_rotation_and_velocity(randf_range(min_angle, max_angle))
	
	if self.shiny_particles != null:
		# Create a new material instance to prevent shared material modifications
		var particles_material = self.shiny_particles.process_material.duplicate()
		self.shiny_particles.process_material = particles_material
		
		# Set color based on shiny status
		if self.is_shiny:
			particles_material.color = Color(1.0, 0.9, 0.2) # Gold color
		else:
			particles_material.color = Color(0.2, 0.7, 1.0) # Blue color
			
		# Enable particles if shiny
		shiny_particles.emitting = self.is_shiny
	
func set_z_rotation_and_velocity(deg: float) -> void:
	rotation[2] = 0
	var rad = deg_to_rad(deg)
	var facing_left = is_facing_left()
	
	# Debug: Uncomment to track direction changes
	# print("Fish ", name, ": set_z_rotation_and_velocity deg=", deg, " facing_left=", facing_left, " rotation.y=", rotation.y)
		
	if facing_left:
		rotate_z(-rad)
		velocity = Vector3.MODEL_RIGHT * speed
		velocity = velocity.rotated(Vector3.FORWARD, rotation.z)
	else:
		rotate_z(rad)
		velocity = Vector3.MODEL_LEFT * speed
		velocity = velocity.rotated(Vector3.BACK, rotation.z)
	
	# Validate that orientation matches velocity direction
	validate_orientation_velocity_match()
	
	
# Validate that the fish's visual orientation matches its velocity direction
func validate_orientation_velocity_match():
	if velocity.length() < 0.1: # Skip validation if fish is barely moving
		return
	
	var facing_left = is_facing_left()
	var moving_left = velocity.x < 0
	var moving_right = velocity.x > 0
	
	# CORRECT coordinate system based on debug output:
	# - Vector3.MODEL_RIGHT = (-1, 0, 0) → negative X direction
	# - Vector3.MODEL_LEFT = (1, 0, 0) → positive X direction
	# Therefore:
	# - facing_left = true → velocity = Vector3.MODEL_RIGHT → moves LEFT (negative X)
	# - facing_left = false → velocity = Vector3.MODEL_LEFT → moves RIGHT (positive X)
	
	var orientation_matches = (facing_left && moving_left) || (!facing_left && moving_right)
	
	if !orientation_matches:
		print("WARNING: Fish ", name, " orientation mismatch!")
		print("  - facing_left: ", facing_left)
		print("  - rotation.y: ", rotation.y)
		print("  - velocity.x: ", velocity.x)
		print("  - Expected: facing_left=", facing_left, " should move ", "LEFT" if facing_left else "RIGHT")
		print("  - Actual: moving ", "LEFT" if moving_left else "RIGHT")
		
		# Fix the mismatch by flipping the fish to match velocity direction
		print("  - Attempting to fix orientation...")
		var old_facing_left = facing_left
		rotate_y(deg_to_rad(180))
		normalize_y_rotation()
		var new_facing_left = is_facing_left()
		print("  - Fixed. New rotation.y: ", rotation.y, " facing_left: ", old_facing_left, " → ", new_facing_left)
		
		# Verify the fix worked
		var new_moving_left = velocity.x < 0
		var new_moving_right = velocity.x > 0
		var fixed_orientation_matches = (new_facing_left && new_moving_left) || (!new_facing_left && new_moving_right)
		if !fixed_orientation_matches:
			print("  - ERROR: Fix failed! Still mismatched after rotation.")
	elif debug_orientation:
		# Optional detailed logging when debug mode is enabled
		print("Fish ", name, " orientation OK: facing_left=", facing_left,
			  " moving=", "LEFT" if moving_left else "RIGHT")

# Helper function to normalize Y rotation and prevent accumulation errors
func normalize_y_rotation():
	var old_rotation = rotation.y
	rotation.y = fmod(rotation.y, 2 * PI)
	if rotation.y > PI:
		rotation.y -= 2 * PI
	elif rotation.y < -PI:
		rotation.y += 2 * PI
	
	# Debug: Uncomment to track rotation normalization
	# if abs(old_rotation - rotation.y) > 0.1:
	#	print("Fish ", name, ": Normalized rotation.y from ", old_rotation, " to ", rotation.y)

func is_facing_left() -> bool:
	# Normalize rotation.y to handle edge cases and floating point precision
	var normalized_y = fmod(rotation.y, 2 * PI)
	if normalized_y > PI:
		normalized_y -= 2 * PI
	elif normalized_y < -PI:
		normalized_y += 2 * PI
	
	# Fish faces left when rotated around 180° (π radians)
	# This includes both positive π and negative π values
	# Range: roughly between π/2 and 3π/2, or in normalized form: |normalized_y| > π/2
	return abs(normalized_y) > (PI / 2 + 0.01) # Small epsilon to avoid floating point issues
	
func is_looking_up() -> bool:
	return rotation[2] > 0
	
func scatter(body: Node3D) -> void:
	if (body.global_position.x < global_position.x && is_facing_left() or
		  body.global_position.x > global_position.x && !is_facing_left()):
		rotate_y(deg_to_rad(180))
		# Normalize rotation to prevent accumulation errors
		normalize_y_rotation()
	if (body.global_position.y < global_position.y):
		set_z_rotation_and_velocity(randf_range(35, 55))
	else:
		set_z_rotation_and_velocity(randf_range(-35, -55))
	
	# Extra validation after scattering
	validate_orientation_velocity_match()
	
	pass

func apply_collision_damage(amount: float, hitter: Node3D) -> bool:
	var now = Time.get_ticks_msec() / 1000.0
	if now - collision_last_hit_time < collision_damage_cooldown:
		return false
	collision_last_hit_time = now

	collision_health -= max(0.1, amount)
	if collision_health <= 0.0:
		if hitter and hitter.has_method("catch_fish"):
			hitter.catch_fish(self)
		else:
			queue_free()
		return true

	if hitter:
		scatter(hitter)
	
	# Burst speed feedback on collision without becoming uncontrollable
	speed = min(max_speed * collision_speed_cap_multiplier, speed + collision_damage_speed_boost)
	set_z_rotation_and_velocity(randf_range(min_angle, max_angle))
	return false

func get_scale_for_weight(max_weight, min_weight, mWeight) -> Vector3:
	var weight_range = (max_weight - min_weight)
	var normal_weight = weight_range / 2
	var weight_sanitized = mWeight - min_weight
	var weight_factor = weight_sanitized / normal_weight
	var scale_factor = 1 + weight_factor * .3
	return Vector3(scale_factor, scale_factor, 1)

# Record that this fish type has reached the surface for achievements
func record_surface_achievement():
	# Only record if we can find the achievement system
	var achievement_manager = get_node_or_null("/root/AchievementManager")
	if achievement_manager and achievement_manager.has_method("get_achievement_system"):
		var achievement_system = achievement_manager.get_achievement_system()
		achievement_system.record_fish_surface(self.type)
