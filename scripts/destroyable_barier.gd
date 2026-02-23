extends StaticBody3D

class_name DestroyableBarrier

@export var max_health: int = 10
@export var current_health: int = 10

var hint_shown = false # Track if we've shown the hint already
var hint_cooldown = 3.0 # Cooldown between showing hints
var pickaxe_icon_material = null

const CRACK_STAGE_COUNT := 7
const CRACK_TEXTURE := preload("res://textures/effects/screen_crack.png")

var crack_overlay_material: StandardMaterial3D = null
var current_crack_stage: int = -1

# Static variable to track if any barrier is showing a hint
static var global_hint_shown = false
static var global_hint_cooldown = 5.0 # Cooldown between ANY barrier showing a hint

# Reward configuration
var min_money_reward = 5
var max_money_reward = 40
var reward_chances = {
	"money": 0.65, # 65% chance for money
	"health": 0.15, # 15% chance for health
	"super_reward": 0.15, # 15% chance for super reward
	"special": 0.05 # 5% chance for special reward
}

func _ready():
	# Diese Einstellungen aus der TSCN-Datei verwenden
	collision_layer = 3 # Schon in der TSCN gesetzt
	collision_mask = 3 # Schon in der TSCN gesetzt
	add_to_group("abbaubare_objekte")
	#print("DestroyableBarrier bereit - Health:", current_health)
	
	# Add area detection for player proximity
	var area = Area3D.new()
	area.name = "PlayerDetectionArea"
	
	var collision_shape = CollisionShape3D.new()
	var shape = BoxShape3D.new()
	shape.size = Vector3(8, 8, 5) # Adjust size as needed
	collision_shape.shape = shape
	
	area.add_child(collision_shape)
	add_child(area)
	
	# Set up collision layers for player detection
	area.collision_layer = 0
	area.collision_mask = 1 # Layer for player
	
	# Connect signals
	area.body_entered.connect(_on_player_detection_area_body_entered)
	
	# Add visual indicator for pickaxe breakability
	add_pickaxe_indicator()
	setup_crack_overlay()
	update_crack_appearance()

# Give player a random reward when box is destroyed
func give_random_reward():
	var rand = randf()
	var cumulative_chance = 0.0
	
	# Get the depth level to scale rewards
	var depth_multiplier = max(1.0, GameState.depth / 100.0)
	
	for reward_type in reward_chances:
		cumulative_chance += reward_chances[reward_type]
		if rand <= cumulative_chance:
			match reward_type:
				"money":
					# Give money scaled by depth
					var money_amount = int(randi_range(min_money_reward, max_money_reward) * depth_multiplier)
					GameState.money += money_amount
					show_reward_popup("+ " + str(money_amount) + " Money!", Color(1, 0.8, 0))
					return
				"health":
					# Give health boost
					var health_amount = randi_range(5, 15)
					GameState.health = min(100, GameState.health + health_amount)
					show_reward_popup("+ " + str(health_amount) + " Health!", Color(0, 1, 0.3))
					return
				"super_reward":
					# Higher reward based on depth
					var super_amount = int(randi_range(50, 100) * depth_multiplier)
					GameState.money += super_amount
					show_reward_popup("SUPER REWARD: + " + str(super_amount) + " Money!", Color(1, 0.5, 0))
					return
				"special":
					# Special rewards that are more rare
					give_special_reward()
					return
			break

func show_reward_popup(message, color):
	var popup_manager = get_node_or_null("/root/PopupManager")
	if popup_manager:
		popup_manager.show_popup(
			message,
			global_position + Vector3(0, 1.5, 0),
			color
		)
	
	# Create particle effect for reward visual feedback
	create_reward_particles(color)

