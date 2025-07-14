extends Node

signal inventory_updated

#DEFINITIONS
enum Stage {SURFACE, DEEP, DEEPER, SUPERDEEP, HOT, LAVA, VOID}
enum GameMode {NORMAL, INTRO_MISSION}
var depthStageMap = {
	0: Stage.SURFACE,
	100: Stage.DEEP,
	200: Stage.DEEPER,
	300: Stage.SUPERDEEP,
	400: Stage.HOT,
	500: Stage.LAVA,
	600: Stage.VOID
}

enum Upgrade {CARGO_SIZE, DEPTH_RESISTANCE, PICKAXE_UNLOCKED, VERT_SPEED, HOR_SPEED, LAMP_UNLOCKED, AK47, DUALAK47, HARPOON, HARPOON_ROTATION, INVENTORY_MANAGEMENT, SURFACE_BUOY, INVENTORY_SAVE, DRONE_SELLING}
var upgradeCosts = {
	Upgrade.CARGO_SIZE: 25,
	Upgrade.DEPTH_RESISTANCE: 50,
	Upgrade.PICKAXE_UNLOCKED: 200,
	Upgrade.VERT_SPEED: 25,
	Upgrade.HOR_SPEED: 25,
	Upgrade.LAMP_UNLOCKED: 50,
	Upgrade.AK47: 500,
	Upgrade.DUALAK47: 5000,
	Upgrade.HARPOON: 100,
	Upgrade.HARPOON_ROTATION: 150,
	Upgrade.INVENTORY_MANAGEMENT: 400,
	Upgrade.SURFACE_BUOY: 1000,
	Upgrade.INVENTORY_SAVE: 250,
	Upgrade.DRONE_SELLING: 300
}

var maxUpgrades = {
	Upgrade.CARGO_SIZE: 5,
	Upgrade.DEPTH_RESISTANCE: 5,
	Upgrade.PICKAXE_UNLOCKED: 1,
	Upgrade.VERT_SPEED: 2,
	Upgrade.HOR_SPEED: 3,
	Upgrade.LAMP_UNLOCKED: 1,
	Upgrade.AK47: 1,
	Upgrade.DUALAK47: 1,
	Upgrade.HARPOON: 1,
	Upgrade.HARPOON_ROTATION: 1,
	Upgrade.INVENTORY_MANAGEMENT: 1,
	Upgrade.SURFACE_BUOY: 1,
	Upgrade.INVENTORY_SAVE: 1,
	Upgrade.DRONE_SELLING: 1
}

#STATE
var upgrades = {
	Upgrade.CARGO_SIZE: 0,
	Upgrade.DEPTH_RESISTANCE: 0,
	Upgrade.PICKAXE_UNLOCKED: 0,
	Upgrade.VERT_SPEED: 0,
	Upgrade.HOR_SPEED: 0,
	Upgrade.LAMP_UNLOCKED: 0,
	Upgrade.AK47: 0,
	Upgrade.DUALAK47: 0,
	Upgrade.HARPOON: 0,
	Upgrade.HARPOON_ROTATION: 0,
	Upgrade.INVENTORY_MANAGEMENT: 0,
	Upgrade.SURFACE_BUOY: 0,
	Upgrade.INVENTORY_SAVE: 0,
	Upgrade.DRONE_SELLING: 0
}
var depth = 0
var maxDepthReached = 0
var money: int = 25
var isDocked = false
var fishes_lower_boarder = -15 - 12

var player_node: CharacterBody3D = null
var god_mode = false
var health = 100.0
var headroom = 0
var death_screen = false
var paused = true

var inventory: Inv = Inv.new()

var playerInStage: Stage = Stage.SURFACE

# Intro mission state
var current_game_mode: GameMode = GameMode.NORMAL
var is_first_time_player = true
var intro_mission_completed = false
var friend_death_position: Vector3 = Vector3.ZERO

func _ready():
	# Connect to inventory's methods to emit the inventory_updated signal
	inventory.connect_signals_to_gamestate(self)
	
	#  automatically start intro mission for first-time players
	if is_first_time_player and not intro_mission_completed:
		start_intro_mission()

func _process(_delta):
	# Ensure game is not paused during intro mission
	if is_intro_mission_active():
		paused = false
		get_tree().paused = false
		isDocked = false

func setDepth(d: int):
	depth = d
	if (maxDepthReached < d):
		maxDepthReached = d
	
	# During intro mission, force stage to be HOT until mission is complete
	if is_intro_mission_active():
		# Keep player in HOT stage during intro mission
		playerInStage = Stage.HOT
		return
	
	# Calculate the appropriate stage based on depth
	var snapped_depth = snapped(d, 100)
	var new_stage = Stage.SURFACE # Default to SURFACE for shallow depths
	
	# Sort depth thresholds and find the highest one that we meet or exceed
	var sorted_depths = depthStageMap.keys()
	sorted_depths.sort()
	
	for depth_threshold in sorted_depths:
		if snapped_depth >= depth_threshold:
			new_stage = depthStageMap[depth_threshold]
		# Continue to find the highest threshold we meet
	
	GameState.playerInStage = new_stage

func getUpgradeCost(mUpgrade: Upgrade) -> int:
	return int((upgrades[mUpgrade] + 1) * upgradeCosts[mUpgrade])

