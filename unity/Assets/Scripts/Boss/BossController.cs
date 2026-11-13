using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/boss.gd + scripts/boss_character.gd + scenes/boss.tscn (the blobfish).
    ///
    /// Spawns once the player has been deeper than 500 m, 25 m below the deepest point, together
    /// with the boss section. State machine: PREPARING → CHARGING (25 u/s for 1 s at the player)
    /// → COOLDOWN (3 s); below 90 % health it pauses to spawn mind-control zones. When the player
    /// is more than 100 m above or below its spawn point it swims back, regenerates 5 HP/s,
    /// switches its zones off and ignores damage.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        enum State { Cooldown, Charging, Preparing, OutOfRange, SpawningZones }

        public static bool IsDefeated { get; private set; }
        public static bool HasSpawned { get; private set; }
        /// <summary>The living boss, if any (boss health bar).</summary>
        public static BossController Current { get; private set; }

        const float ChargeSpeed = 25f, ChargeDuration = 1f, CooldownDuration = 3f;
        const int DamageAmount = 30;
        const float MaxRangeFromSpawn = 100f, HealthRegenRate = 5f;
        const int MaxZones = 5;
        const float ZoneSpawnDistanceMin = 5f, ZoneSpawnDistanceMax = 12f, ZoneSpawnCooldown = 4f, ZoneLifespan = 15f;
        public const float BossZ = 0.33f; // Godot z -0.33

        public int MaxHealth = 100;
        public float HealthF { get; private set; }
        public int Health => Mathf.CeilToInt(HealthF);

        State _state = State.Preparing;
        float _timer, _zoneSpawnTimer;
        Vector3 _chargeDir, _spawnPos;
        bool _hasHitPlayer, _inRange = true;
        Transform _pivot;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        readonly List<(MindControlZone zone, float age)> _zones = new();

        public static void TrySpawn(Transform worldRoot)
        {
            if (HasSpawned || IsDefeated) return;
            var gs = GameState.Instance;
            if (gs == null || gs.MaxDepthReached <= 500) return; // boss.gd boss_spawn_height

            HasSpawned = true;

            // level.gd spawnBoss(): x -5, 25 m below the max depth
            var go = new GameObject("Boss_Blobfish");
            go.tag = "Boss";
            go.transform.SetParent(worldRoot, false);
            go.transform.position = GodotSpace.Pos(-5f, -gs.MaxDepthReached - 25f, -0.33f);

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(go.transform, false);
            GodotAssets.SpawnModel(pivot, "MeshInstance3D", "meshes/SM_Blobert.obj", "fishes",
                1.33748e-15f, -3.0598e-08f, -0.7f, -0.7f, -3.0598e-08f, 4.44692e-21f, -3.0598e-08f, 0.7f, -3.0598e-08f,
                0f, 0.909437f, 0f);

            var col = go.AddComponent<SphereCollider>();
            col.radius = 1.50957f;
            col.center = GodotSpace.Pos(0.00968528f, 1.97357f, -0.00248528f);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var boss = go.AddComponent<BossController>();
            boss.HealthF = boss.MaxHealth;
            boss._spawnPos = go.transform.position;
            boss._pivot = pivot;
            boss._renderers = pivot.GetComponentsInChildren<Renderer>();
            Current = boss;

            // level.gd: the boss section (friend, rocks, crates, lava) 25 m below the lowest section
            gs.Level?.SpawnBossSection();

            // boss.gd setBossSpawned()
            Dialogs.SetStage(DialogSection.BossIntro);
            if (!gs.BossEncountered && !gs.IsIntro())
            {
                gs.BossEncountered = true; // unlocks the gun in the shop
                boss.Invoke(nameof(ShowGunHint), 0.5f);
            }
        }

        void ShowGunHint() => Dialogs.SetStage(DialogSection.Ak47Unlocked);

        Transform Player => GameState.Instance?.PlayerTransform;

        void Update()
        {
            var player = Player;
            if (player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _zoneSpawnTimer -= dt;
            UpdateZoneLifespans(dt);

            _inRange = Mathf.Abs(player.position.y - _spawnPos.y) <= MaxRangeFromSpawn;
            if (!_inRange)
            {
                if (_state != State.OutOfRange)
                {
                    _state = State.OutOfRange;
                    ResetAppearance();
                    foreach (var z in _zones) if (z.zone != null) z.zone.Deactivate();
                }
                HealthF = Mathf.Min(MaxHealth, HealthF + HealthRegenRate * dt);
                var back = _spawnPos - transform.position;
                back.z = 0f;
                if (back.magnitude > 0.05f) Move(back.normalized * (ChargeSpeed * 0.5f) * dt);
                return;
            }

            if (_state == State.OutOfRange)
            {
                _state = State.Preparing;
                _timer = 0f;
                foreach (var z in _zones) if (z.zone != null) z.zone.Activate();
            }

            switch (_state)
            {
                case State.Preparing:
                    _hasHitPlayer = false;
                    if (ShouldSpawnZones())
                    {
                        _state = State.SpawningZones;
                        _timer = 1f;
                        return;
                    }
                    _chargeDir = player.position - transform.position;
                    _chargeDir.z = 0f;
                    _chargeDir = _chargeDir.sqrMagnitude > 1e-4f ? _chargeDir.normalized : Vector3.left;
                    _state = State.Charging;
                    _timer = ChargeDuration;
                    LookAt(player.position);
                    break;

                case State.SpawningZones:
                    _timer -= dt;
                    AnimateSpawnZones(1f - _timer);
                    if (_timer <= 0f)
                    {
                        SpawnMindControlZones(player.position);
                        _state = State.Cooldown;
                        _timer = CooldownDuration;
                        _zoneSpawnTimer = ZoneSpawnCooldown;
                        ResetAppearance();
                    }
                    break;

                case State.Charging:
                    Move(_chargeDir * ChargeSpeed * dt);
                    CheckCollisions();
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _state = State.Cooldown;
                        _timer = CooldownDuration;
                    }
                    break;

                case State.Cooldown:
                    _timer -= dt;
                    if (_timer <= 0f) _state = State.Preparing;
                    break;
            }
        }

        void Move(Vector3 delta)
        {
            var p = transform.position + delta;
            // The play-area walls stop the boss like they stop everything else
            p.x = Mathf.Clamp(p.x, -28.5f, -1.5f);
            p.z = BossZ;
            transform.position = p;
        }

        /// <summary>Godot look_at(player, up = +Z): the boss's -Z points at the player.</summary>
        void LookAt(Vector3 target)
        {
            // Godot space: z = -dir, x = up × z, y = z × x, with up = (0, 0, 1)
            var d = target - transform.position;
            var gd = new Vector3(d.x, d.y, 0f).normalized; // in the XY plane, same in both spaces
            if (gd.sqrMagnitude < 1e-6f) return;
            var z = -gd;
            var x = new Vector3(-z.y, z.x, 0f);   // up × z
            var y = new Vector3(0f, 0f, 1f);      // z × x
            GodotSpace.Apply(transform, x.x, y.x, z.x, x.y, y.y, z.y, x.z, y.z, z.z,
                transform.position.x, transform.position.y, -transform.position.z);
        }

        /// <summary>check_player_collision() / _on_body_entered(): smash crates, hit the player once per charge.</summary>
        void CheckCollisions()
        {
            var center = transform.TransformPoint(GodotSpace.Pos(0.00968528f, 1.97357f, -0.00248528f));
            foreach (var c in Physics.OverlapSphere(center, 1.50957f + 0.1f, ~0, QueryTriggerInteraction.Ignore))
            {
                var crate = c.GetComponentInParent<DestroyableBarrier>();
                if (crate != null)
                {
                    crate.Smash();
                    GameState.Instance?.Player?.AddTrauma(0.3f);
                    continue;
                }
                if (_hasHitPlayer) continue;
                var pc = c.GetComponentInParent<PlayerController>();
                if (pc == null) continue;
                _hasHitPlayer = true;
                pc.AddTrauma(1f);
                SoundPlayer.Play("urrgh");
                GameState.Instance.Damage(DamageAmount);
            }
        }

        bool ShouldSpawnZones()
        {
            CleanupZones();
            return _zoneSpawnTimer <= 0f && _zones.Count < MaxZones && HealthF / MaxHealth <= 0.9f;
        }

        void SpawnMindControlZones(Vector3 playerPos)
        {
            CleanupZones();
            int count = Mathf.Min(MaxZones - _zones.Count, 3);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Random.Range(ZoneSpawnDistanceMin, ZoneSpawnDistanceMax);
                var pos = playerPos + new Vector3(Mathf.Cos(angle) * dist, Random.Range(-3f, 3f), 0f);
                _zones.Add((MindControlZone.Create(transform.parent, pos), 0f));
            }
        }

        void UpdateZoneLifespans(float dt)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var (zone, age) = _zones[i];
                if (zone == null) { _zones.RemoveAt(i); continue; }
                age += dt;
                if (age >= ZoneLifespan)
                {
                    Destroy(zone.gameObject);
                    _zones.RemoveAt(i);
                }
                else
                    _zones[i] = (zone, age);
            }
        }

        void CleanupZones() => _zones.RemoveAll(z => z.zone == null);

        // boss.tscn "spawn_zones" animation: scale pulse and white → red → purple → white tint
        void AnimateSpawnZones(float t)
        {
            float[] times = { 0f, 0.3f, 0.6f, 0.8f, 1f };
            float[] scales = { 1f, 0.4f, 1.3f, 1.1f, 1f };
            float s = 1f;
            for (int i = 0; i < times.Length - 1; i++)
                if (t >= times[i] && t <= times[i + 1])
                    s = Mathf.SmoothStep(scales[i], scales[i + 1], (t - times[i]) / (times[i + 1] - times[i]));
            _pivot.localScale = Vector3.one * s;

            Color c;
            if (t < 0.3f) c = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.2f), t / 0.3f);
            else if (t < 0.6f) c = Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(0.8f, 0.1f, 0.8f), (t - 0.3f) / 0.3f);
            else c = Color.Lerp(new Color(0.8f, 0.1f, 0.8f), Color.white, Mathf.Clamp01((t - 0.6f) / 0.4f));
            Tint(c);
        }

        void ResetAppearance()
        {
            if (_pivot != null) _pivot.localScale = Vector3.one;
            Tint(Color.white);
        }

        void Tint(Color c)
        {
            if (_renderers == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor("_Color", c);
            foreach (var r in _renderers) if (r != null) r.SetPropertyBlock(_mpb);
        }

        /// <summary>boss_character.gd take_damage(): ignored while the player is out of range.</summary>
        public void TakeDamage(int amount)
        {
            if (!_inRange || HealthF <= 0f) return;
            HealthF -= amount;
            PopupText.Show($"-{amount}", transform.position + Vector3.up * 3f, Color.red);

            var gs = GameState.Instance;
            if (gs.IsIntro() && HealthF <= MaxHealth * 0.5f)
            {
                // boss.gd: in the intro the fight ends at half health (level.gd switch_back_to_original_player)
                gs.Level?.SwitchBackToOriginalPlayer();
                return;
            }

            if (HealthF <= 0f) Defeat();
        }

        /// <summary>boss.gd defeat_boss()</summary>
        void Defeat()
        {
            IsDefeated = true;
            Dialogs.SetStage(DialogSection.BossDefeated);
            Destroy(gameObject);
        }

        /// <summary>friend.gd: the boss leaves once the friend is back near the surface.</summary>
        public static void Remove()
        {
            if (Current != null) Destroy(Current.gameObject);
        }

        void OnDestroy()
        {
            foreach (var z in _zones) if (z.zone != null) Destroy(z.zone.gameObject);
            if (Current == this) Current = null;
        }

        public static void ResetFlags()
        {
            IsDefeated = false;
            HasSpawned = false;
            if (Current != null) Destroy(Current.gameObject);
            Current = null;
        }
    }
}
