extends Node3D

@export var sectionType: GameState.Stage = GameState.Stage.SURFACE
@export var lastSectionType: GameState.Stage = GameState.Stage.SURFACE

@export var water: MeshInstance3D
@export var spawn_marker_a: Marker3D
@export var spawn_marker_b: Marker3D
@export var background_mat: StandardMaterial3D
@export var lava_vine_mat: StandardMaterial3D = preload("res://materials/walls/veins_lava.tres")
@export var particles_enabled: bool = true
@export var add_destructible_barriers: bool = true # Add option to toggle barrier generation

var sectionBackgroundMap: Dictionary = {
	GameState.Stage.SURFACE: preload("res://materials/backgrounds/bg_loop.tres"),
	GameState.Stage.DEEP: preload("res://materials/backgrounds/bg_loop.tres"),
	GameState.Stage.DEEPER: preload("res://materials/backgrounds/bg_loop.tres"),
	GameState.Stage.SUPERDEEP: preload("res://materials/backgrounds/bg_loop.tres"),
	GameState.Stage.HOT: preload("res://materials/backgrounds/bg_loop.tres"),
	GameState.Stage.LAVA: preload("res://materials/backgrounds/bg_lava.tres"),
	GameState.Stage.VOID: preload("res://materials/backgrounds/bg_lava.tres"),
}

var sectionTransitions: Dictionary = {
	GameState.Stage.HOT: preload("res://materials/backgrounds/bg_deep_to_lava.tres"),
	GameState.Stage.LAVA: preload("res://materials/backgrounds/bg_lava_to_void.tres"),
}

# Dictionary defining which stage transitions should have destructible barriers
var stageTransitionsWithBarriers: Dictionary = {
	GameState.Stage.DEEP: GameState.Stage.DEEPER,
	GameState.Stage.DEEPER: GameState.Stage.SUPERDEEP,
	GameState.Stage.SUPERDEEP: GameState.Stage.HOT,
	GameState.Stage.HOT: GameState.Stage.LAVA,
	GameState.Stage.LAVA: GameState.Stage.VOID
}

var depth: int = 0
var is_on_screen: bool = false
var destroyable_barrier_scene = preload("res://scenes/destroyable_barier.tscn")

func setDepth(d: int):
	depth = d

func spawn_fish(spawn_all: bool = false):
	if !FishesConfig.fishSectionMap.has(sectionType):
		print("No fish spawns defined for ", GameState.Stage.keys()[sectionType])
		return
	var amount = len(get_tree().get_nodes_in_group("fishes").filter(func(x): return x.home == self.get_instance_id()))
	while amount < FishesConfig.fishSectionMap[sectionType].max_fish_amount:
		var r = randf()
		var spawn_pos = Vector3(randf_range(spawn_marker_a.position.x, spawn_marker_b.position.x),
							randf_range(spawn_marker_a.position.y, spawn_marker_b.position.y), 1)
		spawn_pos.z = -0.3
		# Get available fish types (excluding boss mini if boss not defeated)
		var available_fish_types = {}
		var total_spawn_rate = 0.0
		
		for type in FishesConfig.fishSectionMap[sectionType].spawnRates:
			# Skip boss mini fish if boss hasn't been defeated yet
			if type == FishesConfig.FishType.BOSS_MINI and not GameState.is_boss_defeated():
				continue
			available_fish_types[type] = FishesConfig.fishSectionMap[sectionType].spawnRates[type]
			total_spawn_rate += FishesConfig.fishSectionMap[sectionType].spawnRates[type]
		
		# Normalize random value to available fish types
		r = r * total_spawn_rate
		
		var commulative_spawn_rate = 0
		var spawn_fish_config = null
		var fishType = null
		for type in available_fish_types:
			commulative_spawn_rate += available_fish_types[type]
			if r <= commulative_spawn_rate:
				fishType = type
				spawn_fish_config = FishesConfig.fishConfigMap[type]
				break
		var fish = spawn_fish_config.scene.instantiate()
		
		var shiny = randf() < FishesConfig.fishSectionMap[sectionType].shiny_rate
		var weight_multiplier = FishesConfig.fishSectionMap[sectionType].weight_multiplier
	
		fish.initialize(spawn_pos, self.get_instance_id(),
			spawn_fish_config.speed_min, spawn_fish_config.speed_max,
			spawn_fish_config.difficulty,
			spawn_fish_config.weight_min, spawn_fish_config.weight_max,
			spawn_fish_config.price_weight_multiplier,
			fishType,
			weight_multiplier,
			shiny
			)
		add_child(fish)
		amount += 1
		if !spawn_all:
			break

