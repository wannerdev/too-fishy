using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scenes/mind_control_zone.tscn + scripts/mind_control_zone.gd: a 0.8 m rift sphere
    /// the boss spawns around the player. While the submarine touches it, it is pulled to the
    /// centre (11 × (1 − d/15) × intensity) and shaken; the pull intensifies by 0.1 every 0.1 s up
    /// to 3.
    /// </summary>
    public class MindControlZone : MonoBehaviour
    {
        const float ZoneRadius = 0.8f, PullStrength = 11f, MaxPullDistance = 15f;
        // Godot asks for 16 upgrade levels, more than exist (max 3 + 2 + 5 = 10); fully upgraded
        // speed and depth resistance escape here.
        const int EscapeThreshold = 10;

        bool _active = true;
        bool _playerInside;
        float _intensity = 1f;
        float _pullTimer;
        float _time;
        Renderer _renderer;
        MaterialPropertyBlock _mpb;

        public static MindControlZone Create(Transform parent, Vector3 position)
        {
            var go = new GameObject("MindControlZone");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, PlayerController.PlayerZ);
            var zone = go.AddComponent<MindControlZone>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "VisualEffect";
            Object.Destroy(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * ZoneRadius * 2f;
            var shader = Resources.Load<Shader>("Shaders/MindControl");
            var r = visual.GetComponent<Renderer>();
            r.sharedMaterial = shader != null ? new Material(shader) : Materials.Transparent(new Color(0.8f, 0.3f, 0.9f, 0.6f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            zone._renderer = r;
            return zone;
        }

        public void Activate()
        {
            _active = true;
            _intensity = 1f;
            _renderer.enabled = true;
        }

        public void Deactivate()
        {
            _active = false;
            _playerInside = false;
            _renderer.enabled = false;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.deltaTime;
            _time += dt;

            var player = GameState.Instance?.Player;
            if (player != null)
            {
                bool inside = TouchesHull(player.transform);
                if (inside && !_playerInside) player.AddTrauma(0.3f); // _on_body_entered
                _playerInside = inside;

                if (inside)
                {
                    _pullTimer += dt;
                    while (_pullTimer >= 0.1f)
                    {
                        _pullTimer -= 0.1f;
                        _intensity = Mathf.Min(_intensity + 0.1f, 3f);
                    }
                    Pull(player, dt);
                }
            }

            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetFloat("_PullStrength", _intensity);
            _mpb.SetFloat("_CustomTime", _time);
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>apply_mind_control_effect()</summary>
        void Pull(PlayerController player, float dt)
        {
            var center = transform.position;
            float distance = Vector3.Distance(player.transform.position, center);
            var gs = GameState.Instance;
            int escape = gs.GetUpgradeLevel(Upgrade.HorSpeed) + gs.GetUpgradeLevel(Upgrade.VertSpeed) + gs.GetUpgradeLevel(Upgrade.DepthResistance);
            if (escape >= EscapeThreshold && distance >= MaxPullDistance) return;

            var dir = center - player.transform.position;
            dir.z = 0f;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
            float factor = Mathf.Max(0f, 1f - distance / MaxPullDistance);
            player.AddExternalForce(dir * PullStrength * factor * _intensity * dt);
            player.AddTrauma(0.02f * _intensity * factor);
        }

        // Area3D sphere (r 0.8) against the submarine's horizontal capsule (r 0.5, length 2.8)
        bool TouchesHull(Transform player)
        {
            var c = player.TransformPoint(GodotSpace.Pos(-0.0102715f, 0.129017f, 0.00199914f));
            const float halfSegment = 2.79761f / 2f - 0.5f;
            var a = c + Vector3.left * halfSegment;
            var b = c + Vector3.right * halfSegment;
            var p = transform.position;
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            var closest = a + ab * t;
            var d = p - closest;
            d.z = 0f;
            return d.magnitude <= ZoneRadius + 0.5f;
        }
    }
}
