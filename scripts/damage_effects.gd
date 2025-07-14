extends Control

# Damage visual effects controller
# Handles screen crack and red flash effects when player takes damage

@onready var red_flash = $RedFlash

var flash_duration = 0.5 # Duration of red flash in seconds
var crack_fade_time = 3.0 # Time for crack to fade out
var max_cracks = 5 # Maximum number of crack layers
var current_crack_count = 0
var crack_sprites = []

# Pressure damage system
var pressure_cracks = []
var max_pressure_cracks = 8
var current_pressure_cracks = 0
var is_under_pressure = false
var pressure_accumulation_timer = 0.0
var pressure_crack_spawn_interval = 1.0 # Spawn new crack every second under pressure

# WebGL Performance optimizations
var is_webgl_build = false
var preloaded_crack_texture = null
var webgl_max_cracks = 3 # Reduced max cracks for WebGL
var webgl_max_pressure_cracks = 4 # Reduced pressure cracks for WebGL

func _ready():
	# Detect WebGL and apply optimizations
	is_webgl_build = OS.get_name() == "Web"
	
	if is_webgl_build:
		max_cracks = webgl_max_cracks
		max_pressure_cracks = webgl_max_pressure_cracks
		crack_fade_time = 2.0 # Faster fade on WebGL
		pressure_crack_spawn_interval = 2.0 # Slower spawn rate
	
	# Preload crack texture once to avoid repeated loading
	preloaded_crack_texture = load("res://textures/effects/screen_crack.png")
	
	# Initialize with invisible effects
	red_flash.modulate.a = 0
	red_flash.color = Color(1, 0, 0, 0) # Pure red, fully transparent initially

# Called when player takes regular damage
func show_damage_effects():
	# Show red flash - make it more visible
	red_flash.modulate.a = 1.0 # Fully opaque
	red_flash.color = Color(1, 0, 0, 0.6) # Semi-transparent red
	var flash_tween = create_tween()
	flash_tween.tween_property(red_flash, "color:a", 0.0, flash_duration)
	
	# Add a new crack layer if we haven't reached the maximum
	if current_crack_count < max_cracks:
		add_crack_layer()

# Called every frame while player is taking pressure damage
func process_pressure_damage(delta):
	# Only process if we're actually under pressure
	if not is_under_pressure:
		is_under_pressure = true
		pressure_accumulation_timer = 0.0
		print("Started pressure damage processing")
	
	# Accumulate pressure damage over time
	pressure_accumulation_timer += delta
	
	# Show red flash that gets more intense over time
	var intensity = min(pressure_accumulation_timer / 3.0, 1.0)  # Max intensity after 3 seconds
	red_flash.color = Color(1, 0, 0, intensity * 0.4)  # Red with up to 40% opacity
	
	# Add cracks periodically
	if pressure_accumulation_timer > pressure_crack_spawn_interval and current_crack_count < max_cracks:
		add_crack_layer()
		pressure_accumulation_timer = 0.0  # Reset timer after adding crack

# Called when pressure damage stops
func reset_pressure_damage():
	if is_under_pressure:
		is_under_pressure = false
		pressure_accumulation_timer = 0.0
		print("Ended pressure damage processing")
	
	# Fade out red flash
	red_flash.color = Color(1, 0, 0, 0)

