using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Builds the entire playable game at runtime so the project works
    /// without hand-authored prefabs. Attach to an empty GameObject in Main.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("If true, start as the friend intro dive in the Hot zone.")]
        public bool EnableIntroMission = false;

        void Awake()
        {
            // Phones and WebGL default to 30 fps otherwise
            Application.targetFrameRate = 60;

            Layers.Configure();
            Settings.Load();
            Dialogs.Reset();
            Achievements.Reset();
            BossController.ResetFlags();

            var gsGo = new GameObject("GameState");
            var gs = gsGo.AddComponent<GameState>();
            gs.EnableIntroMission = EnableIntroMission;

            var world = new GameObject("World").transform;

            BuildLighting();
            var player = BuildPlayer(world);
            gs.Player = player;
            gs.PlayerTransform = player.transform;

            var levelGo = new GameObject("Level");
            levelGo.transform.SetParent(world, false);
            var level = levelGo.AddComponent<LevelGenerator>();
            level.Initialize(world);

            BuildUI();

            var camFx = player.GetComponentInChildren<Camera>();
            if (camFx != null)
                camFx.gameObject.AddComponent<UnderwaterCamera>();

            new GameObject("Audiomanager").AddComponent<MusicPlayer>();

            // Boss watcher
            var watcher = new GameObject("BossWatcher").AddComponent<BossWatcher>();
            watcher.WorldRoot = world;
        }

        void BuildLighting()
        {
            // DirectionalLight3D of scenes/main_scene.tscn
            var lightGo = new GameObject("DirectionalLight3D");
            GodotSpace.Apply(lightGo.transform,
                0.94702f, 0.320556f, -0.0199305f, -0.0267635f, 0.140602f, 0.989704f, 0.320058f, -0.936736f, 0.141732f,
                -6.47082f, 1.04463f, 15.1856f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            UnderwaterCamera.Sun = light;

            // Environment of scenes/player.tscn: panorama sky as background and ambient source.
            var sky = GodotAssets.Material("sky");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                DynamicGI.UpdateEnvironment();
            }
            else
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.105882f, 0.203922f, 0.647059f);
            }
        }

        PlayerController BuildPlayer(Transform world)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(world, false);
            go.transform.position = GodotSpace.Pos(-8f, 0f, 0.33f);
            go.tag = "Player";

            // Godot uses a horizontal capsule (height 2.8, radius 0.5); a CharacterController is
            // always upright, so it approximates the hull with a sphere-ish capsule.
            go.layer = Layers.Player;
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.2f;
            cc.radius = 0.5f;
            cc.center = GodotSpace.Pos(-0.0102715f, 0.129017f, 0.00199914f);

            // The real hull of player.tscn: capsule height 2.79761, radius 0.5, lying along X.
            // Fish get pushed out of it (FishBehaviour.ResolveCollisions).
            var hull = new GameObject("Hull");
            hull.layer = Layers.Player;
            hull.transform.SetParent(go.transform, false);
            var hullCol = hull.AddComponent<CapsuleCollider>();
            hullCol.isTrigger = true;
            hullCol.direction = 0;
            hullCol.height = 2.79761f;
            hullCol.radius = 0.5f;
            hullCol.center = GodotSpace.Pos(-0.0102715f, 0.129017f, 0.00199914f);

            // ScatterArea: box 8 × 9.12 × 8 centred 3.9 below the player (top edge 0.66 above it)
            var scatter = new GameObject("ScatterArea");
            scatter.layer = Layers.PlayerSensor;
            scatter.transform.SetParent(go.transform, false);
            scatter.transform.localPosition = GodotSpace.Pos(0f, -2.20848f, 0f);
            var scatterShape = new GameObject("CollisionShape3D");
            scatterShape.layer = Layers.PlayerSensor;
            scatterShape.transform.SetParent(scatter.transform, false);
            GodotSpace.Apply(scatterShape.transform, 7.99981f, 0.0550123f, 0f, -0.0550123f, 7.99981f, 0f, 0f, 0f, 8f, 0.0548361f, -1.6927f, 0.0304167f);
            var scatterBox = scatterShape.AddComponent<BoxCollider>();
            scatterBox.isTrigger = true;
            scatterBox.size = new Vector3(1f, 1.13977f, 1f);
            scatterShape.AddComponent<ScatterArea>().Player = go.transform;

            // scenes/player.tscn: Pivot (scale 20) > SmFishSubmarine (scale 0.01) = 0.2 overall
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(go.transform, false);
            GodotSpace.Apply(pivot, 20f, 0.00383925f, 0f, -0.00383925f, 20f, 0f, 0f, 0f, 20f, 0f, 0.155944f, 0f);

            var launch = new GameObject("HarpoonLaunchPoint").transform;
            launch.SetParent(pivot, false);
            GodotSpace.Apply(launch, 0.01f, 0f, 0f, 0f, 0.01f, 0f, 0f, 0f, 0.01f, 0.00037846f, 0.000150716f, -3.20524e-05f);

            var sub = GodotAssets.SpawnModel(pivot, "SmFishSubmarine", "meshes/SM_FishSubmarine_FINAL.obj", "submarine",
                0.01f, 0f, 0f, 0f, 0.01f, 0f, 0f, 0f, 0.01f, 0.00037846f, 0.000150717f, -3.20524e-05f);

            // Hand > pickaxe > Pivot > MeshInstance3D (scenes/pickaxe.tscn), shown with the upgrade
            var hand = new GameObject("Hand").transform;
            hand.SetParent(sub, false);
            GodotSpace.Apply(hand, 4.96379f, 0.600684f, 0f, -0.600684f, 4.96379f, 0f, 0f, 0f, 5f, 3.76286f, -2.57438f, 1.55206f);
            var pickaxe = new GameObject("pickaxe").transform;
            pickaxe.SetParent(hand, false);
            GodotSpace.Apply(pickaxe, 0.932009f, 0.362438f, 0f, -0.362438f, 0.932009f, 0f, 0f, 0f, 1f, -0.00366241f, 0.0302158f, 0f);
            var pickPivot = new GameObject("Pivot").transform;
            pickPivot.SetParent(pickaxe, false);
            pickPivot.localPosition = GodotSpace.Pos(0.319393f, 0f, 0f);
            GodotAssets.SpawnModel(pickPivot, "PickaxeMesh", "meshes/SM_Pickaxe.obj", "assets",
                -1.70474e-08f, 0.39f, 0f, 1.70474e-08f, 7.45167e-16f, -0.39f, -0.39f, -1.70474e-08f, -1.70474e-08f, 0f, 0f, 0f);
            var pickaxeTool = pickaxe.gameObject.AddComponent<Pickaxe>();
            pickaxe.gameObject.SetActive(false);

            // AK47s (scenes/ak47.tscn, scenes/ak_47_.tscn), shown at the dock once bought
            var ak = SpawnGun(sub, "ak47_0406195124_texture", "meshes/ak47_0406195124_texture.fbx", "ak47", true,
                -2.5f, 0f, 2.18557e-07f, 0f, 2.5f, 0f, -2.18557e-07f, 0f, -2.5f, 2.13988f, -0.0920478f, 2.07124f);
            var ak2 = SpawnGun(sub, "ak47_", "meshes/ak47_texture.fbx", "ak47_second", false,
                -2.5f, 3.20142e-10f, -2.18557e-07f, 4.36557e-10f, 2.5f, 3.1225e-17f, 2.18557e-07f, 0f, -2.5f, 2.10641f, -0.0983091f, -1.93976f);

            // UnlockableLamp (SpotLight3D, hidden until the lamp upgrade)
            var lamp = new GameObject("UnlockableLamp");
            lamp.transform.SetParent(sub, false);
            GodotSpace.Apply(lamp.transform,
                -4.37114e-08f, 0.207912f, -0.978148f, 0f, 0.978148f, 0.207912f, 1f, 9.08811e-09f, -4.27562e-08f,
                5.34924f, 1.78326f, 0.00318527f);
            var spot = lamp.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 37.798f;
            spot.spotAngle = 28.444f * 2f; // Godot spot_angle is the half angle
            spot.intensity = 4f;
            spot.color = Color.white;
            spot.enabled = false;

            // Camera3D: child of the player at (0, 1.18841, 5.28607), no rotation, fov 75
            var camGo = new GameObject("Camera3D");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = GodotSpace.Pos(0f, 1.18841f, 5.28607f);
            camGo.transform.localRotation = Quaternion.identity;
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.65f, 0.9f);
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            var player = go.AddComponent<PlayerController>();
            var upgrades = go.AddComponent<PlayerUpgradeVisuals>();
            upgrades.Lamp = spot;
            upgrades.Pickaxe = pickaxe.gameObject;
            upgrades.Ak47 = ak.Model;
            upgrades.Ak47Second = ak2.Model;
            player.PickaxeTool = pickaxeTool;

            // PopupSpawnPosition (player-local 0.153614, 0.666139, 0)
            var popup = new GameObject("PopupSpawnPosition").transform;
            popup.SetParent(go.transform, false);
            popup.localPosition = GodotSpace.Pos(0.153614f, 0.666139f, 0f);
            player.PopupSpawn = popup;
            return player;
        }

        static Ak47 SpawnGun(Transform sub, string name, string model, string material, bool primary, params float[] godot)
        {
            var node = GodotAssets.SpawnModel(sub, name, model, material, godot);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(node, false);
            muzzle.localPosition = GodotSpace.Pos(-0.982369f, 0.228667f, 0.0308859f);
            var popup = new GameObject("PopupSpawnPosition").transform;
            popup.SetParent(node, false);
            popup.localPosition = GodotSpace.Pos(0.0282827f, 0.666139f, 0f);
            var gun = sub.gameObject.AddComponent<Ak47>();
            gun.Primary = primary;
            gun.Muzzle = muzzle;
            gun.PopupSpawn = popup;
            gun.Model = node.gameObject;
            node.gameObject.SetActive(false);
            return gun;
        }

        void BuildUI() => GameUI.Create();
    }

    /// <summary>
    /// Upgrade-gated parts of the submarine. player.gd process_dock() reveals the lamp and the
    /// guns when docking after buying them; the pickaxe shows as soon as it is owned (pickaxe.gd).
    /// </summary>
    public class PlayerUpgradeVisuals : MonoBehaviour
    {
        public Light Lamp;
        public GameObject Pickaxe, Ak47, Ak47Second;

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            bool lamp = gs.GetUpgradeLevel(Upgrade.LampUnlocked) > 0;
            bool ak = gs.GetUpgradeLevel(Upgrade.Ak47) > 0;
            bool ak2 = gs.GetUpgradeLevel(Upgrade.DualAk47) > 0;

            if (gs.IsDocked)
            {
                if (lamp && Lamp != null) Lamp.enabled = true;
                if (ak && Ak47 != null) Ak47.SetActive(true);
                if (ak2 && Ak47Second != null) Ak47Second.SetActive(true);
            }
            // Upgrades can also be taken away (end of the intro mission)
            if (!lamp && Lamp != null) Lamp.enabled = false;
            if (!ak && Ak47 != null) Ak47.SetActive(false);
            if (!ak2 && Ak47Second != null) Ak47Second.SetActive(false);
            if (Pickaxe != null) Pickaxe.SetActive(gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0);
        }
    }

    /// <summary>player.gd scatter_area_entered(): a fish entering the area darts away.</summary>
    public class ScatterArea : MonoBehaviour
    {
        public Transform Player;

        void OnTriggerEnter(Collider other)
        {
            var fish = other.GetComponentInParent<FishBehaviour>();
            if (fish != null && Player != null) fish.Scatter(Player);
        }
    }

    public class BossWatcher : MonoBehaviour
    {
        public Transform WorldRoot;
        void Update() => BossController.TrySpawn(WorldRoot);
    }
}
