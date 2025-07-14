extends Node

class_name AchievementSystem

# Signal for when an achievement is updated
signal achievement_updated

# Dictionary to store fish achievements
# Structure: fish_type_id (int) -> { caught: bool, shiny: bool, surface: bool }
var fish_achievements = {}

# Reference to GameState for signaling
var game_state = null

func _ready():
	# Initialize achievements for all fish types using integer IDs instead of name strings
	for type_id in range(FishesConfig.FishType.size()):
		fish_achievements[type_id] = {
			"caught": false,
			"shiny": false,
			"surface": false
		}

# Connect to GameState
func connect_to_gamestate(state):
	game_state = state
	# Connect to inventory updated signal to check for new fish
	if game_state:
		#print("Achievement System: Connected to GameState")
		game_state.connect("inventory_updated", Callable(self, "_on_inventory_updated"))
		# Initial check of inventory
		_on_inventory_updated()

# Called when inventory is updated
func _on_inventory_updated():
	# Skip achievements during intro mission
	if GameState.is_intro_mission_active():
		return
	
	#print("Achievement System: Inventory updated, checking for new fish")
	# Check for new fish catches
	for item in GameState.inventory.items:
		# Convert the string fish type ID to integer
		var fish_type_id = item.type
		
		# Ensure fish_type_id is an integer
		if typeof(fish_type_id) != TYPE_INT:
			#print("WARNING: Non-integer fish type ID: ", fish_type_id, ", converting to int")
			if str(fish_type_id).is_valid_int():
				fish_type_id = int(fish_type_id)
			else:
				# If it's a string like "FISH_A", try to find its enum value
				var enum_keys = FishesConfig.FishType.keys()
				var found = false
				for i in range(enum_keys.size()):
					if enum_keys[i] == fish_type_id:
						fish_type_id = i
						found = true
						break
				if !found:
					print("ERROR: Could not convert fish type ID to integer: ", fish_type_id)
					continue
		
		#print("Achievement System: Found fish with ID: ", fish_type_id, " shiny: ", item.shiny)
		
		# Make sure we have this fish type in our achievements
		if not fish_achievements.has(fish_type_id):
			#print("Achievement System: Creating new achievement for fish ID: ", fish_type_id)
			fish_achievements[fish_type_id] = {
				"caught": false,
				"shiny": false,
				"surface": false
			}
		
		# Mark this fish type as caught
		if not fish_achievements[fish_type_id]["caught"]:
			#print("Achievement System: Marking fish ID ", fish_type_id, " as caught")
			fish_achievements[fish_type_id]["caught"] = true
			emit_signal("achievement_updated", fish_type_id, "caught")
		
		# Check if this fish is shiny
		if item.shiny and not fish_achievements[fish_type_id]["shiny"]:
			#print("Achievement System: Marking fish ID ", fish_type_id, " as shiny")
			fish_achievements[fish_type_id]["shiny"] = true
			emit_signal("achievement_updated", fish_type_id, "shiny")

# Called when a fish is brought to the surface
func record_fish_surface(fish_type_id):
	# Skip achievements during intro mission
	if GameState.is_intro_mission_active():
		return
	
	# Ensure fish_type_id is an integer
	if typeof(fish_type_id) != TYPE_INT:
		if str(fish_type_id).is_valid_int():
			fish_type_id = int(fish_type_id)
		else:
			# Try to convert string names like "FISH_A" to numeric IDs
			if fish_type_id in FishesConfig.FishType:
				fish_type_id = FishesConfig.FishType[fish_type_id]
			else:
				# If all else fails, assume it's the first fish type (0)
				fish_type_id = 0
	
	# Make sure we have this fish type in our achievements
	if not fish_achievements.has(fish_type_id):
		fish_achievements[fish_type_id] = {
			"caught": true, # If brought to surface, it was caught
			"shiny": false,
			"surface": false
		}
	
	if not fish_achievements[fish_type_id]["surface"]:
		fish_achievements[fish_type_id]["surface"] = true
		# Also mark as caught if not already
		if not fish_achievements[fish_type_id]["caught"]:
			fish_achievements[fish_type_id]["caught"] = true
		emit_signal("achievement_updated", fish_type_id, "surface")

