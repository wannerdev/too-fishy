extends PanelContainer

var john_texture = preload("res://textures/characters/john.png")
var blobfish_texture = preload("res://textures/icons/boss_icon.png")

func _ready() -> void:
	# Set initial size based on viewport
	_update_size()
	
	# Connect to window resize signal
	get_viewport().size_changed.connect(_update_size)
	
	$VBoxContainer/ContinueButton.pressed.connect(func():
		if Boss.boss_dialog_index < len(Strings.boss_dialog_lines[Boss.boss_dialog_section]) - 1:
			Boss.boss_dialog_index += 1
		else:
			if Boss.boss_dialog_section == Boss.BossDialogSections.BOSS_KILLS_FRIEND:
				GameState.health = 0
			Boss.boss_dialog_displayed = false
			Boss.boss_dialog_index = 0
			GameState.paused = false
			get_tree().paused = false
	)

func _update_size() -> void:
	var viewport_size = get_viewport().get_visible_rect().size
	var target_width = min(viewport_size.x * 0.7, 700) # 70% of viewport width, max 700
	var target_height = min(viewport_size.y * 0.4, 200) # 40% of viewport height, max 80
	
	custom_minimum_size = Vector2(target_width, target_height)
	$VBoxContainer/MessageLabel.custom_minimum_size = Vector2(target_width * 0.9, target_height * 0.7)
	
	# Set a fixed, appropriate size for the profile picture
	var profile_size = min(target_height * 0.55, 200) # Limit profile picture size
	var profile_picture = $VBoxContainer/HeaderContainer/ProfilePicture
	profile_picture.custom_minimum_size = Vector2(profile_size, profile_size)
	profile_picture.expand = true
	profile_picture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED

func _process(_delta: float) -> void:
	if Boss.boss_dialog_displayed:
		var dialog_text = Strings.boss_dialog_lines[Boss.boss_dialog_section][Boss.boss_dialog_index]
		$VBoxContainer/MessageLabel.bbcode_text = dialog_text
		$VBoxContainer/HeaderContainer/NameLabel.text = Boss.boss_dialog_from[Boss.boss_dialog_section]
		
		# Show/hide profile picture based on sender
		var profile_picture = $VBoxContainer/HeaderContainer/ProfilePicture
		var sender = Boss.boss_dialog_from[Boss.boss_dialog_section]
		if sender == "John":
			profile_picture.texture = john_texture
			profile_picture.visible = true
		elif sender == "Blobfish" or sender == "???":
			profile_picture.texture = blobfish_texture
			profile_picture.visible = true
		else:
			profile_picture.visible = false
		
		visible = true
	else:
		visible = false
