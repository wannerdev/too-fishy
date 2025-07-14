extends PanelContainer

var buttons = {}
var upgrade_descriptions = null
var money_label = null # Direct reference to money display label

# Category definitions
var categories = {
	"EQUIPMENT": [
		GameState.Upgrade.PICKAXE_UNLOCKED,
		GameState.Upgrade.LAMP_UNLOCKED,
		GameState.Upgrade.AK47,
		GameState.Upgrade.DUALAK47,
		GameState.Upgrade.HARPOON,
		GameState.Upgrade.HARPOON_ROTATION
	],
	"PERFORMANCE": [
		GameState.Upgrade.CARGO_SIZE,
		GameState.Upgrade.DEPTH_RESISTANCE,
		GameState.Upgrade.VERT_SPEED,
		GameState.Upgrade.HOR_SPEED
	],
	"UTILITY": [
		GameState.Upgrade.INVENTORY_MANAGEMENT,
		GameState.Upgrade.SURFACE_BUOY,
		GameState.Upgrade.INVENTORY_SAVE,
		GameState.Upgrade.DRONE_SELLING
	]
}

func _ready():
	_setup_upgrade_menu()

func create_category_column(category_name, upgrade_keys):
	# Create a container for this category
	var category_container = VBoxContainer.new()
	category_container.size_flags_horizontal = SIZE_EXPAND_FILL
	category_container.size_flags_vertical = SIZE_EXPAND_FILL
	
	# Add category header panel with style
	var header_panel = PanelContainer.new()
	var header_style = StyleBoxFlat.new()
	header_style.bg_color = Color(0.15, 0.3, 0.5, 0.8) # Blue
	header_style.set_border_width_all(2)
	header_style.border_color = Color(0.3, 0.5, 0.7)
	header_style.set_corner_radius_all(8)
	header_panel.add_theme_stylebox_override("panel", header_style)
	category_container.add_child(header_panel)
	
	# Add margin to header panel
	var header_margin = MarginContainer.new()
	header_margin.add_theme_constant_override("margin_left", 10)
	header_margin.add_theme_constant_override("margin_right", 10)
	header_margin.add_theme_constant_override("margin_top", 8)
	header_margin.add_theme_constant_override("margin_bottom", 8)
	header_panel.add_child(header_margin)
	
	# Add category header
	var header = Label.new()
	header.text = category_name
	header.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	header.add_theme_color_override("font_color", Color(0.9, 0.9, 0.5)) # Yellow-ish
	header.add_theme_font_size_override("font_size", 20)
	header_margin.add_child(header)
	
	# Add spacing after header
	var spacer = Control.new()
	spacer.custom_minimum_size = Vector2(0, 10)
	category_container.add_child(spacer)
	
	# Create a container for the upgrade buttons
	var upgrade_list = VBoxContainer.new()
	upgrade_list.size_flags_horizontal = SIZE_EXPAND_FILL
	upgrade_list.size_flags_vertical = SIZE_EXPAND_FILL
	upgrade_list.add_theme_constant_override("separation", 10)
	category_container.add_child(upgrade_list)
	
	# Add upgrade buttons to the grid
	for key in upgrade_keys:
		var button_container = create_upgrade_button(key)
		upgrade_list.add_child(button_container)
	
	return category_container