func add_crack_layer():
	current_crack_count += 1
	
	# Create a new crack sprite
	var crack_sprite = TextureRect.new()
	crack_sprite.name = "CrackLayer" + str(current_crack_count)
	
	# Use preloaded texture instead of loading each time
	if preloaded_crack_texture == null:
		return
	
	crack_sprite.texture = preloaded_crack_texture
	
	# Set up the crack sprite properties for positioned cracks
	crack_sprite.layout_mode = 0 # Free positioning
	crack_sprite.mouse_filter = Control.MOUSE_FILTER_IGNORE
	crack_sprite.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	crack_sprite.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	
	# Smaller cracks on WebGL for better performance
	var base_size = Vector2(200, 200)
	if is_webgl_build:
		base_size = Vector2(150, 150) # Smaller base size for WebGL
	
	# Random size for variety (between 50% and 150% of base size)
	var scale_factor = randf_range(0.5, 1.5)
	var crack_size = base_size * scale_factor
	crack_sprite.custom_minimum_size = crack_size
	crack_sprite.size = crack_size
	
	# Random position on screen (ensure cracks stay within screen bounds)
	var screen_size = get_viewport().get_visible_rect().size
	var max_x = screen_size.x - crack_size.x
	var max_y = screen_size.y - crack_size.y
	var random_pos = Vector2(
		randf_range(0, max_x),
		randf_range(0, max_y)
	)
	crack_sprite.position = random_pos
	
	# Make cracks more visible with different opacities
	var crack_alpha = 0.8 - (current_crack_count * 0.1) # Start at 0.8, decrease slightly
	# Slightly more opaque on WebGL for visibility with fewer cracks
	if is_webgl_build:
		crack_alpha = 0.9 - (current_crack_count * 0.15)
	crack_sprite.modulate = Color(1, 1, 1, crack_alpha)
	
	# Randomize the crack appearance
	crack_sprite.rotation = randf() * 2 * PI # Random rotation
	
	# Mark as not fading yet
	crack_sprite.set_meta("is_fading", false)
	
	# Add to scene
	add_child(crack_sprite)
	crack_sprites.append(crack_sprite)
	
	# Only start fade timer if NOT under pressure
	if not is_under_pressure:
		start_crack_fade_timer(crack_sprite)

func start_crack_fade_timer(crack_sprite):
	# Store original alpha for restoration if needed
	crack_sprite.set_meta("original_alpha", crack_sprite.modulate.a)
	
	# Fade out this crack over time (but make it slower so cracks accumulate)
	var crack_tween = create_tween()
	crack_tween.tween_interval(crack_fade_time) # Wait before starting to fade
	crack_tween.tween_property(crack_sprite, "modulate:a", 0, 1.0) # Then fade out over 1 second
	crack_tween.tween_callback(func(): remove_crack_layer(crack_sprite))
	
	# Store the tween reference so we can cancel it later if needed
	crack_sprite.set_meta("fade_tween", crack_tween)

func remove_crack_layer(crack_sprite):
	if crack_sprite and is_instance_valid(crack_sprite):
		crack_sprites.erase(crack_sprite)
		crack_sprite.queue_free()
		current_crack_count = max(0, current_crack_count - 1)

# Call this when player heals or resets
func clear_all_cracks():
	for crack in crack_sprites:
		if is_instance_valid(crack):
			crack.queue_free()
	crack_sprites.clear()
	current_crack_count = 0
	
	for crack in pressure_cracks:
		if is_instance_valid(crack):
			crack.queue_free()
	pressure_cracks.clear()
	current_pressure_cracks = 0

# Test function - call this to manually test crack accumulation
func test_add_cracks():
	for i in range(3):
		show_damage_effects()
		await get_tree().create_timer(0.2).timeout # Small delay between cracks

func add_pressure_crack():
	current_pressure_cracks += 1
	
	# Create a new pressure crack sprite
	var crack_sprite = TextureRect.new()
	crack_sprite.name = "PressureCrack" + str(current_pressure_cracks)
	
	# Load the texture
	var crack_texture = load("res://textures/effects/screen_crack.png")
	if crack_texture == null:
		return
	
	crack_sprite.texture = crack_texture
	
	# Set up as a pressure crack (starts smaller, will grow)
	crack_sprite.layout_mode = 0 # Free positioning
	crack_sprite.mouse_filter = Control.MOUSE_FILTER_IGNORE
	crack_sprite.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	crack_sprite.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	
	# Start small and pick a final position immediately
	var initial_size = Vector2(80, 80)
	var final_size = Vector2(250, 250)
	
	# Pick final position that accommodates the full grown size
	var screen_size = get_viewport().get_visible_rect().size
	var final_pos = Vector2(
		randf_range(0, screen_size.x - final_size.x),
		randf_range(0, screen_size.y - final_size.y)
	)
	
	# Position crack so it grows from its center (adjust for size difference)
	var size_diff = (final_size - initial_size) * 0.5
	var initial_pos = final_pos + size_diff
	
	crack_sprite.position = initial_pos
	crack_sprite.custom_minimum_size = initial_size
	crack_sprite.size = initial_size
	
	# Pressure cracks start more opaque and stay visible
	crack_sprite.modulate = Color(1, 1, 1, 0.9)
	crack_sprite.rotation = randf() * 2 * PI
	
	# Store properties for jump-based growth
	crack_sprite.set_meta("final_position", final_pos)
	crack_sprite.set_meta("final_size", final_size)
	crack_sprite.set_meta("growth_stage", 0) # 0-4 growth stages
	crack_sprite.set_meta("next_growth_time", randf_range(0.5, 2.0)) # Random initial growth delay
	crack_sprite.set_meta("is_fading", false)
	
	# Add to scene
	add_child(crack_sprite)
	pressure_cracks.append(crack_sprite)

