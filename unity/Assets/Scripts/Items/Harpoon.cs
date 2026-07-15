using UnityEngine;

namespace TooFishy
{
    public class Harpoon : MonoBehaviour
    {
        public float Speed = 10f;
        public float Lifetime = 3f;
        public float MaxDistance = 10f;

        PlayerController _owner;
        Vector3 _dir;
        Vector3 _start;
        float _age;
        bool _piercing;

        public static Harpoon Spawn(Vector3 pos, Vector3 dir, PlayerController owner)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Harpoon";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);

            Object.Destroy(go.GetComponent<Collider>());
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.75f, 0.55f, 0.25f);
            mat.SetFloat("_Metallic", 0.6f);
            go.GetComponent<Renderer>().material = mat;

            var h = go.AddComponent<Harpoon>();
            h._owner = owner;
            h._dir = dir.normalized;
            h._start = owner.transform.position;
            h._piercing = GameState.Instance.GetUpgradeLevel(Upgrade.Harpoon) >= 1;
            return h;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += _dir * Speed * dt;
            _age += dt;

            if (_age >= Lifetime || Vector3.Distance(transform.position, _start) > MaxDistance)
            {
                Destroy(gameObject);
                return;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Fish") || other.GetComponent<FishBehaviour>() != null)
            {
                var fish = other.GetComponent<FishBehaviour>();
                if (fish != null && _owner != null)
                    _owner.CatchFish(fish);
                if (!_piercing)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (other.CompareTag("Boss") || other.GetComponent<BossController>() != null)
            {
                var boss = other.GetComponent<BossController>();
                boss?.TakeDamage(10);
                Destroy(gameObject);
            }
        }
    }
}
