extends Node

var stageNames = {
	GameState.Stage.SURFACE: "Shallow Waters",
	GameState.Stage.DEEP: "Medium Waters",
	GameState.Stage.DEEPER: "Deep Waters",
	GameState.Stage.SUPERDEEP: "Dark Deep Waters",
	GameState.Stage.HOT: "Hot-Zone",
	GameState.Stage.LAVA: "Lava",
	GameState.Stage.VOID: "THE VOID"
}

var upgradeNames = {
	GameState.Upgrade.CARGO_SIZE: "Increased Cargo Space",
	GameState.Upgrade.DEPTH_RESISTANCE: "Depth Resistance",
	GameState.Upgrade.PICKAXE_UNLOCKED: "Unlock Pickaxe",
	GameState.Upgrade.VERT_SPEED: "Agility",
	GameState.Upgrade.HOR_SPEED: "Thrust",
	GameState.Upgrade.LAMP_UNLOCKED: "Unlock Lamp",
	GameState.Upgrade.AK47: "Unlock Gun",
	GameState.Upgrade.DUALAK47: "Unlock 2nd Gun",
	GameState.Upgrade.HARPOON: "Upgrade Harpoon",
	GameState.Upgrade.HARPOON_ROTATION: "Rotatable Harpoon",
	GameState.Upgrade.INVENTORY_MANAGEMENT: "Smart Inventory",
	GameState.Upgrade.SURFACE_BUOY: "Emergency Buoy",
	GameState.Upgrade.INVENTORY_SAVE: "Inventory Insurance",
	GameState.Upgrade.DRONE_SELLING: "Remote Selling Drone",
}

var boss_dialog_lines = {
	Boss.BossDialogSections.TUTORIAL1: [
	"[color=#E0E0E0]Hey man! Is your sub ready yet?\nYes? Awesome! Go shoot some fish with that spear of yours!\n\nYou can move your submarine by using[/color] [color=#2C3E50]W A S D[/color][color=#E0E0E0], or the [/color][color=#2C3E50]Arrow Keys[/color][color=#E0E0E0]\nTo catch the fish, throw a spear with a [/color][color=#2C3E50]'Left-CLICK'[/color][color=#E0E0E0]\n\nWatch your [/color][color=#f21820ae]pressure[/color][color=#E0E0E0] or else you get damage and [/color][color=#1B5E20]Health[/color][color=#E0E0E0] levels and return to the surface when needed!\nRemember, you can swim near the dock to [/color][color=#1B5E20]upgrade your sub[/color][color=#E0E0E0] and [/color][color=#1B5E20]sell fish[/color][color=#E0E0E0].[/color]"
	],
	Boss.BossDialogSections.TUTORIAL2: ["[color=#E0E0E0]There are some crazy fish down here! I was able to sell some of them for a lot of [/color][color=#1B5E20]money[/color][color=#E0E0E0]![/color]"],
	Boss.BossDialogSections.TUTORIAL3: ["[color=#E0E0E0]I found that the [/color][color=#2C3E50]pickaxe[/color][color=#E0E0E0] is actually very useful!\nYou can break the barriers with them.\nIf you have it Press [/color][color=#2C3E50]'SPACE'[/color][color=#E0E0E0] to use it![/color]"],
	Boss.BossDialogSections.TUTORIAL4: ["[color=#E0E0E0]Its awesome down here, you gotta check it out!\nThe [/color][color=#f21820ae]blobfish[/color][color=#E0E0E0] is looking at me kinda wierd... You should be careful![/color]"],
	Boss.BossDialogSections.RESCUE_CALL: ["[color=#E0E0E0]Uhhhh- I did an oopsie, I think you need to come and get me :S[/color]"],
	Boss.BossDialogSections.BOSS_INTRO: ["[color=#E0E0E0]Hahaha, I got your friend, looser!\nHis mind is under my control now!\nYou better not do anything stupid or he dies![/color]"],
	Boss.BossDialogSections.BOSS_KILLS_FRIEND: ["[color=#E0E0E0]Ohhhh, that was a mistake![/color]", "[color=#E0E0E0]*Splash*[/color]", "[color=#E0E0E0]Your friend is dead now![/color]"],
	Boss.BossDialogSections.FRIEND_RESCUED: ["[color=#E0E0E0]Oh my god thank you! That [/color][color=#f21820ae]mind controlling blobfish[/color][color=#E0E0E0]!\nYou killed it![/color]", "[color=#E0E0E0]I thought I was a goner![/color]", "[color=#E0E0E0]I will follow you to the surface, I can't wait to get out of here![/color]"],
	Boss.BossDialogSections.WIN: ["[color=#E0E0E0]You rescued your friend from the evil mind controlling blobfish![/color]", "[color=#E0E0E0]Congratulations, you beat our game. Feel free to keep fishing, the first game idea was created in Ludum dare 57.[/color]"],
	Boss.BossDialogSections.BOSS_DEFEATED: ["[color=#E0E0E0]The mind-controlling blobfish has been killed![/color]", "[color=#E0E0E0]Thank you! Come get us, we will follow your lead![/color]"],
	Boss.BossDialogSections.POST_INTRO_RESCUE: ["[color=#E0E0E0]Noo the blobfish used some crazy mind control on you. You have to rescue your friend, but first you need to get a better submarine to be able to beat the blobfish.[/color]"],
	Boss.BossDialogSections.AK47_UNLOCKED: ["[color=#E0E0E0]Damn, that blobfish is no joke! [/color][color=#f21820ae]I need better weapons...[/color][color=#E0E0E0]\n\nThat's it! I heard about a [/color][color=#2C3E50]gun upgrade[/color][color=#E0E0E0] available at the dock. If I'm going to save my friend from that creature, I'll need some serious firepower.\n\nI should head back to the dock and check the [/color][color=#2C3E50]EQUIPMENT[/color][color=#E0E0E0] section for an [/color][color=#f21820ae]AK47[/color][color=#E0E0E0]. Time to show that blobfish who's boss![/color]"],
	Boss.BossDialogSections.DOCK_HINT: ["[color=#E0E0E0]Swim away if you are done shopping.[/color]"]
}

