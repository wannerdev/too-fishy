extends PanelContainer

@onready var grid = $VBoxContainer/GridContainer
@export var is_open = false

func addButton(text, mCall):
	var upgradeButton: Button = Button.new()
	upgradeButton.text = text
	upgradeButton.pressed.connect(mCall)
	grid.add_child(upgradeButton)

func _ready():
	addButton("Upgrade All", func(): upgrade_all())
	addButton("1000+ $", func(): GameState.money += 1000)
	addButton("Down 100", func(): GameState.player_node.position.y -= 100)
	addButton("Go Up", func(): GameState.player_node.position.y = 0)
	addButton("God mode", func(): GameState.god_mode = not GameState.god_mode)
	addButton("Heal", func(): GameState.health = 100)
	addButton("Kill", func(): GameState.health = 0)
	addButton("Skip Dialog", func(): skip_dialog())
	addButton("Skip Intro", func(): skip_intro_mission())
	addButton("trauma 1", func(): GameState.player_node.traumaShakeMode = 1)
	addButton("trauma 2", func(): GameState.player_node.traumaShakeMode = 2)
	addButton("trauma 3", func(): GameState.player_node.traumaShakeMode = 3)
	
	# Comment out the close() call to keep the menu open by default
	# close()


func upgrade_all():
	for key in GameState.upgrades:
		GameState.upgrades[key] = GameState.maxUpgrades[key]
	pass
	
func skip_dialog() -> void:
	@warning_ignore("int_as_enum_without_cast")
	Boss.boss_dialog_section = 999
	Boss.boss_dialog_displayed = false
	Boss.boss_dialog_index = 0
	GameState.paused = false
	get_tree().paused = false

func _process(_delta):
	if GameState.god_mode:
		GameState.health = 100
	if GameState.isDocked:
		close()

func _input(event):
	if event.is_action_pressed("cht_toggle"):
		if GameState.isDocked:
			return
		if is_open:
			close()
		else:
			open()

func open():
	visible = true
	is_open = true


func close():
	visible = false
	is_open = false

func skip_intro_mission() -> void:
	# Only skip if currently in intro mission
	if not GameState.is_intro():
		print("Not in intro mission, nothing to skip")
		return
	
	print("Skipping intro mission via cheat")
	
	# Get the level node to call its cleanup functions
	var level_node = get_node_or_null("/root/Node3D/Level")
	if not level_node:
		print("Level node not found")
		return
	
	# Store current position as "death position" for completion
	var current_pos = GameState.player_node.position if GameState.player_node else Vector3(-8, -450, 0.33)
	
	# Move player to surface
	if GameState.player_node:
		GameState.player_node.position = Vector3(-8, 0, 0.33)
	
	# Reset depth to surface
	GameState.setDepth(0)
	
	# Complete intro mission (this resets upgrades and sets mode to normal)
	GameState.complete_intro_mission(current_pos)
	
	# Reset level's lastSpawned to surface level for normal gameplay
	if level_node.has_method("remove_intro_mission_elements"):
		level_node.remove_intro_mission_elements()
		level_node.lastSpawned = -35
	
	# Hide boss health bar since player is no longer in boss fight
	if Boss.has_method("hide_boss_health_bar"):
		Boss.hide_boss_health_bar()
	
	# Switch back to normal submarine appearance AFTER upgrades are reset
	if GameState.player_node and GameState.player_node.has_method("switch_to_normal_submarine"):
		GameState.player_node.switch_to_normal_submarine()
	
	# Ensure game is unpaused and UI is properly restored
	GameState.paused = false
	get_tree().paused = false
	GameState.death_screen = false
	GameState.isDocked = false
	
	print("Intro mission skipped successfully - player is now in normal mode at surface")
