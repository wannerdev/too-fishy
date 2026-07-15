using UnityEngine;

namespace TooFishy
{
    public class BossController : MonoBehaviour
    {
        public static bool IsDefeated { get; private set; }
        public static bool HasSpawned { get; private set; }

        public int MaxHealth = 100;
        public int Health { get; private set; }

        float _bobTime;
        Vector3 _origin;
        Transform _player;

        public static void TrySpawn(Transform worldRoot)
        {
            if (HasSpawned || IsDefeated) return;
            var gs = GameState.Instance;
            if (gs == null || gs.MaxDepthReached <= 500) return;

            HasSpawned = true;
            gs.BossEncountered = true;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Boss_Blobfish";
            go.tag = "Boss";
            go.transform.SetParent(worldRoot, false);
            go.transform.position = new Vector3(-4f, -520f, -0.5f);
            go.transform.localScale = new Vector3(6f, 5f, 4f);

            Object.Destroy(go.GetComponent<Collider>());
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.6f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.55f, 0.6f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.5f, 0.15f, 0.2f));
            go.GetComponent<Renderer>().material = mat;

            // Eyes
            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3(i == 0 ? -0.25f : 0.25f, 0.2f, -0.45f);
                eye.transform.localScale = new Vector3(0.15f, 0.2f, 0.1f);
                Object.Destroy(eye.GetComponent<Collider>());
                var em = new Material(Shader.Find("Standard"));
                em.color = Color.black;
                eye.GetComponent<Renderer>().material = em;
            }

            var boss = go.AddComponent<BossController>();
            boss.Health = boss.MaxHealth;
            boss._origin = go.transform.position;
        }

        void Start()
        {
            _player = GameState.Instance?.PlayerTransform;
        }

        void Update()
        {
            _bobTime += Time.deltaTime;
            transform.position = _origin + new Vector3(Mathf.Sin(_bobTime * 0.5f) * 2f, Mathf.Sin(_bobTime) * 0.5f, 0f);

            if (_player != null)
            {
                float dist = Vector3.Distance(transform.position, _player.position);
                if (dist < 4f)
                {
                    var pc = _player.GetComponent<PlayerController>();
                    pc?.Hurt(2);
                }
            }
        }

        public void TakeDamage(int amount)
        {
            Health = Mathf.Max(0, Health - amount);
            PopupText.Show($"-{amount}", transform.position + Vector3.up * 3f);
            transform.localScale *= 0.98f;

            if (GameState.Instance.IsIntro() && Health <= MaxHealth / 2)
            {
                GameState.Instance.CompleteIntroMission(transform.position);
                Destroy(gameObject);
                return;
            }

            if (Health <= 0)
            {
                IsDefeated = true;
                GameState.Instance.BossEncountered = true;
                PopupText.Show("BOSS DEFEATED!", transform.position);
                GameState.Instance.Money += 500;
                Destroy(gameObject);
            }
        }

        public static void ResetFlags()
        {
            IsDefeated = false;
            HasSpawned = false;
        }
    }
}
