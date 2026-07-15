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

        float _harpoonCd, _buoyCd, _droneCd;
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
            float inputX = 0f, inputY = 0f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX = 1f;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX = -1f;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                inputY = 1f;
                if (transform.position.y >= -0.2f) inputY = 0f;
            }
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                inputY = -1f;
            else if (transform.position.y >= -0.2f)
                inputY = -0.3f; // auto-sink at surface when idle

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

            _externalForces = Vector3.Lerp(_externalForces, Vector3.zero, 5f * dt);
            var move = new Vector3(vx, vy, 0f) + _externalForces;
            _cc.Move(move * dt);

            // Lock Z
            var p = transform.position;
            p.z = 0.33f;
            if (p.y > 0.5f) p.y = 0.5f;
            transform.position = p;

            // Subtle rocking
            if (_pivot != null)
            {
                float rock = Mathf.Sin(Time.time * 2f) * 8f * Mathf.Clamp01(Mathf.Abs(_velX) / _maxSpeedX);
                var e = _pivot.localEulerAngles;
                e.z = rock;
                _pivot.localEulerAngles = e;
            }
        }

        void SetFacing(bool right)
        {
            if (_facingRight == right) return;
            _facingRight = right;
            if (_pivot != null)
            {
                var s = _pivot.localScale;
                s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
                _pivot.localScale = s;
            }
        }

        void HandleActions()
        {
            var gs = GameState.Instance;
            if (Input.GetMouseButtonDown(0) && _harpoonCd <= 0f)
                ShootHarpoon();

            if (Input.GetKeyDown(KeyCode.B) && gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0 && _buoyCd <= 0f)
            {
                Teleport(new Vector3(transform.position.x, -1f, 0.33f));
                _buoyCd = BuoyCooldown;
            }

            if (Input.GetKeyDown(KeyCode.Q) && gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0 && _droneCd <= 0f)
            {
                int sold = gs.Inventory.SellItems();
                if (sold > 0) PopupText.Show($"+${sold} (drone)", transform.position + Vector3.up);
                _droneCd = DroneCooldown;
            }

            if (Input.GetKeyDown(KeyCode.Space) && gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0)
                SwingPickaxe();
        }

        void ShootHarpoon()
        {
            _harpoonCd = HarpoonCooldown;
            Vector3 dir;
            var gs = GameState.Instance;
            if (gs.GetUpgradeLevel(Upgrade.HarpoonRotation) > 0 && _cam != null)
            {
                var mouse = Input.mousePosition;
                mouse.z = Mathf.Abs(_cam.transform.position.z - transform.position.z);
                var world = _cam.ScreenToWorldPoint(mouse);
                dir = (world - _launchPoint.position);
                dir.z = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = _facingRight ? Vector3.right : Vector3.left;
                dir.Normalize();
            }
            else
                dir = _facingRight ? Vector3.right : Vector3.left;

            Harpoon.Spawn(_launchPoint.position, dir, this);
        }

        void SwingPickaxe()
        {
            var hits = Physics.OverlapSphere(transform.position + (_facingRight ? Vector3.right : Vector3.left) * 1.2f, 1.2f);
            foreach (var h in hits)
            {
                var barrier = h.GetComponent<DestroyableBarrier>();
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
            _cam.transform.localPosition = new Vector3(0f, 1.19f, 5.29f) + offset;
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
