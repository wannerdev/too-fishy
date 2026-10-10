using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/level.gd + scenes/section.tscn + scripts/section.gd and the static world of
    /// scenes/main_scene.tscn (dock, water, surface rocks, play-area walls).
    ///
    /// Section layout matches Godot: FirstSection at y -10.5 (bg_first), SecondSection at
    /// y -35.4 (bg_afterfirst), then one section every 25 m starting at y -60. Godot keeps every
    /// section it spawns; on phones the port streams them instead (sections more than
    /// <see cref="ViewDistance"/> away are destroyed and rebuilt when the player returns).
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        public const float SectionHeight = 25f;
        public float ViewDistance = 80f;

        // section.gd constants and the SpawnerA / SpawnerB markers of section.tscn
        const float BgYOverscan = 1.15f;
        const float FishRespawnInterval = 15f;
        static readonly Vector2 SpawnerA = new(-23.8985f, 9.53181f);
        static readonly Vector2 SpawnerB = new(-4.71813f, -11.169f);
        // LeftBarrier / RightBarrier of section.tscn
        const float LeftBarrierX = -29.94f, RightBarrierX = -0.0240569f;

        class SectionState
        {
            public GameObject Root;
            public Stage Type;
            public float Y;
            public float RespawnTimer;
            public readonly Dictionary<FishType, float> Cooldowns = new();
        }

        readonly Dictionary<int, SectionState> _sections = new();
        readonly List<int> _toRemove = new();
        Transform _worldRoot;

        public void Initialize(Transform worldRoot)
        {
            _worldRoot = worldRoot;
            BuildStaticWorld();
            UpdateSections(0f);
        }

        void Update()
        {
            var player = GameState.Instance?.PlayerTransform;
            if (player == null) return;
            UpdateSections(player.position.y);
            UpdateFishRespawn(player.position.y);
        }

        // ------------------------------------------------------------------ section layout

        static float SectionY(int index) => index switch
        {
            0 => -10.5f,
            1 => -35.4018f,
            _ => -35f - SectionHeight * (index - 1)
        };

        static int IndexAt(float y)
        {
            if (y > -23f) return 0;
            if (y > -47.5f) return 1;
            return Mathf.Max(2, Mathf.RoundToInt((-35f - y) / SectionHeight) + 1);
        }

        /// <summary>level.gd spawnNewSection(): stage of snapped(depth, 100), capped at the last stage.</summary>
        static Stage StageAt(int index)
        {
            if (index < 2) return Stage.Surface; // FirstSection / SecondSection keep the default type
            int band = Mathf.Min(GameState.SnapDepth(Mathf.RoundToInt(-SectionY(index))), 600);
            Stage stage = Stage.Surface;
            foreach (var kv in GameState.DepthStageMap)
                if (band >= kv.Key) stage = kv.Value;
            return stage;
        }

        void UpdateSections(float playerY)
        {
            int first = IndexAt(playerY + ViewDistance);
            int last = IndexAt(playerY - ViewDistance);
            for (int i = first; i <= last; i++)
                if (!_sections.ContainsKey(i)) SpawnSection(i);

            _toRemove.Clear();
            foreach (var kv in _sections)
                if (kv.Value.Root == null || kv.Key < first - 1 || kv.Key > last + 1) _toRemove.Add(kv.Key);
            foreach (int i in _toRemove)
            {
                if (_sections[i].Root != null) Destroy(_sections[i].Root);
                _sections.Remove(i);
            }

            // level.gd: fishes_lower_boarder = lastSpawned - sectionHeight / 2 - 1
            if (GameState.Instance != null)
                GameState.Instance.FishesLowerBorder = SectionY(last) - SectionHeight / 2f - 1f;
        }

        void SpawnSection(int index)
        {
            float y = SectionY(index);
            Stage stage = StageAt(index);
            Stage lastStage = index == 0 ? Stage.Surface : StageAt(index - 1);

            var root = new GameObject($"Section_{stage}_{Mathf.RoundToInt(-y)}m");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = new Vector3(0f, y, 0f);
            var state = new SectionState { Root = root, Type = stage, Y = y, RespawnTimer = FishRespawnInterval };
            _sections[index] = state;

            BuildBackground(root.transform, BackgroundFor(index, stage, lastStage));

            string veins = stage == Stage.Lava ? "veins_lava" : "veins";
            BuildRockWall(root.transform, "LeftWall", veins,
                0.917378f, 0f, -0.492565f, 0f, 1.36003f, 0f, 0.458728f, 0f, 0.985046f, -30.8771f, 1.37305f, 1.14596f);
            BuildRockWall(root.transform, "LeftWall2", veins,
                -0.840678f, 0f, -0.630943f, 0f, 1.36003f, 0f, 0.5876f, 0f, -0.902689f, 2.58816f, 1.37305f, 1.14596f);

            AddBarrierBoxes(root.transform, index, y, stage, lastStage);
            SpawnFish(state, spawnAll: true);
        }

        /// <summary>section.gd _ready(): the HOT→LAVA transition texture, otherwise the per-stage map.</summary>
        static string BackgroundFor(int index, Stage stage, Stage lastStage)
        {
            if (index == 0) return "bg_first";
            if (index == 1) return "bg_afterfirst";
            if (lastStage == Stage.Hot && stage == Stage.Lava) return "bg_deep_to_lava";
            return stage >= Stage.Lava ? "bg_lava" : "bg_loop";
        }

        static void BuildBackground(Transform section, string material)
        {
            // Background: QuadMesh, Transform3D(25, 0, 0, 0, 25, 0, 0, 0, 1, -15.293, 0, -3.007),
            // stretched 1.15× in Y so neighbouring sections crossfade (section.gd)
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Background";
            Object.Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(section, false);
            bg.transform.localPosition = GodotSpace.Pos(-15.293f, 0f, -3.007f);
            bg.transform.localScale = new Vector3(25f, 25f * BgYOverscan, 1f);
            var mat = GodotAssets.Material(material);
            bg.GetComponent<Renderer>().sharedMaterial = mat != null ? mat : Materials.Opaque(new Color(0.08f, 0.28f, 0.48f));
        }

        /// <summary>LeftWall / LeftWall2 of section.tscn: gestein rock with overlay, veins, box collider.</summary>
        static void BuildRockWall(Transform parent, string name, string veinsMaterial, params float[] godotTransform)
        {
            var wall = new GameObject(name).transform;
            wall.SetParent(parent, false);
            GodotSpace.Apply(wall, godotTransform);

            var rock = GodotAssets.SpawnModel(wall, "Gestein", "meshes/gestein_v003.obj", "walls", null);
            GodotAssets.AddOverlay(rock, "walls_overlay_section");
            GodotAssets.SpawnModel(wall, "Veins", "meshes/vains_v003.obj", veinsMaterial, null);

            var col = wall.gameObject.AddComponent<BoxCollider>();
            col.center = GodotSpace.Pos(1.57438f, -0.818055f, 0.261556f);
            col.size = new Vector3(4.19824f, 19.4489f, 13.8901f);
        }

        /// <summary>section.gd add_barrier_boxes(): nine crates across the play area at a stage change.</summary>
        static void AddBarrierBoxes(Transform section, int index, float y, Stage stage, Stage lastStage)
        {
            var gs = GameState.Instance;
            if (gs == null || gs.IsIntro()) return;

            bool transition =
                (lastStage == Stage.Deep && stage == Stage.Deeper) ||
                (lastStage == Stage.Deeper && stage == Stage.SuperDeep) ||
                (lastStage == Stage.SuperDeep && stage == Stage.Hot) ||
                (lastStage == Stage.Hot && stage == Stage.Lava) ||
                (lastStage == Stage.Lava && stage == Stage.Void);
            if (!transition) return;

            int health = stage switch
            {
                Stage.Deeper => 2,
                Stage.SuperDeep => 3,
                Stage.Hot => 4,
                Stage.Lava => 5,
                Stage.Void => 6,
                _ => 1
            };

            // Godot computes the row's y as position.y + 10 but assigns it as a *local* position
            // of the section, which puts the crates at 2·y + 10. The port uses the intended spot,
            // 10 m above the section centre.
            const int count = 9;
            float spacing = Mathf.Abs(RightBarrierX - LeftBarrierX) / (count - 1);
            for (int i = 0; i < count; i++)
            {
                int key = index * 16 + i;
                if (gs.DestroyedBarriers.Contains(key)) continue;
                var pos = GodotSpace.Pos(LeftBarrierX + spacing * i, y + 10f, -0.5f);
                DestroyableBarrier.Create(section, pos, health, key);
            }
        }

        // ------------------------------------------------------------------ fish

        /// <summary>section.gd spawn_fish(): fill up to max_fish_amount (or add one) with the caps applied.</summary>
        void SpawnFish(SectionState section, bool spawnAll)
        {
            var gs = GameState.Instance;
            if (gs == null || !FishConfig.Sections.TryGetValue(section.Type, out var cfg)) return;

            int home = section.Root.GetInstanceID();
            var perSection = new Dictionary<FishType, int>();
            var global = new Dictionary<FishType, int>();
            int amount = 0;
            foreach (var fish in FindObjectsByType<FishBehaviour>(FindObjectsSortMode.None))
            {
                global[fish.Type] = global.GetValueOrDefault(fish.Type) + 1;
                if (fish.Home == home)
                {
                    perSection[fish.Type] = perSection.GetValueOrDefault(fish.Type) + 1;
                    amount++;
                }
            }

            while (amount < cfg.MaxFishAmount)
            {
                float total = 0f;
                var available = new List<KeyValuePair<FishType, float>>();
                foreach (var kv in cfg.SpawnRates)
                {
                    var stats = FishConfig.Stats[kv.Key];
                    if (stats.RequiresBossDefeat && !BossController.IsDefeated) continue;
                    if (stats.RequiresBossDefeat && gs.IsIntro()) continue; // spawn_during_intro = false
                    if (stats.MinRequiredDepth > 0 && gs.MaxDepthReached < stats.MinRequiredDepth) continue;
                    if (perSection.GetValueOrDefault(kv.Key) >= stats.MaxActivePerSection) continue;
                    if (global.GetValueOrDefault(kv.Key) >= stats.MaxActiveGlobal) continue;
                    if (section.Cooldowns.TryGetValue(kv.Key, out float cd) && cd > 0f) continue;
                    available.Add(kv);
                    total += kv.Value;
                }
                if (available.Count == 0 || total <= 0f) break;

                float r = Random.value * total;
                float acc = 0f;
                FishType type = available[0].Key;
                foreach (var kv in available)
                {
                    acc += kv.Value;
                    if (r <= acc) { type = kv.Key; break; }
                }

                var pos = new Vector3(
                    Random.Range(SpawnerA.x, SpawnerB.x),
                    section.Y + Random.Range(SpawnerB.y, SpawnerA.y),
                    FishBehaviour.FishZ);
                FishBehaviour.Spawn(pos, type, section.Type, section.Root.transform, home);

                var spawnStats = FishConfig.Stats[type];
                if (spawnStats.SpawnCooldownSec > 0f)
                    section.Cooldowns[type] = Mathf.Max(spawnStats.SpawnCooldownSec, section.Cooldowns.GetValueOrDefault(type));
                perSection[type] = perSection.GetValueOrDefault(type) + 1;
                global[type] = global.GetValueOrDefault(type) + 1;
                amount++;
                if (!spawnAll) break;
            }
        }

        /// <summary>section.gd FishRespawnTimer (15 s): add one fish while the section is off screen.</summary>
        void UpdateFishRespawn(float playerY)
        {
            float dt = Time.deltaTime;
            foreach (var section in _sections.Values)
            {
                if (section.Root == null) continue;
                if (section.Cooldowns.Count > 0)
                {
                    var keys = new List<FishType>(section.Cooldowns.Keys);
                    foreach (var k in keys)
                    {
                        float left = section.Cooldowns[k] - dt;
                        if (left <= 0f) section.Cooldowns.Remove(k);
                        else section.Cooldowns[k] = left;
                    }
                }

                section.RespawnTimer -= dt;
                if (section.RespawnTimer > 0f) continue;
                section.RespawnTimer = FishRespawnInterval;
                // VisibleOnScreenNotifier: AABB y ±12.5 around the section; the camera sees about ±8
                bool onScreen = Mathf.Abs(playerY - section.Y) < 12.5f + 8f;
                if (!onScreen) SpawnFish(section, spawnAll: false);
            }
        }

        // ------------------------------------------------------------------ static world

        void BuildStaticWorld()
        {
            // dock3_remesh at (-3.04975, 0.560926, 0.184795)
            var dock = GodotAssets.SpawnModel(_worldRoot, "dock3_remesh", "meshes/dock3_remesh.fbx", "dock3_remesh",
                1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, -3.04975f, 0.560926f, 0.184795f);
            dock.tag = "Dock";

            // Water: QuadMesh (orientation Y, i.e. horizontal) with Shaders/Water.tres
            var water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "Water";
            Object.Destroy(water.GetComponent<Collider>());
            var waterNode = new GameObject("WaterPlane").transform;
            waterNode.SetParent(_worldRoot, false);
            GodotSpace.Apply(waterNode,
                -32f, 0f, 0f, 0f, 0.734f, -1.0079e-06f, 0f, 6.41683e-08f, 11.529f, -14.3034f, 0.00877783f, 3.04191f);
            water.transform.SetParent(waterNode, false);
            // Unity's Quad lies in XY; turn it into the XZ plane facing up like Godot's orientation = 1.
            water.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var waterMat = GodotAssets.Material("water");
            water.GetComponent<Renderer>().sharedMaterial =
                waterMat != null ? waterMat : Materials.Transparent(new Color(0f, 0.55f, 0.65f, 0.7f), glossiness: 0.98f);

            // The big rock ridge behind the surface (main_scene.tscn LeftWall)
            BuildRockWall(_worldRoot, "SurfaceRocks", "veins",
                -0.00814527f, 1.2191f, 0.00407302f, -0.672664f, -0.00789123f, 1.01674f, 1.01673f, 0.0045457f, 0.672698f,
                -13.6118f, -4.41036f, -8.67722f);

            // LeftBarrier / RightBarrier (level_barrier.tscn, invisible): one tall pair for all sections
            foreach (float x in new[] { LeftBarrierX, RightBarrierX })
            {
                var wall = new GameObject("LevelBarrier");
                wall.transform.SetParent(_worldRoot, false);
                wall.transform.position = GodotSpace.Pos(x - 0.026f, -1000f, -0.812f + 0.006f);
                var col = wall.AddComponent<BoxCollider>();
                col.size = new Vector3(1.02258f, 2100f, 4.979f);
            }
        }
    }
}
