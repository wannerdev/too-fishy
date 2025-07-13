extends Control

var ring_radius: float = 8.0
var ring_thickness: float = 4.0
var progress: float = 1.0  # 0.0 to 1.0, 1.0 means ready
var item_name: String = ""
var icon_texture: Texture2D
var is_available: bool = true

# Colors from HUD
const COLOR_COOLDOWN_READY = Color(0, 0.36, 0.83, 0.8) # Blue
const COLOR_COOLDOWN_ACTIVE = Color(0.918, 0.525, 0.212, 0.82) # Orange
const COLOR_BACKGROUND = Color(0.05, 0.05, 0.05, 0.3) # Dark background

func _ready():
	custom_minimum_size = Vector2(ring_radius * 2 + 10, ring_radius * 2 + 10)
	size = custom_minimum_size

func setup(item_name_param: String):
	item_name = item_name_param
	# For now, use text fallbacks since specific icons don't exist
	icon_texture = null

func update_cooldown(progress_value: float, available: bool = true):
	progress = clamp(progress_value, 0.0, 1.0)
	is_available = available
	queue_redraw()

func _draw():
	if not is_available:
		return
	
	var center = size / 2
	var full_circle = 2 * PI
	
	# Draw background ring
	draw_arc(center, ring_radius, 0, full_circle, 64, COLOR_BACKGROUND, ring_thickness + 2)
	
	# Draw progress ring
	var progress_angle = progress * full_circle
	var ring_color = COLOR_COOLDOWN_READY if progress >= 1.0 else COLOR_COOLDOWN_ACTIVE
	
	if progress > 0:
		# Draw from top (-PI/2) clockwise
		draw_arc(center, ring_radius, -PI/2, -PI/2 + progress_angle, 64, ring_color, ring_thickness)
	
	# Draw icon in the center if available
	if icon_texture:
		var icon_size = Vector2(16, 16)
		var icon_rect = Rect2(center - icon_size / 2, icon_size)
		draw_texture_rect(icon_texture, icon_rect, false)
	else:
		# Draw text as fallback
		var font = ThemeDB.fallback_font
		var text_size = font.get_string_size(item_name.to_upper(), HORIZONTAL_ALIGNMENT_CENTER, -1, 12)
		var text_pos = center - text_size / 2
		#draw_string(font, text_pos, item_name.to_upper(), HORIZONTAL_ALIGNMENT_CENTER, -1, 12, Color.WHITE) 