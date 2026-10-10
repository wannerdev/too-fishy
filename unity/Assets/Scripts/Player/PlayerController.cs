using UnityEngine;

namespace TooFishy
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float HarpoonCooldown = 1f;
        public const float BuoyCooldown = 8f;
        public const float DroneCooldown = 5f;

        [SerializeField] float speedHorizontal = 0.5f;
        [SerializeField] float speedVertical = 0.5f;

        CharacterController _cc;
        Transform _pivot;
        Transform _launchPoint;
        Camera _cam;

        float _velX, _velY;
        float _accelX = 2.5f, _decelX = 3.8f, _maxSpeedX = 5f;
        float _accelY = 2.2f, _decelY = 3.0f, _maxSpeedY = 4f;

        /// <summary>Godot keeps the player at z = 0.33 (game_state.gd), i.e. Unity z = -0.33.</summary>
        public const float PlayerZ = -0.33f;

        float _harpoonCd, _buoyCd, _droneCd;
        float _rockZ;
        bool _facingRight = true;
        bool _canBeHurt = true;
        float _trauma;
        bool _wasDocked;
        Vector3 _externalForces;

        public float Trauma => _trauma;
        public Transform Pivot => _pivot;
        public bool FacingRight => _facingRight;
        public float HarpoonCdRemaining => _harpoonCd;
        public float BuoyCdRemaining => _buoyCd;
        public float DroneCdRemaining => _droneCd;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _pivot = transform.Find("Pivot");
            if (_pivot == null)
            {
                var go = new GameObject("Pivot");
                go.transform.SetParent(transform, false);
                _pivot = go.transform;
            }
            _launchPoint = _pivot.Find("HarpoonLaunchPoint");
            if (_launchPoint == null)
            {
                var lp = new GameObject("HarpoonLaunchPoint");
                lp.transform.SetParent(_pivot, false);
                lp.transform.localPosition = new Vector3(0.8f, 0f, 0f);
                _launchPoint = lp.transform;
            }
            _cam = GetComponentInChildren<Camera>();
        }

        void Start()
        {
            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.Player = this;
                gs.PlayerTransform = transform;
            }
        }

        void Update()
        {
            var gs = GameState.Instance;
            if (gs == null || gs.Paused || gs.DeathScreen) return;

            TickCooldowns(Time.deltaTime);
            HandleMovement(Time.deltaTime);
            HandleActions();
            UpdateDepthAndDock(Time.deltaTime);
            gs.ApplyPressureDamage(Time.deltaTime);
            DecayTrauma(Time.deltaTime);
            ApplyCameraShake();
        }

        void TickCooldowns(float dt)
        {
            if (_harpoonCd > 0f) _harpoonCd -= dt;
            if (_buoyCd > 0f) _buoyCd -= dt;
            if (_droneCd > 0f) _droneCd -= dt;
        }

        void HandleMovement(float dt)
        {
            // Camera at -Z looking +Z (see GodotSpace): screen-right is world +X, as in Godot.
            float inputX = GameInput.Horizontal;
            float inputY = GameInput.Vertical;
            bool atSurface = transform.position.y >= -0.2f;
            if (inputY > 0f && atSurface) inputY = 0f;
            else if (inputY == 0f && atSurface) inputY = -0.3f; // auto-sink at surface when idle

            if (inputX != 0f)
            {
                _velX = Mathf.MoveTowards(_velX, inputX * _maxSpeedX, _accelX * dt);
                SetFacing(inputX > 0f);
            }
            else
                _velX = Mathf.MoveTowards(_velX, 0f, _decelX * dt);

            if (transform.position.y >= -0.2f && _velY > 0f)
                _velY = 0f;

            if (inputY != 0f)
                _velY = Mathf.MoveTowards(_velY, inputY * _maxSpeedY, _accelY * dt);
            else
                _velY = Mathf.MoveTowards(_velY, 0f, _decelY * dt);

            var gs = GameState.Instance;
            float horBonus = speedHorizontal + gs.GetUpgradeLevel(Upgrade.HorSpeed) * 0.5f;
            float vertBonus = speedVertical + gs.GetUpgradeLevel(Upgrade.VertSpeed) * 0.3f;

            float vx = _velX * horBonus;
            float vy = _velY * vertBonus;
            if (vy > 0f) vy *= 1.2f;
            if (transform.position.y >= -0.2f && inputY < 0f) vy *= 2f;

            var move = new Vector3(vx, vy, 0f) + _externalForces;
            _externalForces = Vector3.Lerp(_externalForces, Vector3.zero, 5f * dt);
            // player.gd calls move_and_slide() twice per physics tick (movement() and
            // collision()) with the same velocity, so the submarine covers twice the distance.
            _cc.Move(move * dt);
            _cc.Move(move * dt);

            // Lock Z
            var p = transform.position;
            p.z = PlayerZ;
            if (p.y > 0.5f) p.y = 0.5f;
            transform.position = p;

            RockingMotion(dt, move);
        }

        // player.gd rockingMotion(): slow sway when idle, tilt against horizontal motion, ±8°.
        void RockingMotion(float dt, Vector3 velocity)
        {
            if (_pivot == null) return;
            if (Mathf.Abs(velocity.x) < 0.1f && Mathf.Abs(velocity.y) < 0.1f)
            {
                float rocking = Mathf.Sin(Time.time * 0.5f) * 1.3f * Mathf.Deg2Rad;
                _rockZ = Mathf.Lerp(_rockZ, rocking, dt * 0.8f);
            }
            else
            {
                float tilt = -velocity.x * 0.01f;
                _rockZ = Mathf.Lerp(_rockZ, tilt, dt * 2f);
            }
            _rockZ = Mathf.Clamp(_rockZ, -8f * Mathf.Deg2Rad, 8f * Mathf.Deg2Rad);
            ApplyPivotRotation();
        }

        // Godot Pivot rotation (YXZ Euler): facing yaw, then the rocking roll.
        void ApplyPivotRotation() =>
            _pivot.localRotation = Quaternion.Euler(0f, _facingRight ? 0f : 180f, 0f) * GodotSpace.RotZ(_rockZ * Mathf.Rad2Deg);

        void SetFacing(bool right)
        {
            if (_facingRight == right) return;
            _facingRight = right;
            // player.gd turns the Pivot 180° around Y instead of mirroring it.
            if (_pivot != null) ApplyPivotRotation();
        }

        void HandleActions()
        {
            var gs = GameState.Instance;
            bool fire = GameInput.ConsumeFire(out Vector2 aimScreenPos);
            if (fire && _harpoonCd <= 0f)
                ShootHarpoon(aimScreenPos);

            if (GameInput.ConsumeBuoy() && gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0 && _buoyCd <= 0f)
            {
                Teleport(new Vector3(transform.position.x, -1f, PlayerZ));
                SoundPlayer.Play("bup");
                _buoyCd = BuoyCooldown;
            }

            if (GameInput.ConsumeDrone() && !gs.IsDocked)
                ActivateSellingDrone();

            if (GameInput.ConsumePickaxe() && gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0)
                SwingPickaxe();
        }

        /// <summary>player.gd activate_selling_drone() (Q / inventory menu button).</summary>
        public void ActivateSellingDrone()
        {
            var gs = GameState.Instance;
            if (gs == null || gs.GetUpgradeLevel(Upgrade.DroneSelling) <= 0 || gs.IsIntro()) return;
            if (_droneCd > 0f || gs.Inventory.Items.Count == 0) return;
            int sold = gs.Inventory.SellItems();
            _droneCd = DroneCooldown;
            Achievements.RecordDroneLift();
            if (sold > 0)
            {
                SoundPlayer.Play("coins");
                PopupText.Show("Drone sold all fish for $" + sold, transform.position + Vector3.up, Color.green);
            }
        }

        void ShootHarpoon(Vector2 aimScreenPos)
        {
            _harpoonCd = HarpoonCooldown;
            var gs = GameState.Instance;
            Vector3 launch = _launchPoint.position;
            float angleDeg;

            if (gs.GetUpgradeLevel(Upgrade.HarpoonRotation) > 0 && _cam != null)
            {
                // player.gd: screen-space angle from the launch point to the cursor (screen Y
                // points down in Godot, up in Unity), applied as the harpoon's Z rotation.
                Vector2 launchScreen = _cam.WorldToScreenPoint(launch);
                Vector2 d = aimScreenPos - launchScreen;
                angleDeg = d.sqrMagnitude > 0.1f ? Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg : 0f;
            }
            else
            {
                // Default: one unit beside the hull on the facing side, flying straight ahead.
                launch = transform.position + (_facingRight ? Vector3.right : Vector3.left);
                angleDeg = _facingRight ? 0f : 180f;
            }

            Harpoon.Spawn(launch, angleDeg, this);
            SoundPlayer.Play("harp");
        }

        void SwingPickaxe()
        {
            var hits = Physics.OverlapSphere(transform.position + (_facingRight ? Vector3.right : Vector3.left) * 1.2f, 1.2f);
            foreach (var h in hits)
            {
                // The component sits on the barrier root; the hit collider is one of its blocks.
                var barrier = h.GetComponentInParent<DestroyableBarrier>();
                if (barrier != null) barrier.TakeDamage(1);
            }
        }

        void UpdateDepthAndDock(float dt)
        {
            var gs = GameState.Instance;
            int depth = Mathf.Max(0, Mathf.RoundToInt(-transform.position.y));
            gs.SetDepth(depth);

            bool docked = transform.position.y >= -1f && transform.position.x > -7f && !gs.IsIntro();
            gs.IsDocked = docked;
            if (docked)
            {
                gs.Heal(5f * dt);
                if (!_wasDocked)
                {
                    int sold = gs.Inventory.SellItems();
                    if (sold > 0) PopupText.Show($"+${sold}", transform.position + Vector3.up * 1.5f);
                }
            }
            _wasDocked = docked;
        }

        public void CatchFish(FishBehaviour fish)
        {
            if (fish == null) return;
            var item = fish.ToInventoryItem();
            float trauma = Mathf.Clamp(item.Weight / 50f, 0.2f, 0.6f);
            if (item.Shiny) trauma *= 1.5f;
            AddTrauma(trauma);

            bool added = GameState.Instance.Inventory.Add(item);
            if (added)
                PopupText.Show(item.Shiny ? $"★ {item.Price}$" : $"{item.Price}$", fish.transform.position);
            else
                PopupText.Show("Cargo full!", transform.position + Vector3.up);

            Destroy(fish.gameObject);
        }

        public void Hurt(int damage)
        {
            if (!_canBeHurt) return;
            AddTrauma(1f);
            GameState.Instance.Damage(damage);
            SoundPlayer.Play("ughhh");
            DamageEffects.Instance?.ShowDamage();
            _canBeHurt = false;
            Invoke(nameof(ResetHurt), 1f);
        }

        void ResetHurt() => _canBeHurt = true;

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);
        void DecayTrauma(float dt) => _trauma = Mathf.Max(0f, _trauma - 1.7f * dt);

        void ApplyCameraShake()
        {
            if (_cam == null) return;
            float shake = _trauma * _trauma * 0.1f;
            var offset = Random.insideUnitSphere * shake;
            offset.z = 0f;
            _cam.transform.localPosition = GodotSpace.Pos(0f, 1.18841f, 5.28607f) + offset;
        }

        public void Teleport(Vector3 pos)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            _velX = _velY = 0f;
        }

        public void AddExternalForce(Vector3 force) => _externalForces += force;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var fish = hit.collider.GetComponent<FishBehaviour>();
            if (fish != null && fish.Type == FishType.Spikey)
                Hurt(5);
            else if (fish == null && Mathf.Abs(hit.normal.x) > 0.7f)
                _velX = 0f;
        }
    }
}