func create_upgrade_button(key):
	# Get upgrade data
	var current_level = GameState.upgrades[key]
	var max_level = GameState.maxUpgrades[key]
	var is_max_level = current_level >= max_level
	var upgrade_cost = GameState.getUpgradeCost(key)
	var can_afford = GameState.money >= upgrade_cost
	
	# Container for the button with fixed size
	var container = PanelContainer.new()
	container.size_flags_horizontal = SIZE_EXPAND_FILL
	container.custom_minimum_size = Vector2(0, 90) # Slightly taller for level indicators
	
	# Add a theme style to make it look better with conditional coloring
	var style = StyleBoxFlat.new()
	
	if is_max_level:
		# Gold/green style for max level
		style.bg_color = Color(0.2, 0.35, 0.2, 0.7) # Slightly green
		style.set_border_width_all(2)
		style.border_color = Color(0.5, 0.8, 0.3) # Green border
	elif can_afford:
		# Standard blue style for affordable upgrades
		style.bg_color = Color(0.1, 0.25, 0.4, 0.7) # Standard blue
		style.set_border_width_all(2)
		style.border_color = Color(0.3, 0.5, 0.7) # Blue border
	else:
		# Slightly darker/red style for unaffordable upgrades
		style.bg_color = Color(0.25, 0.15, 0.15, 0.7) # Darker red tint
		style.set_border_width_all(2)
		style.border_color = Color(0.5, 0.3, 0.3) # Red-ish border
	
	style.set_corner_radius_all(8)
	container.add_theme_stylebox_override("panel", style)
	
	# Set tooltip for the entire container
	if upgrade_descriptions and Strings.upgradeDescriptions.has(key):
		container.tooltip_text = Strings.upgradeDescriptions[key]
		# Set tooltip to appear immediately (set here explicitly for this container)
		container.mouse_filter = Control.MOUSE_FILTER_STOP
		container.focus_mode = Control.FOCUS_ALL
		container.mouse_default_cursor_shape = Control.CURSOR_HELP
		# Add theme override to increase tooltip text size
		container.add_theme_font_size_override("tooltip_font_size", 18)
	
	# Add margin to container
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 12)
	margin.add_theme_constant_override("margin_right", 12)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_bottom", 8)
	margin.size_flags_horizontal = SIZE_EXPAND_FILL
	margin.size_flags_vertical = SIZE_EXPAND_FILL
	container.add_child(margin)
	
	# Main content container
	var content = VBoxContainer.new()
	content.size_flags_horizontal = SIZE_EXPAND_FILL
	content.size_flags_vertical = SIZE_EXPAND_FILL
	margin.add_child(content)
	
	# Top row with name and hover indicator
	var top_row = HBoxContainer.new()
	top_row.size_flags_horizontal = SIZE_EXPAND_FILL
	content.add_child(top_row)
	
	# Upgrade name
	var name_label = Label.new()
	name_label.text = Strings.upgradeNames[key]
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	name_label.size_flags_horizontal = SIZE_EXPAND_FILL
	name_label.add_theme_font_size_override("font_size", 16)
	name_label.add_theme_color_override("font_color", Color(1, 1, 1)) # White
	top_row.add_child(name_label)
	
	
	# Middle row for level indicators
	var level_row = HBoxContainer.new()
	level_row.size_flags_horizontal = SIZE_EXPAND_FILL
	content.add_child(level_row)
	
	# Create visual level indicators
	for i in range(max_level):
		var level_indicator = PanelContainer.new()
		level_indicator.custom_minimum_size = Vector2(25, 8)
		level_indicator.size_flags_horizontal = SIZE_SHRINK_CENTER
		
		var indicator_style = StyleBoxFlat.new()
		indicator_style.bg_color = Color(0.545, 0.545, 0.545) # bright gray for unachieved levels

		if i < current_level:
			# Filled level
			indicator_style.bg_color = Color(0.3, 0.7, 1.0)
	
		indicator_style.set_corner_radius_all(3)
		level_indicator.add_theme_stylebox_override("panel", indicator_style)
		
		level_row.add_child(level_indicator)
		
		# Add spacer between indicators
		if i < max_level - 1:
			var indicator_spacer = Control.new()
			indicator_spacer.custom_minimum_size = Vector2(5, 0)
			level_row.add_child(indicator_spacer)
	
	# Bottom row with level/cost info and upgrade button
	var bottom_row = HBoxContainer.new()
	bottom_row.size_flags_horizontal = SIZE_EXPAND_FILL
	bottom_row.size_flags_vertical = SIZE_EXPAND_FILL
	content.add_child(bottom_row)
	
	# Level and cost info with appropriate coloring
	var info_label = Label.new()
	if is_max_level:
		info_label.text = "MAXIMUM LEVEL"
		info_label.add_theme_color_override("font_color", Color(0.5, 0.9, 0.5)) # Green
	else:
		# Show cost with appropriate color
		info_label.text = "Cost: $" + str(upgrade_cost)
		if can_afford:
			info_label.add_theme_color_override("font_color", Color(0.9, 0.9, 0.5)) # Yellow - can afford
		else:
			info_label.add_theme_color_override("font_color", Color(0.9, 0.5, 0.5)) # Red - can't afford
	
	info_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	info_label.size_flags_horizontal = SIZE_EXPAND_FILL
	info_label.size_flags_vertical = SIZE_SHRINK_CENTER
	bottom_row.add_child(info_label)
	
	# Create the upgrade button with conditional styling
	var upgradeButton = Button.new()
	upgradeButton.text = "Upgrade"
	upgradeButton.size_flags_horizontal = SIZE_SHRINK_END
	upgradeButton.size_flags_vertical = SIZE_SHRINK_CENTER
	upgradeButton.custom_minimum_size = Vector2(100, 30)
	
	# Style the button based on affordability
	if !is_max_level:
		if can_afford:
			# Affordable - highlight the button
			var button_style = StyleBoxFlat.new()
			button_style.bg_color = Color(0.2, 0.4, 0.7)
			button_style.set_border_width_all(1)
			button_style.border_color = Color(0.4, 0.6, 0.9)
			button_style.set_corner_radius_all(5)
			upgradeButton.add_theme_stylebox_override("normal", button_style)
			upgradeButton.add_theme_color_override("font_color", Color(1, 1, 1))
		else:
			# Can't afford - dim the button
			var button_style = StyleBoxFlat.new()
			button_style.bg_color = Color(0.3, 0.3, 0.35)
			button_style.set_border_width_all(1)
			button_style.border_color = Color(0.4, 0.4, 0.45)
			button_style.set_corner_radius_all(5)
			upgradeButton.add_theme_stylebox_override("normal", button_style)
			upgradeButton.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))
	
	# Connect signals
	upgradeButton.pressed.connect(func(): on_upgrade_pressed(key, info_label))
	
	# Set same tooltip on the button
	if upgrade_descriptions and Strings.upgradeDescriptions.has(key):
		upgradeButton.tooltip_text = Strings.upgradeDescriptions[key]
		# Apply tooltip settings to the button
		upgradeButton.mouse_filter = Control.MOUSE_FILTER_STOP
		upgradeButton.focus_mode = Control.FOCUS_ALL
		upgradeButton.mouse_default_cursor_shape = Control.CURSOR_POINTING_HAND
		upgradeButton.add_theme_font_size_override("tooltip_font_size", 18)
	
	# Hide button if max level reached
	if is_max_level:
		upgradeButton.visible = false
	
	# Add the upgrade button
	bottom_row.add_child(upgradeButton)
	
	# Store references for later updates
	buttons[key] = {
		"button": upgradeButton,
		"container": container,
		"info_label": info_label,
		"level_indicators": []
	}
	
	# Store the level indicators for updates
	for i in range(level_row.get_child_count()):
		var child = level_row.get_child(i)
		if child is PanelContainer:
			buttons[key].level_indicators.append(child)
	
	return container

