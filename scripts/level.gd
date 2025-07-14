extends Node3D

@export var section: PackedScene
@export var level_wrapper: Node3D
@export var player: CharacterBody3D
@export var boss: PackedScene
@export var boss_section: PackedScene

@export var sectionHeight: float
@export var preloadSectionsCount: int

var last_section_type: GameState.Stage = GameState.Stage.SURFACE

var lastSpawned = -35
var snappedDepth = 0

# Invisible barrier for intro mission
var invisible_barrier: StaticBody3D = null
var intro_scene = preload("res://scenes/intro_scene.tscn")
var current_player: CharacterBody3D = null
var intro_scene_spawned = false

func _ready():
	# Immediately setup intro mission if active
	if GameState.is_intro_mission_active():
		# Ensure game state is properly reset for intro mission
		GameState.death_screen = false
		GameState.paused = false
		get_tree().paused = false
		
		# Force hide any visible UI elements that might be left over
		call_deferred("force_hide_death_ui")
		
		if invisible_barrier == null:
			setup_invisible_barrier()
		if current_player == null:
			switch_to_friend_player()

func _process(_delta: float) -> void:
	GameState.setDepth(int(player.position.y) * -1)
	snappedDepth = snapped(player.position.y, 1) * -1
	if player.position.y < (lastSpawned):
		spawnNewSection(lastSpawned - sectionHeight)
	if GameState.maxDepthReached > Boss.boss_spawn_height && Boss.boss_spawned == false && Boss.boss_defeated_permanently == false:
		var boss_spawn_loc = (GameState.maxDepthReached * -1) - 25
		spawnBoss(boss_spawn_loc)
	
	# During intro mission, ensure boss spawns when friend reaches deep hot zone
	if GameState.is_intro_mission_active() && Boss.boss_spawned == false && GameState.depth >= 500:
		if Boss.boss_defeated_permanently == false:
			var boss_spawn_loc = -550  # Spawn boss deeper in lava zone
			spawnBoss(boss_spawn_loc)
	
	# Manage intro mission setup
	if GameState.is_intro_mission_active():
		if invisible_barrier == null:
			setup_invisible_barrier()
		if current_player == null:
			switch_to_friend_player()
	else:
		if invisible_barrier != null:
			remove_invisible_barrier()
		if current_player != null:
			switch_back_to_original_player()
	
	Boss.process_dialog_depth()
	
func spawnNewSection(mPosition: float):
	
	var newSection = section.instantiate()
	newSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	newSection.sectionType = GameState.depthStageMap[i]
	newSection.lastSectionType = last_section_type
	lastSpawned = mPosition
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1
	if mPosition <= -50:
		newSection.setDepth(mPosition * -1)
	add_child(newSection)
	last_section_type = GameState.depthStageMap[i]

func spawnBoss(mPosition: float):
	print("Spawn boss", mPosition)
	var spawned_boss = boss.instantiate()
	spawned_boss.position.y = mPosition
	spawned_boss.position.z = -0.33
	spawned_boss.position.x = -5
	spawned_boss.player = player
	add_child(spawned_boss)
	Boss.setBossSpawned(spawned_boss)
	
	mPosition = lastSpawned - sectionHeight
	var bossSection = boss_section.instantiate()
	bossSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	
	lastSpawned = mPosition + 50
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1

	add_child(bossSection)
	
	# Hide loot boxes during intro mission since friend has no pickaxe
	if GameState.is_intro_mission_active():
		hide_loot_boxes_in_boss_section(bossSection)

func setup_invisible_barrier():
	"""Create invisible barrier above intro zone for intro mission"""
	if invisible_barrier != null:
		return  # Already exists
	
	print("Setting up invisible barrier for intro mission")
	invisible_barrier = StaticBody3D.new()
	invisible_barrier.name = "InvisibleBarrier"
	
	# Position above intro starting area (friend starts at y=-450, barrier at y=-420)
	invisible_barrier.position = Vector3(0, -420, 0)
	
	# Create collision shape - large horizontal plane
	var collision_shape = CollisionShape3D.new()
	var box_shape = BoxShape3D.new()
	box_shape.size = Vector3(100, 1, 100)  # Wide horizontal barrier
	collision_shape.shape = box_shape
	
	invisible_barrier.add_child(collision_shape)
	add_child(invisible_barrier)