# Function to add destructible barriers at section borders
func add_barrier_boxes():
	# Check if this is a transition that should have barriers
	var should_add_barriers = false
	var barrier_health = 1
	
	for from_stage in stageTransitionsWithBarriers:
		if lastSectionType == from_stage and sectionType == stageTransitionsWithBarriers[from_stage]:
			should_add_barriers = true
			
			# Increase barrier health as the player goes deeper
			match sectionType:
				GameState.Stage.DEEPER:
					barrier_health = 2
				GameState.Stage.SUPERDEEP:
					barrier_health = 3
				GameState.Stage.HOT:
					barrier_health = 4
				GameState.Stage.LAVA:
					barrier_health = 5
				GameState.Stage.VOID:
					barrier_health = 6
			
			break
			
	if should_add_barriers:
		# Calculate the barrier line position - place at the top of the section
		# This creates a horizontal barrier at the section boundary
		var barrier_y_position = position.y + 10
		
		# Create a horizontal line of barriers blocking the downward path
		# Width calculation based on section width from left to right barrier
		var left_barrier_x = $LeftBarrier.position.x
		var right_barrier_x = $RightBarrier.position.x
		var section_width = abs(right_barrier_x - left_barrier_x)
		var num_barriers = 9 # Number of barriers to place in a row
		var spacing = section_width / (num_barriers - 1) # Even spacing
		
		# Place barriers in a horizontal line
		for i in range(num_barriers):
			var barrier = destroyable_barrier_scene.instantiate()
			barrier.max_health = barrier_health
			barrier.current_health = barrier_health
			
			# Calculate x position with even spacing
			var x_pos = left_barrier_x + (spacing * i)
			
			# Position the barriers in a horizontal line at the section boundary
			barrier.position = Vector3(x_pos, barrier_y_position, 0)
			
			# Use default rotation so the "NO LOOT" text faces forward
			# Adjust z position slightly to ensure proper visibility
			barrier.position.z = -0.5
			
			add_child(barrier)

func _ready() -> void:
	# Add this section to the sections group for settings management
	add_to_group("sections")
	
	# More aggressive particle optimization for WebGL
	var is_webgl = OS.get_name() == "Web"
	var should_disable_particles = (not particles_enabled or
									SettingsManager.should_disable_environmental_particles())
	
	if should_disable_particles or is_webgl:
		disableParticles()
	elif is_webgl:
		# Reduce particle counts for WebGL performance
		optimizeParticlesForWebGL()
	
	spawn_fish(true)
	
	# Add destructible barriers if enabled
	if add_destructible_barriers:
		add_barrier_boxes()
	
	var shouldBeTransition = false
	var shouldTransitionTo: GameState.Stage = GameState.Stage.SURFACE
	if sectionTransitions.has(lastSectionType) and sectionTransitions.has(sectionType):
		if lastSectionType == GameState.Stage.HOT and sectionType == GameState.Stage.LAVA:
			shouldBeTransition = true
			shouldTransitionTo = GameState.Stage.HOT
	
	if shouldBeTransition:
		background_mat = sectionTransitions[shouldTransitionTo]
		$Background.set_surface_override_material(0, background_mat)
	else:
		if background_mat == null:
			if sectionBackgroundMap[sectionType] != null:
				background_mat = sectionBackgroundMap[sectionType]
			else:
				print("No background material found for ", GameState.Stage.keys()[sectionType])
				background_mat = sectionBackgroundMap[GameState.Stage.SURFACE]
	$Background.set_surface_override_material(0, background_mat)

	if sectionType == GameState.Stage.LAVA:
		$LeftWall/Node3D2/Veins.set_surface_override_material(0, lava_vine_mat)
		$LeftWall2/Node3D2/Veins.set_surface_override_material(0, lava_vine_mat)
	
func screen_entered() -> void:
	is_on_screen = true

func screen_exited() -> void:
	is_on_screen = false

func _on_particles_button_pressed() -> void:
	disableParticles()

func disableParticles() -> void:
		$Debris.visible = false
		$Bubbles.visible = false
		$Debris.emitting = false
		$Bubbles.emitting = false

func optimizeParticlesForWebGL() -> void:
	# Reduce particle counts significantly for WebGL performance
	if has_node("Bubbles"):
		var bubbles = $Bubbles
		var bubble_material = bubbles.process_material as ParticleProcessMaterial
		if bubble_material:
			# Reduce from 400 to 50 particles
			bubbles.amount = 50
			bubble_material.emission_shape_scale = Vector3(1.0, 1.0, 1.0) # Smaller emission area
	
	if has_node("Debris"):
		var debris = $Debris
		var debris_material = debris.process_material as ParticleProcessMaterial
		if debris_material:
			# Reduce from 5000 to 100 particles
			debris.amount = 100
			debris_material.emission_shape_scale = Vector3(1.0, 1.0, 1.0) # Smaller emission area
			debris.lifetime = 2.0 # Shorter lifetime

func respawn_timer_expired() -> void:
	if !is_on_screen:
		spawn_fish()
