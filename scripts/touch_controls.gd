extends Control

@export var joystick_radius := 140.0
@export var joystick_handle_radius := 52.0
@export var shoot_button_size := Vector2(180, 180)
@export var screen_margin := 24.0
@export var joystick_deadzone := 0.12

var joystick_active := false
var joystick_origin := Vector2.ZERO
var joystick_position := Vector2.ZERO
var joystick_direction := Vector2.ZERO
var joystick_touch_index := -1

var shoot_button_rect := Rect2()
var shoot_button_pressed := false
var shoot_touch_index := -1

signal joystick_input(direction: Vector2)
signal shoot_pressed()

func _ready():
	var should_show := OS.has_feature("mobile") or OS.has_feature("web") or DisplayServer.is_touchscreen_mode_enabled()
	visible = should_show
	set_process(should_show)
	set_process_input(should_show)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_level = true
	focus_mode = Control.FOCUS_NONE
	z_index = 128
	set_anchors_preset(Control.PRESET_FULL_RECT)
	update_layout()
	if get_viewport():
		get_viewport().size_changed.connect(update_layout)

func update_layout():
	if not is_visible_in_tree():
		return
	var screen_size := get_viewport().get_visible_rect().size if get_viewport() else size
	shoot_button_rect.size = shoot_button_size
	shoot_button_rect.position = Vector2(
		screen_size.x - shoot_button_rect.size.x - screen_margin,
		screen_size.y - shoot_button_rect.size.y - screen_margin
	)
	queue_redraw()

func _input(event):
	if event is InputEventScreenTouch:
		_handle_screen_touch(event)
	elif event is InputEventScreenDrag:
		_handle_screen_drag(event)

func _handle_screen_touch(event: InputEventScreenTouch) -> void:
	if event.pressed:
		if shoot_touch_index == -1 and shoot_button_rect.has_point(event.position):
			shoot_touch_index = event.index
			shoot_button_pressed = true
			emit_signal("shoot_pressed")
			queue_redraw()
			return
		
		if joystick_touch_index == -1:
			joystick_touch_index = event.index
			joystick_active = true
			joystick_origin = event.position
			joystick_position = event.position
			joystick_direction = Vector2.ZERO
			emit_signal("joystick_input", joystick_direction)
			queue_redraw()
	else:
		if event.index == shoot_touch_index:
			shoot_touch_index = -1
			shoot_button_pressed = false
			queue_redraw()
		if event.index == joystick_touch_index:
			joystick_touch_index = -1
			joystick_active = false
			joystick_direction = Vector2.ZERO
			emit_signal("joystick_input", Vector2.ZERO)
			queue_redraw()

func _handle_screen_drag(event: InputEventScreenDrag) -> void:
	if event.index != joystick_touch_index or not joystick_active:
		return
	joystick_position = event.position
	var direction := joystick_position - joystick_origin
	var distance := direction.length()
	if distance > joystick_radius and distance > 0:
		direction = direction / distance * joystick_radius
		joystick_position = joystick_origin + direction
	var normalized := direction / joystick_radius
	if normalized.length() < joystick_deadzone:
		normalized = Vector2.ZERO
	joystick_direction = normalized
	emit_signal("joystick_input", joystick_direction)
	queue_redraw()

func _draw():
	if not visible:
		return
	if not (OS.has_feature("mobile") or OS.has_feature("web") or DisplayServer.is_touchscreen_mode_enabled()):
		return
	if joystick_active:
		draw_circle(joystick_origin, joystick_radius, Color(0.5, 0.5, 0.5, 0.35))
		draw_circle(joystick_position, joystick_handle_radius, Color(0.8, 0.8, 0.8, 0.55))
	var button_color := Color(1.0, 0.3, 0.3, 0.65)
	if shoot_button_pressed:
		button_color = Color(1.0, 0.5, 0.5, 0.8)
	draw_circle(shoot_button_rect.position + shoot_button_rect.size / 2.0, shoot_button_rect.size.x / 2.0, button_color)
	var center := shoot_button_rect.position + shoot_button_rect.size / 2.0
	var line_length := shoot_button_rect.size.x * 0.18
	draw_line(center - Vector2(line_length, 0), center + Vector2(line_length, 0), Color.WHITE, 5)
	draw_line(center - Vector2(0, line_length), center + Vector2(0, line_length), Color.WHITE, 5)
