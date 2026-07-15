using System;
using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Central game state singleton — port of Godot GameState autoload.
    /// </summary>
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        public static readonly Dictionary<int, Stage> DepthStageMap = new()
        {
            { 0, Stage.Surface },
            { 100, Stage.Deep },
            { 200, Stage.Deeper },
            { 300, Stage.SuperDeep },
            { 400, Stage.Hot },
            { 500, Stage.Lava },
            { 600, Stage.Void }
        };

        public static readonly Dictionary<Upgrade, int> UpgradeCosts = new()
        {
            { Upgrade.CargoSize, 25 },
            { Upgrade.DepthResistance, 50 },
            { Upgrade.PickaxeUnlocked, 200 },
            { Upgrade.VertSpeed, 25 },
            { Upgrade.HorSpeed, 25 },
            { Upgrade.LampUnlocked, 50 },
            { Upgrade.Ak47, 500 },
            { Upgrade.DualAk47, 5000 },
            { Upgrade.Harpoon, 100 },
            { Upgrade.HarpoonRotation, 150 },
            { Upgrade.InventoryManagement, 400 },
            { Upgrade.SurfaceBuoy, 1000 },
            { Upgrade.InventorySave, 250 },
            { Upgrade.DroneSelling, 300 }
        };

        public static readonly Dictionary<Upgrade, int> MaxUpgrades = new()
        {
            { Upgrade.CargoSize, 5 },
            { Upgrade.DepthResistance, 5 },
            { Upgrade.PickaxeUnlocked, 1 },
            { Upgrade.VertSpeed, 2 },
            { Upgrade.HorSpeed, 3 },
            { Upgrade.LampUnlocked, 1 },
            { Upgrade.Ak47, 1 },
            { Upgrade.DualAk47, 1 },
            { Upgrade.Harpoon, 1 },
            { Upgrade.HarpoonRotation, 1 },
            { Upgrade.InventoryManagement, 1 },
            { Upgrade.SurfaceBuoy, 1 },
            { Upgrade.InventorySave, 1 },
            { Upgrade.DroneSelling, 1 }
        };

        public Dictionary<Upgrade, int> Upgrades { get; private set; } = new();

        public int Depth { get; private set; }
        public int MaxDepthReached { get; private set; }
        public int Money { get; set; } = 25;
        public float Health { get; set; } = 100f;
        public float Headroom { get; private set; }
        public bool IsDocked { get; set; }
        public bool Paused { get; set; }
        public bool DeathScreen { get; set; }
        public bool GodMode { get; set; }
        public Stage PlayerInStage { get; private set; } = Stage.Surface;
        public float FishesLowerBorder { get; set; } = -27f;

        public GameMode CurrentGameMode { get; private set; } = GameMode.Normal;
        public bool IntroMissionCompleted { get; private set; }
        public bool BossEncountered { get; set; }
        public bool EnableIntroMission = false; // Start in normal mode for Unity port by default

        public Inventory Inventory { get; private set; } = new();
        public Transform PlayerTransform { get; set; }
        public PlayerController Player { get; set; }

        public event Action OnInventoryUpdated;
        public event Action OnMoneyChanged;
        public event Action OnHealthChanged;
        public event Action OnDepthChanged;
        public event Action OnUpgradesChanged;
        public event Action OnDeath;
        public event Action OnRespawn;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResetUpgrades();
            Inventory.Bind(this);
        }

        void Start()
        {
            if (EnableIntroMission && !IntroMissionCompleted)
                StartIntroMission();
            else
                StartNormalMode();
        }

        public void NotifyInventoryUpdated() => OnInventoryUpdated?.Invoke();

        public void SetDepth(int d)
        {
            Depth = d;
            if (MaxDepthReached < d) MaxDepthReached = d;

            if (IsIntro())
            {
                PlayerInStage = Stage.Hot;
                OnDepthChanged?.Invoke();
                return;
            }

            int band = (d / 100) * 100; // 0-99 Surface, 100-199 Deep, ...
            Stage newStage = Stage.Surface;
            foreach (var kv in DepthStageMap)
            {
                if (band >= kv.Key)
                    newStage = kv.Value;
            }
            PlayerInStage = newStage;
            OnDepthChanged?.Invoke();
        }

        public int GetUpgradeCost(Upgrade upgrade) =>
            (Upgrades[upgrade] + 1) * UpgradeCosts[upgrade];

        public bool TryUpgrade(Upgrade upgrade)
        {
            int cost = GetUpgradeCost(upgrade);
            if (Money >= cost && Upgrades[upgrade] < MaxUpgrades[upgrade])
            {
                Money -= cost;
                Upgrades[upgrade]++;
                OnMoneyChanged?.Invoke();
                OnUpgradesChanged?.Invoke();
                return true;
            }
            return false;
        }

        public int GetUpgradeLevel(Upgrade upgrade) => Upgrades[upgrade];

        public void ResetUpgrades()
        {
            Upgrades = new Dictionary<Upgrade, int>();
            foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
                Upgrades[u] = 0;
        }

        public bool IsIntro() => CurrentGameMode == GameMode.IntroMission;

        public void StartNormalMode()
        {
            CurrentGameMode = GameMode.Normal;
            IntroMissionCompleted = true;
            BossEncountered = false;
            DeathScreen = false;
            Paused = false;
            Health = 100f;
            IsDocked = false;
            PlayerInStage = Stage.Surface;
            Depth = 0;
            MaxDepthReached = 0;
            Money = 25;
            ResetUpgrades();
            Inventory.Clear();
            Time.timeScale = 1f;

            if (Player != null)
                Player.Teleport(new Vector3(-8f, 0f, 0.33f));
        }

        public void StartIntroMission()
        {
            CurrentGameMode = GameMode.IntroMission;
            IntroMissionCompleted = false;
            BossEncountered = false;
            DeathScreen = false;
            Paused = false;
            Health = 100f;
            IsDocked = false;
            PlayerInStage = Stage.Hot;
            Depth = 450;
            Money = 1000;
            SetupFriendUpgrades();
            Time.timeScale = 1f;

            if (Player != null)
                Player.Teleport(new Vector3(-8f, -450f, 0.33f));
        }

        void SetupFriendUpgrades()
        {
            foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
            {
                if (u == Upgrade.PickaxeUnlocked || u == Upgrade.SurfaceBuoy)
                    Upgrades[u] = 0;
                else
                    Upgrades[u] = MaxUpgrades[u];
            }
        }

        public void CompleteIntroMission(Vector3 deathPosition)
        {
            IntroMissionCompleted = true;
            ResetUpgrades();
            Money = 25;
            MaxDepthReached = 0;
            CurrentGameMode = GameMode.Normal;
            PlayerInStage = Stage.Surface;
            Depth = 0;
            BossEncountered = false;
            Inventory.Clear();
            if (Player != null)
                Player.Teleport(new Vector3(-8f, 0f, 0.33f));
        }

        public void ApplyPressureDamage(float dt)
        {
            if (GodMode || IsDocked) return;
            Headroom = (GetUpgradeLevel(Upgrade.DepthResistance) + 1) * 100f - Depth;
            if (Headroom < 0f)
            {
                float dps = Mathf.Max(1f, Mathf.Abs(Headroom) / 10f);
                Damage(dps * dt);
            }
        }

        public void Damage(float amount)
        {
            if (GodMode || DeathScreen) return;
            Health = Mathf.Max(0f, Health - amount);
            OnHealthChanged?.Invoke();
            if (Health <= 0f)
                Die();
        }

        public void Heal(float amount)
        {
            Health = Mathf.Min(100f, Health + amount);
            OnHealthChanged?.Invoke();
        }

        public void Die()
        {
            if (DeathScreen) return;
            DeathScreen = true;
            Paused = true;

            if (GetUpgradeLevel(Upgrade.InventorySave) < 1)
                Inventory.Clear();

            OnDeath?.Invoke();
        }

        public void Respawn()
        {
            DeathScreen = false;
            Paused = false;
            Health = 100f;
            IsDocked = false;
            Time.timeScale = 1f;
            if (Player != null)
                Player.Teleport(new Vector3(-8f, 0f, 0.33f));
            OnRespawn?.Invoke();
        }

        public static string StageName(Stage stage) => stage switch
        {
            Stage.Surface => "Shallow Waters",
            Stage.Deep => "Medium Waters",
            Stage.Deeper => "Deep Waters",
            Stage.SuperDeep => "Dark Deep Waters",
            Stage.Hot => "Hot-Zone",
            Stage.Lava => "Lava",
            Stage.Void => "THE VOID",
            _ => stage.ToString()
        };

        public static string UpgradeName(Upgrade upgrade) => upgrade switch
        {
            Upgrade.CargoSize => "Cargo Size",
            Upgrade.DepthResistance => "Depth Resistance",
            Upgrade.PickaxeUnlocked => "Pickaxe",
            Upgrade.VertSpeed => "Vertical Speed",
            Upgrade.HorSpeed => "Horizontal Speed",
            Upgrade.LampUnlocked => "Lamp",
            Upgrade.Ak47 => "AK-47",
            Upgrade.DualAk47 => "Dual AK-47",
            Upgrade.Harpoon => "Harpoon Pierce",
            Upgrade.HarpoonRotation => "Harpoon Aim",
            Upgrade.InventoryManagement => "Smart Inventory",
            Upgrade.SurfaceBuoy => "Surface Buoy",
            Upgrade.InventorySave => "Inventory Save",
            Upgrade.DroneSelling => "Selling Drone",
            _ => upgrade.ToString()
        };
    }
}
