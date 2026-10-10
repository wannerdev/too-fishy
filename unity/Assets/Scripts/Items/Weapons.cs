using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/items/pickaxe.gd: SPACE swings the pickaxe forward for 0.4 s (smoothstep
    /// eased rotation around Z) and back for 0.4 s. Anything breakable inside the small hitbox at
    /// the pick's tip takes 1 damage and ends the forward swing early.
    /// </summary>
    public class Pickaxe : MonoBehaviour
    {
        const float SwingSpeed = 5f;      // schwing_geschwindigkeit
        const float SwingDuration = 0.4f;

        bool _swing, _backSwing;
        float _swingTime;
        float _baseZ, _angle;

        void Start() => _baseZ = transform.localEulerAngles.z;

        public void TrySwing()
        {
            if (_swing || _backSwing || !gameObject.activeInHierarchy) return;
            _swing = true;
            _swingTime = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_swing)
            {
                _swingTime += dt;
                float progress = _swingTime / SwingDuration;
                _angle -= SwingSpeed * Ease(progress) * dt * Mathf.Rad2Deg;
                Apply();
                bool hit = HitBreakables();
                if (progress >= 1f || hit)
                {
                    _swing = false;
                    _backSwing = true;
                    _swingTime = 0f;
                }
            }
            else if (_backSwing)
            {
                _swingTime += dt;
                float progress = _swingTime / SwingDuration;
                _angle += SwingSpeed * Ease(1f - progress) * dt * Mathf.Rad2Deg;
                if (progress >= 1f)
                {
                    _angle = 0f;
                    _backSwing = false;
                }
                Apply();
            }
        }

        void Apply() => transform.localRotation = Quaternion.Euler(0f, 0f, _baseZ + _angle);

        // PickaxeHitbox: box (1, 1.13977, 1) scaled (0.1, 0.79065, 0.1) at (0.943962, -0.009, 0)
        bool HitBreakables()
        {
            var center = transform.TransformPoint(GodotSpace.Pos(0.943962f, -0.00903125f, 0.00048995f));
            var half = Vector3.Scale(new Vector3(0.05f, 1.13977f * 0.79065f * 0.5f, 0.05f), transform.lossyScale);
            half = new Vector3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z));
            bool hit = false;
            foreach (var c in Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                var barrier = c.GetComponentInParent<DestroyableBarrier>();
                if (barrier != null) { barrier.TakeDamage(1); hit = true; continue; }
                var boss = c.GetComponentInParent<BossController>();
                if (boss != null) { boss.TakeDamage(1); hit = true; }
            }
            return hit;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Port of scripts/items/ak47.gd + secondAk47.gd: hold the right mouse button to fire every
    /// 0.1 s. Both guns share one magazine (30 rounds, 60 with the second gun) and a 3 s reload.
    /// </summary>
    public class Ak47 : MonoBehaviour
    {
        public const int BaseMaxAmmo = 30, DualMaxAmmo = 60;
        public const float ReloadCooldown = 3f;
        const float FireRate = 0.1f;

        public static int SharedAmmo = BaseMaxAmmo;
        public static bool Reloading { get; private set; }
        static float _reloadLeft;
        static bool _wasDual;

        public bool Primary = true;
        public GameObject Model;
        public Transform Muzzle;
        public Transform PopupSpawn;
        float _cooldown;
        float _popupCooldown;

        public static int MaxAmmo =>
            GameState.Instance != null && GameState.Instance.GetUpgradeLevel(Upgrade.DualAk47) > 0 ? DualMaxAmmo : BaseMaxAmmo;

        public static float ReloadProgress => Reloading ? 1f - _reloadLeft / ReloadCooldown : 1f;

        public static void ResetAmmo()
        {
            SharedAmmo = MaxAmmo;
            Reloading = false;
            _reloadLeft = 0f;
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null || gs.Paused) return;
            float dt = Time.deltaTime;
            if (_cooldown > 0f) _cooldown -= dt;
            if (_popupCooldown > 0f) _popupCooldown -= dt;

            if (Primary)
            {
                // Buying the second gun fills the shared magazine to 60 (player.gd process_dock)
                bool dual = gs.GetUpgradeLevel(Upgrade.DualAk47) > 0;
                if (dual && !_wasDual) SharedAmmo = MaxAmmo;
                _wasDual = dual;

                if (Reloading)
                {
                    _reloadLeft -= dt;
                    if (_reloadLeft <= 0f)
                    {
                        SharedAmmo = MaxAmmo;
                        Reloading = false;
                        PopupText.Show("Reload complete!", PopupPos, Color.green);
                    }
                }
            }

            if (!GameInput.ShootHeld || _cooldown > 0f || Reloading) return;

            int required = Primary ? (int)Upgrade.Ak47 : (int)Upgrade.DualAk47;
            if (gs.GetUpgradeLevel((Upgrade)required) <= 0)
            {
                if (Primary && _popupCooldown <= 0f)
                {
                    PopupText.Show("Buy Gun Upgrade to use gun", PopupPos, Color.red);
                    _popupCooldown = 1f;
                }
                return;
            }

            if (SharedAmmo > 0)
            {
                Shoot();
                _cooldown = FireRate;
            }
            else if (Primary)
                StartReload();
        }

        Vector3 PopupPos => PopupSpawn != null ? PopupSpawn.position : transform.position + Vector3.up * 0.7f;

        void Shoot()
        {
            if (Muzzle == null) return;
            // bullet.gd moves along the muzzle's local -X (Godot), i.e. out of the barrel
            Bullet.Spawn(Muzzle.position, -Muzzle.right);
            SharedAmmo--;
            if (SharedAmmo <= 0) StartReload();
        }

        void StartReload()
        {
            if (Reloading) return;
            Reloading = true;
            _reloadLeft = ReloadCooldown;
            PopupText.Show("Reloading...", PopupPos, Color.yellow);
        }
    }

    /// <summary>
    /// Port of scenes/bullet.tscn + scripts/items/bullet.gd: a small yellow ball at 50 u/s for 2 s.
    /// Its hit cylinder (radius 0.1, 4.16 deep along Z) catches the first fish it touches or
    /// deals 5 damage to the boss.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        const float Speed = 50f;
        const float Lifetime = 2f;
        Vector3 _dir;
        float _age;

        public static void Spawn(Vector3 position, Vector3 direction)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bullet";
            go.layer = Layers.Projectile;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.1f; // SphereMesh r 0.5 × 0.1
            go.GetComponent<Renderer>().sharedMaterial = Materials.Opaque(new Color(0.973f, 1f, 0.19f));
            var b = go.AddComponent<Bullet>();
            direction.z = 0f;
            b._dir = direction.normalized;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += _dir * Speed * dt;
            _age += dt;
            if (_age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            var p = transform.position;
            var a = new Vector3(p.x, p.y, -2.08f);
            var b = new Vector3(p.x, p.y, 2.08f);
            foreach (var c in Physics.OverlapCapsule(a, b, 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var fish = c.GetComponentInParent<FishBehaviour>();
                if (fish != null)
                {
                    GameState.Instance?.Player?.CatchFish(fish);
                    Destroy(gameObject);
                    return;
                }
                var boss = c.GetComponentInParent<BossController>();
                if (boss != null)
                {
                    boss.TakeDamage(5);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
