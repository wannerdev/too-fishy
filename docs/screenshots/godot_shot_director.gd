extends Node
# Screenshot director for the Godot original, mirroring unity/Assets/Scripts/Core/ScreenshotDirector.cs.
# Dev tool only: add as autoload in a throw-away checkout and start with `-- <output dir>`.

var out_dir := ""
var n := 0

func _ready():
	var args = OS.get_cmdline_user_args()
	if args.size() == 0:
		queue_free()
		return
	out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(out_dir)
	call_deferred("run")

func wait(s: float):
	await get_tree().create_timer(s).timeout

func shot(label: String):
	await RenderingServer.frame_post_draw
	var img = get_viewport().get_texture().get_image()
	n += 1
	img.save_jpg("%s/%02d_%s.jpg" % [out_dir, n, label], 0.88)
	log_line("shot " + label + " " + str(img.get_size()))

func log_line(m: String):
	var f = FileAccess.open(out_dir + "/director.log", FileAccess.READ_WRITE if FileAccess.file_exists(out_dir + "/director.log") else FileAccess.WRITE)
	f.seek_end()
	var p = GameState.player_node.position if GameState.player_node else Vector3.ZERO
	f.store_line("%.1fs depth=%d pos=%s docked=%s %s" % [Time.get_ticks_msec() / 1000.0, GameState.depth, str(p), str(GameState.isDocked), m])
	f.close()

func teleport(x: float, y: float):
	var p = GameState.player_node
	p.position = Vector3(x, y, p.position.z)

func skip_dialogs():
	for i in range(12):
		Boss.boss_dialog_section = 999
		Boss.boss_dialog_displayed = false
		Boss.boss_dialog_index = 0
		GameState.paused = false
		get_tree().paused = false
		await wait(0.2)

func skip_intro():
	# copy of cheats.gd skip_intro_mission()
	if not GameState.is_intro():
		return
	var level_node = get_node_or_null("/root/Node3D/Level")
	var current_pos = GameState.player_node.position
	GameState.player_node.position = Vector3(-8, 0, 0.33)
	GameState.setDepth(0)
	GameState.complete_intro_mission(current_pos)
	if level_node and level_node.has_method("remove_intro_mission_elements"):
		level_node.remove_intro_mission_elements()
		level_node.lastSpawned = -35
	if Boss.has_method("hide_boss_health_bar"):
		Boss.hide_boss_health_bar()
	GameState.player_node.switch_to_normal_submarine()
	GameState.paused = false
	get_tree().paused = false
	GameState.death_screen = false
	GameState.isDocked = false

func nearest_fish(max_d: float):
	var best = null
	var bd = max_d
	for f in get_tree().get_nodes_in_group("fishes"):
		var d = f.global_position.distance_to(GameState.player_node.global_position)
		if d < bd:
			bd = d
			best = f
	return best

func fire_at(world_pos: Vector3):
	var cam = get_viewport().get_camera_3d()
	var sp = cam.unproject_position(world_pos)
	get_viewport().warp_mouse(sp)
	await wait(0.05)
	Input.action_press("throw")
	await wait(0.1)
	Input.action_release("throw")

func dump_rocks():
	for n in get_tree().root.find_children("*", "MeshInstance3D", true, false):
		if n.mesh == null or n.get_aabb().size == Vector3.ZERO:
			continue
		var rp = n.mesh.resource_path
		if rp.contains("gestein") or rp.contains("vains"):
			var a = n.global_transform * n.get_aabb()
			var c = a.get_center()
			if c.y > -70.0:
				log_line("ROCK %s mesh=%s center=(%.2f,%.2f,%.2f) size=(%.2f,%.2f,%.2f)" % [str(n.get_path()), rp.get_file(), c.x, c.y, c.z, a.size.x, a.size.y, a.size.z])
func run():
	while GameState.player_node == null or get_node_or_null("/root/Node3D/Level") == null:
		await wait(0.1)
	await wait(2.5)
	if OS.get_cmdline_user_args().size() > 1:
		# dockmode: only the surface/dock views, then quit
		await skip_dialogs()
		skip_intro()
		await wait(1.5)
		await skip_dialogs()
		GameState.god_mode = true
		dump_rocks()
		for pos in [[-3.0, 0.5, "dock"], [-8.0, 0.5, "dock_left"], [-14.0, 0.5, "dock_far_left"], [-3.0, -3.0, "dock_below"]]:
			teleport(pos[0], pos[1])
			await wait(3.0)
			await shot(pos[2])
		get_tree().quit()
		return
	await shot("intro_first_dialog")
	await skip_dialogs()
	await wait(2.0)
	await shot("intro_friend_sub_450m")
	skip_intro()
	await wait(1.5)
	await skip_dialogs()
	GameState.god_mode = true
	GameState.money = 2000
	teleport(-3.0, 0.5)
	await wait(3.0)
	await shot("dock_shop_opens")
	await wait(0.5)
	for key in GameState.upgrades:
		GameState.upgrades[key] = GameState.maxUpgrades[key]
	await wait(1.0)
	teleport(-8.0, -12.0)
	await wait(3.0)
	await shot("diving_shallow")
	teleport(-8.0, -45.0)
	await wait(4.0)
	await shot("diving_deep")
	for attempt in range(8):
		var fish = nearest_fish(14.0)
		if fish == null:
			await wait(2.0)
			continue
		var before = GameState.inventory.items.size()
		await fire_at(fish.global_position)
		await wait(0.2)
		if attempt == 0:
			await shot("catching_harpoon_fired")
		await wait(0.6)
		var after = GameState.inventory.items.size()
		if after > before:
			await shot("catching_catch_popup")
			break
		await wait(1.1)
	await wait(1.0)
	await shot("hud_cargo")
	teleport(-8.0, -146.5)
	await wait(4.0)
	await shot("barrier_crates")
	for entry in [[-135.0, "stage_deep_135m"], [-235.0, "stage_deeper_235m"], [-335.0, "stage_superdeep_335m"], [-435.0, "stage_hot_435m"], [-485.0, "stage_lava_485m"]]:
		teleport(-8.0, entry[0])
		await wait(4.5)
		await shot(entry[1])
	teleport(-8.0, -520.0)
	await wait(5.0)
	await shot("boss_dialog")
	await skip_dialogs()
	await wait(3.0)
	var boss = Boss.boss_node
	if boss:
		log_line("boss at " + str(boss.global_position))
		teleport(-8.0, boss.global_position.y - 5.0)
		await wait(1.5)
	await shot("boss_approach")
	for k in range(8):
		boss = Boss.boss_node
		if boss:
			await fire_at(boss.global_position)
		await wait(1.2)
		await shot("boss_fight_%d" % (k + 1))
		await skip_dialogs()
	log_line("done")
	get_tree().quit()