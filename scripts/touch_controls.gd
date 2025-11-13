extends Control

const TAU := PI * 2.0

const COLOR_PRIMARY := Color("#2e6fce") # main action blue
const COLOR_PRIMARY_LIGHT := Color("#3e84f5")
const COLOR_PRIMARY_DARK := Color("#2459a8")
const COLOR_ACCENT := Color("#54a3ff")
const COLOR_ACCENT_LIGHT := Color("#6bb2ff")

@export var joystick_radius := 140.0
@export var joystick_handle_radius := 52.0
@export var joystick_deadzone := 0.12
@export var action_deadzone := 0.1
@export var screen_margin := 24.0
@export var button_spacing := 18.0
@export var action_button_size := Vector2(180, 180)
@export var ability_button_size := Vector2(150, 150)
@export var utility_button_size := Vector2(140, 140)
@export var hold_threshold_default := 0.6
@export var show_in_editor := true
@export var force_enable := false

signal joystick_input(direction: Vector2)
signal shoot_pressed() # legacy signal for compatibility

var joystick_active := false
var joystick_origin := Vector2.ZERO
var joystick_position := Vector2.ZERO
var joystick_direction := Vector2.ZERO
var joystick_touch_index := -1
var joystick_rest_position := Vector2.ZERO

var screen_size := Vector2.ZERO

var buttons: Dictionary = {}
const ACTION_ORDER := ["harpoon", "shoot", "pickaxe"]
const ABILITY_ORDER := ["buoy", "drone", "save"]
const UTILITY_ORDER := ["inventory", "pause"]

func _ready():
	var should_show := _should_show_controls()
	visible = should_show
	set_process(should_show)
	set_process_input(should_show)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	top_level = true
	focus_mode = Control.FOCUS_NONE
	z_index = 128
	set_anchors_preset(Control.PRESET_FULL_RECT)
	if not should_show:
		return

	_initialize_buttons()
	_update_screen_size()
	_update_layout()
	if get_viewport():
		get_viewport().size_changed.connect(_on_viewport_size_changed)

func _should_show_controls() -> bool:
	if force_enable:
		return true
	if OS.has_feature("mobile") or OS.has_feature("web"):
		return true
	if Engine.is_editor_hint() and show_in_editor:
		return true
	return false

func _initialize_buttons() -> void:
	buttons.clear()
	buttons["harpoon"] = _make_button("throw", "action", "Harpoon", action_button_size, COLOR_PRIMARY_LIGHT)
	buttons["shoot"] = _make_button("shoot", "action", "Gun", action_button_size * 0.9, COLOR_PRIMARY, GameState.Upgrade.AK47)
	buttons["pickaxe"] = _make_button("swing_pickaxe", "action", "Pick", action_button_size * 0.85, COLOR_PRIMARY_DARK, GameState.Upgrade.PICKAXE_UNLOCKED, 0.0, true)

	buttons["buoy"] = _make_button("upgrade_surface_buoy", "ability", "Buoy", ability_button_size, COLOR_ACCENT, GameState.Upgrade.SURFACE_BUOY, 0.0, true)
	buttons["drone"] = _make_button("upgrade_drone_selling", "ability", "Drone", ability_button_size, COLOR_ACCENT_LIGHT, GameState.Upgrade.DRONE_SELLING, hold_threshold_default, true)
	buttons["save"] = _make_button("inventory_save", "ability", "Save", ability_button_size, COLOR_PRIMARY_LIGHT, GameState.Upgrade.INVENTORY_SAVE, hold_threshold_default, true)

	buttons["inventory"] = _make_button("inv_toggle", "utility", "Inventory", utility_button_size, COLOR_PRIMARY, -1, 0.0, true)
	buttons["pause"] = _make_button("esc", "utility", "Pause", utility_button_size * Vector2(0.85, 0.85), COLOR_PRIMARY_DARK, -1, 0.0, true)

func _make_button(action: String, group: String, label: String, size: Vector2, color: Color, requires_upgrade: int = -1, hold_threshold: float = 0.0, auto_release: bool = false) -> Dictionary:
	return {
		"action": action,
		"group": group,
		"label": label,
		"size": size,
		"color": color,
		"requires_upgrade": requires_upgrade,
		"hold_threshold": hold_threshold,
		"auto_release": auto_release,
		"rect": Rect2(),
		"touch_index": -1,
		"pressed": false,
		"hold_elapsed": 0.0,
		"hold_triggered": false,
		"active": false,
		"visible": true
	}

func _on_viewport_size_changed() -> void:
	_update_screen_size()
	_update_layout()

func _update_screen_size() -> void:
	var new_size := _get_viewport_size()
	if new_size != Vector2.ZERO:
		screen_size = new_size

func _get_viewport_size() -> Vector2:
	if get_viewport():
		return get_viewport().get_visible_rect().size
	if get_window():
		return get_window().size
	return size

