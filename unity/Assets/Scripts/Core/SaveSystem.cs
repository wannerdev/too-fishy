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
        }

        public struct Info
        {
            public DateTime Timestamp;
            public int Depth, MaxDepth, Money;
        }

        static string SavePath => Path.Combine(Application.persistentDataPath, "savegame.save");

        public static bool HasSaveFile() => File.Exists(SavePath);

        public static bool SaveGame()
        {
            var gs = GameState.Instance;
            if (gs == null) return false;
            var d = new SaveDict
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                depth = gs.Depth,
                max_depth_reached = gs.MaxDepthReached,
                money = gs.Money,
                health = gs.Health,
                player_in_stage = (int)gs.PlayerInStage,
                inventory_items = new List<InventoryItem>(gs.Inventory.Items),
                achievements = Achievements.Export()
            };
            foreach (var kv in gs.Upgrades)
            {
                d.upgrade_keys.Add((int)kv.Key);
                d.upgrade_values.Add(kv.Value);
            }
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(d));
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
            var d = Read();
            if (gs == null || d == null) return false;

            var upgrades = new Dictionary<Upgrade, int>();
            for (int i = 0; i < d.upgrade_keys.Count && i < d.upgrade_values.Count; i++)
                upgrades[(Upgrade)d.upgrade_keys[i]] = d.upgrade_values[i];
            gs.LoadState(d.depth, d.max_depth_reached, d.money, d.health, upgrades, d.inventory_items);
            Achievements.Import(d.achievements);
            return true;
        }

        public static bool DeleteSave()
        {
            if (!HasSaveFile()) return false;
            try
            {
                File.Delete(SavePath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Error deleting save file: {e.Message}");
                return false;
            }
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
