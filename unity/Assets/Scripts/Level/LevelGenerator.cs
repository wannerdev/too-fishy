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
        // Sections replaced by the boss section's own sections: index -> background
        readonly Dictionary<int, string> _bossSections = new();
        Transform _bossSectionRoot;
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
            if (!_intro) UpdateSections(player.position.y);
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
            Stage stage = StageOf(index);
            Stage lastStage = index == 0 ? Stage.Surface : StageOf(index - 1);
            string bg = _bossSections.TryGetValue(index, out var bossBg) ? bossBg : BackgroundFor(index, stage, lastStage);
            SpawnSectionAt(index, SectionY(index), stage, lastStage, bg, !_bossSections.ContainsKey(index));
        }

        /// <summary>One scenes/section.tscn instance.</summary>
        void SpawnSectionAt(int key, float y, Stage stage, Stage lastStage, string background, bool barriers, string name = null)
        {
            int index = key;
            var root = new GameObject(name ?? $"Section_{stage}_{Mathf.RoundToInt(-y)}m");
            root.transform.SetParent(_worldRoot, false);
            root.transform.position = new Vector3(0f, y, 0f);
            var state = new SectionState { Root = root, Type = stage, Y = y, RespawnTimer = FishRespawnInterval };
            _sections[index] = state;

            BuildBackground(root.transform, background);

            string veins = stage == Stage.Lava ? "veins_lava" : "veins";
            BuildRockWall(root.transform, "LeftWall", veins,
                0.917378f, 0f, -0.492565f, 0f, 1.36003f, 0f, 0.458728f, 0f, 0.985046f, -30.8771f, 1.37305f, 1.14596f);
            BuildRockWall(root.transform, "LeftWall2", veins,
                -0.840678f, 0f, -0.630943f, 0f, 1.36003f, 0f, 0.5876f, 0f, -0.902689f, 2.58816f, 1.37305f, 1.14596f);

            if (barriers) AddBarrierBoxes(root.transform, index, y, stage, lastStage);
            // main_scene.tscn: FirstSection has particles_enabled = false; Godot never shows them on the web
            if (key != 0 && Application.platform != RuntimePlatform.WebGLPlayer) SectionParticles.Add(root.transform);
            SpawnFish(state, spawnAll: true);
        }

        Stage StageOf(int index) => _bossSections.ContainsKey(index) ? Stage.Void : StageAt(index);

        /// <summary>
        /// level.gd spawnBoss(): scenes/boss_section.tscn 25 m below the lowest section — three
        /// VOID sections (lava, lava, lava→void backgrounds) with the friend, two rocks, two crates
        /// and lava. The extras stay for the rest of the game; the sections stream as usual.
        /// </summary>
        public void SpawnBossSection()
        {
            if (_bossSectionRoot != null) return;
            string[] bgs = { "bg_lava", "bg_lava", "bg_lava_to_void" };
            float topY;
            if (_intro)
            {
                // In the intro lastSpawned is -525, so the boss section starts at -550
                topY = -550f;
                for (int i = 0; i < 3; i++)
                    SpawnSectionAt(IntroBossKey + i, topY - i * SectionHeight, Stage.Void, Stage.Void, bgs[i], false, $"BossSection_{i}");
            }
            else
            {
                var player = GameState.Instance?.PlayerTransform;
                float playerY = player != null ? player.position.y : -500f;
                int top = IndexAt(playerY) + 2;
                for (int i = 0; i < 3; i++)
                {
                    _bossSections[top + i] = bgs[i];
                    if (_sections.TryGetValue(top + i, out var existing))
                    {
                        if (existing.Root != null) Destroy(existing.Root);
                        _sections.Remove(top + i);
                    }
                }
                topY = SectionY(top);
            }

            var root = new GameObject("BossSection").transform;
            root.SetParent(_worldRoot, false);
            root.position = new Vector3(0f, topY, 0f);
            _bossSectionRoot = root;

            Friend.Create(root, new Vector3(-7.19162f, -42.9264f, -1.75951f));

            // StaticBody3D: gestein_v001 rock (walls material under the opaque walls_overlay)
            var rock1 = new GameObject("StaticBody3D").transform;
            rock1.SetParent(root, false);
            GodotSpace.Apply(rock1, -0.0290731f, 0f, -0.999577f, -0.996283f, -0.0811209f, 0.0289773f, -0.0810867f, 0.996704f, 0.00235844f, -4.98166f, -32.2469f, -2.98919f);
            var mesh1 = GodotAssets.SpawnModel(rock1, "GesteinV001", "meshes/gestein_v001.obj", "walls",
                1f, -7.45058e-09f, -1.45519e-10f, 0f, 1f, 2.32831e-10f, 1.71713e-09f, 0f, 1f, -0.018158f, -0.00147867f, 0.000528336f);
            GodotAssets.AddOverlay(mesh1, "walls_overlay");
            RockCollider(rock1, new Vector3(0.745636f, 4.69141f, 1.1996f));

            // StaticBody3D2: two gestein_v003 rocks with walls_overlay as material
            var rock2 = new GameObject("StaticBody3D2").transform;
            rock2.SetParent(root, false);
            GodotSpace.Apply(rock2, 0.890488f, 0.073508f, -0.44903f, -0.447723f, -0.0343101f, -0.893514f, -0.0810867f, 0.996704f, 0.00235844f, -15.6951f, -38.3413f, -2.98919f);
            GodotAssets.SpawnModel(rock2, "GesteinV001", "meshes/gestein_v003.obj", "walls_overlay", null);
            GodotAssets.SpawnModel(rock2, "GesteinV002", "meshes/gestein_v003.obj", "walls_overlay",
                -0.986472f, -0.161673f, 0.0270993f, -0.161543f, 0.986841f, 0.00693622f, -0.0278641f, 0.00246467f, -0.999609f, 4.14006f, 0.675316f, 1.17944f);
            RockCollider(rock2, new Vector3(0.396845f, 4.71446f, -0.160854f));

            // DestroyableBarier ×2 (scene default health 10)
            DestroyableBarrier.Create(root, root.TransformPoint(GodotSpace.Pos(-21.0308f, -35.9612f, -0.33f)), 10, -1);
            DestroyableBarrier.Create(root, root.TransformPoint(GodotSpace.Pos(-20.7442f, -54.631f, -0.33f)), 10, -2);

            // Lava_side and a chain of three lava pools
            LavaZone.Create(root, "Lava_side", true, 1.52204f, 0f, 0f, 0f, 1.40241f, 0f, 0f, 0f, 1f, -5.79982f, -5.23136f, 7.79199f);
            var lava = LavaZone.Create(root, "Lava", false, 10.2079f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, -19.5684f, -4.75327f, 0f);
            var lava2 = LavaZone.Create(lava, "Lava", false, 1.28968f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 1.08293f, -7.2533f, 0f);
            LavaZone.Create(lava2, "Lava", false, 0.661976f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, -0.763632f, -7.2533f, 0f);
        }

        // ------------------------------------------------------------------ intro mission

        const int IntroKey = 100000, IntroBossKey = 200000;
        bool _intro;
        Transform _introRoot;

        /// <summary>
        /// level.gd spawn_intro_mission_sections(): six fixed sections from -400 to -525 m, no
        /// crate barriers and no further sections; a lava ceiling and an invisible wall at -400 m
        /// keep the friend down there, and fish near that wall scatter.
        /// </summary>
        public void StartIntro()
        {
            _intro = true;
            ClearSections();

            (float y, Stage stage, Stage last)[] layout =
            {
                (-400f, Stage.SuperDeep, Stage.SuperDeep), (-425f, Stage.Hot, Stage.SuperDeep), (-450f, Stage.Hot, Stage.Hot),
                (-475f, Stage.Hot, Stage.Hot), (-500f, Stage.Lava, Stage.Hot), (-525f, Stage.Lava, Stage.Lava),
            };
            string[] names = { "PreBarrier_400m", "Transition_425m", "HotZone_450m", "HotZone_475m", "LavaZone_500m", "LavaZone_525m" };
            for (int i = 0; i < layout.Length; i++)
            {
                var (y, stage, last) = layout[i];
                string bg = last == Stage.Hot && stage == Stage.Lava ? "bg_deep_to_lava" : stage >= Stage.Lava ? "bg_lava" : "bg_loop";
                SpawnSectionAt(IntroKey + i, y, stage, last, bg, false, "IntroSection_" + names[i]);
            }
            if (GameState.Instance != null) GameState.Instance.FishesLowerBorder = -525f - SectionHeight / 2f - 1f;

            _introRoot = new GameObject("IntroMission").transform;
            _introRoot.SetParent(_worldRoot, false);
            // LavaWall: lava_side scaled (30, 15, 10) at (-15, -400, 0)
            LavaZone.Create(_introRoot, "LavaWall", true, 30f, 0f, 0f, 0f, 15f, 0f, 0f, 0f, 10f, -15f, -400f, 0f);
            // InvisibleBarrier400m: box 30 × 2 × 10
            var wall = new GameObject("InvisibleBarrier400m");
            wall.transform.SetParent(_introRoot, false);
            wall.transform.localPosition = GodotSpace.Pos(-15f, -400f, 0f);
            wall.AddComponent<BoxCollider>().size = new Vector3(30f, 2f, 10f);
            // InvisibleBarrierScatterArea: box 40 × 60 × 15
            var scatter = new GameObject("InvisibleBarrierScatterArea");
            scatter.transform.SetParent(_introRoot, false);
            scatter.transform.localPosition = GodotSpace.Pos(-15f, -400f, 0f);
            var box = scatter.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(40f, 60f, 15f);
            scatter.AddComponent<IntroScatterArea>();
        }

        /// <summary>
        /// level.gd switch_back_to_original_player(): the intro ends (friend "dies" or the boss
        /// drops to half health). Back to the surface as the real submarine, intro elements and
        /// boss removed, then the rescue dialog and the regular death.
        /// </summary>
        public void SwitchBackToOriginalPlayer() => EndIntro(true);

        /// <summary>cheats.gd skip_intro_mission(): same, without the death that follows the rescue dialog.</summary>
        public void SkipIntro() => EndIntro(false);

        void EndIntro(bool regularDeathAfter)
        {
            var gs = GameState.Instance;
            if (!_intro || gs == null || !gs.IsIntro()) return;
            var player = gs.Player;
            var deathPos = player != null ? player.transform.position : Vector3.zero;
            if (player != null) player.Teleport(GodotSpace.Pos(-8f, 0f, 0.33f));
            gs.SetDepth(0);
            gs.CompleteIntroMission(deathPos);

            // remove_intro_mission_elements()
            _intro = false;
            ClearSections();
            if (_introRoot != null) Destroy(_introRoot.gameObject);
            if (_bossSectionRoot != null) Destroy(_bossSectionRoot.gameObject);
            _bossSectionRoot = null;
            _bossSections.Clear();
            BossController.ResetFlags();

            if (player != null) player.SwitchToNormalSubmarine();
            gs.PendingRegularDeathTransition = regularDeathAfter;
            if (player != null) UpdateSections(player.transform.position.y);
        }

        void ClearSections()
        {
            foreach (var kv in _sections)
                if (kv.Value.Root != null) Destroy(kv.Value.Root);
            _sections.Clear();
        }

        /// <summary>level.gd _on_barrier_scatter_area_entered()</summary>
        class IntroScatterArea : MonoBehaviour
        {
            void OnTriggerEnter(Collider other)
            {
                var fish = other.GetComponentInParent<FishBehaviour>();
                var player = GameState.Instance?.PlayerTransform;
                if (fish != null && player != null) fish.Scatter(player);
            }
        }

        static void RockCollider(Transform body, Vector3 godotCenter)
        {
            var col = body.gameObject.AddComponent<BoxCollider>();
            col.center = GodotSpace.Pos(godotCenter);
            col.size = new Vector3(6.53244f, 9.52234f, 16.658f);
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
                    if (stats.RequiresBossDefeat && !Dialogs.WinReached) continue;
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

            // Level/lava of main_scene.tscn: a lava_side patch near the right wall (~20 m) and a lava
            // column near the left wall (~35-48 m); both hurt
            var lavaRoot = new GameObject("lava").transform;
            lavaRoot.SetParent(_worldRoot, false);
            lavaRoot.localPosition = GodotSpace.Pos(0f, -10f, 3.28338f);
            LavaZone.Create(lavaRoot, "Lava2", true,
                1.38184f, 0f, -0.826246f, 0f, 1.62324f, 0f, 2.02685f, 0f, 0.56331f, -1.95878f, -10.4057f, -2.69743f);
            LavaZone.Create(lavaRoot, "Lava5", false,
                0.580655f, 0f, 0.258595f, 0f, 7f, 0f, -1.35555f, 0f, 0.11077f, -26.5695f, -30.7557f, -3.59032f);

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
