extends Node3D

var speed = 10.0 # Speed of harpoon movement
var lifetime = 3.0 # Seconds before despawn if no hit
var submarine = null # Reference to submarine to call catch_fish
var direction = Vector3.ZERO # Direction set by submarine
var max_distance = 10.0 # Maximum distance the harpoon can travel before despawning
var initial_position = Vector3.ZERO # To store the player's position at launch

func _ready():
	# Store initial position (player's position)
	if submarine:
		initial_position = submarine.global_position
	
	# Start a timer to despawn if it doesn't hit anything
	await get_tree().create_timer(lifetime).timeout
	queue_free()

func _physics_process(delta):
	var move_direction = global_transform.basis.x.normalized()
	var velocity = move_direction * speed

	position += velocity * delta 
	
	# Check if harpoon has exceeded maximum distance from player
	if submarine and global_position.distance_to(submarine.global_position) > max_distance:
		queue_free()
		return
	
	# Check for collisions with fish
	for body in $Area3D.get_overlapping_bodies():
		if body.is_in_group("fishes"):
			if submarine:
				submarine.catch_fish(body) # Call catch_fish on submarine
			if GameState.upgrades[GameState.Upgrade.HARPOON]<1:
				queue_free() # Remove harpoon after hit
				break # Only catch one fish per harpoon
		if body.is_in_group("boss"):
			Boss.take_damage(10)
			queue_free()
			break

func set_direction(dir):
	direction = dir
