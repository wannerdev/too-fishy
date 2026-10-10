using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// The Bubbles and Debris GPUParticles3D of scenes/section.tscn: rising bubbles and slowly
    /// sinking specks in a box around the section. Godot turns them off on the web and when
    /// "Show Particles" is off; phones get the reduced counts Godot uses for WebGL (50 / 100).
    /// </summary>
    public class SectionParticles : MonoBehaviour
    {
        ParticleSystem _bubbles, _debris;

        public static void Add(Transform section)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(section, false);
            var sp = go.AddComponent<SectionParticles>();

            bool mobile = Application.isMobilePlatform;
            // Node transform scale (2.655, 3.31466, 1.308) at (-11.9884, -1.03899, 3.67558),
            // emission box extents (6.595, 4.96, 3) offset (0, 0, -3.275)
            var center = GodotSpace.Pos(-11.9884f, -1.03899f, 3.67558f + 1.308f * -3.275f);
            var size = new Vector3(6.595f * 2.655f, 4.96f * 3.31466f, 3f * 1.308f) * 2f;

            sp._bubbles = Make(go.transform, "Bubbles", center, size, mobile ? 50 : 400, 3f,
                new Color(1f, 1f, 1f, 0.517647f), 0f, 0.05f, new Vector3(0f, 0.5f, 0f), 1f, 2f);
            sp._debris = Make(go.transform, "Debris", center, size, mobile ? 100 : 5000, mobile ? 2f : 3f,
                new Color(1f, 1f, 1f, 0.552941f), 0.007f, 0.014f, new Vector3(0f, -1f, 0f), 0f, 0f);
            var noise = sp._debris.noise;
            noise.enabled = true; // turbulence_enabled
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            sp.Apply();
        }

        static ParticleSystem Make(Transform parent, string name, Vector3 center, Vector3 size, int amount, float lifetime,
            Color color, float sizeMin, float sizeMax, Vector3 gravity, float speedMin, float speedMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.85f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color;
            main.maxParticles = amount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var emission = ps.emission;
            emission.rateOverTime = amount / lifetime;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            shape.rotation = new Vector3(-90f, 0f, 0f); // emit along +Y (rising bubbles)
            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space = ParticleSystemSimulationSpace.World;
            force.x = 0f;
            force.y = gravity.y;
            force.z = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = name == "Debris" ? ProceduralMeshes.Prism : Effects.Sphere;
            r.sharedMaterial = Effects.ParticleMaterial;
            ps.Play();
            return ps;
        }

        bool _shown = true;

        void Update()
        {
            if (Settings.ShowParticles != _shown) Apply();
        }

        void Apply()
        {
            _shown = Settings.ShowParticles;
            foreach (var ps in new[] { _bubbles, _debris })
            {
                if (ps == null) continue;
                ps.gameObject.SetActive(_shown);
            }
        }
    }
}
