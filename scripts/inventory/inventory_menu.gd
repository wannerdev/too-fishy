extends Control

# Node references
@onready var fish_grid = $Panel/MarginContainer/VBoxContainer/ScrollContainer/MarginContainer/FishGrid
@onready var fish_count_label = $Panel/MarginContainer/VBoxContainer/InfoSection/Labels/FishCount
@onready var total_weight_label = $Panel/MarginContainer/VBoxContainer/InfoSection/Labels/TotalWeight
@onready var total_value_label = $Panel/MarginContainer/VBoxContainer/InfoSection/Labels/TotalValue
@onready var close_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/CloseButton
@onready var sell_drone_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/SellDroneButton

# Resource references
var fish_item_scene = preload("res://scenes/ui/fish_item.tscn")

func _ready():
	# Hide menu initially
	hide()
	
	# Style buttons
	_style_buttons()
	
	# Connect signals
	close_button.pressed.connect(_on_close_button_pressed)
	sell_drone_button.pressed.connect(_on_sell_drone_pressed)
	
	# Connect to inventory changes
	GameState.inventory_updated.connect(refresh_inventory)

func _style_buttons():
	var buttons = [close_button, sell_drone_button]
	
	for button in buttons:
		var button_style = StyleBoxFlat.new()
		button_style.bg_color = Color(0.2, 0.4, 0.7, 0.9)
		button_style.set_border_width_all(1)
		button_style.border_color = Color(0.4, 0.6, 0.9)
		button_style.set_corner_radius_all(5)
		button.add_theme_stylebox_override("normal", button_style)
		button.add_theme_color_override("font_color", Color(1, 1, 1))
		
		var hover_style = StyleBoxFlat.new()
		hover_style.bg_color = Color(0.3, 0.5, 0.8, 0.9)
		hover_style.set_border_width_all(1)
		hover_style.border_color = Color(0.5, 0.7, 1.0)
		hover_style.set_corner_radius_all(5)
		button.add_theme_stylebox_override("hover", hover_style)
		
		var pressed_style = StyleBoxFlat.new()
		pressed_style.bg_color = Color(0.15, 0.35, 0.65, 0.9)
		pressed_style.set_border_width_all(1)
		pressed_style.border_color = Color(0.3, 0.5, 0.8)
		pressed_style.set_corner_radius_all(5)
		button.add_theme_stylebox_override("pressed", pressed_style)

func open():
	# Get latest inventory data
	refresh_inventory()
	
	# Show the menu
	show()

func close():
	# Hide the menu
	hide()

# Function to determine if two fish items are stackable (have same properties)
func are_fish_stackable(fish1: InvItem, fish2: InvItem) -> bool:
	return (
		fish1.type == fish2.type and
		abs(fish1.weight - fish2.weight) < 0.1 and
		fish1.price == fish2.price and
		fish1.shiny == fish2.shiny
	)

func refresh_inventory():
	# Clear existing fish items
	for child in fish_grid.get_children():
		child.queue_free()
	
	# Get inventory data
	var inventory = GameState.inventory
	
	# Update info labels
	fish_count_label.text = "Fish Count: %d" % inventory.inventoryCumulatedValues[inventory.InventoryValues.FishesCaught]
	total_weight_label.text = "Total Weight: %.1f / %.1f kg" % [inventory.inventoryCumulatedValues[inventory.InventoryValues.TotalWeight], inventory.get_max_weight()]
	total_value_label.text = "Total Value: $%.2f" % inventory.inventoryCumulatedValues[inventory.InventoryValues.TotalValue]
	
	# Group similar fish together
	var fish_groups = []
	
	for item in inventory.items:
		var found_group = false
		
		# Try to find an existing group to add this fish to
		for group in fish_groups:
			if are_fish_stackable(group[0], item):
				group.append(item)
				found_group = true
				break
				
		# If no matching group was found, create a new one
		if not found_group:
			fish_groups.append([item])
	
	# Add fish items to grid
	for fish_group in fish_groups:
		# Create fish item UI
		var fish_item_ui = fish_item_scene.instantiate()
		fish_grid.add_child(fish_item_ui)
		
		# Setup fish item with the group
		fish_item_ui.setup(fish_group)
		
		# Connect release signal
		fish_item_ui.fish_released.connect(_on_fish_released)
	
	# Update sell drone button visibility and state
	# Disabled during intro mission to avoid spawning drone in intro mode
	if GameState.upgrades[GameState.Upgrade.DRONE_SELLING] > 0 and !GameState.is_intro():
		sell_drone_button.visible = true
		sell_drone_button.disabled = inventory.items.size() == 0
	else:
		sell_drone_button.visible = false

func _on_close_button_pressed():
	close()

func _on_sell_drone_pressed():
	# Only process if drone selling upgrade is purchased and we're not in intro mission
	if GameState.upgrades[GameState.Upgrade.DRONE_SELLING] > 0 and !GameState.is_intro():
		var sold_amount = GameState.inventory.sellItems()
		
		if sold_amount > 0:
			# Show popup message using player's position as reference point
			if GameState.player_node:
				PopupManager.show_popup("Drone sold all fish for $" + str(sold_amount), GameState.player_node.get_node("PopupSpawnPosition").global_position, Color.GREEN)
			
			# Play sell sound
			if GameState.player_node and GameState.player_node.has_method("play_sound"):
				GameState.player_node.play_sound("coins")
			
			# Create a drone visual effect if player node exists
			if GameState.player_node and GameState.player_node.has_method("activate_selling_drone"):
				GameState.player_node.activate_selling_drone()
		
		# Refresh inventory display
		refresh_inventory()

func _on_fish_released(fish_item_data):
	# Find and remove the fish from inventory
	for i in range(GameState.inventory.items.size()):
		if GameState.inventory.items[i].id == fish_item_data.id:
			var fish = GameState.inventory.items[i]
			GameState.inventory.release_fish(fish)
			GameState.inventory.items.remove_at(i)
			GameState.inventory.updateTotal()
			break
	
	# Update UI
	refresh_inventory() 
