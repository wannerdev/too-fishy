extends Node


enum FishType {
	FLAMY,
	GREENY,
	ANGLER,
	SMALLY,
	SPIKEY,
	BOSS_MINI
}

var fishConfigMap = {
	FishType.FLAMY: {
		weight_min = 1,
		weight_max = 10,
		price_weight_multiplier = 1,
		speed_min = 1,
		speed_max = 2.5,
		difficulty = 1,
		scene = preload("res://scenes/mobs/BasicFishA.tscn"),
		icon = preload("res://textures/icons/fish_a.png")
	},
	FishType.GREENY: {
		weight_min = 1,
		weight_max = 5,
		price_weight_multiplier = 1.2,
		speed_min = 2,
		speed_max = 5,
		difficulty = 1,
		scene = preload("res://scenes/mobs/BasicFishB.tscn"),
		icon = preload("res://textures/icons/fish_b.png")
	},
	FishType.ANGLER: {
		weight_min = 5,
		weight_max = 10,
		price_weight_multiplier = 3,
		speed_min = 0.1,
		speed_max = 1,
		difficulty = 5,
		scene = preload("res://scenes/mobs/AnglerFish.tscn"),
		icon = preload("res://textures/icons/angler_fish.png")
	},
	FishType.SMALLY: {
		weight_min = 5,
		weight_max = 10,
		price_weight_multiplier = 10,
		speed_min = 3,
		speed_max = 7,
		difficulty = 10,
		scene = preload("res://scenes/mobs/dummy_fish.tscn"),
		icon = preload("res://textures/icons/dummy_fish.png")
	},
	FishType.SPIKEY: {
		weight_min = 5,
		weight_max = 10,
		price_weight_multiplier = 3,
		speed_min = 0.5,
		speed_max = 1.5,
		difficulty = 5,
		scene = preload("res://scenes/mobs/spikey_fish.tscn"),
		icon = preload("res://textures/icons/spikey_fish.png")
	},
	FishType.BOSS_MINI: {
		# Mini-blobfish balancing pass: keep it rewarding, but faster and less tanky.
		weight_min = 3,
		weight_max = 8,
		price_weight_multiplier = 12,
		speed_min = 1.1,
		speed_max = 2.8,
		difficulty = 10,
		scene = preload("res://scenes/mobs/boss_mini_fish.tscn"),
		icon = preload("res://textures/icons/boss_icon.png")
	}
}

var fishSectionMap = {
	GameState.Stage.SURFACE: {
		max_fish_amount = 15,
		shiny_rate = .02,
		weight_multiplier = .8,
		spawnRates = {
			FishType.FLAMY: .9,
			FishType.GREENY: .1,
		}
	},
	GameState.Stage.DEEP: {
		max_fish_amount = 10,
		shiny_rate = .02,
		weight_multiplier = .9,
		spawnRates = {
			FishType.FLAMY: .7,
			FishType.GREENY: .3,
		}
	},
	GameState.Stage.DEEPER: {
		max_fish_amount = 10,
		shiny_rate = .04,
		weight_multiplier = 1,
		spawnRates = {
			FishType.FLAMY: .5,
			FishType.GREENY: .4,
			FishType.SPIKEY: .1
		}
	},
	GameState.Stage.SUPERDEEP: {
		max_fish_amount = 8,
		shiny_rate = .04,
		weight_multiplier = 1.1,
		spawnRates = {
			FishType.FLAMY: .2,
			FishType.GREENY: .43,
			FishType.ANGLER: .14,
			FishType.SMALLY: .03,
			FishType.SPIKEY: .2
		}
	},
	GameState.Stage.HOT: {
		max_fish_amount = 5,
		shiny_rate = .05,
		weight_multiplier = 1.15,
		spawnRates = {
			FishType.GREENY: .2,
			FishType.ANGLER: .5,
			FishType.SMALLY: .1,
			FishType.SPIKEY: .2
		}
	},
	GameState.Stage.LAVA: {
		max_fish_amount = 4,
		shiny_rate = .06,
		weight_multiplier = 1.15,
		spawnRates = {
			FishType.ANGLER: .5,
			FishType.SMALLY: .25,
			FishType.SPIKEY: .17,
			FishType.BOSS_MINI: .08
		}
	},
	GameState.Stage.VOID: {
		max_fish_amount = 1,
		shiny_rate = .08,
		weight_multiplier = 1.2,
		spawnRates = {
			FishType.SMALLY: .1,
			FishType.ANGLER: .1,
			FishType.FLAMY: .8,
		}
	}
}
