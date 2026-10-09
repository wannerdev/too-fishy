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
        PlayerController _playerController;

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

            go.GetComponent<Renderer>().sharedMaterial =
                Materials.Emissive(new Color(1f, 0.55f, 0.6f), new Color(0.5f, 0.15f, 0.2f));

            // Eyes
            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3(i == 0 ? -0.25f : 0.25f, 0.2f, -0.45f);
                eye.transform.localScale = new Vector3(0.15f, 0.2f, 0.1f);
                Object.Destroy(eye.GetComponent<Collider>());
                eye.GetComponent<Renderer>().sharedMaterial = Materials.Opaque(Color.black);
            }

            var boss = go.AddComponent<BossController>();
            boss.Health = boss.MaxHealth;
            boss._origin = go.transform.position;
        }

        void Start()
        {
            _player = GameState.Instance?.PlayerTransform;
            _playerController = _player != null ? _player.GetComponent<PlayerController>() : null;
        }

        void Update()
        {
            _bobTime += Time.deltaTime;
            transform.position = _origin + new Vector3(Mathf.Sin(_bobTime * 0.5f) * 2f, Mathf.Sin(_bobTime) * 0.5f, 0f);

            if (_player != null)
            {
                // Boss collider radius (0.6 * scale 6 = 3.6) plus the submarine radius (0.5)
                float dist = Vector3.Distance(transform.position, _player.position);
                if (dist < 4.3f && _playerController != null)
                    _playerController.Hurt(2);
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
                // The intro boss escapes; it must be able to reappear in the normal game.
                ResetFlags();
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
