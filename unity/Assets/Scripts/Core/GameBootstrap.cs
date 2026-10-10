using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

            // Ensure tags exist at runtime for built player (editor has TagManager)
            EnsureTags();

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

        void EnsureTags()
        {
            // Tags must be defined in TagManager; runtime Create doesn't add them.
            // Fish/Boss detection also uses GetComponent fallbacks.
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
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.2f;
            cc.radius = 0.5f;
            cc.center = GodotSpace.Pos(-0.0102715f, 0.129017f, 0.00199914f);

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
            pickaxe.gameObject.SetActive(false);

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
            return player;
        }

        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            GameHUD.Create(canvasGo.transform);
            if (GameInput.TouchMode)
                TouchControls.Create(canvasGo.transform);
        }
    }

    /// <summary>Shows upgrade-gated parts of the submarine (player.gd process_dock).</summary>
    public class PlayerUpgradeVisuals : MonoBehaviour
    {
        public Light Lamp;
        public GameObject Pickaxe;

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            if (Lamp != null) Lamp.enabled = gs.GetUpgradeLevel(Upgrade.LampUnlocked) > 0;
            if (Pickaxe != null) Pickaxe.SetActive(gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0);
        }
    }

    public class BossWatcher : MonoBehaviour
    {
        public Transform WorldRoot;
        void Update() => BossController.TrySpawn(WorldRoot);
    }
}
