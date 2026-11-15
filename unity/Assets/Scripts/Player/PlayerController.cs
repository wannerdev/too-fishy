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

        public Pickaxe PickaxeTool;
        public Transform SubMesh;
        public bool IsFriendSubmarine { get; private set; }
        Light _friendLamp;
        public Transform PopupSpawn;

        public float Trauma => _trauma;
        Vector3 PopupPos => PopupSpawn != null ? PopupSpawn.position : transform.position + Vector3.up * 0.67f;
        public Transform Pivot => _pivot;
        public bool FacingRight => _facingRight;
        public float HarpoonCdRemaining => _harpoonCd;
        public float BuoyCdRemaining => _buoyCd;
        public float DroneCdRemaining => _droneCd;
        /// <summary>player.gd:292: the drone cooldown is over and the sub is not docked.</summary>
        public bool DroneReady => _droneCd <= 0f && !(GameState.Instance != null && GameState.Instance.IsDocked);

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
            if (LavaZone.Contains(_cc)) ProcessLavaDamage(Time.deltaTime); // player.gd is_in_lava_area
            ProcessTrauma(Time.deltaTime);
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
            // player.gd:231 is_action_pressed("move_down") or touch_direction.y < 0, before the clamps below
            bool diveHeld = inputY < 0f;
            if (inputY > 0f && atSurface) inputY = 0f; // player.gd:197-198 / :204-205
            // player.gd:208-209: a separate if, so holding up at the surface still auto-sinks (bobbing)
            if (inputY == 0f && atSurface) inputY = -0.3f;

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
            // player.gd:231-232: only an active dive is doubled, not the idle auto-sink
            if (_velY < 0f && atSurface && diveHeld) vy *= 2f;

            var move = new Vector3(vx, vy, 0f) + _externalForces;
            _externalForces = Vector3.Lerp(_externalForces, Vector3.zero, 5f * dt);
            // player.gd calls move_and_slide() twice per physics tick (movement() and
            // collision()) with the same velocity, so the submarine covers twice the distance.
            _cc.Move(SweepHull(LimitByFish(move * dt), dt));
            _cc.Move(SweepHull(LimitByFish(move * dt), dt));

            // Lock Z
            var p = transform.position;
            var locked = new Vector3(p.x, Mathf.Min(p.y, 0.5f), PlayerZ);
            if ((locked - p).sqrMagnitude > 1e-8f)
            {
                // auto sync transforms is off: a moved CharacterController must be re-enabled
                // or its next Move() restores the old position
                _cc.enabled = false;
                transform.position = locked;
                _cc.enabled = true;
            }

            RockingMotion(dt, move);
        }

        /// <summary>
        /// Godot's "push a fish" quirk is emergent: two CharacterBody3Ds meet, the submarine is
        /// held back and the fish is shoved out of the hull a little every tick. Measured in Godot
        /// 4.5.1 (sub pushing a fish upward): the sub only advances ~1.3 m/s into the fish instead
        /// of 4.8 m/s, the fish rises at the same speed, turns every ~0.15 s and eventually slides
        /// off the round hull. Here the motion into an overlapped fish is capped at that speed and
        /// FishBehaviour.ResolveCollisions shoves the fish out of the hull.
        /// </summary>
        Vector3 LimitByFish(Vector3 motion)
        {
            if (_hull == null) _hull = transform.Find("Hull")?.GetComponent<CapsuleCollider>();
            if (_hull == null || motion.sqrMagnitude < 1e-8f) return motion;

            // two Move() calls per frame share the measured speed
            float allowed = FishPushSpeed * Time.deltaTime * 0.5f;
            var hullPos = _hull.transform.position + motion;
            var hullRot = _hull.transform.rotation;
            int n = Physics.OverlapSphereNonAlloc(hullPos, 2.5f, FishHits, 1 << Layers.Fish, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var fishCol = FishHits[i];
                if (fishCol == null) continue;
                if (!Physics.ComputePenetration(_hull, hullPos, hullRot, fishCol, fishCol.transform.position, fishCol.transform.rotation,
                        out var outDir, out float depth))
                    continue;
                outDir.z = 0f;
                if (outDir.sqrMagnitude < 1e-6f) continue;
                outDir.Normalize();
                // outDir pushes the hull out of the fish; the motion against it goes into the fish
                float into = -Vector3.Dot(motion, outDir);
                if (into > allowed) motion += outDir * (into - allowed);
            }
            return motion;
        }

        const float FishPushSpeed = 1.3f;
        CapsuleCollider _hull;
        static readonly Collider[] FishHits = new Collider[16];

        /// <summary>
        /// player.gd move_and_slide() with the real body of player.tscn:14-15/66-68 (capsule 2.79761 x r 0.5
        /// lying along X). The CharacterController is upright and only covers the middle of that hull, so
        /// the motion is first swept with the Hull capsule against solid walls (rocks, barriers, crates,
        /// the boss) and slid along them; fish, triggers and the player's own colliders are not in the mask.
        /// A hull that already overlaps a wall (teleport, crate spawned on the sub) may never move deeper
        /// and is pushed back out, like move_and_slide's penetration recovery, so it cannot get stuck.
        /// </summary>
        Vector3 SweepHull(Vector3 motion, float dt)
        {
            if (_hull == null) _hull = transform.Find("Hull")?.GetComponent<CapsuleCollider>();
            if (_hull == null) return motion;
            var ht = _hull.transform;
            Vector3 c = ht.TransformPoint(_hull.center);
            Vector3 axis = ht.right * Mathf.Max(0f, _hull.height * 0.5f - _hull.radius);
            float r = _hull.radius;
            var planar = new Vector3(motion.x, motion.y, 0f);

            // start inside a wall: remember the push-out directions (in the XY plane; Z is locked)
            _penCount = 0;
            Vector3 recover = Vector3.zero;
            int n = Physics.OverlapCapsuleNonAlloc(c + axis, c - axis, r, WallHits, WallMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = WallHits[i];
                if (col == null || col.isTrigger) continue;
                if (!Physics.ComputePenetration(_hull, ht.position, ht.rotation, col, col.transform.position, col.transform.rotation,
                        out var outDir, out float depth))
                    continue;
                outDir.z = 0f;
                if (outDir.sqrMagnitude < 0.25f) continue; // overlap only along the locked Z axis
                outDir.Normalize();
                if (_penCount < PenDirs.Length) PenDirs[_penCount++] = outDir;
                if (depth > HullSkin) recover += outDir * Mathf.Min(depth, HullRecoverSpeed * dt);
            }

            // sweep and slide (up to 3 contacts, like move_and_slide's max_slides)
            Vector3 done = Vector3.zero;
            Vector3 remaining = NotDeeper(planar);
            for (int iter = 0; iter < 3; iter++)
            {
                float dist = remaining.magnitude;
                if (dist < 1e-5f) break;
                Vector3 dir = remaining / dist;
                Vector3 p = c + done;
                int hits = Physics.CapsuleCastNonAlloc(p + axis, p - axis, r, dir, WallCastHits, dist + HullSkin, WallMask,
                    QueryTriggerInteraction.Ignore);
                int best = -1;
                float bestDist = float.MaxValue;
                for (int j = 0; j < hits; j++)
                {
                    var h = WallCastHits[j];
                    if (h.collider == null || h.collider.isTrigger) continue;
                    // overlapping at the start (reported at distance 0, normal = -dir): handled by NotDeeper above,
                    // and it must not block moving back out
                    if (h.distance <= 0f) continue;
                    if (Vector3.Dot(h.normal, dir) >= 0f) continue;
                    if (h.distance < bestDist) { bestDist = h.distance; best = j; }
                }
                if (best < 0)
                {
                    done += remaining;
                    break;
                }
                var hit = WallCastHits[best];
                float step = Mathf.Min(Mathf.Max(0f, bestDist - HullSkin), dist);
                done += dir * step;
                // player.gd:113-114 collision(): a solid wall (not a CharacterBody3D like the boss) kills
                // horizontal momentum
                if (Mathf.Abs(hit.normal.x) > 0.7f && hit.collider.GetComponentInParent<BossController>() == null)
                    _velX = 0f;
                var nrm = new Vector3(hit.normal.x, hit.normal.y, 0f);
                if (nrm.sqrMagnitude < 1e-6f) break;
                nrm.Normalize();
                remaining = dir * (dist - step);
                remaining = NotDeeper(remaining - nrm * Vector3.Dot(remaining, nrm));
            }
            return new Vector3(done.x + recover.x, done.y + recover.y, motion.z);
        }

        /// <summary>Removes the part of the motion that goes deeper into a wall the hull already overlaps.</summary>
        Vector3 NotDeeper(Vector3 motion)
        {
            for (int i = 0; i < _penCount; i++)
            {
                float into = -Vector3.Dot(motion, PenDirs[i]);
                if (into > 0f) motion += PenDirs[i] * into;
            }
            return motion;
        }

        const int WallMask = (1 << Layers.Default) | (1 << Layers.World);
        const float HullSkin = 0.02f;
        const float HullRecoverSpeed = 5f;
        static readonly Collider[] WallHits = new Collider[16];
        static readonly RaycastHit[] WallCastHits = new RaycastHit[16];
        static readonly Vector3[] PenDirs = new Vector3[16];
        int _penCount;

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

            // player.gd activate_surface_buoy(): only below y -1, not docked
            if (GameInput.ConsumeBuoy() && gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0 && !gs.IsDocked &&
                _buoyCd <= 0f && transform.position.y < -1f)
            {
                // player.gd:316 only sets position.y; velocity_x/velocity_y carry on
                Teleport(new Vector3(transform.position.x, -1f, PlayerZ), keepVelocity: true);
                SoundPlayer.Play("bup");
                Effects.BuoyEffect(transform.position);
                _buoyCd = BuoyCooldown;
            }

            // Quick save (V) with Inventory Insurance
            if (GameInput.ConsumeQuickSave() && gs.GetUpgradeLevel(Upgrade.InventorySave) > 0 && !gs.IsDocked)
            {
                // SaveGame refuses (with its own popup) during the intro
                if (SaveSystem.SaveGame())
                {
                    SoundPlayer.Play("save");
                    PopupText.Show("Game Saved", PopupPos, Color.green);
                }
            }

            if (GameInput.ConsumeDrone() && !gs.IsDocked)
                ActivateSellingDrone();

            if (GameInput.ConsumePickaxe() && gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0 && PickaxeTool != null)
                PickaxeTool.TrySwing();
        }

        /// <summary>
        /// player.gd activate_selling_drone() (Q / inventory menu button): the drone sub rises
        /// from the submarine and drags the released catch to the dock at about 3 u/s; the money
        /// arrives with it.
        /// </summary>
        public void ActivateSellingDrone()
        {
            var gs = GameState.Instance;
            if (gs == null || gs.GetUpgradeLevel(Upgrade.DroneSelling) <= 0 || gs.IsIntro()) return;
            if (_droneCd > 0f || gs.IsDocked || gs.Inventory.Items.Count == 0) return;
            DroneRun.Launch(this, gs);
            _droneCd = DroneCooldown;
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

        void UpdateDepthAndDock(float dt)
        {
            var gs = GameState.Instance;
            // level.gd: depth = int(player.y) * -1 (truncated)
            int depth = Mathf.Max(0, -(int)transform.position.y);
            gs.SetDepth(depth);

            bool docked = transform.position.y >= -1f && transform.position.x > -7f && !gs.IsIntro();
            gs.IsDocked = docked;
            if (docked)
            {
                if (gs.Health < 100f) gs.Heal(5f * dt);
                if (!_wasDocked)
                {
                    // player.gd onDock()
                    int sold = gs.Inventory.SellItems();
                    if (sold > 0)
                    {
                        SoundPlayer.Play("coins");
                        PopupText.Show("Sold items for: $" + sold, PopupPos, Color.yellow);
                    }
                }
            }
            _wasDocked = docked;
        }

        public void CatchFish(FishBehaviour fish)
        {
            if (fish == null) return;
            // Two bullets (dual AK47) or harpoon + bullet can reach the same fish in one frame. Destroy is
            // deferred, so the second hit would run catch_fish again: a second effect and shake and a false
            // "Inventory full!" (same bug in player.gd:411-448 / fish.gd:81 queue_free). Catch it once.
            if (_caughtFrame != Time.frameCount)
            {
                _caughtFrame = Time.frameCount;
                _caughtThisFrame.Clear();
            }
            if (!_caughtThisFrame.Add(fish.GetInstanceID())) return;
            // drop its colliders now, so the next bullet's overlap test this frame flies past the dead fish
            foreach (var col in fish.GetComponentsInChildren<Collider>()) col.enabled = false;

            var item = fish.ToInventoryItem();
            float trauma = Mathf.Clamp(item.Weight / 50f, 0.2f, 0.6f);
            if (item.Shiny) trauma *= 1.5f;
            AddTrauma(trauma);

            Effects.CatchEffect(fish.transform.position, item.Shiny);
            Destroy(fish.gameObject);

            // player.gd catch_fish()
            if (GameState.Instance.Inventory.Add(item))
                PopupText.Show($"Weight added: {item.Weight:0} kg\nValue: ${item.Price}", PopupPos, Color.green);
            else
                PopupText.Show("Inventory full!", PopupPos, Color.red);
        }

        readonly System.Collections.Generic.HashSet<int> _caughtThisFrame = new();
        int _caughtFrame = -1;

        /// <summary>player.gd hurtPlayer(): trauma always, damage at most once per second.</summary>
        public void Hurt(int damage)
        {
            AddTrauma(1f);
            if (!_canBeHurt) return;
            GameState.Instance.Damage(damage);
            SoundPlayer.Play("ughhh");
            DamageEffects.Instance?.ShowDamage();
            _canBeHurt = false;
            Invoke(nameof(ResetHurt), 1f);
        }

        void ResetHurt() => _canBeHurt = true;

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);
        /// <summary>
        /// player.gd processTrauma() (traumaShakeMode 1): random camera rotation of up to
        /// (10°, 10°, 5°) × trauma² × 0.1, trauma decaying at 1.7/s.
        /// </summary>
        void ProcessTrauma(float dt)
        {
            _shakeTime += dt;
            _trauma = Mathf.Max(0f, _trauma - 1.7f * dt);
            if (_cam == null) return;
            if (_trauma <= 0f)
            {
                _cam.transform.localRotation = Quaternion.identity;
                return;
            }
            float shake = _trauma * _trauma * 0.1f;
            float x, y, z;
            switch (TraumaShakeMode)
            {
                case 2: // sine wave
                    x = Mathf.Sin(_shakeTime * 20f) * 10f * shake;
                    y = Mathf.Cos(_shakeTime * 15f) * 10f * shake;
                    z = Mathf.Sin(_shakeTime * 10f) * 5f * shake;
                    break;
                case 3: // pseudo-random
                    x = PseudoRandom(_shakeTime) * 10f * shake;
                    y = PseudoRandom(_shakeTime + 100f) * 10f * shake;
                    z = PseudoRandom(_shakeTime + 200f) * 5f * shake;
                    break;
                default: // 1: random jitter (Godot default)
                    x = Random.Range(-10f, 10f) * shake;
                    y = Random.Range(-10f, 10f) * shake;
                    z = Random.Range(-5f, 5f) * shake;
                    break;
            }
            _cam.transform.localRotation = Quaternion.Euler(-x, -y, z);
        }

        /// <summary>player.gd traumaShakeMode (cheats can switch it).</summary>
        public int TraumaShakeMode = 1;
        float _shakeTime;

        static float PseudoRandom(float seed)
        {
            float v = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
            return (v - (float)System.Math.Truncate(v)) * 2f - 1f;
        }

        /// <summary>player.gd process_lava_damage(): 10 HP/s, shaking, groans and damage flashes.</summary>
        void ProcessLavaDamage(float dt)
        {
            GameState.Instance.Damage(10f * dt);
            AddTrauma(0.05f);
            if (Random.value < 0.1f) SoundPlayer.Play("ughhh");
            if (Random.value < 0.02f) DamageEffects.Instance?.ShowDamage();
        }

        /// <summary>player.gd switch_to_friend_submarine(): yellow hull, omni light instead of the lamp.</summary>
        public void SwitchToFriendSubmarine()
        {
            if (IsFriendSubmarine || SubMesh == null) return;
            IsFriendSubmarine = true;
            Friend.TintFriend(SubMesh);
            var lampGo = new GameObject("FriendLamp");
            lampGo.transform.SetParent(SubMesh, false);
            _friendLamp = lampGo.AddComponent<Light>();
            _friendLamp.type = LightType.Point;
            _friendLamp.color = new Color(1f, 1f, 0.8f);
            _friendLamp.intensity = 1f;
            _friendLamp.range = 8f;
            // Godot's GL Compatibility renderer lights it per pixel; never let it fall back to vertex lighting
            _friendLamp.renderMode = LightRenderMode.ForcePixel;
        }

        /// <summary>player.gd switch_to_normal_submarine()</summary>
        public void SwitchToNormalSubmarine()
        {
            if (!IsFriendSubmarine || SubMesh == null) return;
            IsFriendSubmarine = false;
            GodotAssets.SetMaterial(SubMesh, "submarine");
            if (_friendLamp != null) Destroy(_friendLamp.gameObject);
        }

        public void Teleport(Vector3 pos, bool keepVelocity = false)
        {
            _cc.enabled = false;
            transform.position = pos;
            _cc.enabled = true;
            if (!keepVelocity) _velX = _velY = 0f;
        }

        /// <summary>
        /// destroyable_barier.gd:413-429 crate reward: speed_horizontal/speed_vertical raised for a while,
        /// then lowered by the same amount (boosts stack). create_timer() also runs while paused.
        /// </summary>
        public void ApplySpeedBoost(float addHorizontal, float addVertical, float seconds)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(SpeedBoost(addHorizontal, addVertical, seconds));
        }

        System.Collections.IEnumerator SpeedBoost(float addHorizontal, float addVertical, float seconds)
        {
            speedHorizontal += addHorizontal;
            speedVertical += addVertical;
            yield return new WaitForSecondsRealtime(seconds);
            speedHorizontal -= addHorizontal;
            speedVertical -= addVertical;
        }

        public void AddExternalForce(Vector3 force) => _externalForces += force;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // player.gd collision(): a solid wall kills horizontal momentum (fish never block the
            // CharacterController; they are pushed by the hull instead)
            // (player.gd:113 skips CharacterBody3Ds such as the boss, as SweepHull does)
            if (Mathf.Abs(hit.normal.x) > 0.7f && hit.collider.GetComponentInParent<BossController>() == null) _velX = 0f;
        }
    }
}