func _update_layout() -> void:
	if screen_size == Vector2.ZERO:
		_update_screen_size()
	joystick_rest_position = Vector2(screen_margin + joystick_radius, screen_size.y - screen_margin - joystick_radius)
	if not joystick_active:
		joystick_origin = joystick_rest_position
		joystick_position = joystick_rest_position

	var action_column_x := screen_size.x - screen_margin - action_button_size.x
	var action_bottom := screen_size.y - screen_margin
	for name in ACTION_ORDER:
		if not buttons.has(name):
			continue
		var data: Dictionary = buttons[name]
		if not data.visible:
			data.rect = Rect2()
			buttons[name] = data
			continue
		var size: Vector2 = data.size
		var pos := Vector2(
			action_column_x + (action_button_size.x - size.x) * 0.5,
			action_bottom - size.y
		)
		data.rect = Rect2(pos, size)
		action_bottom = pos.y - button_spacing
		buttons[name] = data

	var ability_column_x := action_column_x - ability_button_size.x - button_spacing
	var ability_bottom := screen_size.y - screen_margin
	for name in ABILITY_ORDER:
		if not buttons.has(name):
			continue
		var data: Dictionary = buttons[name]
		if not data.visible:
			data.rect = Rect2()
			buttons[name] = data
			continue
		var size: Vector2 = data.size
		ability_bottom -= size.y
		var pos := Vector2(
			ability_column_x + (ability_button_size.x - size.x) * 0.5,
			ability_bottom
		)
		data.rect = Rect2(pos, size)
		ability_bottom -= button_spacing
		buttons[name] = data

	if buttons.has("inventory"):
		var data_inv: Dictionary = buttons["inventory"]
		if data_inv.visible:
			var size_inv: Vector2 = data_inv.size
			var pos_inv := Vector2(
				action_column_x + (action_button_size.x - size_inv.x) * 0.5,
				max(screen_margin, action_bottom - size_inv.y)
			)
			data_inv.rect = Rect2(pos_inv, size_inv)
		else:
			data_inv.rect = Rect2()
		buttons["inventory"] = data_inv

	if buttons.has("pause"):
		var data_pause: Dictionary = buttons["pause"]
		if data_pause.visible:
			var size_pause: Vector2 = data_pause.size
			var pos_pause := Vector2(
				screen_size.x - screen_margin - size_pause.x,
				screen_margin
			)
			data_pause.rect = Rect2(pos_pause, size_pause)
		else:
			data_pause.rect = Rect2()
		buttons["pause"] = data_pause

	queue_redraw()

func _input(event):
	if event is InputEventScreenTouch:
		_handle_screen_touch(event)
	elif event is InputEventScreenDrag:
		_handle_screen_drag(event)

func _handle_screen_touch(event: InputEventScreenTouch) -> void:
	if event.pressed:
		if _process_button_press(event):
			return
		if joystick_touch_index == -1 and _joystick_allowed(event.position):
			joystick_touch_index = event.index
			joystick_active = true
			joystick_origin = event.position
			joystick_position = event.position
			joystick_direction = Vector2.ZERO
			emit_signal("joystick_input", joystick_direction)
			_update_action_strengths()
			queue_redraw()
	else:
		_process_button_release(event.index)
		if event.index == joystick_touch_index:
			joystick_touch_index = -1
			joystick_active = false
			joystick_direction = Vector2.ZERO
			joystick_origin = joystick_rest_position
			joystick_position = joystick_rest_position
			emit_signal("joystick_input", Vector2.ZERO)
			_update_action_strengths()
			queue_redraw()

func _handle_screen_drag(event: InputEventScreenDrag) -> void:
	if event.index != joystick_touch_index or not joystick_active:
		return
	joystick_position = event.position
	var direction := joystick_position - joystick_origin
	var distance := direction.length()
	if distance > joystick_radius and distance > 0.0:
		direction = direction / distance * joystick_radius
		joystick_position = joystick_origin + direction
	var normalized := direction / joystick_radius
	var adjusted := Vector2(normalized.x, -normalized.y)
	if adjusted.length() < joystick_deadzone:
		adjusted = Vector2.ZERO
	joystick_direction = adjusted
	emit_signal("joystick_input", joystick_direction)
	_update_action_strengths()
	queue_redraw()

func _joystick_allowed(position: Vector2) -> bool:
	return position.x <= screen_size.x * 0.55

func _process(delta: float) -> void:
	if not visible:
		return
	var new_size := _get_viewport_size()
	if new_size != Vector2.ZERO and new_size != screen_size:
		screen_size = new_size
		_update_layout()

	var layout_dirty := _refresh_button_visibility()
	for name in buttons.keys():
		var data: Dictionary = buttons[name]
		if data.pressed:
			data.hold_elapsed += delta
			if data.hold_threshold > 0.0 and not data.hold_triggered and data.hold_elapsed >= data.hold_threshold:
				_trigger_action(name)
			buttons[name] = data
	if layout_dirty:
		_update_layout()
	queue_redraw()

