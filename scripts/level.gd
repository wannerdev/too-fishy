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
func _process(_delta: float) -> void:
	GameState.setDepth(int(player.position.y) * -1)
	snappedDepth = snapped(player.position.y, 1) * -1
	
	# During intro mission, ensure camera environment is set to HOT zone
	if GameState.is_intro():
		# Force camera environment to hot zone
		var camera = player.get_node("Camera3D")
		if camera and camera.has_method("change_section_environment"):
			camera.change_section_environment(GameState.Stage.HOT)
	
	if player.position.y < (lastSpawned) && GameState.current_game_mode == GameState.GameMode.NORMAL:
		spawnNewSection(lastSpawned - sectionHeight)
	if GameState.maxDepthReached > Boss.boss_spawn_height && Boss.boss_spawned == false && Boss.boss_defeated_permanently == false:
		var boss_spawn_loc = (GameState.maxDepthReached * -1) - 25
		spawnBoss(boss_spawn_loc)
	
	Boss.process_dialog_depth()
	
func _ready():
	# Check if intro mission should spawn special sections
	if GameState.is_intro():
		spawn_intro_mission_sections()

func spawn_intro_mission_sections():
	"""Spawn special sections for intro mission"""
	if GameState.DEBUG_PRINTS:
		print("Spawning intro mission sections")
	
	# Create invisible barrier at 400m depth (y=-400)
	var lavaWall = preload("res://scenes/lava_side.tscn").instantiate()
	lavaWall.name = "LavaWall"
	lavaWall.position = Vector3(-15, -400, 0)  # Center it at x=-15 to cover full section width
	lavaWall.scale = Vector3(30, 15, 10)  # Wide horizontal Wall
	
	# Create invisible barrier at 400m depth (y=-400)
	var invisible_barrier = StaticBody3D.new()
	invisible_barrier.name = "InvisibleBarrier400m"
	invisible_barrier.position = Vector3(-15, -400, 0)  # Center it at x=-15 to cover full section width

	# Create collision shape for the barrier
	var collision_shape = CollisionShape3D.new()
	var box_shape = BoxShape3D.new()
	box_shape.size = Vector3(30, 2, 10)  # Wide horizontal barrier
	collision_shape.shape = box_shape
	invisible_barrier.add_child(collision_shape)
	add_child(invisible_barrier)
	
	# Make it invisible (no mesh, only collision)
	#invisible_barrier.collision_layer = 3
	#invisible_barrier.collision_mask = 3
	
	add_child(lavaWall)
	if GameState.DEBUG_PRINTS:
		print("lavaWall created at 400m depth")
	
	# Create scatter area around and below the invisible barrier
	var scatter_area = Area3D.new()
	scatter_area.name = "InvisibleBarrierScatterArea"
	scatter_area.position = Vector3(-15, -400, 0)  # 20m below the barrier
	scatter_area.monitorable = false

	# Create collision shape for scatter area (larger area around and below barrier)
	var scatter_collision = CollisionShape3D.new()
	var scatter_box = BoxShape3D.new()
	scatter_box.size = Vector3(40, 60, 15)  # Wide area around and below barrier
	scatter_collision.shape = scatter_box
	scatter_area.add_child(scatter_collision)

	# Connect scatter signal
	scatter_area.body_entered.connect(_on_barrier_scatter_area_entered)

	add_child(scatter_area)
	if GameState.DEBUG_PRINTS:
		print("Scatter area created around invisible barrier")
	
	# Spawn PreBarrier section at y=-400
	var pre_barrier_section = section.instantiate()
	pre_barrier_section.name = "IntroSection_PreBarrier_400m"
	pre_barrier_section.position.y = -400
	pre_barrier_section.sectionType = GameState.Stage.SUPERDEEP
	pre_barrier_section.lastSectionType = GameState.Stage.SUPERDEEP
	pre_barrier_section.add_destructible_barriers = false
	add_child(pre_barrier_section)
	
	# Spawn Transition section at y=-425
	var transition_section = section.instantiate()
	transition_section.name = "IntroSection_Transition_425m"
	transition_section.position.y = -425
	transition_section.sectionType = GameState.Stage.HOT
	transition_section.lastSectionType = GameState.Stage.SUPERDEEP
	transition_section.add_destructible_barriers = false
	add_child(transition_section)
	
	# Spawn HotZone sections at y=-450 and y=-475
	var hot_zone_section1 = section.instantiate()
	hot_zone_section1.name = "IntroSection_HotZone_450m"
	hot_zone_section1.position.y = -450
	hot_zone_section1.sectionType = GameState.Stage.HOT
	hot_zone_section1.lastSectionType = GameState.Stage.HOT
	hot_zone_section1.add_destructible_barriers = false
	add_child(hot_zone_section1)
	
	var hot_zone_section2 = section.instantiate()
	hot_zone_section2.name = "IntroSection_HotZone_475m"
	hot_zone_section2.position.y = -475
	hot_zone_section2.sectionType = GameState.Stage.HOT
	hot_zone_section2.lastSectionType = GameState.Stage.HOT
	hot_zone_section2.add_destructible_barriers = false
	add_child(hot_zone_section2)
	
	# Spawn Lava sections at y=-500 and y=-525
	var lava_zone_section1 = section.instantiate()
	lava_zone_section1.name = "IntroSection_LavaZone_500m"
	lava_zone_section1.position.y = -500
	lava_zone_section1.sectionType = GameState.Stage.LAVA
	lava_zone_section1.lastSectionType = GameState.Stage.HOT
	lava_zone_section1.add_destructible_barriers = false
	add_child(lava_zone_section1)
	
	var lava_zone_section2 = section.instantiate()
	lava_zone_section2.name = "IntroSection_LavaZone_525m"
	lava_zone_section2.position.y = -525
	lava_zone_section2.sectionType = GameState.Stage.LAVA
	lava_zone_section2.lastSectionType = GameState.Stage.LAVA
	lava_zone_section2.add_destructible_barriers = false
	add_child(lava_zone_section2)
	
	# Update lastSpawned to prevent conflicts
	lastSpawned = -525
	
	print("Intro mission sections spawned successfully")