var upgradeDescriptions = {
							  GameState.Upgrade.CARGO_SIZE: "Increases the maximum weight your submarine can carry, allowing you to collect more fish before returning to dock.",
							  GameState.Upgrade.DEPTH_RESISTANCE: "Improves your submarine's hull integrity, allowing you to dive deeper without taking damage from pressure.",
							  GameState.Upgrade.PICKAXE_UNLOCKED: "Equips your submarine with a pickaxe that can break through barriers. Press SPACE to use.",
							  GameState.Upgrade.VERT_SPEED: "Increases your submarine's vertical speed, allowing you to ascend and descend more quickly.",
							  GameState.Upgrade.HOR_SPEED: "Increases your submarine's horizontal speed, allowing you to move left and right more quickly.",
							  GameState.Upgrade.LAMP_UNLOCKED: "Adds a powerful lamp to your submarine, improving visibility in deeper waters.",
							  GameState.Upgrade.AK47: "Equips your submarine with a gun that can be used to defend against aggressive fish.",
							  GameState.Upgrade.DUALAK47: "Adds a second gun to your submarine for increased firepower.",
							  GameState.Upgrade.HARPOON: "Upgrades your harpoon to pierce through multiple fish with a single shot.",
							  GameState.Upgrade.HARPOON_ROTATION: "Allows you to aim your harpoon in any direction using your mouse or touch controls.",
							  GameState.Upgrade.INVENTORY_MANAGEMENT: "Automatically replaces less valuable fish with more expensive ones when your inventory is full.",
							  GameState.Upgrade.SURFACE_BUOY: "Provides an emergency buoy that quickly returns you to the surface when you press B.",
							  GameState.Upgrade.INVENTORY_SAVE: "Insures your inventory, preventing loss of collected fish when you die.",
							  GameState.Upgrade.DRONE_SELLING: "Deploys a drone that sells your inventory remotely when you press Q, without returning to dock."
						  }
