using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/fish.gd with the fish scenes of scenes/mobs (model, material, collision).
    /// A fish faces right (+X) with no yaw and left with a 180° yaw; it swims along its nose,
    /// tilted by an angle around Z (positive = up), and turns around when it hits the play area
    /// border.
    /// </summary>
    public class FishBehaviour : MonoBehaviour
    {
        public FishType Type { get; private set; }
        public float Weight { get; private set; }
        public int Price { get; private set; }
        public bool IsShiny { get; private set; }
        /// <summary>Section that spawned this fish (fish.gd <c>home</c>).</summary>
        public int Home { get; private set; }

        const float MinAngle = -30f, MaxAngle = 30f;
        const float RotationCooldown = 0.1f;
        /// <summary>Godot fish live at z = -0.3.</summary>
        public const float FishZ = 0.3f;
        // Inner faces of the LeftBarrier / RightBarrier walls of scenes/section.tscn
        const float AreaMinX = -29.43f, AreaMaxX = -0.53f;

        // fish.gd shader animation rate
        const float BaseAnimRate = 0.5f, SpeedToAnimRate = 1.25f, MinAnimRate = 0.2f, MaxAnimRate = 1.6f;

        float _speed;
        bool _facingLeft;
        float _angle;
        float _rotationCdLeft;
        float _animTime;
        Vector3 _velocity;
        Renderer[] _animated;
        MaterialPropertyBlock _mpb;
        int _id;
        static int _nextId = 1;
        static readonly int AnimTimeId = Shader.PropertyToID("_AnimTime");

        public static FishBehaviour Spawn(Vector3 pos, FishType type, Stage stage, Transform parent, int home = 0)
        {
            var stats = FishConfig.Stats[type];
            var section = FishConfig.Sections[stage];

            var go = new GameObject($"Fish_{type}");
            go.tag = "Fish";
            go.layer = Layers.Fish;
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(pos.x, pos.y, FishZ);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var fish = go.AddComponent<FishBehaviour>();
            fish.BuildVisual(type);

            bool shiny = Random.value < section.ShinyRate;
            // fish.gd: `weight` is an int export, so the clamped random weight is truncated.
            int weight = (int)Mathf.Clamp(
                Random.Range(stats.WeightMin, stats.WeightMax) * section.WeightMultiplier,
                stats.WeightMin, stats.WeightMax);
            int price = Mathf.RoundToInt(weight * stats.PriceWeightMultiplier);
            if (shiny) price *= 3;

            // fish.gd get_scale_for_weight()
            float normalWeight = (stats.WeightMax - stats.WeightMin) / 2f;
            float factor = normalWeight > 0f ? (weight - stats.WeightMin) / normalWeight : 0f;
            float s = 1f + factor * 0.3f;
            go.transform.localScale = new Vector3(s, s, 1f);

            fish.Type = type;
            fish.Weight = weight;
            fish.Price = price;
            fish.IsShiny = shiny;
            fish.Home = home;
            fish._speed = Random.Range(stats.SpeedMin, stats.SpeedMax);
            fish._id = _nextId++;
            fish._animTime = Random.value * 2f * Mathf.PI;
            fish.SetAngle(Random.Range(MinAngle, MaxAngle));
            if (shiny) fish.AddShinyParticles();
            return fish;
        }

        /// <summary>inventory.gd release_fish(): put a caught fish back, scattering away from the sub.</summary>
        public static FishBehaviour SpawnReleased(InventoryItem item, Vector3 pos, Transform parent, bool scatter = true)
        {
            var fish = Spawn(pos, item.Type, Stage.Surface, parent);
            fish.Weight = item.Weight;
            fish.Price = item.Price;
            if (item.Shiny && !fish.IsShiny) fish.AddShinyParticles();
            fish.IsShiny = item.Shiny;
            var player = GameState.Instance?.PlayerTransform;
            if (scatter && player != null) fish.Scatter(player);
            return fish;
        }

        /// <summary>Model, material and collision shape from the Godot fish scene of each type.</summary>
        void BuildVisual(FishType type)
        {
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);
            Transform model = null;

            switch (type)
            {
                case FishType.Flamy: // scenes/mobs/BasicFishA.tscn
                case FishType.Greeny: // scenes/mobs/BasicFishB.tscn
                    pivot.localScale = Vector3.one * 0.15f;
                    model = GodotAssets.SpawnModel(pivot, "SmBasicFish",
                        type == FishType.Flamy ? "meshes/SM_Fish_A.obj" : "meshes/SM_Fish_B.obj", "fish_a_animated",
                        -1f, 0f, -8.74228e-08f, 0f, 1f, 0f, 8.74228e-08f, 0f, -1f, 0f, 0f, 0f);
                    _animated = model.GetComponentsInChildren<Renderer>();
                    AddCapsule(0.211779f, 0.978053f, type == FishType.Flamy
                        ? GodotSpace.Pos(-0.00279042f, -0.0233175f, 0f)
                        : GodotSpace.Pos(-0.00212356f, 0.0263436f, 0f));
                    break;

                case FishType.Angler: // scenes/mobs/AnglerFish.tscn
                    pivot.localScale = Vector3.one * 0.3f;
                    model = GodotAssets.SpawnModel(pivot, "SmAnglerFish", "meshes/SM_AnglerFish.obj", "fishes",
                        -0.7f, 0f, -1.05697e-07f, 0f, 0.7f, 0f, 1.05697e-07f, 0f, -0.7f, 0f, 0.559967f, 0f);
                    var lure = new GameObject("OmniLight3D");
                    lure.transform.SetParent(model, false);
                    lure.transform.localPosition = GodotSpace.Pos(-2.95979f, 1.79049f, 0f);
                    var light = lure.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 1f, 0.639216f);
                    light.intensity = 2.5f;
                    light.range = 2.2f;
                    AddCapsule(0.395173f, 1.53551f, GodotSpace.Pos(-0.0879799f, 0.118642f, 0f));
                    break;

                case FishType.Spikey: // scenes/mobs/spikey_fish.tscn
                    pivot.localPosition = GodotSpace.Pos(0.118526f, 0f, 0f);
                    GodotAssets.SpawnModel(pivot, "Spiky_remesh", "meshes/Spiky_remesh.fbx", "Spiky_remesh",
                        -0.00335101f, 0f, 0.999994f, 0f, 1f, 0f, -0.999994f, 0f, -0.00335101f, -0.118526f, 0f, 0f);
                    AddCapsule(0.5f, 2f, GodotSpace.Pos(0.809324f, 0f, 0f));
                    break;

                case FishType.BossMini: // scenes/mobs/boss_mini_fish.tscn
                    pivot.localScale = Vector3.one * 0.085f;
                    GodotAssets.SpawnModel(pivot, "MeshInstance3D", "meshes/SM_Blobert.obj", "boss_mini",
                        -0.7f, 0.000280873f, -7.17421e-06f, 0.000280964f, 0.699772f, -0.017872f, 7.81212e-10f, -0.017872f, -0.699772f, 0f, 0f, 0f);
                    var sphere = gameObject.AddComponent<SphereCollider>();
                    sphere.radius = 0.13f;
                    break;

                default: // FishType.Smally: scenes/mobs/dummy_fish.tscn, built from primitives in Godot too
                    pivot.localScale = Vector3.one * 0.2f;
                    DummyFishParts.Build(pivot);
                    AddCapsule(0.105422f, 0.395723f, GodotSpace.Pos(-0.0372254f, 0f, 0f));
                    break;
            }
        }

        // Godot fish capsules are rotated 90° around Z, i.e. they lie along X.
        void AddCapsule(float radius, float height, Vector3 center)
        {
            var c = gameObject.AddComponent<CapsuleCollider>();
            c.direction = 0;
            c.radius = radius;
            c.height = height;
            c.center = center;
        }

        void AddShinyParticles()
        {
            // materials/mobs/ShinyParticles.tres with the shiny colour set in fish.gd
            var go = new GameObject("ShinyParticles");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1.5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1f);
            main.startSize = 0.02f;
            main.startColor = new Color(1f, 0.9f, 0.2f, 0.55f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 200f / 1.5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            shape.scale = new Vector3(0.5f, 0.3f, 0.3f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.25f, 1f), new Keyframe(0.75f, 0.67f), new Keyframe(1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = ProceduralMeshes.Prism;
            r.sharedMaterial = Materials.Emissive(new Color(0.864675f, 0.601447f, 0f), new Color(0.517711f, 0.434439f, 0f) * 10f);
            ps.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_rotationCdLeft > 0f) _rotationCdLeft -= dt;

            // fish.gd: any slide collision of the last move (walls, crates, other fish, the
            // submarine) turns the fish around, at most every 0.1 s.
            if (_collided && _rotationCdLeft <= 0f)
            {
                _rotationCdLeft = RotationCooldown;
                _facingLeft = !_facingLeft;
                SetAngle(Random.Range(MinAngle, MaxAngle));
            }
            _collided = false;

            var p = transform.position;
            if (p.y >= -0.5f)
            {
                Achievements.RecordSurface(Type); // fish.gd record_surface_achievement()
                Destroy(gameObject);
                return;
            }

            float lower = GameState.Instance != null ? GameState.Instance.FishesLowerBorder : -30f;
            if (p.y >= -0.75f)
            {
                if (IsLookingUp) SetAngle(Random.Range(MinAngle, MinAngle / 2f));
            }
            else if (p.y <= lower)
            {
                if (!IsLookingUp) SetAngle(Random.Range(MaxAngle / 2f, MaxAngle));
            }
            else if (Random.value < 0.004f)
                SetAngle(Random.Range(MinAngle, MaxAngle));

            p += _velocity * dt;
            p.z = FishZ;

            // Safety net in case a fish slips past the play-area walls
            if ((p.x < AreaMinX && _velocity.x < 0f) || (p.x > AreaMaxX && _velocity.x > 0f))
            {
                p.x = Mathf.Clamp(p.x, AreaMinX, AreaMaxX);
                _collided = true;
            }
            transform.position = p;
            ResolveCollisions();

            // Godot keeps every fish until it is caught or surfaces; despawn far-away ones so
            // the streamed level does not accumulate them.
            var player = GameState.Instance?.PlayerTransform;
            if (player != null && Mathf.Abs(p.y - player.position.y) > 130f)
            {
                Destroy(gameObject);
                return;
            }

            UpdateShaderAnimation(dt);
        }

        bool IsLookingUp => _angle > 0f;

        bool _collided;
        Collider _collider;
        static readonly Collider[] Overlaps = new Collider[16];
        const int CollisionMask = (1 << Layers.Default) | (1 << Layers.World) | (1 << Layers.Fish) | (1 << Layers.Player);

        /// <summary>
        /// The depenetration part of Godot's move_and_slide(): the fish is pushed out of anything it
        /// overlaps. Because the submarine's hull moves into fish (it does not stop at them), this
        /// is what lets the player push a fish around — e.g. up to the surface, which counts for
        /// the "surfaced" achievement. Spikey fish hurt the submarine on contact.
        /// </summary>
        void ResolveCollisions()
        {
            if (_collider == null) _collider = GetComponent<Collider>();
            if (_collider == null) return;

            var pos = transform.position;
            var rot = transform.rotation;
            int n = Physics.OverlapSphereNonAlloc(pos, 3f, Overlaps, CollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var other = Overlaps[i];
                if (other == null || other.transform.IsChildOf(transform)) continue;
                bool hull = other.gameObject.layer == Layers.Player && other is CapsuleCollider;
                if (other.isTrigger && !hull) continue;
                if (other is CharacterController) continue;

                if (!Physics.ComputePenetration(_collider, pos, rot, other, other.transform.position, other.transform.rotation,
                        out var dir, out float dist))
                    continue;

                dir.z = 0f;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.up;
                pos += dir.normalized * dist;
                _collided = true;

                if (hull && Type == FishType.Spikey)
                    GameState.Instance?.Player?.Hurt(5); // player.gd collision(): spikey fish hurt
            }
            pos.z = FishZ;
            transform.position = pos;
        }

        /// <summary>fish.gd set_z_rotation_and_velocity().</summary>
        void SetAngle(float deg)
        {
            _angle = deg;
            float rad = deg * Mathf.Deg2Rad;
            _velocity = new Vector3(Mathf.Cos(rad) * (_facingLeft ? -1f : 1f), Mathf.Sin(rad), 0f) * _speed;
            // Godot Euler YXZ: yaw for the facing, then the tilt around Z.
            transform.rotation = Quaternion.Euler(0f, _facingLeft ? 180f : 0f, 0f) * GodotSpace.RotZ(deg);
        }

        void UpdateShaderAnimation(float dt)
        {
            if (_animated == null || _animated.Length == 0) return;
            float rate = Mathf.Clamp(BaseAnimRate + _velocity.magnitude * SpeedToAnimRate, MinAnimRate, MaxAnimRate);
            _animTime = (_animTime + dt * rate) % (2f * Mathf.PI);
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetFloat(AnimTimeId, _animTime);
            foreach (var r in _animated)
                if (r != null) r.SetPropertyBlock(_mpb);
        }

        /// <summary>fish.gd scatter(): turn away from the body and dart up or down at 35-55°.</summary>
        public void Scatter(Transform from)
        {
            var fp = from.position;
            var p = transform.position;
            if ((fp.x < p.x && _facingLeft) || (fp.x > p.x && !_facingLeft))
                _facingLeft = !_facingLeft;
            SetAngle(fp.y < p.y ? Random.Range(35f, 55f) : Random.Range(-35f, -55f));
        }

        public InventoryItem ToInventoryItem() =>
            new InventoryItem(Type, Weight, Price, _id, IsShiny);
    }

    /// <summary>The primitive parts of scenes/mobs/dummy_fish.tscn (Godot default material).</summary>
    static class DummyFishParts
    {
        public static void Build(Transform pivot)
        {
            var mat = Materials.Opaque(new Color(0.8f, 0.8f, 0.8f), glossiness: 0.5f);
            Part(pivot, "Body", PrimitiveType.Capsule, null, mat,
                -4.37114e-08f, 0.7f, 0f, -1f, -3.0598e-08f, 0f, 0f, 0f, 0.7f, 0f, 0f, 0f);
            Part(pivot, "Fin", PrimitiveType.Cube, ProceduralMeshes.Prism, mat,
                -3.49691e-08f, 0.8f, 0f, -0.8f, -3.49691e-08f, 0f, 0f, 0f, 0.65f, -0.601267f, 0f, 0f);
            Part(pivot, "Mouth", PrimitiveType.Capsule, null, mat,
                4.77671e-16f, -1.31134e-08f, -0.25f, -0.25f, -1.31134e-08f, 0f, -1.09278e-08f, 0.3f, -1.09278e-08f, 0.585195f, -0.190077f, 0f);
            Part(pivot, "Eye", PrimitiveType.Capsule, null, mat,
                0.3f, 0f, 0f, 0f, -1.74846e-08f, 0.3f, 0f, -0.4f, -1.31134e-08f, 0.301853f, 0.173736f, 0f);
        }

        static void Part(Transform parent, string name, PrimitiveType type, Mesh meshOverride, Material mat, params float[] godot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            if (meshOverride != null) go.GetComponent<MeshFilter>().sharedMesh = meshOverride;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.SetParent(parent, false);
            GodotSpace.Apply(go.transform, godot);
        }
    }
}