func remove_invisible_barrier():
	if invisible_barrier != null:
		print("Removing invisible barrier")
		invisible_barrier.queue_free()
		invisible_barrier = null

func switch_to_friend_player():
	if current_player != null:
		return  # Already switched
	
	print("Switching to friend submarine appearance for intro mission")
	
	# Spawn intro scene if not already spawned
	if not intro_scene_spawned:
		spawn_intro_scene()
	
	# Position player directly in the hot zone (depth 400+ corresponds to hot zone)
	player.position = Vector3(-8, -450, 0.33)  # Start in hot zone at depth 450
	
	# Set depth to starting position (hot zone) FIRST
	GameState.setDepth(450)  # Starting depth in hot zone
	
	print("Player positioned at: ", player.position)
	print("Depth manually set to: ", GameState.depth)
	print("Current player stage: ", GameState.playerInStage)
	
	# Give player full health and temporary invulnerability BEFORE switching appearance
	GameState.health = 100
	player.can_be_hurt = false  # Re-enable invulnerability during setup
	
	print("Intro mission setup - Health set to: ", GameState.health, " Can be hurt: ", player.can_be_hurt)
	
	# Switch player to friend submarine appearance (this applies upgrades)
	player.switch_to_friend_submarine()
	
	# Mark that we've switched to friend mode
	current_player = player
	
	# Enable vulnerability after a longer delay to ensure everything is properly set up
	get_tree().create_timer(3.0).timeout.connect(func():
		if GameState.is_intro_mission_active():
			player.can_be_hurt = true
			print("Friend submarine invulnerability ended - can now take damage")
			print("Final health check: ", GameState.health)
			print("Final depth check: ", GameState.depth)
			print("Final stage check: ", GameState.playerInStage)
	)
	
	print("Player switched to friend submarine appearance at: ", player.position)

func switch_back_to_original_player():
	if current_player == null:
		return  # Not switched
	
	print("Switching back to normal submarine appearance")
	
	# Store friend death position for static model placement
	var friend_death_pos = player.position
	
	# Create static friend model at death position for visual continuity
	create_static_friend_model(friend_death_pos)
	
	# Switch back to normal submarine appearance
	player.switch_to_normal_submarine()
	
	# Move player to surface
	player.position = Vector3(-8, 0, 0.33)
	
	# Reset depth to surface
	GameState.setDepth(0)
	
	# Hide boss health bar since player is no longer in boss fight
	Boss.hide_boss_health_bar()
	
	# Clean up intro scene
	cleanup_intro_scene()
	
	# Complete intro mission
	GameState.complete_intro_mission(friend_death_pos)
	
	# Restore normal UI state
	restore_normal_ui()
	
	# Mark that we've switched back
	current_player = null
	
	print("Switched back to normal submarine appearance at surface")

func force_hide_death_ui():
	"""Force hide any death screen or shop UI elements"""
	print("Force hiding death UI elements")
	
	# Find and hide death screen with correct path
	var death_screen = get_node_or_null("/root/Node3D/UI/CenterContainer/DeathScreen")
	if death_screen:
		death_screen.visible = false
		print("Death screen hidden")
	else:
		print("Death screen not found")
	
	# Find and hide upgrade menu with correct path
	var upgrade_menu = get_node_or_null("/root/Node3D/UI/CenterContainer/Upgrades")
	if upgrade_menu:
		upgrade_menu.visible = false
		print("Upgrade menu hidden")
	else:
		print("Upgrade menu not found")
	
	# Ensure game is unpaused
	GameState.paused = false
	GameState.death_screen = false
	get_tree().paused = false
	print("Game state reset: paused =", GameState.paused, "death_screen =", GameState.death_screen)

