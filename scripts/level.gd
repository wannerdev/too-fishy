extends Node3D

@export var section: PackedScene
@export var level_wrapper: Node3D
@export var player: CharacterBody3D
@export var boss: PackedScene
@export var boss_section: PackedScene

@export var sectionHeight: float
@export var preloadSectionsCount: int

var last_section_type: GameState.Stage = GameState.Stage.SURFACE

var lastSpawned = -35
var snappedDepth = 0
func _process(_delta: float) -> void:
	GameState.setDepth(int(player.position.y) * -1)
	snappedDepth = snapped(player.position.y, 1) * -1
	if player.position.y < (lastSpawned):
		spawnNewSection(lastSpawned - sectionHeight)
	if GameState.maxDepthReached > Boss.boss_spawn_height && Boss.boss_spawned == false && Boss.boss_defeated_permanently == false:
		var boss_spawn_loc = (GameState.maxDepthReached * -1) - 25
		spawnBoss(boss_spawn_loc)
	
	Boss.process_dialog_depth()
	
func spawnNewSection(mPosition: float):
	
	var newSection = section.instantiate()
	newSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	newSection.sectionType = GameState.depthStageMap[i]
	newSection.lastSectionType = last_section_type
	lastSpawned = mPosition
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1
	if mPosition <= -50:
		newSection.setDepth(mPosition * -1)
	add_child(newSection)
	last_section_type = GameState.depthStageMap[i]

func spawnBoss(mPosition: float):
	print("Spawn boss", mPosition)
	var spawned_boss = boss.instantiate()
	spawned_boss.position.y = mPosition
	spawned_boss.position.z = -0.33
	spawned_boss.position.x = -5
	spawned_boss.player = player
	add_child(spawned_boss)
	Boss.setBossSpawned(spawned_boss)
	
	mPosition = lastSpawned - sectionHeight
	var bossSection = boss_section.instantiate()
	bossSection.position.y = mPosition
	var i = snapped(-mPosition, 100)
	
	i = min(i, GameState.depthStageMap.keys()[len(GameState.depthStageMap.keys())-1])
	
	lastSpawned = mPosition + 50
	GameState.fishes_lower_boarder = lastSpawned - sectionHeight/2 - 1

	add_child(bossSection)
