using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/items/harpoon.gd + scenes/harpoon.tscn: flies along its local +X at 10 u/s,
    /// despawns after 3 s or 10 units away from the submarine's current position.
    /// </summary>
    public class Harpoon : MonoBehaviour
    {
        public float Speed = 10f;
        public float Lifetime = 3f;
        public float MaxDistance = 10f;

        PlayerController _owner;
        float _age;
        bool _piercing;

        public static Harpoon Spawn(Vector3 pos, float angleDeg, PlayerController owner)
        {
            var go = new GameObject("Harpoon");
            go.transform.position = pos;
            go.transform.rotation = GodotSpace.RotZ(angleDeg);

            GodotAssets.SpawnModel(go.transform, "Lanceharpoon", "meshes/Lanceharpoon.fbx", "Lanceharpoon",
                -2.18557e-08f, 0.5f, 0f, -0.5f, -2.18557e-08f, 0f, 0f, 0f, 0.5f, 0f, 0f, 0f);

            // Area3D > CollisionShape3D: cylinder (height 2.2, radius 0.5) scaled 0.5 at z -0.33
            var area = new GameObject("Area3D");
            area.transform.SetParent(go.transform, false);
            area.transform.localPosition = GodotSpace.Pos(0f, 0f, -0.33f);
            var col = area.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.direction = 1;
            col.radius = 0.25f;
            col.height = 1.1f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var h = go.AddComponent<Harpoon>();
            h._owner = owner;
            h._piercing = GameState.Instance.GetUpgradeLevel(Upgrade.Harpoon) >= 1;
            return h;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += transform.right * Speed * dt;
            _age += dt;

            if (_age >= Lifetime ||
                (_owner != null && Vector3.Distance(transform.position, _owner.transform.position) > MaxDistance))
                Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            var fish = other.GetComponentInParent<FishBehaviour>();
            if (fish != null)
            {
                if (_owner != null) _owner.CatchFish(fish);
                if (!_piercing) Destroy(gameObject);
                return;
            }

            var boss = other.GetComponentInParent<BossController>();
            if (boss != null)
            {
                boss.TakeDamage(10);
                Destroy(gameObject);
            }
        }
    }
}
