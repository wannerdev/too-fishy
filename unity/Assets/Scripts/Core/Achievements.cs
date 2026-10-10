using System;
using System.Collections.Generic;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/achievement/achievement_system.gd: per fish type whether it was caught,
    /// caught shiny, or brought to the surface, plus the "Drone Lift" meta achievement.
    /// </summary>
    public static class Achievements
    {
        [Serializable]
        public class FishRecord
        {
            public bool Caught, Shiny, Surface;
        }

        public static readonly Dictionary<FishType, FishRecord> Fish = new();
        public static bool DroneLift { get; private set; }
        public static event Action Updated;

        static Achievements() => Reset();

        public static void Reset()
        {
            Fish.Clear();
            foreach (FishType t in Enum.GetValues(typeof(FishType)))
                Fish[t] = new FishRecord();
            DroneLift = false;
            Updated?.Invoke();
        }

        /// <summary>achievement_system.gd _on_inventory_updated(): every fish in the inventory counts as caught.</summary>
        public static void OnInventoryUpdated(Inventory inventory)
        {
            if (GameState.Instance != null && GameState.Instance.IsIntro()) return;
            bool changed = false;
            foreach (var item in inventory.Items)
            {
                var rec = Fish[item.Type];
                if (!rec.Caught) { rec.Caught = true; changed = true; }
                if (item.Shiny && !rec.Shiny) { rec.Shiny = true; changed = true; }
            }
            if (changed) Updated?.Invoke();
        }

        /// <summary>fish.gd record_surface_achievement(): a fish swam up to the surface.</summary>
        public static void RecordSurface(FishType type)
        {
            if (GameState.Instance != null && GameState.Instance.IsIntro()) return;
            var rec = Fish[type];
            if (rec.Surface) return;
            rec.Surface = true;
            rec.Caught = true;
            Updated?.Invoke();
        }

        public static void RecordDroneLift()
        {
            if (DroneLift) return;
            DroneLift = true;
            Updated?.Invoke();
        }

        /// <summary>achievement_system.gd get_fish_name()</summary>
        public static string FishName(FishType type) => type switch
        {
            FishType.Flamy => "FLAMY",
            FishType.Greeny => "GREENY",
            FishType.Angler => "ANGLER",
            FishType.Smally => "SMALLY",
            FishType.Spikey => "SPIKEY",
            FishType.BossMini => "BOSS_MINI",
            _ => type.ToString().ToUpperInvariant()
        };

        /// <summary>fishes_config.gd icon per type</summary>
        public static string IconPath(FishType type) => type switch
        {
            FishType.Flamy => "textures/icons/fish_a.png",
            FishType.Greeny => "textures/icons/fish_b.png",
            FishType.Angler => "textures/icons/angler_fish.png",
            FishType.Smally => "textures/icons/dummy_fish.png",
            FishType.Spikey => "textures/icons/spikey_fish.png",
            FishType.BossMini => "textures/icons/boss_icon.png",
            _ => "textures/icons/questionmark.png"
        };

        // ------------------------------------------------------------------ save data

        [Serializable]
        public class SaveData
        {
            public List<int> Types = new();
            public List<FishRecord> Records = new();
            public bool DroneLift;
        }

        public static SaveData Export()
        {
            var d = new SaveData { DroneLift = DroneLift };
            foreach (var kv in Fish)
            {
                d.Types.Add((int)kv.Key);
                d.Records.Add(new FishRecord { Caught = kv.Value.Caught, Shiny = kv.Value.Shiny, Surface = kv.Value.Surface });
            }
            return d;
        }

        public static void Import(SaveData d)
        {
            if (d == null) return;
            for (int i = 0; i < d.Types.Count && i < d.Records.Count; i++)
            {
                var type = (FishType)d.Types[i];
                if (Fish.ContainsKey(type)) Fish[type] = d.Records[i];
            }
            DroneLift = d.DroneLift;
            Updated?.Invoke();
        }
    }
}