# Create particles with color matching the reward type
func create_reward_particles(color):
	var particles = GPUParticles3D.new()

	# Configure particle material
	var material = ParticleProcessMaterial.new()
	material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	material.emission_sphere_radius = 0.4
	material.gravity = Vector3(0, 2, 0) # Particles float upward
	material.initial_velocity_min = 1.0
	material.initial_velocity_max = 3.0
	
	# Add color to the particles
	material.color = color
	
	# Add sparkle/flashing effect for special rewards
	if color.r > 0.9 and color.g < 0.5: # Check if it's a special reward color
		material.scale_min = 0.05
		material.scale_max = 0.2
		material.color_ramp = create_color_gradient(color)
	else:
		material.scale_min = 0.05
		material.scale_max = 0.1
	
	particles.process_material = material

	# Set the particle mesh
	var mesh = SphereMesh.new()
	mesh.radius = 0.05
	mesh.height = 0.1
	particles.draw_pass_1 = mesh

	# Configure particle emission
	particles.one_shot = true
	particles.emitting = true
	particles.amount = 30
	particles.lifetime = 1.5
	particles.explosiveness = 0.8

	# Add to scene and position
	get_parent().add_child(particles)
	particles.global_position = global_position + Vector3(0, 0.5, 0)

	# Clean up after particles are done
	await get_tree().create_timer(2.0).timeout
	if is_instance_valid(particles):
		particles.queue_free()

# Helper function to create a color gradient for particles
func create_color_gradient(base_color):
	var gradient = Gradient.new()
	var bright_color = Color(
		min(base_color.r + 0.3, 1.0),
		min(base_color.g + 0.3, 1.0),
		min(base_color.b + 0.3, 1.0),
		1.0
	)
	gradient.add_point(0, base_color)
	gradient.add_point(0.5, bright_color)
	gradient.add_point(1, base_color)
	
	var gradient_texture = GradientTexture1D.new()
	gradient_texture.gradient = gradient
	
	return gradient_texture

func add_pickaxe_indicator():
	# Create a small pickaxe icon or cracks indicator on the crate
	var indicator = MeshInstance3D.new()
	indicator.name = "PickaxeIndicator"
	
	# Create a quad mesh for the indicator
	var quad = QuadMesh.new()
	quad.size = Vector2(0.5, 0.5)
	indicator.mesh = quad
	
	# Position it on the front of the box
	indicator.position = Vector3(0, 0.5, -1.5)
	
	# Create a standard material with a custom texture or color
	var material = StandardMaterial3D.new()
	material.albedo_color = Color(1, 0.8, 0, 1) # Gold/yellow color
	
	# Add a metallic effect to make it look like a tool
	material.metallic = 0.8
	material.roughness = 0.2
	
	# Make it billboard to always face the camera
	material.billboard_mode = StandardMaterial3D.BILLBOARD_FIXED_Y
	
	# Add emission to make it stand out
	material.emission_enabled = true
	material.emission = Color(0.8, 0.6, 0, 1)
	material.emission_energy_multiplier = 0.5
	
	indicator.material_override = material
	
	add_child(indicator)
	
	# Make it slowly rotate to catch attention
	var tween = create_tween()
	tween.tween_property(indicator, "rotation_degrees:y", 360, 3)
	tween.set_loops()
	
	# Crack stage visuals are managed by setup_crack_overlay()/update_crack_appearance().

func add_crack_decals():
	# Legacy hook kept for compatibility. Cracks now use staged mesh overlay.
	setup_crack_overlay()

func setup_crack_overlay():
	if crack_overlay_material != null:
		return

	var mesh_instance := get_node_or_null("MeshInstance3D") as MeshInstance3D
	if mesh_instance == null:
		return

	crack_overlay_material = StandardMaterial3D.new()
	crack_overlay_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	crack_overlay_material.albedo_texture = CRACK_TEXTURE
	crack_overlay_material.albedo_color = Color(1, 1, 1, 0)
	crack_overlay_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	crack_overlay_material.cull_mode = BaseMaterial3D.CULL_DISABLED
	crack_overlay_material.billboard_mode = BaseMaterial3D.BILLBOARD_DISABLED
	crack_overlay_material.uv1_scale = Vector3(0.6, 0.6, 1.0)

	mesh_instance.material_overlay = crack_overlay_material

