extends CharacterBody3D

@export var player: Node3D
@export var charge_speed: float = 25.0
@export var charge_duration: float = 1.0
@export var cooldown_duration: float = 3.0
@export var damage_amount: int = 30
@export var max_range_from_spawn: float = 100.0
@export var health_regen_rate: float = 5.0  # Health per second when out of range

# Mind control zone properties
@export var mind_control_zone_scene: PackedScene = preload("res://scenes/mind_control_zone.tscn")
@export var max_zones: int = 5  # Increased from 3
@export var zone_spawn_distance_min: float = 5.0  # Minimum distance from player
@export var zone_spawn_distance_max: float = 12.0  # Maximum distance from player
@export var zone_spawn_cooldown: float = 4.0  # Reduced from 10.0 seconds
@export var zone_lifespan: float = 15.0  # How long zones live before respawning

enum BossStates {COOLDOWN, CHARGING, PREPARING, OUT_OF_RANGE, SPAWNING_ZONES}
var state = BossStates.PREPARING
var timer = 0.0
var charge_direction = Vector3.ZERO
var has_hit_player = false
var spawn_position: Vector3
var is_player_in_range = true

# Mind control zone management - now tracks spawn times
var active_zones: Array[Dictionary] = []  # [{zone: Node3D, spawn_time: float}]
var zone_spawn_timer: float = 0.0
var zones_spawned_this_fight: int = 0

func _ready():
	$Area3D.body_entered.connect(_on_body_entered)
	add_to_group("boss")  # Add boss to group for projectile detection
	spawn_position = global_position

func _physics_process(delta):
	if player == null:
		return
	
	# Update zone spawn timer
	zone_spawn_timer -= delta
	
	# Update zone lifespans and respawn expired zones
	update_zone_lifespans(delta)
	
	# Check if player is within range
	var distance_from_spawn = abs(player.global_position.y - spawn_position.y)
	is_player_in_range = distance_from_spawn <= max_range_from_spawn
	
	# Handle out of range behavior
	if not is_player_in_range:
		if state != BossStates.OUT_OF_RANGE:
			state = BossStates.OUT_OF_RANGE
			velocity = Vector3.ZERO
			# Deactivate all zones when out of range
			deactivate_all_zones()
		
		# Regenerate health when out of range
		if Boss.boss_health < Boss.boss_max_health:
			var health_to_add = health_regen_rate * delta
			Boss.boss_health = min(Boss.boss_max_health, Boss.boss_health + health_to_add)
			Boss.emit_signal("boss_health_changed", Boss.boss_health)
		
		# Move back towards spawn position
		var direction_to_spawn = (spawn_position - global_position).normalized()
		direction_to_spawn.z = 0
		velocity = direction_to_spawn * (charge_speed * 0.5)  # Move slower when returning
		move_and_slide()
		position.z = -0.33
		return
	
	# Resume normal behavior if player comes back in range
	if state == BossStates.OUT_OF_RANGE and is_player_in_range:
		state = BossStates.PREPARING
		timer = 0.0
		# Reactivate zones when back in range
		reactivate_all_zones()
		
	match state:
		BossStates.PREPARING:
			has_hit_player = false
			
			# Check if we should spawn mind control zones
			if should_spawn_zones():
				state = BossStates.SPAWNING_ZONES
				timer = 1.0  # Reduced from 1.5 seconds
				velocity = Vector3.ZERO
				return
		
			charge_direction = (player.global_position - global_position).normalized()
			charge_direction.z = 0
			state = BossStates.CHARGING
			timer = charge_duration
		
			#rotate towards player
			look_at(player.global_position, Vector3(0, 0, 1))
			
		BossStates.SPAWNING_ZONES:
			velocity = Vector3.ZERO
			timer -= delta
			if timer <= 0:
				spawn_mind_control_zones()
				state = BossStates.COOLDOWN
				timer = cooldown_duration
				zone_spawn_timer = zone_spawn_cooldown
			
		BossStates.CHARGING:
			velocity = charge_direction * charge_speed
			var _collision = move_and_slide()
			check_player_collision()
			
			timer -= delta
			if timer <= 0:
				state = BossStates.COOLDOWN
				timer = cooldown_duration
				velocity = Vector3.ZERO
		
		BossStates.COOLDOWN:
			timer -= delta
			if timer <= 0:
				state = BossStates.PREPARING
	position.z = -0.33

