using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    public class FishStats
    {
        public float WeightMin, WeightMax;
        public float PriceWeightMultiplier;
        public float SpeedMin, SpeedMax;
        public int Difficulty;
        public bool RequiresBossDefeat;
        public int MinRequiredDepth;
        public int MaxActivePerSection = 99;
        public int MaxActiveGlobal = 99;
        public float SpawnCooldownSec;
        public Color Color;
        public Vector3 Scale = Vector3.one;
    }

    public class StageSpawnConfig
    {
        public int MaxFishAmount;
        public float ShinyRate;
        public float WeightMultiplier;
        public Dictionary<FishType, float> SpawnRates;
    }

    public static class FishConfig
    {
        public static readonly Dictionary<FishType, FishStats> Stats = new()
        {
            { FishType.Flamy, new FishStats {
                WeightMin = 1, WeightMax = 10, PriceWeightMultiplier = 1f,
                SpeedMin = 1f, SpeedMax = 2.5f, Difficulty = 1,
                Color = new Color(1f, 0.45f, 0.15f), Scale = new Vector3(0.6f, 0.35f, 0.25f)
            }},
            { FishType.Greeny, new FishStats {
                WeightMin = 1, WeightMax = 5, PriceWeightMultiplier = 1.2f,
                SpeedMin = 2f, SpeedMax = 5f, Difficulty = 1,
                Color = new Color(0.2f, 0.85f, 0.35f), Scale = new Vector3(0.45f, 0.28f, 0.2f)
            }},
            { FishType.Angler, new FishStats {
                WeightMin = 5, WeightMax = 10, PriceWeightMultiplier = 3f,
                SpeedMin = 0.1f, SpeedMax = 1f, Difficulty = 5,
                Color = new Color(0.55f, 0.35f, 0.75f), Scale = new Vector3(0.9f, 0.55f, 0.4f)
            }},
            { FishType.Smally, new FishStats {
                WeightMin = 5, WeightMax = 10, PriceWeightMultiplier = 10f,
                SpeedMin = 3f, SpeedMax = 7f, Difficulty = 10,
                Color = new Color(1f, 0.85f, 0.2f), Scale = new Vector3(0.3f, 0.2f, 0.15f)
            }},
            { FishType.Spikey, new FishStats {
                WeightMin = 5, WeightMax = 10, PriceWeightMultiplier = 3f,
                SpeedMin = 0.5f, SpeedMax = 1.5f, Difficulty = 5,
                Color = new Color(0.85f, 0.2f, 0.25f), Scale = new Vector3(0.7f, 0.45f, 0.35f)
            }},
            { FishType.BossMini, new FishStats {
                WeightMin = 3, WeightMax = 8, PriceWeightMultiplier = 11f,
                SpeedMin = 1.3f, SpeedMax = 2.5f, Difficulty = 10,
                RequiresBossDefeat = true, MinRequiredDepth = 520,
                MaxActivePerSection = 1, MaxActiveGlobal = 2, SpawnCooldownSec = 55f,
                Color = new Color(1f, 0.55f, 0.65f), Scale = new Vector3(0.8f, 0.7f, 0.5f)
            }}
        };

        public static readonly Dictionary<Stage, StageSpawnConfig> Sections = new()
        {
            { Stage.Surface, new StageSpawnConfig {
                MaxFishAmount = 15, ShinyRate = 0.02f, WeightMultiplier = 0.8f,
                SpawnRates = new() { { FishType.Flamy, 0.9f }, { FishType.Greeny, 0.1f } }
            }},
            { Stage.Deep, new StageSpawnConfig {
                MaxFishAmount = 10, ShinyRate = 0.02f, WeightMultiplier = 0.9f,
                SpawnRates = new() { { FishType.Flamy, 0.7f }, { FishType.Greeny, 0.3f } }
            }},
            { Stage.Deeper, new StageSpawnConfig {
                MaxFishAmount = 10, ShinyRate = 0.04f, WeightMultiplier = 1f,
                SpawnRates = new() { { FishType.Flamy, 0.5f }, { FishType.Greeny, 0.4f }, { FishType.Spikey, 0.1f } }
            }},
            { Stage.SuperDeep, new StageSpawnConfig {
                MaxFishAmount = 8, ShinyRate = 0.04f, WeightMultiplier = 1.1f,
                SpawnRates = new() {
                    { FishType.Flamy, 0.2f }, { FishType.Greeny, 0.43f },
                    { FishType.Angler, 0.14f }, { FishType.Smally, 0.03f }, { FishType.Spikey, 0.2f }
                }
            }},
            { Stage.Hot, new StageSpawnConfig {
                MaxFishAmount = 5, ShinyRate = 0.05f, WeightMultiplier = 1.15f,
                SpawnRates = new() {
                    { FishType.Greeny, 0.2f }, { FishType.Angler, 0.5f },
                    { FishType.Smally, 0.1f }, { FishType.Spikey, 0.2f }
                }
            }},
            { Stage.Lava, new StageSpawnConfig {
                MaxFishAmount = 4, ShinyRate = 0.06f, WeightMultiplier = 1.15f,
                SpawnRates = new() {
                    { FishType.Angler, 0.53f }, { FishType.Smally, 0.24f },
                    { FishType.Spikey, 0.18f }, { FishType.BossMini, 0.05f }
                }
            }},
            { Stage.Void, new StageSpawnConfig {
                MaxFishAmount = 1, ShinyRate = 0.08f, WeightMultiplier = 1.2f,
                SpawnRates = new() {
                    { FishType.Smally, 0.1f }, { FishType.Angler, 0.1f }, { FishType.Flamy, 0.8f }
                }
            }}
        };

        public static FishType PickType(Stage stage)
        {
            var cfg = Sections[stage];
            float roll = Random.value;
            float acc = 0f;
            foreach (var kv in cfg.SpawnRates)
            {
                acc += kv.Value;
                if (roll <= acc) return kv.Key;
            }
            return FishType.Flamy;
        }

        public static Color StageFogColor(Stage stage) => stage switch
        {
            Stage.Surface => new Color(0.15f, 0.45f, 0.65f),
            Stage.Deep => new Color(0.08f, 0.28f, 0.48f),
            Stage.Deeper => new Color(0.04f, 0.15f, 0.32f),
            Stage.SuperDeep => new Color(0.02f, 0.05f, 0.15f),
            Stage.Hot => new Color(0.35f, 0.12f, 0.05f),
            Stage.Lava => new Color(0.45f, 0.05f, 0.02f),
            Stage.Void => new Color(0.02f, 0.0f, 0.05f),
            _ => Color.black
        };

        public static float StageFogDensity(Stage stage) => stage switch
        {
            Stage.Surface => 0.02f,
            Stage.Deep => 0.035f,
            Stage.Deeper => 0.05f,
            Stage.SuperDeep => 0.07f,
            Stage.Hot => 0.055f,
            Stage.Lava => 0.06f,
            Stage.Void => 0.09f,
            _ => 0.03f
        };
    }
}