func upgrade(mUpgrade: Upgrade) -> bool:
	var cost = getUpgradeCost(mUpgrade)
	if money >= cost && upgrades[mUpgrade] < maxUpgrades[mUpgrade]:
		money -= cost
		# Ensure money stays as integer
		money = int(money)
		upgrades[mUpgrade] += 1
		return true
	else:
		return false

func notify_inventory_updated():
	emit_signal("inventory_updated")

func is_boss_defeated() -> bool:
	# Boss is defeated when the WIN dialog stage has been reached
	return Boss.boss_dialog_section == Boss.BossDialogSections.WIN

# Intro mission functions
func start_intro_mission():
	print("Starting intro mission")
	current_game_mode = GameMode.INTRO_MISSION
	is_first_time_player = true
	intro_mission_completed = false
	
	# Reset game state properly for intro mission
	death_screen = false
	paused = false
	health = 100
	isDocked = false  # Ensure player is not docked during intro mission
	
	# Force hide death UI and reset all states
	force_hide_death_ui()
	
	# Start in hot zone where friend will spawn
	playerInStage = Stage.HOT
	depth = 450  # Starting depth in hot zone (matches spawn position)
	
	# Configure friend upgrades
	setup_friend_upgrades()
	
	# Wait for player node to be available, then set position
	await get_tree().process_frame
	if player_node:
		player_node.position = Vector3(-8, -450, 0.33)  # Start in hot zone
		# Note: Removed invulnerability - friend can take damage during intro mission
	
	# Additional state reset to ensure clean start
	get_tree().create_timer(0.1).timeout.connect(func(): force_hide_death_ui())
	
	print("Intro mission setup complete")

func force_hide_death_ui():
	"""Force hide death screen and reset all UI states"""
	print("Forcing death UI to hide during intro mission")
	death_screen = false
	paused = false
	isDocked = false  # Make sure player is not docked
	
	# Find and hide death screen UI
	var ui_node = get_node_or_null("/root/Node3D/UI")
	if ui_node:
		# Hide death screen
		var death_screen_node = ui_node.find_child("DeathScreen", true, false)
		if death_screen_node:
			death_screen_node.visible = false
			print("Death screen hidden")
		
		# Hide upgrade menu specifically
		var upgrade_menu = ui_node.find_child("Upgrades", true, false)
		if upgrade_menu:
			upgrade_menu.visible = false
			print("Upgrade menu hidden")
		
		# Hide inventory menu if it's open
		var inventory_menu = ui_node.find_child("InventoryMenu", true, false)
		if inventory_menu:
			inventory_menu.visible = false
			print("Inventory menu hidden")
		
		# Hide pause menu if it's open
		var pause_menu = ui_node.find_child("PauseMenu", true, false)
		if pause_menu:
			pause_menu.visible = false
			print("Pause menu hidden")
		
		# Hide achievement UI during intro mission
		var achievement_ui = ui_node.find_child("AchievementUI", true, false)
		if achievement_ui:
			achievement_ui.visible = false
			print("Achievement UI hidden")
	
	# Reset tree pause state
	get_tree().paused = false
	print("Game state reset for intro mission")

func complete_intro_mission(death_position: Vector3):
	print("Completing intro mission at position: ", death_position)
	friend_death_position = death_position
	intro_mission_completed = true
	# Reset upgrades to 0 for normal player
	reset_upgrades()
	current_game_mode = GameMode.NORMAL
	is_first_time_player = false
	# Spawn player back at surface
	playerInStage = Stage.SURFACE
	depth = 0
	
	# Restore achievement UI visibility after intro mission
	restore_ui_after_intro_mission()

func reset_upgrades():
	for upgrade_key in upgrades:
		upgrades[upgrade_key] = 0

func is_intro_mission_active() -> bool:
	return current_game_mode == GameMode.INTRO_MISSION

func should_disable_barriers() -> bool:
	return is_intro_mission_active()

func setup_friend_upgrades():
	for upgrade2 in upgrades:
		if upgrade2 == Upgrade.PICKAXE_UNLOCKED:
			upgrades[upgrade2] = 0  # Friend has no pickaxe
		else:
			upgrades[upgrade2] = maxUpgrades[upgrade2]  # Max level for everything else
	
	# Set friend to have lots of money for the intro
	money = 1000

func restore_ui_after_intro_mission():
	
	# Find and restore achievement UI
	var ui_node = get_node_or_null("/root/Node3D/UI")
	if ui_node:
		var achievement_ui = ui_node.find_child("AchievementUI", true, false)
		if achievement_ui:
			achievement_ui.visible = true
			print("Achievement UI restored")
		
		# Find and completely rebuild upgrade menu to show all buttons
		var upgrade_menu = ui_node.find_child("Upgrades", true, false)
		if upgrade_menu and upgrade_menu.has_method("rebuild_upgrade_menu"):
			upgrade_menu.rebuild_upgrade_menu()
			print("Upgrade menu rebuilt - all buttons should now be visible")
		elif upgrade_menu and upgrade_menu.has_method("refresh_all_upgrades"):
			upgrade_menu.refresh_all_upgrades()
			print("Upgrade menu refreshed (fallback method)")
		else:
			print("Upgrade menu not found or missing rebuild method")
