using UnityEngine;

namespace TooFishy
{
    /// <summary>Godot GPU/CPU particle effects rebuilt with Unity's ParticleSystem.</summary>
    public static class Effects
    {
        static Material _particleMat;
        static Mesh _sphere;

        /// <summary>Unlit vertex-coloured transparent material (Resources/Shaders/ParticleUnlit).</summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMat != null) return _particleMat;
                var shader = Resources.Load<Shader>("Shaders/ParticleUnlit");
                _particleMat = shader != null ? new Material(shader) : Materials.Transparent(Color.white);
                return _particleMat;
            }
        }

        public static Mesh Sphere
        {
            get
            {
                if (_sphere != null) return _sphere;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = go.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(go);
                return _sphere;
            }
        }

        public struct Burst
        {
            public int Amount;
            public float Lifetime;
            public float SpeedMin, SpeedMax;
            public float Spread;          // degrees around +Y (Godot direction (0,1,0))
            public float EmitRadius;
            public Vector3 Gravity;
            public float Size;            // mesh diameter in world units
            public float SizeMin, SizeMax; // Godot scale_min/max
            public Color Color;
        }

        /// <summary>A one-shot burst (Godot one_shot + explosiveness) that cleans itself up.</summary>
        public static ParticleSystem SpawnBurst(Vector3 position, Burst b, Transform parent = null)
        {
            var go = new GameObject("Particles");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = b.Lifetime;
            main.loop = false;
            main.startLifetime = b.Lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(b.SpeedMin, b.SpeedMax);
            float sMin = b.SizeMin > 0 ? b.SizeMin : 1f, sMax = b.SizeMax > 0 ? b.SizeMax : 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(b.Size * sMin, b.Size * sMax);
            main.startColor = b.Color;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = b.Amount;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)b.Amount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = Mathf.Clamp(b.Spread, 0f, 89f);
            shape.radius = Mathf.Max(0.0001f, b.EmitRadius);
            shape.rotation = new Vector3(-90f, 0f, 0f); // cone along +Y

            if (b.Gravity != Vector3.zero)
            {
                var force = ps.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = b.Gravity.x;
                force.y = b.Gravity.y;
                force.z = -b.Gravity.z;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Sphere;
            r.sharedMaterial = ParticleMaterial;
            ps.Play();
            return ps;
        }

        /// <summary>
        /// scenes/catch_effect.tscn + catch_effect.gd: 12 bubbles and a fading omni light, blue
        /// normally and gold for shiny fish, with bup.wav (harp3.wav for shiny — Godot meant to
        /// play it but set the flag too late).
        /// </summary>
        public static void CatchEffect(Vector3 fishPosition, bool shiny)
        {
            var color = shiny ? new Color(1f, 0.9f, 0.2f) : new Color(0.2f, 0.7f, 1f);
            // Keep it in front of the section background: z = max(fish.z + 0.08, -0.2) in Godot
            var pos = new Vector3(fishPosition.x, fishPosition.y, Mathf.Min(fishPosition.z - 0.08f, 0.2f));
            var burstColor = color;
            burstColor.a = 0.7f;
            SpawnBurst(pos, new Burst
            {
                Amount = 12, Lifetime = 1f, SpeedMin = 1f, SpeedMax = 3f, Spread = 30f, EmitRadius = 0.2f,
                Size = 0.2f, SizeMin = 0.3f, SizeMax = 0.7f, Color = burstColor
            });

            var lightGo = new GameObject("CatchLight");
            lightGo.transform.position = pos;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 3f;
            light.intensity = shiny ? 3f : 2f;
            lightGo.AddComponent<FadeLight>().Duration = 0.5f;
            Object.Destroy(lightGo, 2f);

            if (shiny) SoundPlayer.Play("harp3", 0.8f, 1.26f);
            else SoundPlayer.Play("bup", Random.Range(0.9f, 1.1f));
        }

        /// <summary>player.tscn SurfaceBuoyEffect: 25 blue spheres shooting up and falling back.</summary>
        public static void BuoyEffect(Vector3 position)
        {
            SpawnBurst(position, new Burst
            {
                Amount = 25, Lifetime = 1.8f, SpeedMin = 3f, SpeedMax = 8f, Spread = 40f,
                Gravity = new Vector3(0f, -5f, 0f), Size = 0.1f, Color = new Color(0.141176f, 0.372549f, 1f)
            });
        }

        class FadeLight : MonoBehaviour
        {
            public float Duration = 0.5f;
            Light _light;
            float _start, _t;

            void Start()
            {
                _light = GetComponent<Light>();
                _start = _light.intensity;
            }

            void Update()
            {
                _t += Time.deltaTime;
                _light.intensity = Mathf.Lerp(_start, 0f, _t / Duration);
            }
        }
    }
}
