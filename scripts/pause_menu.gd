extends Control

signal pause_state_changed(paused: bool)

@onready var save_menu = $SaveMenu
@onready var settings_menu = $SettingsMenu
@onready var panel = $Panel
@onready var buttons_container = $Panel/MarginContainer/VBoxContainer/ButtonsContainer
@onready var resume_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/ResumeButton
@onready var save_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/SaveButton
@onready var settings_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/SettingsButton
@onready var quit_button = $Panel/MarginContainer/VBoxContainer/ButtonsContainer/QuitButton
@onready var title_section = $Panel/MarginContainer/VBoxContainer/TitleSection
@onready var player = get_tree().get_first_node_in_group("player")
@onready var upgrade_menu = get_tree().get_first_node_in_group("upgrades_menu")
var is_paused = false

func _ready():
	# Hide the pause menu initially
	hide()
	save_menu.hide()
	settings_menu.hide()
	
	# Style buttons
	_style_buttons()

func _style_buttons():
	var buttons = [resume_button, save_button, settings_button, quit_button]
	
	for button in buttons:
		var button_style = StyleBoxFlat.new()
		button_style.bg_color = Color(0.1, 0.25, 0.4, 0.9)
		button_style.set_border_width_all(2)
		button_style.border_color = Color(0.3, 0.5, 0.7)
		button_style.set_corner_radius_all(8)
		button.add_theme_stylebox_override("normal", button_style)
		
		var hover_style = StyleBoxFlat.new()
		hover_style.bg_color = Color(0.15, 0.3, 0.45, 0.9)
		hover_style.set_border_width_all(2)
		hover_style.border_color = Color(0.4, 0.6, 0.8)
		hover_style.set_corner_radius_all(8)
		button.add_theme_stylebox_override("hover", hover_style)
		
		var pressed_style = StyleBoxFlat.new()
		pressed_style.bg_color = Color(0.05, 0.2, 0.35, 0.9)
		pressed_style.set_border_width_all(2)
		pressed_style.border_color = Color(0.2, 0.4, 0.6)
		pressed_style.set_corner_radius_all(8)
		button.add_theme_stylebox_override("pressed", pressed_style)


func _input(event):
	if Input.is_action_pressed("esc") and GameState.isDocked:
		return
	if Input.is_action_just_pressed("esc"):
		if is_paused:
			resume_game()
		else:
			pause_game()

func pause_game():
	# Show pause menu and pause the game
	show()
	show_pause_elements(true)
	get_tree().paused = true
	is_paused = true
	pause_state_changed.emit(true)

func resume_game():
	# Hide pause menu and resume the game
	hide()
	save_menu.hide()
	settings_menu.hide()
	get_tree().paused = false
	is_paused = false
	pause_state_changed.emit(false)

func show_pause_elements(show_elements: bool):
	if show_elements:
		panel.show()
	else:
		panel.hide()

func _on_resume_button_pressed():
	resume_game()

func _on_save_button_pressed():
	# Hide pause menu elements and show save menu
	show_pause_elements(false)
	save_menu.show()
	save_menu.update_ui()

func _on_settings_button_pressed():
	# Hide pause menu elements and show settings menu
	show_pause_elements(false)
	settings_menu.show()

func _on_quit_button_pressed():
	# Quit game
	get_tree().quit()

	pass
