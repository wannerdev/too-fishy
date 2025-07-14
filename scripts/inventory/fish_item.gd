extends PanelContainer

# Signals
signal fish_released(fish_data)

# Node references
@onready var fish_image = $MarginContainer/VBoxContainer/FishImageContainer/FishTexture
@onready var weight_label = $MarginContainer/VBoxContainer/InfoContainer/WeightLabel
@onready var value_label = $MarginContainer/VBoxContainer/InfoContainer/ValueLabel
@onready var release_button = $MarginContainer/VBoxContainer/InfoContainer/ReleaseButton
@onready var count_badge = $MarginContainer/VBoxContainer/FishImageContainer/CountBadge
@onready var count_label = $MarginContainer/VBoxContainer/FishImageContainer/CountBadge/CountLabel
@onready var fish_image_container = $MarginContainer/VBoxContainer/FishImageContainer

# Fish data
var fish_data_list = [] # List of InvItem instances with same properties
var fish_type: int = 0

func _ready():
	# Connect button signal
	release_button.pressed.connect(_on_release_button_pressed)
	
	# Style the container
	var hover_style = StyleBoxFlat.new()
	hover_style.bg_color = Color(0.15, 0.3, 0.45, 0.8)
	hover_style.set_border_width_all(2)
	hover_style.border_color = Color(0.4, 0.6, 0.8)
	hover_style.set_corner_radius_all(8)
	add_theme_stylebox_override("hover", hover_style)
	
	# Style the release button
	var button_style = StyleBoxFlat.new()
	button_style.bg_color = Color(0.2, 0.4, 0.7, 0.9)
	button_style.set_border_width_all(1)
	button_style.border_color = Color(0.4, 0.6, 0.9)
	button_style.set_corner_radius_all(5)
	release_button.add_theme_stylebox_override("normal", button_style)
	release_button.add_theme_color_override("font_color", Color(1, 1, 1))
	
	var button_hover_style = StyleBoxFlat.new()
	button_hover_style.bg_color = Color(0.3, 0.5, 0.8, 0.9)
	button_hover_style.set_border_width_all(1)
	button_hover_style.border_color = Color(0.5, 0.7, 1.0)
	button_hover_style.set_corner_radius_all(5)
	release_button.add_theme_stylebox_override("hover", button_hover_style)
	
	# Style the count badge
	var badge_style = StyleBoxFlat.new()
	badge_style.bg_color = Color(0.15, 0.3, 0.5, 0.9)
	badge_style.set_border_width_all(1)
	badge_style.border_color = Color(0.3, 0.5, 0.7)
	badge_style.set_corner_radius_all(12)
	count_badge.add_theme_stylebox_override("panel", badge_style)
	
	# Add additional slot styling
	var slot_style = StyleBoxFlat.new()
	slot_style.bg_color = Color(0.05, 0.15, 0.25, 0.8)
	slot_style.set_border_width_all(2)
	slot_style.border_color = Color(0.3, 0.5, 0.7, 0.5)
	slot_style.set_corner_radius_all(6)
	
	# Add subtle grid pattern to the slot
	slot_style.shadow_color = Color(0.2, 0.4, 0.6, 0.15)
	slot_style.shadow_size = 1
	slot_style.shadow_offset = Vector2(1, 1)
	
	fish_image_container.add_theme_stylebox_override("panel", slot_style)