func on_upgrade_pressed(key, info_label):
	var success = GameState.upgrade(key)
	
	if success:
		# Update money display immediately
		update_money_display()
		
		# First update this button's state
		update_button_state(key)
		
		# Then update affordability of all other buttons since money has changed
		for upgrade_key in buttons:
			if upgrade_key != key: # Skip the one we just updated
				update_button_affordability(upgrade_key)

func update_button_state(key):
	if not buttons.has(key):
		return
	
	var button_data = buttons[key]
	var upgradeButton = button_data.button
	var info_label = button_data.info_label
	var level_indicators = button_data.level_indicators
	
	var current_level = GameState.upgrades[key]
	var max_level = GameState.maxUpgrades[key]
	var is_max_level = current_level >= max_level
	var upgrade_cost = GameState.getUpgradeCost(key)
	var can_afford = GameState.money >= upgrade_cost
	
	# Update container style
	var container = button_data.container
	var style = container.get_theme_stylebox("panel")
	
	if is_max_level:
		# Gold/green style for max level
		style.bg_color = Color(0.2, 0.35, 0.2, 0.7) # Slightly green
		style.border_color = Color(0.5, 0.8, 0.3) # Green border
	elif can_afford:
		# Standard blue style for affordable upgrades
		style.bg_color = Color(0.1, 0.25, 0.4, 0.7) # Standard blue
		style.border_color = Color(0.3, 0.5, 0.7) # Blue border
	else:
		# Slightly darker/red style for unaffordable upgrades
		style.bg_color = Color(0.25, 0.15, 0.15, 0.7) # Darker red tint
		style.border_color = Color(0.5, 0.3, 0.3) # Red-ish border
	
	# Update level indicators
	for i in range(level_indicators.size()):
		var indicator = level_indicators[i]
		var indicator_style = indicator.get_theme_stylebox("panel")
		
		# Empty level
		indicator_style.bg_color = Color(0.307, 0.307, 0.307, 0.5)
		if i < current_level:
			# Filled level
			indicator_style.bg_color = Color(0.3, 0.7, 1.0)
	
	# Update cost/level info
	if is_max_level:
		info_label.text = "MAXIMUM LEVEL"
		info_label.add_theme_color_override("font_color", Color(0.5, 0.9, 0.5))
		upgradeButton.visible = false
	else:
		info_label.text = "Cost: $" + str(upgrade_cost)
		if can_afford:
			info_label.add_theme_color_override("font_color", Color(0.9, 0.9, 0.5)) # Yellow - can afford
			
			# Update button style for affordable upgrades
			var button_style = StyleBoxFlat.new()
			button_style.bg_color = Color(0.2, 0.4, 0.7)
			button_style.set_border_width_all(1)
			button_style.border_color = Color(0.4, 0.6, 0.9)
			button_style.set_corner_radius_all(5)
			upgradeButton.add_theme_stylebox_override("normal", button_style)
			upgradeButton.add_theme_color_override("font_color", Color(1, 1, 1))
		else:
			info_label.add_theme_color_override("font_color", Color(0.9, 0.5, 0.5)) # Red - can't afford
			
			# Update button style for unaffordable upgrades
			var button_style = StyleBoxFlat.new()
			button_style.bg_color = Color(0.3, 0.3, 0.35)
			button_style.set_border_width_all(1)
			button_style.border_color = Color(0.4, 0.4, 0.45)
			button_style.set_corner_radius_all(5)
			upgradeButton.add_theme_stylebox_override("normal", button_style)
			upgradeButton.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))

