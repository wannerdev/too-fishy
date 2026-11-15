using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/save_system.gd: one JSON save file with depth, money, health, upgrades,
    /// inventory and achievements. Loading does not move the submarine (as in Godot).
    /// </summary>
    public static class SaveSystem
    {
        [Serializable]
        class SaveDict
        {
            public int version = 1;
            public long timestamp;
            public int depth;
            public int max_depth_reached;
            public int money;
            public float health;
            public int player_in_stage;
            public List<int> upgrade_keys = new();
            public List<int> upgrade_values = new();
            public List<InventoryItem> inventory_items = new();
            public Achievements.SaveData achievements;
            // Story flags: save_system.gd:7-25 keeps none of them (godot-bug-also), so after a
            // restart the beaten boss came back. Missing in old saves = false (JsonUtility).
            public bool boss_encountered;
            public bool boss_defeated;
            public bool win_reached;
        }

        public struct Info
        {
            public DateTime Timestamp;
            public int Depth, MaxDepth, Money;
        }

        static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.save");

        /// <summary>This run has saved or loaded the slot, so an autosave cannot replace a save the player never loaded.</summary>
        static bool _sessionOwnsSave;

        public static bool HasSaveFile() => File.Exists(SavePath);

        public static bool SaveGame()
        {
            var gs = GameState.Instance;
            if (gs == null) return false;
            if (gs.IsIntro())
            {
                // Deviation: save_system.gd:6, player.gd:281 and save_menu.gd:93 have no intro
                // guard, so Godot can store the friend's maxed sub and $10000 (player.gd:877-886).
                var at = gs.Player != null && gs.Player.PopupSpawn != null ? gs.Player.PopupSpawn.position
                    : gs.PlayerTransform != null ? gs.PlayerTransform.position + Vector3.up : Vector3.zero;
                // From the pause menu (timeScale 0) the menu shows its own status line instead
                if (Time.timeScale > 0f) PopupText.Show("Can't save during the intro!", at, Color.red);
                return false;
            }
            var d = new SaveDict
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                depth = gs.Depth,
                max_depth_reached = gs.MaxDepthReached,
                // player.gd:728/756: the cargo is gone at launch, the money comes with the drone;
                // count the money of a drone still in flight so the catch is not lost
                money = gs.Money + DroneRun.PendingMoney,
                health = gs.Health,
                player_in_stage = (int)gs.PlayerInStage,
                inventory_items = new List<InventoryItem>(gs.Inventory.Items),
                achievements = Achievements.Export(),
                boss_encountered = gs.BossEncountered,
                boss_defeated = BossController.IsDefeated,
                win_reached = Dialogs.WinReached
            };
            foreach (var kv in gs.Upgrades)
            {
                d.upgrade_keys.Add((int)kv.Key);
                d.upgrade_values.Add(kv.Value);
            }
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(d));
                FlushWeb();
                _sessionOwnsSave = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Error writing save file: {e.Message}");
                return false;
            }
        }

        public static bool LoadGame()
        {
            var gs = GameState.Instance;
            // Loading in the intro would put the real save on the friend sub (CompleteIntroMission wipes it)
            if (gs == null || gs.IsIntro()) return false;
            var d = Read();
            if (d == null) return false;

            // A drone launched before the load must not pay into the loaded game (player.gd:756 tween)
            DroneRun.CancelAll();

            var upgrades = new Dictionary<Upgrade, int>();
            for (int i = 0; i < d.upgrade_keys.Count && i < d.upgrade_values.Count; i++)
                upgrades[(Upgrade)d.upgrade_keys[i]] = d.upgrade_values[i];
            int maxFishId = 0;
            foreach (var item in d.inventory_items)
                if (item != null && item.Id > maxFishId) maxFishId = item.Id;
            gs.LoadState(d.depth, d.max_depth_reached, d.money, d.health, upgrades, d.inventory_items);
            Achievements.Import(d.achievements);
            FishBehaviour.ReserveIdsAbove(maxFishId);
            RestoreStory(gs, d, upgrades);
            _sessionOwnsSave = true;
            return true;
        }

        /// <summary>
        /// Story flags only move forward: a load never un-meets or un-beats the boss of this run,
        /// matching Godot, where a load leaves them untouched (save_system.gd:71-150).
        /// </summary>
        static void RestoreStory(GameState gs, SaveDict d, Dictionary<Upgrade, int> upgrades)
        {
            // upgrades.gd:554-557: the gun card shows once the boss was met; an owned gun means it was (old saves)
            if (d.boss_encountered || (upgrades.TryGetValue(Upgrade.Ak47, out var ak) && ak > 0))
                gs.BossEncountered = true;

            // A beaten boss whose friend was not yet brought up stays to be fought again: without
            // the boss spawn there is no boss section and no friend (BossController.cs:78), so WIN
            // could never be reached.
            if (!d.win_reached) return;
            if (!Dialogs.WinReached)
            {
                // friend.gd:24-25 WIN, opened and closed at once so it neither shows nor pauses
                Dialogs.SetStage(DialogSection.Win);
                for (int i = 0; i < 8 && Dialogs.Displayed; i++) Dialogs.Continue();
            }
            // boss.gd boss_defeated_permanently: stops TrySpawn (BossController.cs:47)
            if (!BossController.IsDefeated) BossController.MarkDefeatedFromSave();
            BossController.Remove();
        }

        /// <summary>
        /// Android/iOS autosave when the app is backgrounded or quit (Godot has none; phones kill
        /// apps). Skipped in the intro, on the death screen, during the boss intro, and when the
        /// slot holds a save this run never loaded.
        /// </summary>
        public static bool TryAutoSave()
        {
            var gs = GameState.Instance;
            if (gs == null || gs.IsIntro() || gs.DeathScreen || gs.PendingRegularDeathTransition || gs.Health <= 0f) return false;
            if (Dialogs.Displayed && (Dialogs.Section == DialogSection.BossIntro || Dialogs.Section == DialogSection.BossKillsFriend)) return false;
            if (!_sessionOwnsSave && HasSaveFile()) return false;
            return SaveGame();
        }

        public static bool DeleteSave()
        {
            if (!HasSaveFile()) return false;
            try
            {
                File.Delete(SavePath);
                FlushWeb();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Error deleting save file: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// WebGL: persistentDataPath is IDBFS; Godot syncs user:// to IndexedDB when the file is
        /// closed. The template sets autoSyncPersistentDataPath; PlayerPrefs.Save() also syncs
        /// the mount (JS_FileSystem_Sync) in case another template is used.
        /// </summary>
        static void FlushWeb()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PlayerPrefs.Save();
#endif
        }

        public static Info? GetSaveInfo()
        {
            var d = Read();
            if (d == null) return null;
            return new Info
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(d.timestamp).LocalDateTime,
                Depth = d.depth,
                MaxDepth = d.max_depth_reached,
                Money = d.money
            };
        }

        static SaveDict Read()
        {
            if (!HasSaveFile()) return null;
            try
            {
                return JsonUtility.FromJson<SaveDict>(File.ReadAllText(SavePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Error reading save file: {e.Message}");
                return null;
            }
        }
    }
}
