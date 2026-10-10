using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/camera.gd: per-stage fog colour (1 s tween) and sun energy (3 s tween),
    /// fog only below the surface, and the FOV adjustment for non-16:9 screens.
    /// </summary>
    public class UnderwaterCamera : MonoBehaviour
    {
        public static Light Sun;

        // Environment of scenes/player.tscn
        const float FogDensity = 0.0476f;

        const float ReferenceAspect = 1920f / 1080f;
        const float BaseFov = 75f, MinFov = 65f, MaxFov = 85f;

        Stage _lastStage = (Stage)(-1);
        Color _fogFrom, _fogTo;
        float _lightFrom, _lightTo;
        float _fogT = 1f, _lightT = 1f;
        Camera _cam;
        float _lastAspect;

        void Start()
        {
            _cam = GetComponent<Camera>();
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = _fogTo = _fogFrom = StageColor(Stage.Surface);
            _lightFrom = _lightTo = Sun != null ? Sun.intensity : 1f;
        }

        void LateUpdate()
        {
            AdjustFov();

            var gs = GameState.Instance;
            if (gs == null) return;

            RenderSettings.fog = transform.position.y <= -0.2f;

            var stage = gs.IsIntro() ? Stage.Hot : gs.PlayerInStage;
            if (_lastStage != stage)
            {
                _lastStage = stage;
                _fogFrom = RenderSettings.fogColor;
                _fogTo = StageColor(stage);
                _fogT = 0f;
                _lightFrom = Sun != null ? Sun.intensity : 1f;
                _lightTo = StageLight(stage);
                _lightT = 0f;
            }

            if (_fogT < 1f)
            {
                _fogT = Mathf.Min(1f, _fogT + Time.deltaTime / 1f);
                RenderSettings.fogColor = Color.Lerp(_fogFrom, _fogTo, _fogT);
            }
            if (_lightT < 1f && Sun != null)
            {
                _lightT = Mathf.Min(1f, _lightT + Time.deltaTime / 3f);
                Sun.intensity = Mathf.Lerp(_lightFrom, _lightTo, _lightT);
            }
        }

        void AdjustFov()
        {
            if (_cam == null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;

            float fov = BaseFov;
            if (aspect > ReferenceAspect)
                fov = BaseFov * Mathf.Min(1.2f, aspect / ReferenceAspect);
            else if (aspect < ReferenceAspect)
                fov = BaseFov * Mathf.Max(0.9f, 2f - ReferenceAspect / aspect);
            _cam.fieldOfView = Mathf.Clamp(fov, MinFov, MaxFov);
        }

        // camera.gd environment_color_map
        static Color StageColor(Stage stage) => stage switch
        {
            Stage.Surface or Stage.Deep => Rgb(30, 110, 163),
            Stage.Deeper or Stage.SuperDeep => Rgb(12, 59, 94),
            Stage.Hot or Stage.Lava => Rgb(217, 103, 4),
            _ => Rgb(10, 10, 10)
        };

        // camera.gd environment_light_map
        static float StageLight(Stage stage) => stage switch
        {
            Stage.Surface => 1f,
            Stage.Deep => 0.8f,
            Stage.Deeper => 0.5f,
            Stage.SuperDeep => 0.1f,
            Stage.Hot => 0.2f,
            Stage.Lava => 1f,
            _ => 0.1f
        };

        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    }
}
