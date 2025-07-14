extends StaticBody3D

var swing_active = false
var back_swing_active = false
@export var collision_detected = false
var schwing_geschwindigkeit = 5.0
var swing_duration = 0.4
var swing_time = 5.0
@onready var hand = get_parent()
@onready var player = hand.get_parent()
@onready var particles = null

var can_show_pickaxe_popup = true
var popup_cooldown = 1.0  # 1 second cooldown

func _ready():
	collision_layer = 0  
	collision_mask = 0   
	
	# Initiale Sichtbarkeitsprüfung
	if GameState.upgrades[GameState.Upgrade.PICKAXE_UNLOCKED] != 1:
		visible = false
		set_physics_process(false)
	else:
		activate_pickaxe()
		
	$PickaxeHitbox.collision_layer = 0
	$PickaxeHitbox.collision_mask = 3
	
	# Partikel einrichten
	if not has_node("GPUParticles3D"):
		var particle_node = GPUParticles3D.new()
		particle_node.name = "GPUParticles3D"
		particles = particle_node
		particles.emitting = false
		particles.one_shot = true
		particles.explosiveness = 0.8
		particles.amount = 16
		particles.lifetime = 0.5
		add_child(particles)

func activate_pickaxe():
	# Aktiviert die Pickaxe, wenn das Upgrade gekauft wurde
	visible = true
	set_physics_process(true)
	#print("Pickaxe activated!")

func _process(delta):
	if GameState.upgrades[GameState.Upgrade.PICKAXE_UNLOCKED] != 1:
		if can_show_pickaxe_popup:
			var pos = global_transform.origin
			can_show_pickaxe_popup = false
			await get_tree().create_timer(popup_cooldown).timeout
			can_show_pickaxe_popup = true
		return

	if !visible:
		activate_pickaxe()

	if Input.is_action_just_pressed("swing_pickaxe") and not swing_active and not back_swing_active:
		swing_active = true
		swing_time = 0.0

	if swing_active:
		swing_time += delta
		var progress = swing_time / swing_duration
		var eased_progress = ease_in_out(progress)
		rotate_z(-schwing_geschwindigkeit * eased_progress * delta)
		
		for body in $PickaxeHitbox.get_overlapping_bodies():
			if body.has_method("take_damage"):
				body.take_damage(1)
				collision_detected = true
			

		if progress >= 1.0 or collision_detected:
			swing_active = false
			back_swing_active = true
			collision_detected = false
			swing_time = 0.0

	if back_swing_active:
		swing_time += delta
		var progress = swing_time / swing_duration
		var eased_progress = ease_in_out(1.0 - progress)
		rotate_z(schwing_geschwindigkeit * eased_progress * delta)

		if progress >= 1.0:
			rotation_degrees.z = 0
			back_swing_active = false

func ease_in_out(t):
	return t * t * (3.0 - 2.0 * t)

func is_pickaxe() -> bool:
	return true
