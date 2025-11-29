extends Control

signal mode_selected(mode: GameState.GameMode)

var story_button: Button
var infinite_button: Button
var title_label: Label
var description_label: Label

func _ready():
	# Create main container
	var main_container = VBoxContainer.new()
	main_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	main_container.add_theme_constant_override("separation", 30)
	add_child(main_container)
	
	# Create background panel
	var background = PanelContainer.new()
	background.set_anchors_preset(Control.PRESET_FULL_RECT)
	var panel_style = StyleBoxFlat.new()
	panel_style.bg_color = Color(0.05, 0.1, 0.2, 0.95)
	panel_style.set_border_width_all(4)
	panel_style.border_color = Color(0.3, 0.5, 0.8)
	panel_style.set_corner_radius_all(15)
	background.add_theme_stylebox_override("panel", panel_style)
	main_container.add_child(background)
	
	var margin = MarginContainer.new()
	margin.add_theme_constant_override("margin_left", 50)
	margin.add_theme_constant_override("margin_right", 50)
	margin.add_theme_constant_override("margin_top", 50)
	margin.add_theme_constant_override("margin_bottom", 50)
	background.add_child(margin)
	
	var content = VBoxContainer.new()
	content.add_theme_constant_override("separation", 40)
	margin.add_child(content)
	
	# Title
	title_label = Label.new()
	title_label.text = "SELECT GAME MODE"
	title_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title_label.add_theme_font_size_override("font_size", 36)
	title_label.add_theme_color_override("font_color", Color(1, 1, 1))
	content.add_child(title_label)
	
	# Description
	description_label = Label.new()
	description_label.text = "Choose how you want to play"
	description_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	description_label.add_theme_font_size_override("font_size", 20)
	description_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.9))
	content.add_child(description_label)
	
	# Button container
	var button_container = HBoxContainer.new()
	button_container.add_theme_constant_override("separation", 30)
	button_container.alignment = BoxContainer.ALIGNMENT_CENTER
	content.add_child(button_container)
	
	# Story Mode Button
	story_button = create_mode_button("STORY MODE", "Complete the main story and rescue your friend from the blobfish boss.")
	story_button.pressed.connect(func(): on_mode_selected(GameState.GameMode.NORMAL))
	button_container.add_child(story_button)
	
	# Infinite Mode Button
	infinite_button = create_mode_button("INFINITE MODE", "Continue diving past the boss! Fish values scale down the deeper you go.")
	infinite_button.pressed.connect(func(): on_mode_selected(GameState.GameMode.INFINITE))
	button_container.add_child(infinite_button)

func create_mode_button(text: String, description: String) -> Button:
	var button_container = VBoxContainer.new()
	button_container.custom_minimum_size = Vector2(300, 200)
	button_container.add_theme_constant_override("separation", 15)
	
	var button = Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(300, 80)
	button.add_theme_font_size_override("font_size", 24)
	
	# Style the button
	var button_style = StyleBoxFlat.new()
	button_style.bg_color = Color(0.2, 0.4, 0.7)
	button_style.set_border_width_all(3)
	button_style.border_color = Color(0.4, 0.6, 0.9)
	button_style.set_corner_radius_all(10)
	button.add_theme_stylebox_override("normal", button_style)
	
	var button_hover_style = StyleBoxFlat.new()
	button_hover_style.bg_color = Color(0.3, 0.5, 0.8)
	button_hover_style.set_border_width_all(3)
	button_hover_style.border_color = Color(0.5, 0.7, 1.0)
	button_hover_style.set_corner_radius_all(10)
	button.add_theme_stylebox_override("hover", button_hover_style)
	button.add_theme_stylebox_override("pressed", button_hover_style)
	
	button.add_theme_color_override("font_color", Color(1, 1, 1))
	button_container.add_child(button)
	
	var desc_label = Label.new()
	desc_label.text = description
	desc_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc_label.add_theme_font_size_override("font_size", 16)
	desc_label.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	button_container.add_child(desc_label)
	
	# Wrap in a container to return the button but keep the structure
	var wrapper = Control.new()
	wrapper.add_child(button_container)
	button_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	
	# Store reference to button for easy access
	button_container.set_meta("button", button)
	
	return button

func on_mode_selected(mode: GameState.GameMode):
	emit_signal("mode_selected", mode)
	visible = false
	
	if mode == GameState.GameMode.NORMAL:
		GameState.start_normal_mode()
	elif mode == GameState.GameMode.INFINITE:
		GameState.start_infinite_mode()

func show_menu():
	visible = true
	GameState.paused = true
	get_tree().paused = true

func hide_menu():
	visible = false
	GameState.paused = false
	get_tree().paused = false
