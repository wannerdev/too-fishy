using UnityEngine;

namespace TooFishy
{
    public class FishBehaviour : MonoBehaviour
    {
        public FishType Type { get; private set; }
        public float Weight { get; private set; }
        public int Price { get; private set; }
        public bool IsShiny { get; private set; }

        float _speed;
        float _minAngle = -30f, _maxAngle = 30f;
        float _rotationCd;
        Vector3 _velocity;
        int _id;
        static int _nextId = 1;

        public static FishBehaviour Spawn(Vector3 pos, FishType type, Stage stage, Transform parent)
        {
            var stats = FishConfig.Stats[type];
            var section = FishConfig.Sections[stage];

            if (stats.RequiresBossDefeat && !GameState.Instance.BossEncountered && !BossController.IsDefeated)
                return null;
            if (GameState.Instance.Depth < stats.MinRequiredDepth && stats.MinRequiredDepth > 0)
                return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Fish_{type}";
            go.tag = "Fish";
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = stats.Scale;

            var col = go.GetComponent<SphereCollider>();
            col.isTrigger = false;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            bool shiny = Random.value < section.ShinyRate;
            float weight = Mathf.Clamp(
                Random.Range(stats.WeightMin, stats.WeightMax) * section.WeightMultiplier,
                stats.WeightMin, stats.WeightMax);
            int price = Mathf.RoundToInt(weight * stats.PriceWeightMultiplier);
            if (shiny) price *= 3;

            float t = (weight - stats.WeightMin) / Mathf.Max(0.01f, (stats.WeightMax - stats.WeightMin) / 2f);
            float scaleMul = 1f + t * 0.3f;
            var s = stats.Scale;
            go.transform.localScale = new Vector3(s.x * scaleMul, s.y * scaleMul, s.z);

            var mat = new Material(Shader.Find("Standard"));
            mat.color = shiny ? Color.Lerp(stats.Color, Color.white, 0.55f) : stats.Color;
            if (shiny)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", stats.Color * 0.8f);
            }
            go.GetComponent<Renderer>().material = mat;

            // Simple fin
            var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "Fin";
            fin.transform.SetParent(go.transform, false);
            fin.transform.localPosition = new Vector3(-0.6f, 0f, 0f);
            fin.transform.localScale = new Vector3(0.4f, 0.5f, 0.15f);
            Object.Destroy(fin.GetComponent<Collider>());
            fin.GetComponent<Renderer>().material = mat;

            var fish = go.AddComponent<FishBehaviour>();
            fish.Type = type;
            fish.Weight = weight;
            fish.Price = price;
            fish.IsShiny = shiny;
            fish._speed = Random.Range(stats.SpeedMin, stats.SpeedMax);
            fish._id = _nextId++;
            fish.SetAngle(Random.Range(fish._minAngle, fish._maxAngle));
            return fish;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_rotationCd > 0f) _rotationCd -= dt;

            transform.position += _velocity * dt;
            var p = transform.position;
            p.z = -0.3f;
            transform.position = p;

            if (p.y >= -0.5f)
            {
                Destroy(gameObject);
                return;
            }

            float lower = GameState.Instance != null ? GameState.Instance.FishesLowerBorder : -30f;
            if (p.y >= -0.75f && _velocity.y > 0f)
                SetAngle(Random.Range(_minAngle, _minAngle / 2f));
            else if (p.y <= lower && _velocity.y < 0f)
                SetAngle(Random.Range(_maxAngle / 2f, _maxAngle));
            else if (Random.value < 0.004f)
                SetAngle(Random.Range(_minAngle, _maxAngle));

            // Wall bounce (simple bounds)
            if ((p.x < -14f && _velocity.x < 0f) || (p.x > 6f && _velocity.x > 0f))
            {
                if (_rotationCd <= 0f)
                {
                    _rotationCd = 0.1f;
                    Flip();
                    SetAngle(Random.Range(_minAngle, _maxAngle));
                }
            }

            // Scatter from player
            var player = GameState.Instance?.PlayerTransform;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                if (dist < 3f && Random.value < 0.02f)
                    Scatter(player);
            }
        }

        void SetAngle(float deg)
        {
            float rad = deg * Mathf.Deg2Rad;
            float facing = transform.localScale.x >= 0f ? 1f : -1f;
            // Fish local +X is forward after scale flip; velocity along swim direction
            Vector3 dir = new Vector3(Mathf.Cos(rad) * facing, Mathf.Sin(rad), 0f).normalized;
            _velocity = dir * _speed;
            transform.rotation = Quaternion.Euler(0f, 0f, deg * facing);
        }

        void Flip()
        {
            var s = transform.localScale;
            s.x = -s.x;
            transform.localScale = s;
        }

        public void Scatter(Transform from)
        {
            Vector3 away = (transform.position - from.position).normalized;
            away.z = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            _velocity = away * (_speed * 2f);
            if ((away.x > 0f) != (transform.localScale.x > 0f))
                Flip();
        }

        public InventoryItem ToInventoryItem() =>
            new InventoryItem(Type, Weight, Price, _id, IsShiny);
    }
}
