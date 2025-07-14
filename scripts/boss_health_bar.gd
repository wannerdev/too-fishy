extends Control

@onready var progress_bar = $ProgressBar
@onready var label = $ProgressBar/Label

func _ready():
	visible = false
	Boss.boss_health_changed.connect(on_boss_health_changed)
	Boss.boss_spawned_signal.connect(on_boss_spawned)
	Boss.boss_defeated_signal.connect(on_boss_defeated)
	Boss.boss_health_bar_hidden.connect(on_boss_health_bar_hidden)

func on_boss_spawned(max_health):
	progress_bar.max_value = max_health
	progress_bar.value = max_health
	label.text = "Blobfish"
	visible = true

func on_boss_health_changed(health):
	progress_bar.value = health

func on_boss_defeated():
	visible = false

func on_boss_health_bar_hidden():
	# print("Boss health bar hidden due to intro mission completion")
	visible = false
