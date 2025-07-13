extends CharacterBody3D

@export var player: Node3D
@export var charge_speed: float = 25.0
@export var charge_duration: float = 1.0
@export var cooldown_duration: float = 3.0
@export var damage_amount: int = 30


enum BossStates {COOLDOWN, CHARGING, PREPARING}
var state = BossStates.PREPARING
var timer = 0.0
var charge_direction = Vector3.ZERO
var has_hit_player = false

func _ready():
	$Area3D.body_entered.connect(_on_body_entered)
	add_to_group("boss")  # Add boss to group for projectile detection

func _physics_process(delta):
	if player == null:
		return
		
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
	Boss.take_damage(amount)