func apply_crack_stage(stage: int):
	if crack_overlay_material == null:
		return

	stage = clampi(stage, 0, CRACK_STAGE_COUNT - 1)
	if stage == current_crack_stage:
		return
	current_crack_stage = stage

	var normalized := float(stage) / float(CRACK_STAGE_COUNT - 1)
	var alpha := pow(normalized, 1.1) * 0.9
	crack_overlay_material.albedo_color = Color(1, 1, 1, alpha)

	# Nudge UVs/scale per stage to mimic discrete block-break phases.
	var uv_scale := 0.6 + (normalized * 0.32)
	crack_overlay_material.uv1_scale = Vector3(uv_scale, uv_scale, 1.0)
	crack_overlay_material.uv1_offset = Vector3(normalized * 0.21, normalized * 0.13, 0)

func _on_player_detection_area_body_entered(body):
	# Check if it's the player and no hint is currently shown by ANY barrier
	if body.is_in_group("player") and !hint_shown and !DestroyableBarrier.global_hint_shown:
		show_hint()
		
		# Set hint as shown and start cooldown for this barrier
		hint_shown = true
		# Set global hint flag
		DestroyableBarrier.global_hint_shown = true
		
		# Start cooldown timers
		await get_tree().create_timer(hint_cooldown).timeout
		hint_shown = false
		
		# Global cooldown (slightly longer to prevent rapid successive hints)
		await get_tree().create_timer(global_hint_cooldown - hint_cooldown).timeout
		DestroyableBarrier.global_hint_shown = false

func show_hint():
	hint_shown = true
	# Get the Popup_Manager to show the hint
	var popup_manager = get_node_or_null("/root/PopupManager")
	
	if popup_manager:
		# Check if player has pickaxe
		var has_pickaxe = GameState.upgrades[GameState.Upgrade.PICKAXE_UNLOCKED] == 1
		
		var message
		if has_pickaxe:
			message = "Use your pickaxe (SPACE) to break these blocks and progress!"
		else:
			message = "These blocks are blocking your path deeper! You'll need a pickaxe..."
		
		# Create a special popup instance with longer duration
		var popup_instance = load("res://scenes/popup_text.tscn").instantiate()
		get_tree().current_scene.add_child(popup_instance)
		popup_instance.global_position = global_position + Vector3(0, 1, 0)
		
		# Set a much longer duration (4 seconds instead of default 0.6)
		popup_instance.duration = 4.0
		
		# Make text larger for better visibility
		popup_instance.font_size = 42
		
		# Let start_animation handle the positioning completely
		var hint_position = global_position + Vector3(0, 1, 0)
		popup_instance.start_animation(message, Color.YELLOW, hint_position, 0.0)
	else:
		print("Popup manager not found!")

func take_damage(amount: int):
	current_health -= amount
	print("Schaden genommen! Verbleibende Gesundheit:", current_health)
	create_hit_particles()
	
	# Update crack appearance based on health
	update_crack_appearance()
	
	if current_health <= 0:
		give_random_reward() # Give reward when box is destroyed
		destroy()

func update_crack_appearance():
	setup_crack_overlay()
	if crack_overlay_material == null:
		return

	var safe_max_health := max(1, max_health)
	var clamped_health := clampi(current_health, 0, safe_max_health)
	var damage_ratio := 1.0 - (float(clamped_health) / float(safe_max_health))

	# Minecraft-like staged break visualization: each health chunk advances one crack phase.
	var stage := int(ceil(damage_ratio * float(CRACK_STAGE_COUNT - 1)))
	apply_crack_stage(stage)