func update_button_affordability(key):
	if not buttons.has(key):
		return
	
	var button_data = buttons[key]
	var upgradeButton = button_data.button
	var info_label = button_data.info_label
	
	var current_level = GameState.upgrades[key]
	var max_level = GameState.maxUpgrades[key]
	var is_max_level = current_level >= max_level
	
	if is_max_level:
		return
	
	var upgrade_cost = GameState.getUpgradeCost(key)
	var can_afford = GameState.money >= upgrade_cost
	
	# Update cost color
	if can_afford:
		info_label.add_theme_color_override("font_color", Color(0.9, 0.9, 0.5)) # Yellow - can afford
	else:
		info_label.add_theme_color_override("font_color", Color(0.9, 0.5, 0.5)) # Red - can't afford
	
	# Update container style
	var container = button_data.container
	var style = container.get_theme_stylebox("panel")
	
	if can_afford:
		style.bg_color = Color(0.1, 0.25, 0.4, 0.7) # Standard blue
		style.border_color = Color(0.3, 0.5, 0.7) # Blue border
		
		# Update button style
		var button_style = StyleBoxFlat.new()
		button_style.bg_color = Color(0.2, 0.4, 0.7)
		button_style.set_border_width_all(1)
		button_style.border_color = Color(0.4, 0.6, 0.9)
		button_style.set_corner_radius_all(5)
		upgradeButton.add_theme_stylebox_override("normal", button_style)
		upgradeButton.add_theme_color_override("font_color", Color(1, 1, 1))
	else:
		style.bg_color = Color(0.25, 0.15, 0.15, 0.7) # Darker red tint
		style.border_color = Color(0.5, 0.3, 0.3) # Red-ish border
		
		# Update button style
		var button_style = StyleBoxFlat.new()
		button_style.bg_color = Color(0.3, 0.3, 0.35)
		button_style.set_border_width_all(1)
		button_style.border_color = Color(0.4, 0.4, 0.45)
		button_style.set_corner_radius_all(5)
		upgradeButton.add_theme_stylebox_override("normal", button_style)
		upgradeButton.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))

func update_money_display():
	if money_label:
		var current_money = int(GameState.money)
		money_label.text = "Available: $" + str(current_money)

func refresh_all_upgrades():
	# Update all buttons to reflect current upgrade states
	for key in buttons:
		update_button_state(key)
	
	# Then update affordability of all buttons
	for key in buttons:
		update_button_affordability(key)

func rebuild_upgrade_menu():
	"""Completely rebuild the upgrade menu UI from scratch"""
	print("Rebuilding upgrade menu UI completely")
	
	# Clear all existing children
	for child in get_children():
		child.queue_free()
	
	# Clear buttons dictionary
	buttons.clear()
	
	# Wait a frame for the children to be freed
	await get_tree().process_frame
	
	# Recreate all UI elements (same as _ready())
	_setup_upgrade_menu()

