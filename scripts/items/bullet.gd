extends Node3D

var speed = 50.0 # Meters per second
var velocity = Vector3.ZERO
var lifetime = 2.0 # Seconds before despawn
var direction = Vector3.LEFT # Default direction
var submarine = null # Reference to submarine that shot this bullet

func _ready():
	submarine = GameState.player_node
	# Start a timer to despawn
	await get_tree().create_timer(lifetime).timeout
	queue_free()

func _physics_process(delta):
	# Move harpoon# Move in the direction set by the submarine
	translate(direction * speed * delta)
	
	# Check for collisions with fish
	for body in $Area3D.get_overlapping_bodies():
		if body.is_in_group("fishes"):
			if submarine:
				submarine.catch_fish(body) # Call catch_fish on submarine
			queue_free() # Remove harpoon after hit
			break # Only catch one fish per harpoon
		if body.is_in_group("boss"):
			Boss.take_damage(5)
			queue_free()
			break