func restore_normal_ui():
	"""Restore normal UI state after intro mission ends by triggering normal death sequence"""
	print("Restoring normal UI state via death sequence")
	
	# Set health to 0 to trigger normal death processing
	GameState.health = 0
	
	# The normal death processing in player.gd will handle showing upgrade menu and death screen
	print("Health set to 0 - normal death sequence will handle UI restoration")

func create_static_friend_model(death_position: Vector3):
	"""Create a static friend submarine model at the death position"""
	print("Creating static friend model at death position: ", death_position)
	
	# Create a simple MeshInstance3D for the static friend model
	var static_friend = MeshInstance3D.new()
	static_friend.position = death_position
	static_friend.name = "StaticFriendModel"
	
	# Load the friend submarine mesh and material
	static_friend.mesh = load("res://meshes/SM_FishSubmarine_FINAL.obj")
	
	# Create the friend submarine material
	var friend_material = StandardMaterial3D.new()
	friend_material.albedo_color = Color(1, 1, 0.152941, 1)
	friend_material.albedo_texture = load("res://textures/sub/SM_FishSubmarine_initialShadingGroup_BaseColor.png")
	friend_material.metallic_texture = load("res://textures/sub/SM_FishSubmarine_initialShadingGroup_Metallic.png")
	friend_material.roughness_texture = load("res://textures/sub/SM_FishSubmarine_initialShadingGroup_Roughness.png")
	friend_material.normal_texture = load("res://textures/sub/SM_FishSubmarine_initialShadingGroup_Normal.png")
	friend_material.height_texture = load("res://textures/sub/SM_FishSubmarine_initialShadingGroup_Height.png")
	
	static_friend.set_surface_override_material(0, friend_material)
	
	# Scale to match the player submarine size
	static_friend.scale = Vector3(0.2, 0.2, 0.2)
	
	# Add to scene
	get_parent().add_child(static_friend)
	
	print("Static friend model created and will remain at death position")

func spawn_intro_scene():
	"""Spawn the dedicated intro scene for the friend player"""
	print("Spawning intro scene")
	
	var intro_scene_instance = intro_scene.instantiate()
	intro_scene_instance.name = "IntroSceneInstance"
	
	# Add intro scene to the level
	add_child(intro_scene_instance)
	
	# Update lastSpawned to ensure proper section continuation
	lastSpawned = -550  # Set below the intro scene sections (deepest section at y=-525)
	
	intro_scene_spawned = true
	print("Intro scene spawned with safe starting area and hot zone sections")

func cleanup_intro_scene():
	"""Clean up the intro scene when transitioning back to normal game"""
	print("Cleaning up intro scene")
	
	# Find and remove intro scene
	var intro_scene_instance = get_node_or_null("IntroSceneInstance")
	if intro_scene_instance:
		intro_scene_instance.queue_free()
		print("Intro scene removed")
	
	# Reset intro scene spawned flag
	intro_scene_spawned = false
	
	# Reset lastSpawned to resume normal section generation
	lastSpawned = -35

func hide_loot_boxes_in_boss_section(boss_section_node: Node3D):
	"""Hide loot boxes in boss section during intro mission since friend has no pickaxe"""
	print("Hiding loot boxes in boss section for intro mission")
	
	# Find all DestroyableBarrier nodes in the boss section
	var loot_boxes = []
	for child in boss_section_node.get_children():
		if child.name.begins_with("DestroyableBarier"):
			loot_boxes.append(child)
	
	# Hide all loot boxes
	for loot_box in loot_boxes:
		loot_box.visible = false
		# Also disable collision so player doesn't bump into invisible boxes
		loot_box.set_collision_layer_value(1, false)
		loot_box.set_collision_mask_value(1, false)
	
	print("Hidden ", loot_boxes.size(), " loot boxes in boss section")