func spawnNewSection(mPosition: float):
	var newSection = section.instantiate()
	newSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	newSection.sectionType = GameState.depthStageMap[i]
	newSection.lastSectionType = last_section_type
	
	# Give section a meaningful name based on its type and depth
	var depth_meters = int(abs(mPosition))
	var stage_name = GameState.Stage.keys()[GameState.depthStageMap[i]]
	newSection.name = "Section_%s_%dm" % [stage_name, depth_meters]
	
	lastSpawned = mPosition
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1
	if mPosition <= -50:
		newSection.setDepth(mPosition * -1)
	add_child(newSection)
	last_section_type = GameState.depthStageMap[i]

func spawnBoss(mPosition: float):
	print("Spawn boss", mPosition)
	var spawned_boss = boss.instantiate()
	spawned_boss.name = "Boss_Blobfish_%dm" % int(abs(mPosition))
	spawned_boss.position.y = mPosition
	spawned_boss.position.z = -0.33
	spawned_boss.position.x = -5
	spawned_boss.player = player
	add_child(spawned_boss)
	Boss.setBossSpawned(spawned_boss)
	
	mPosition = lastSpawned - sectionHeight
	var bossSection = boss_section.instantiate()
	bossSection.name = "BossSection_%dm" % int(abs(mPosition))
	bossSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	
	lastSpawned = mPosition + 50
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1

	add_child(bossSection)

func switch_back_to_original_player():
	"""Handle friend death during intro mission and switch back to normal player"""
	if not GameState.is_intro():
		return  # Not in intro mission, nothing to do
	
	print("Switching back to original player after friend death")
	
	# Store friend death position for completion
	var friend_death_pos = player.position
	
	# Move player to surface
	player.position = Vector3(-8, 0, 0.33)
	
	# Reset depth to surface
	GameState.setDepth(0)
	
	# Complete intro mission (this resets upgrades and sets mode to normal)
	GameState.complete_intro_mission(friend_death_pos)
	
	# Reset lastSpawned to surface level for normal gameplay
	lastSpawned = -35
	
	# Clean up intro mission elements
	remove_intro_mission_elements()
	
	# Hide boss health bar since player is no longer in boss fight
	Boss.hide_boss_health_bar()
	
	# Switch back to normal submarine appearance AFTER upgrades are reset
	player.switch_to_normal_submarine()
	
	# Queue normal death transition after post-intro rescue dialog closes
	GameState.pending_regular_death_transition = true
	
	print("Switched back to normal submarine appearance at surface")



func remove_intro_mission_elements():
	"""Remove intro mission specific elements"""
	print("Removing intro mission elements...")
	
	# Remove invisible barrier
	var barrier = get_node_or_null("InvisibleBarrier400m")
	if barrier:
		barrier.queue_free()
		print("Removed invisible barrier")
	
	# Remove scatter area around barrier
	var scatter_area = get_node_or_null("InvisibleBarrierScatterArea")
	if scatter_area:
		scatter_area.queue_free()
		print("Removed barrier scatter area")
	
	# Remove all intro mission sections
	var sections_to_remove = []
	for child in get_children():
		if child.name.begins_with("IntroSection_"):
			sections_to_remove.append(child)
		if child.name.begins_with("Boss"):
			sections_to_remove.append(child)
	for section in sections_to_remove:
		print("Removing intro mission section: ", section.name)
		section.queue_free()
	
	print("Removed %d intro mission sections" % sections_to_remove.size())
	print("Intro mission elements cleanup complete")

func _on_barrier_scatter_area_entered(body: Node3D) -> void:
	"""Handle fish entering the scatter area around the invisible barrier"""
	if body.is_in_group("fishes"):
		# Make fish scatter away from the barrier area
		if body.has_method("scatter"):
			body.scatter(player)  # Use the player as the scatter source
		print("Fish scattered at invisible barrier area: ", body.name)