func setup(items: Array):
	fish_data_list = items
	
	# Use the first item for display properties
	var item = items[0]
	
	# Set labels
	weight_label.text = "Weight: %.1f kg" % item.weight
	value_label.text = "Value: $%d" % item.price
	
	# Update count badge
	if items.size() > 1:
		count_badge.visible = true
		count_label.text = str(items.size())
	else:
		count_badge.visible = false
	
	# Use the actual stored fish type instead of guessing
	var stored_type = item.type
	fish_type = FishesConfig.FishType.FLAMY # Default fallback
	
	#print("Fish item setup - stored_type: '", stored_type, "', price: ", item.price, ", weight: ", item.weight, ", shiny: ", item.shiny)
	
	# Convert stored type (string) to enum value
	if stored_type.is_valid_int():
		# If it's stored as a number string like "0", "1", etc.
		var type_int = int(stored_type)
		if type_int >= 0 and type_int < FishesConfig.FishType.size():
			fish_type = type_int
			#print("Successfully converted stored fish type: ", type_int, " (", FishesConfig.FishType.keys()[type_int], ")")
		else:
			print("ERROR: Invalid fish type number: ", type_int)
	else:
		# If it's stored as a name string like "FLAMY", "GREENY", etc.
		var enum_keys = FishesConfig.FishType.keys()
		var found = false
		for i in range(enum_keys.size()):
			if enum_keys[i] == stored_type:
				fish_type = i
				#print("Successfully converted stored fish type: ", stored_type, " -> ", i)
				found = true
				break
		if not found:
			print("ERROR: Could not find fish type for: '", stored_type, "'")
			print("Available types: ", enum_keys)
	
	# Load the fish image
	load_fish_image(item.shiny)

func load_fish_image(is_shiny: bool):
	# Get the fish config
	var fish_config = FishesConfig.fishConfigMap[fish_type]
	
	# Clear any existing content
	for child in fish_image.get_children():
		child.queue_free()
	
	# Create background color for visual feedback
	var background = ColorRect.new()
	background.color = Color(0.1, 0.1, 0.1, 0.5)  # Semi-transparent dark background
	
	# Change background color for shiny fish
	if is_shiny:
		background.color = Color(0.8, 0.7, 0.2, 0.3)  # Golden tint for shiny fish
	
	background.set_anchors_preset(Control.PRESET_FULL_RECT)
	fish_image.add_child(background)
	
	# Add fish icon
	var icon_texture = TextureRect.new()
	icon_texture.texture = fish_config.icon
	icon_texture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon_texture.expand_mode = TextureRect.EXPAND_FIT_WIDTH_PROPORTIONAL
	icon_texture.set_anchors_preset(Control.PRESET_FULL_RECT)
	
	# Add some padding to the icon
	var margin = 4
	icon_texture.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT, Control.PRESET_MODE_MINSIZE, margin)
	
	# Check if icon is loaded
	if fish_config.icon == null:
		print("WARNING: No icon found for fish type: ", fish_type, " (", FishesConfig.FishType.keys()[fish_type], ")")
		# Add fallback text if icon is missing
		var fallback_label = Label.new()
		fallback_label.text = "Type: " + str(fish_type)
		fallback_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		fallback_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		fallback_label.set_anchors_preset(Control.PRESET_FULL_RECT)
		fish_image.add_child(fallback_label)
	else:
		fish_image.add_child(icon_texture)

func _draw_grid_overlay(control):
	var rect = control.get_rect()
	var line_color = Color(1, 1, 1, 0.1)
	
	# Draw horizontal lines
	for y in range(0, int(rect.size.y), 10):
		control.draw_line(Vector2(0, y), Vector2(rect.size.x, y), line_color, 1)
	
	# Draw vertical lines
	for x in range(0, int(rect.size.x), 10):
		control.draw_line(Vector2(x, 0), Vector2(x, rect.size.y), line_color, 1)

func add_fish(fish_item: InvItem) -> void:
	fish_data_list.append(fish_item)
	
	# Update count badge
	count_badge.visible = true
	count_label.text = str(fish_data_list.size())

func _on_release_button_pressed():
	# Release the last fish in the stack
	var fish_to_release = fish_data_list.pop_back()
	emit_signal("fish_released", fish_to_release)
	
	# Update UI if there are still fish in the stack
	if fish_data_list.size() > 0:
		if fish_data_list.size() > 1:
			count_label.text = str(fish_data_list.size())
		else:
			count_badge.visible = false
	
	# Queue for deletion if stack is empty
	if fish_data_list.size() == 0:
		queue_free() 