# Clear the incorrect string key if it exists
func fix_achievements():
	#print("Achievement System: Checking for invalid achievement keys")
	# Look for string keys
	var keys_to_fix = []
	for key in fish_achievements.keys():
		if typeof(key) != TYPE_INT:
			keys_to_fix.append(key)
	
	# Fix each bad key
	for bad_key in keys_to_fix:
		#print("Achievement System: Found invalid key: ", bad_key)
		var data = fish_achievements[bad_key]
		fish_achievements.erase(bad_key)
		
		# Try to map to correct fish type
		if bad_key == "FLAMY":
			var fish_id = FishesConfig.FishType.FLAMY
			#print("Achievement System: Mapping 'fish' to Fish A (ID: ", fish_id, ")")
			if not fish_achievements.has(fish_id):
				fish_achievements[fish_id] = data
			else:
				# Merge the data
				fish_achievements[fish_id]["caught"] = fish_achievements[fish_id]["caught"] or data["caught"]
				fish_achievements[fish_id]["shiny"] = fish_achievements[fish_id]["shiny"] or data["shiny"]
				fish_achievements[fish_id]["surface"] = fish_achievements[fish_id]["surface"] or data["surface"]
				#print("Achievement System: Merged data for Fish A")
	
	# Also check for any duplicate fish entries with same ID but different achievement status
	for fish_type in FishesConfig.FishType.values():
		if fish_achievements.has(fish_type) and fish_achievements.has(str(fish_type)):
			#print("Achievement System: Found duplicated numeric ID as string: ", str(fish_type))
			var string_data = fish_achievements[str(fish_type)]
			fish_achievements.erase(str(fish_type))
			
			# Merge data
			fish_achievements[fish_type]["caught"] = fish_achievements[fish_type]["caught"] or string_data["caught"]
			fish_achievements[fish_type]["shiny"] = fish_achievements[fish_type]["shiny"] or string_data["shiny"]
			fish_achievements[fish_type]["surface"] = fish_achievements[fish_type]["surface"] or string_data["surface"]
	
	if keys_to_fix.size() > 0:
		emit_signal("achievement_updated", "", "") # Signal UI to refresh
		return true
	return false

# Get all fish achievements
func get_all_achievements():
	# Fix any erroneous string keys
	#fix_achievements()
	return fish_achievements

# Get achievement status for a specific fish type
func get_fish_achievement(fish_type_id):
	# Make sure the ID is an integer
	var type_id = fish_type_id
	if typeof(type_id) != TYPE_INT:
		# Try to convert to integer if possible
		type_id = int(type_id)
	
	# Make sure we have this fish type in our achievements
	if not fish_achievements.has(type_id):
		return null
	
	return fish_achievements[type_id]

# Get fish type name from ID
func get_fish_name(fish_type_id):
	# Ensure fish_type_id is an integer
	var id_to_find = int(fish_type_id)
	
	# Get all keys and values from the FishType enum
	var enum_keys = FishesConfig.FishType.keys()
	var enum_values = []
	
	# Convert values to integers to ensure proper comparison
	for key in enum_keys:
		enum_values.append(FishesConfig.FishType[key])
	
	# Find the key matching our ID
	for i in range(enum_values.size()):
		if enum_values[i] == id_to_find:
			return enum_keys[i]
	
	return "Unknown Fish"

# Print current state of achievements for debugging
func print_achievements():
	print("==== ACHIEVEMENT STATUS ====")
	for fish_id in fish_achievements:
		var f_name = get_fish_name(fish_id)
		var status = fish_achievements[fish_id]
		print(f_name, " (ID: ", fish_id, "): Caught: ", status.caught, ", Shiny: ", status.shiny, ", Surface: ", status.surface)
	print("===========================")

# Save achievement data
func save_data():
	var save_ach_data = {}
	for fish_type_id in fish_achievements:
		# Ensure we only save with string representations of integers
		if typeof(fish_type_id) == TYPE_INT or str(fish_type_id).is_valid_int():
			save_ach_data[str(fish_type_id)] = fish_achievements[fish_type_id]
	return save_ach_data

# Load achievement data
func load_data(data):
	if data == null:
		return
		
	for fish_type_str in data:
		# Convert string key back to integer
		var fish_type_id = int(fish_type_str)
		fish_achievements[fish_type_id] = data[fish_type_str]
	
	#print_achievements()
	emit_signal("achievement_updated", "", "") # Signal that all achievements were updated
