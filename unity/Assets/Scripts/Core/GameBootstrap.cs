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
            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.7f, 0.85f, 1f);
            light.intensity = 0.85f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.25f, 0.35f);
        }

        PlayerController BuildPlayer(Transform world)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(world, false);
            go.transform.position = new Vector3(-8f, 0f, 0.33f);
            go.tag = "Player";

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.2f;
            cc.radius = 0.5f;
            cc.center = Vector3.zero;

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(go.transform, false);

            // Submarine body (capsule)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Hull";
            body.transform.SetParent(pivot, false);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = new Vector3(0.7f, 1.1f, 0.7f);
            Object.Destroy(body.GetComponent<Collider>());
            var bm = new Material(Shader.Find("Standard"));
            bm.color = new Color(0.85f, 0.55f, 0.15f);
            bm.SetFloat("_Metallic", 0.4f);
            body.GetComponent<Renderer>().material = bm;

            // Conning tower
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "Tower";
            tower.transform.SetParent(pivot, false);
            tower.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            tower.transform.localScale = new Vector3(0.4f, 0.35f, 0.4f);
            Object.Destroy(tower.GetComponent<Collider>());
            tower.GetComponent<Renderer>().material = bm;

            // Propeller
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            prop.name = "Prop";
            prop.transform.SetParent(pivot, false);
            prop.transform.localPosition = new Vector3(-1.0f, 0f, 0f);
            prop.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            prop.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
            Object.Destroy(prop.GetComponent<Collider>());
            var pm = new Material(Shader.Find("Standard"));
            pm.color = new Color(0.3f, 0.3f, 0.35f);
            prop.GetComponent<Renderer>().material = pm;

            var launch = new GameObject("HarpoonLaunchPoint").transform;
            launch.SetParent(pivot, false);
            launch.localPosition = new Vector3(1.1f, 0f, 0f);

            // Camera
            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.19f, 5.29f);
            camGo.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.65f, 0.9f);
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            // Lamp (optional upgrade visual)
            var lamp = new GameObject("Lamp");
            lamp.transform.SetParent(pivot, false);
            lamp.transform.localPosition = new Vector3(0.9f, 0.1f, 0f);
            var spot = lamp.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 25f;
            spot.spotAngle = 55f;
            spot.intensity = 0f;
            spot.color = new Color(1f, 0.95f, 0.8f);

            var player = go.AddComponent<PlayerController>();
            go.AddComponent<PlayerLamp>();
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

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            GameHUD.Create(canvasGo.transform);
        }
    }

    public class PlayerLamp : MonoBehaviour
    {
        Light _spot;
        void Start() => _spot = GetComponentInChildren<Light>();
        void Update()
        {
            if (_spot == null || GameState.Instance == null) return;
            _spot.intensity = GameState.Instance.GetUpgradeLevel(Upgrade.LampUnlocked) > 0 ? 2.5f : 0f;
        }
    }

    public class BossWatcher : MonoBehaviour
    {
        public Transform WorldRoot;
        void Update() => BossController.TrySpawn(WorldRoot);
    }
}