func _refresh_button_visibility() -> bool:
	var layout_dirty := false
	for name in buttons.keys():
		var data: Dictionary = buttons[name]
		var available := _is_button_available(name)
		if data.visible != available:
			if data.active:
				Input.action_release(data.action)
			data.visible = available
			data.pressed = false
			data.active = false
			data.touch_index = -1
			data.hold_elapsed = 0.0
			data.hold_triggered = false
			buttons[name] = data
			layout_dirty = true
	return layout_dirty

func _is_button_available(name: String) -> bool:
	if not buttons.has(name):
		return false
	var data: Dictionary = buttons[name]
	if name == "harpoon":
		if GameState.is_intro():
			return false
		if GameState.upgrades.has(GameState.Upgrade.HARPOON) and GameState.upgrades[GameState.Upgrade.HARPOON] > 0:
			return false
	if name == "drone" and GameState.is_intro():
		return false
	if data.requires_upgrade != -1:
		if not GameState.upgrades.has(data.requires_upgrade):
			return false
		if GameState.upgrades[data.requires_upgrade] <= 0:
			return false
	return true

func _process_button_press(event: InputEventScreenTouch) -> bool:
	for name in buttons.keys():
		var data: Dictionary = buttons[name]
		if not data.visible or data.rect.size == Vector2.ZERO:
			continue
		if data.rect.has_point(event.position):
			data.touch_index = event.index
			data.pressed = true
			data.hold_elapsed = 0.0
			data.hold_triggered = false
			buttons[name] = data
			if data.hold_threshold <= 0.0:
				_trigger_action(name)
			return true
	return false

func _process_button_release(touch_index: int) -> void:
	for name in buttons.keys():
		var data: Dictionary = buttons[name]
		if data.touch_index == touch_index:
			_on_button_release(name)
			return

func _trigger_action(name: String) -> void:
	if not buttons.has(name):
		return
	var data: Dictionary = buttons[name]
	if data.hold_triggered and not data.auto_release:
		return
	Input.action_press(data.action)
	if data.auto_release:
		Input.action_release(data.action)
		data.active = false
	else:
		data.active = true
	if name == "harpoon":
		emit_signal("shoot_pressed")
	data.hold_triggered = true
	buttons[name] = data

func _on_button_release(name: String) -> void:
	if not buttons.has(name):
		return
	var data: Dictionary = buttons[name]
	if data.active:
		Input.action_release(data.action)
	data.active = false
	data.pressed = false
	data.touch_index = -1
	data.hold_elapsed = 0.0
	data.hold_triggered = false
	buttons[name] = data

func _update_action_strengths() -> void:
	Input.action_release("move_left")
	Input.action_release("move_right")
	Input.action_release("move_up")
	Input.action_release("move_down")

	var x := joystick_direction.x
	if x > action_deadzone:
		Input.action_press("move_right", clamp(x, 0.0, 1.0))
	elif x < -action_deadzone:
		Input.action_press("move_left", clamp(-x, 0.0, 1.0))

	var y := joystick_direction.y
	if y > action_deadzone:
		Input.action_press("move_up", clamp(y, 0.0, 1.0))
	elif y < -action_deadzone:
		Input.action_press("move_down", clamp(-y, 0.0, 1.0))

func _draw():
	if not visible:
		return

	var base_color := Color(0.2, 0.2, 0.2, 0.35)
	var handle_color := Color(0.95, 0.95, 0.95, 0.8)
	draw_circle(joystick_rest_position, joystick_radius, base_color)
	if joystick_active:
		draw_circle(joystick_origin, joystick_radius, base_color)
		draw_circle(joystick_position, joystick_handle_radius, handle_color)
	else:
		draw_circle(joystick_rest_position, joystick_handle_radius, handle_color)

	var font := get_theme_default_font()
	var font_size := get_theme_default_font_size()
	for name in buttons.keys():
		var data: Dictionary = buttons[name]
		if not data.visible or data.rect.size == Vector2.ZERO:
			continue
		var center: Vector2 = data.rect.position + data.rect.size * 0.5
		var radius: float = min(data.rect.size.x, data.rect.size.y) * 0.5
		var fill_color: Color = data.color
		if data.pressed:
			fill_color = fill_color.lightened(0.12)
		draw_circle(center, radius, fill_color)
		draw_arc(center, radius, 0.0, TAU, 48, fill_color.darkened(0.25), 4.5)
		if font:
			var text: String = data.label
			var text_size: Vector2 = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size)
			var text_pos: Vector2 = center - text_size * 0.5 + Vector2(0, text_size.y * 0.35)
			draw_string(font, text_pos, text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size, Color(0.1, 0.1, 0.1, 1.0))
