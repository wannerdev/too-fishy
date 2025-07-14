extends CharacterBody3D

@export var player: Node3D
@export var charge_speed: float = 25.0
@export var charge_duration: float = 1.0
@export var cooldown_duration: float = 3.0
@export var damage_amount: int = 30
@export var max_range_from_spawn: float = 100.0
@export var health_regen_rate: float = 5.0  # Health per second when out of range

enum BossStates {COOLDOWN, CHARGING, PREPARING, OUT_OF_RANGE}
var state = BossStates.PREPARING
var timer = 0.0
var charge_direction = Vector3.ZERO
var has_hit_player = false
var spawn_position: Vector3
var is_player_in_range = true

func _ready():
	$Area3D.body_entered.connect(_on_body_entered)
	add_to_group("boss")  # Add boss to group for projectile detection
	spawn_position = global_position

func _physics_process(delta):
	if player == null:
		return
	
	# Don't attack the player if intro mission is completed
	if GameState.intro_mission_completed:
		# Boss waits for player to come back
		velocity = Vector3.ZERO
		state = BossStates.PREPARING
		return
	
	# Check if player is within range
	var distance_from_spawn = abs(player.global_position.y - spawn_position.y)
	is_player_in_range = distance_from_spawn <= max_range_from_spawn
	
	# Handle out of range behavior
	if not is_player_in_range:
		if state != BossStates.OUT_OF_RANGE:
			state = BossStates.OUT_OF_RANGE
			velocity = Vector3.ZERO
		
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
		
	match state:
		BossStates.PREPARING:
			has_hit_player = false
		
			charge_direction = (player.global_position - global_position).normalized()
			charge_direction.z = 0
			state = BossStates.CHARGING
			timer = charge_duration
		
			#rotate towards player
			look_at(player.global_position, Vector3(0, 0, 1))
			
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