func _setup_upgrade_menu():
	"""Setup the upgrade menu UI (extracted from _ready())"""
	# Add to group for easier reference
	add_to_group("upgrades_menu")
	
	# Make the panel significantly bigger to display all upgrades without scrolling
	custom_minimum_size = Vector2(900, 570)
	
	# Apply a style to the panel itself instead of adding a background image
	var panel_style = StyleBoxFlat.new()
	panel_style.bg_color = Color(0.05, 0.2, 0.3, 0.95) # Deep blue with high opacity
	panel_style.set_border_width_all(3)
	panel_style.border_color = Color(0.2, 0.4, 0.7)
	panel_style.set_corner_radius_all(12)
	add_theme_stylebox_override("panel", panel_style)
	
	# Create main container
	var main_container = VBoxContainer.new()
	main_container.size_flags_horizontal = SIZE_EXPAND_FILL
	main_container.size_flags_vertical = SIZE_EXPAND_FILL
	
	# Add margin to the container for better padding
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 30)
	margin.add_theme_constant_override("margin_right", 30)
	margin.add_theme_constant_override("margin_top", 30)
	margin.add_theme_constant_override("margin_bottom", 30)
	margin.size_flags_horizontal = SIZE_EXPAND_FILL
	margin.size_flags_vertical = SIZE_EXPAND_FILL
	margin.add_child(main_container)
	add_child(margin)
	
	# Header section with title and money
	var header_section = HBoxContainer.new()
	header_section.size_flags_horizontal = SIZE_EXPAND_FILL
	main_container.add_child(header_section)
	
	# Create left side of the header
	var left_header = VBoxContainer.new()
	left_header.size_flags_horizontal = SIZE_EXPAND_FILL
	header_section.add_child(left_header)
	
	# Add title
	var title = Label.new()
	title.text = "SUBMARINE UPGRADES"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	title.add_theme_font_size_override("font_size", 28)
	title.add_theme_color_override("font_color", Color(1, 1, 1)) # White
	left_header.add_child(title)
	
	# Add subtitle
	var subtitle = Label.new()
	subtitle.text = "Select upgrades to enhance your submarine"
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	subtitle.add_theme_font_size_override("font_size", 16)
	subtitle.add_theme_color_override("font_color", Color(0.8, 0.8, 0.9)) # Light blue-white
	left_header.add_child(subtitle)
	
	# Create right side of the header for money display
	var right_header = VBoxContainer.new()
	right_header.size_flags_horizontal = SIZE_SHRINK_END
	right_header.size_flags_vertical = SIZE_SHRINK_CENTER
	header_section.add_child(right_header)
	
	# Create a stylish money display panel
	var money_panel = PanelContainer.new()
	var money_style = StyleBoxFlat.new()
	money_style.bg_color = Color(0.05, 0.15, 0.3, 0.7) # Darker blue
	money_style.set_border_width_all(2)
	money_style.border_color = Color(0.7, 0.7, 0.2) # Gold border
	money_style.set_corner_radius_all(8)
	money_panel.add_theme_stylebox_override("panel", money_style)
	right_header.add_child(money_panel)
	
	# Add margin to money panel
	var money_margin = MarginContainer.new()
	money_margin.add_theme_constant_override("margin_left", 15)
	money_margin.add_theme_constant_override("margin_right", 15)
	money_margin.add_theme_constant_override("margin_top", 10)
	money_margin.add_theme_constant_override("margin_bottom", 10)
	money_panel.add_child(money_margin)
	
	# Add money label
	money_label = Label.new()
	money_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	money_label.add_theme_font_size_override("font_size", 18)
	money_label.add_theme_color_override("font_color", Color(1, 1, 0.2)) # Gold color
	money_margin.add_child(money_label)
	
	# Add spacing after header
	var header_spacer = Control.new()
	header_spacer.custom_minimum_size = Vector2(0, 20)
	main_container.add_child(header_spacer)
	
	# Create main content area with three columns
	var content_container = HBoxContainer.new()
	content_container.size_flags_horizontal = SIZE_EXPAND_FILL
	content_container.size_flags_vertical = SIZE_EXPAND_FILL
	content_container.add_theme_constant_override("separation", 20)
	main_container.add_child(content_container)
	
	# Create categories
	var equipment_column = create_category_column("EQUIPMENT", categories["EQUIPMENT"])
	var performance_column = create_category_column("PERFORMANCE", categories["PERFORMANCE"])
	var utility_column = create_category_column("UTILITY", categories["UTILITY"])
	
	content_container.add_child(equipment_column)
	content_container.add_child(performance_column)
	content_container.add_child(utility_column)
	
	# Initialize money display
	update_money_display()

# Add variable to track visibility changes
var was_visible = false
var man_override = false

# Function to close the upgrade menu
func close_upgrade_menu():
	GameState.isDocked = false
	visible = false
	was_visible = false

func _process(_delta):
	# Force hide upgrade menu during intro mission
	if GameState.is_intro():
		visible = false
		was_visible = false
		return
	
	if GameState.isDocked:
		if !man_override:
			visible = true
		if Input.is_action_pressed("esc"):
			man_override = true
			close_upgrade_menu()
			return

		# If just became visible, do a full refresh
		if not was_visible:
			refresh_all_upgrades()
			was_visible = true
		
		# Update money display every frame when visible
		update_money_display()
	else:
		visible = false
		was_visible = false
		man_override = false