func destroy():
	create_destruction_particles()
	await get_tree().create_timer(0.5).timeout
	queue_free()
	
func create_hit_particles():
	var particles = GPUParticles3D.new()

	# Partikelmaterial konfigurieren
	var material = ParticleProcessMaterial.new()
	material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	material.emission_sphere_radius = 0.2
	material.gravity = Vector3(0, -1, 0)
	material.initial_velocity_min = 1.0
	material.initial_velocity_max = 3.0
	particles.process_material = material

	# Partikelform
	var mesh = SphereMesh.new()
	mesh.radius = 0.05
	mesh.height = 0.1
	particles.draw_pass_1 = mesh

	particles.one_shot = true
	particles.emitting = true
	particles.amount = 10
	particles.lifetime = 0.5

	get_parent().add_child(particles)
	particles.global_position = global_position

	# Partikel nach Ablauf löschen
	await get_tree().create_timer(1.0).timeout
	if is_instance_valid(particles):
		particles.queue_free()

func create_destruction_particles():
	var particles = GPUParticles3D.new()

	# Partikelmaterial konfigurieren
	var material = ParticleProcessMaterial.new()
	material.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_SPHERE
	material.emission_sphere_radius = 0.5
	material.gravity = Vector3(0, -2, 0)
	material.initial_velocity_min = 2.0
	material.initial_velocity_max = 5.0
	material.color = Color(0.4, 0.2, 0.1) # Dark brown color (was light brown)
	particles.process_material = material

	# Partikelform
	var mesh = BoxMesh.new()
	mesh.size = Vector3(0.1, 0.1, 0.1)
	particles.draw_pass_1 = mesh

	particles.one_shot = true
	particles.emitting = true
	particles.amount = 30
	particles.lifetime = 1.0

	get_parent().add_child(particles)
	particles.global_position = global_position

	# Partikel nach Ablauf löschen
	await get_tree().create_timer(2.0).timeout
	if is_instance_valid(particles):
		particles.queue_free()

# Handle special rewards that are more unique
func give_special_reward():
	# Scale chance based on depth - deeper means better chance of rare rewards
	var depth_level = GameState.depth / 100.0
	
	# Different special rewards based on current gameplay state
	
	# If player doesn't have pickaxe and is deep enough (at least level 1 depth)
	if GameState.upgrades[GameState.Upgrade.PICKAXE_UNLOCKED] == 0 and depth_level >= 1:
		# 20% chance to get free pickaxe
		if randf() < 0.2:
			GameState.upgrades[GameState.Upgrade.PICKAXE_UNLOCKED] = 1
			show_reward_popup("AMAZING! You found a pickaxe!", Color(1, 0.2, 1))
			
			# Try to enable the pickaxe on the player
			var player = GameState.player_node
			if player and player.has_node("Pivot/SmFishSubmarine/Hand/pickaxe"):
				var pickaxe = player.get_node("Pivot/SmFishSubmarine/Hand/pickaxe")
				if pickaxe.has_method("activate_pickaxe"):
					pickaxe.activate_pickaxe()
			return
	
	# Temp speed boost (more common)
	if randf() < 0.5:
		# Get player and boost speed temporarily
		var player = GameState.player_node
		if player:
			# Apply speed boost for 15 seconds
			player.speed_horizontal += 1.0
			player.speed_vertical += 0.5
			
			show_reward_popup("SPEED BOOST for 15 seconds!", Color(0, 0.8, 1))
			
			# Reset after duration
			await get_tree().create_timer(15.0).timeout
			if is_instance_valid(player):
				player.speed_horizontal -= 1.0
				player.speed_vertical -= 0.5
		return
	
	# Otherwise give a big money reward
	var huge_amount = int(randi_range(100, 200) * max(1.0, depth_level))
	GameState.money += huge_amount
	show_reward_popup("JACKPOT: + " + str(huge_amount) + " Money!", Color(1, 0.3, 0.7))