func grow_pressure_cracks(delta):
	for crack in pressure_cracks:
		if not is_instance_valid(crack) or crack.get_meta("is_fading", false):
			continue
		
		var growth_stage = crack.get_meta("growth_stage", 0)
		var next_growth_time = crack.get_meta("next_growth_time", 0.0)
		
		# Count down to next growth jump
		next_growth_time -= delta
		crack.set_meta("next_growth_time", next_growth_time)
		
		# Time for a growth jump?
		if next_growth_time <= 0.0 and growth_stage < 4: # 4 growth stages total
			growth_stage += 1
			crack.set_meta("growth_stage", growth_stage)
			
			# Calculate new size based on growth stage
			var initial_size = Vector2(80, 80)
			var final_size = crack.get_meta("final_size", Vector2(250, 250))
			var final_pos = crack.get_meta("final_position", crack.position)
			
			# Interpolate size based on stage (0.2, 0.4, 0.6, 0.8, 1.0)
			var growth_factor = growth_stage / 4.0
			var new_size = initial_size.lerp(final_size, growth_factor)
			
			# Update size instantly (jump growth)
			crack.size = new_size
			crack.custom_minimum_size = new_size
			
			# Adjust position to grow from center
			var size_diff = (final_size - new_size) * 0.5
			crack.position = final_pos + size_diff
			
			# Set next growth time (random interval between 1-3 seconds)
			crack.set_meta("next_growth_time", randf_range(1.0, 3.0))

func start_all_cracks_fade():
	# Fade regular cracks
	for crack in crack_sprites:
		if is_instance_valid(crack) and not crack.get_meta("is_fading", false):
			crack.set_meta("is_fading", true)
			var fade_tween = create_tween()
			fade_tween.tween_property(crack, "modulate:a", 0, 2.0) # Fade out over 2 seconds
			fade_tween.tween_callback(func(): remove_crack_layer(crack))
	
	# Fade pressure cracks
	for crack in pressure_cracks:
		if is_instance_valid(crack):
			crack.set_meta("is_fading", true)
			var fade_tween = create_tween()
			fade_tween.tween_property(crack, "modulate:a", 0, 2.0) # Fade out over 2 seconds
			fade_tween.tween_callback(func(): remove_pressure_crack(crack))

func remove_pressure_crack(crack_sprite):
	if crack_sprite and is_instance_valid(crack_sprite):
		pressure_cracks.erase(crack_sprite)
		crack_sprite.queue_free()
		current_pressure_cracks = max(0, current_pressure_cracks - 1)

func cancel_all_crack_fades():
	# Cancel fade timers for regular cracks
	for crack in crack_sprites:
		if is_instance_valid(crack):
			var fade_tween = crack.get_meta("fade_tween", null)
			if fade_tween and is_instance_valid(fade_tween):
				fade_tween.kill()
			crack.set_meta("fade_tween", null)
			crack.set_meta("is_fading", false)
			# Restore full opacity in case it was mid-fade
			crack.modulate.a = crack.get_meta("original_alpha", 0.8)
