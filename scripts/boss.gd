extends Node
var boss_spawned = false
var boss_spawn_height = 500
var boss_node: Node3D = null
var boss_defeated_permanently = false

var boss_max_health = 100
var boss_health = 100

signal boss_health_changed(health)
signal boss_spawned_signal(max_health)
signal boss_defeated_signal

enum BossDialogSections {TUTORIAL1, TUTORIAL2, TUTORIAL3, TUTORIAL4, RESCUE_CALL, BOSS_INTRO, BOSS_KILLS_FRIEND, FRIEND_RESCUED, WIN, BOSS_DEFEATED}

var boss_dialog_from = {
	BossDialogSections.TUTORIAL1: "John",
	BossDialogSections.TUTORIAL2: "John",
	BossDialogSections.TUTORIAL3: "John",
	BossDialogSections.TUTORIAL4: "John",
	BossDialogSections.RESCUE_CALL: "John",
	BossDialogSections.BOSS_INTRO: "???",
	BossDialogSections.BOSS_KILLS_FRIEND: "Blobfish",
	BossDialogSections.FRIEND_RESCUED: "John",
	BossDialogSections.BOSS_DEFEATED: "John",
	BossDialogSections.WIN: "Too Fishy",
}

var dialog_depth_map = {
	BossDialogSections.TUTORIAL1: 0,
	BossDialogSections.TUTORIAL2: 25,
	BossDialogSections.TUTORIAL3: 50,
	BossDialogSections.TUTORIAL4: 300,
}

var boss_dialog_displayed = true
var boss_dialog_section = BossDialogSections.TUTORIAL1
var boss_dialog_index = 0

func setBossSpawned(boss: Node3D):
	boss_spawned = true
	boss_node = boss
	boss_health = boss_max_health
	emit_signal("boss_spawned_signal", boss_max_health)
	setDialogStage(BossDialogSections.BOSS_INTRO)

func take_damage(amount):
	boss_health -= amount
	emit_signal("boss_health_changed", boss_health)
	if boss_health <= 0:
		defeat_boss()

func defeat_boss():
	setDialogStage(BossDialogSections.BOSS_DEFEATED)
	emit_signal("boss_defeated_signal")
	if boss_node:
		boss_node.queue_free()
		boss_node = null
	boss_spawned = false
	boss_defeated_permanently = true

func setDialogStage(section: BossDialogSections):
	boss_dialog_section = section
	boss_dialog_index = 0
	boss_dialog_displayed = true
	GameState.paused = true
	get_tree().paused = true
	
func process_dialog_depth():
	# Don't process depth-based dialog after boss fight has started or concluded
	if boss_dialog_section >= BossDialogSections.BOSS_INTRO:
		return
		
	for section in dialog_depth_map.keys():
		if GameState.maxDepthReached >= dialog_depth_map[section]:
			if boss_dialog_section < section:
				setDialogStage(section)