func should_spawn_zones() -> bool:
	# More aggressive zone spawning - spawn based on cooldown and available slots
	var health_percentage = float(Boss.boss_health) / float(Boss.boss_max_health)
	var current_zone_count = get_valid_zones_count()
	
	# Spawn zones more frequently and start earlier in the fight
	if zone_spawn_timer <= 0.0 and current_zone_count < max_zones:
		# Start spawning zones at 90% health (much earlier)
		if health_percentage <= 0.9:
			return true
	
	return false

func get_valid_zones_count() -> int:
	var count = 0
	for zone_data in active_zones:
		if is_instance_valid(zone_data.zone):
			count += 1
	return count

func update_zone_lifespans(delta: float):
	# Check for expired zones and mark them for respawn
	for i in range(active_zones.size() - 1, -1, -1):
		var zone_data = active_zones[i]
		
		# Check if zone is still valid
		if not is_instance_valid(zone_data.zone):
			active_zones.remove_at(i)
			continue
		
		# Update lifespan
		zone_data.spawn_time += delta
		
		# If zone has expired, destroy it and remove from tracking
		if zone_data.spawn_time >= zone_lifespan:
			print("Zone expired after ", zone_lifespan, " seconds, destroying")
			zone_data.zone.queue_free()
			active_zones.remove_at(i)

func spawn_mind_control_zones():
	print("Boss spawning mind control zones!")
	
	# Calculate how many zones to spawn
	var current_count = get_valid_zones_count()
	var zones_to_spawn = min(max_zones - current_count, 3)  # Spawn up to 3 at a time
	
	for i in range(zones_to_spawn):
		var zone_position = calculate_random_zone_position()
		
		var zone = mind_control_zone_scene.instantiate()
		zone.global_position = zone_position
#		zone.zone_radius = 4.0  # Smaller radius for boss fight
#		zone.pull_strength = 8.0  # Moderate pull strength
#		zone.escape_threshold = 8  # Lower threshold during boss fight
		
		# Add to scene
		get_parent().add_child(zone)
		
		# Track with spawn time
		active_zones.append({
			"zone": zone,
			"spawn_time": 0.0
		})
		
		print("Spawned mind control zone at: ", zone_position)
	
	zones_spawned_this_fight += 1

func calculate_random_zone_position() -> Vector3:
	var player_pos = player.global_position
	
	# Generate random offset in a circle around the player
	var angle = randf() * 2.0 * PI
	var distance = randf_range(zone_spawn_distance_min, zone_spawn_distance_max)
	
	var offset = Vector3(
		cos(angle) * distance,
		randf_range(-3.0, 3.0),  # Random Y offset (up/down from player)
		0  # Smaller Z variation to keep zones in play area
	)
	
	return player_pos + offset

func deactivate_all_zones():
	for zone_data in active_zones:
		if is_instance_valid(zone_data.zone):
			zone_data.zone.deactivate_zone()

func reactivate_all_zones():
	for zone_data in active_zones:
		if is_instance_valid(zone_data.zone):
			zone_data.zone.activate_zone()

func cleanup_destroyed_zones():
	# Remove invalid zones from tracking
	for i in range(active_zones.size() - 1, -1, -1):
		if not is_instance_valid(active_zones[i].zone):
			active_zones.remove_at(i)

func destroy_all_zones():
	for zone_data in active_zones:
		if is_instance_valid(zone_data.zone):
			zone_data.zone.queue_free()
	active_zones.clear()

func check_player_collision():
	if has_hit_player:
		return
		
	for i in get_slide_collision_count():
		var collision = get_slide_collision(i)
		var collider = collision.get_collider()
		
		if collider == player:
			on_player_collision(collider)
			has_hit_player = true
			velocity = Vector3.ZERO
			break
		
func on_player_collision(_player):
	print("Boss hit the player!")
	player.add_trauma(1)
	player.sound_player.play_sound("urrgh")
	GameState.health -= damage_amount

func _on_body_entered(body):
	if state == BossStates.CHARGING and body == player:
		on_player_collision(player)
		has_hit_player = true

func take_damage(amount):
	# Only take damage if player is in range
	if is_player_in_range:
		Boss.take_damage(amount)
		# Clean up destroyed zones when boss takes damage
		cleanup_destroyed_zones()

func _on_boss_defeated():
	# Clean up all zones when boss is defeated
	destroy_all_zones()

# Connect to boss defeat signal when ready
func _notification(what):
	if what == NOTIFICATION_READY:
		if Boss:
			Boss.boss_defeated_signal.connect(_on_boss_defeated)
