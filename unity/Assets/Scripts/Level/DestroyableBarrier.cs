using System.Collections.Generic;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scenes/destroyable_barier.tscn + scripts/destroyable_barier.gd: one crate
    /// (SM_Crate + assets material) with its own health. section.gd places nine of them in a row
    /// to seal a stage transition; the pickaxe breaks them, and each broken crate drops a reward.
    /// </summary>
    public class DestroyableBarrier : MonoBehaviour
    {
        public int MaxHealth = 1;
        public int Health = 1;
        int _key;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;

        const int CrackStageCount = 7;

        // Godot never frees a section, so a half-mined crate keeps its damage. The port streams
        // sections; the health of damaged crates (key >= 0: streamed crate rows) is kept here.
        static readonly Dictionary<int, int> DamagedHealth = new();
        static GameState s_healthOwner;

        // destroyable_barier.gd:8-9,21-22 hint cooldowns (3 s per crate, 5 s for any crate)
        const float HintCooldown = 3f, GlobalHintCooldown = 5f;
        // PlayerDetectionArea: BoxShape3D 8 × 8 × 5 (destroyable_barier.gd:46-49) against the hull
        // capsule (2.8 × 1.0, player.tscn)
        static readonly Vector2 HintReach = new(4f + 2.79761f / 2f, 4f + 0.5f);
        static float s_globalHintUntil = -1f;
        float _hintUntil = -1f;
        bool _playerInHintArea;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DamagedHealth.Clear();
            s_healthOwner = null;
            s_globalHintUntil = -1f;
        }

        static Dictionary<int, int> HealthMemory()
        {
            var gs = GameState.Instance;
            if (gs != s_healthOwner)
            {
                // a new GameState is a new game, like DestroyedBarriers
                DamagedHealth.Clear();
                s_healthOwner = gs;
            }
            return DamagedHealth;
        }

        public static DestroyableBarrier Create(Transform parent, Vector3 position, int hp, int key)
        {
            var root = new GameObject("DestroyableBarier");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.tag = "Barrier";

            var mesh = GodotAssets.SpawnModel(root.transform, "MeshInstance3D", "meshes/SM_Crate.obj", "assets",
                -4.37114e-08f, 0f, -1f, 0f, 1f, 0f, 1f, 0f, -4.37114e-08f, 0f, 0f, 0f);

            // SM_Crate spans about ±1.7 units
            var col = root.AddComponent<BoxCollider>();
            col.center = GodotSpace.Pos(-0.144622f, 0.00805664f, 0.00814819f);
            col.size = new Vector3(3.42f, 3.36f, 3.42f);

            var barrier = root.AddComponent<DestroyableBarrier>();
            barrier.MaxHealth = hp;
            barrier.Health = hp;
            barrier._key = key;
            barrier._renderers = mesh.GetComponentsInChildren<Renderer>();
            if (key >= 0 && HealthMemory().TryGetValue(key, out int left) && left > 0 && left < hp)
            {
                barrier.Health = left;
                barrier.UpdateCrackAppearance();
            }
            return barrier;
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            UpdateCrackAppearance();
            if (_key >= 0)
            {
                if (Health > 0) HealthMemory()[_key] = Health;
                else HealthMemory().Remove(_key);
            }
            if (Health <= 0)
            {
                GiveRandomReward();
                GameState.Instance?.DestroyedBarriers.Add(_key);
                Destroy(gameObject);
            }
        }

        /// <summary>boss_character.gd destroy_barrier(): the charging boss smashes a crate (no reward).</summary>
        public void Smash()
        {
            Effects.SpawnBurst(transform.position, new Effects.Burst
            {
                Amount = 50, Lifetime = 2f, SpeedMin = 3f, SpeedMax = 8f, Spread = 89f, EmitRadius = 1f,
                Gravity = new Vector3(0f, -3f, 0f), Size = 0.2f, SizeMin = 0.1f, SizeMax = 0.3f, Color = new Color(0.8f, 0.4f, 0.1f)
            });
            GameState.Instance?.DestroyedBarriers.Add(_key);
            if (_key >= 0) HealthMemory().Remove(_key);
            Destroy(gameObject);
        }

        /// <summary>
        /// destroyable_barier.gd:253-299 _on_player_detection_area_body_entered(): when the submarine
        /// enters the 8 × 8 × 5 area, a 4 s yellow hint (font 42) explains the crates, at most once per
        /// 3 s for this crate and once per 5 s for any crate.
        /// </summary>
        void Update()
        {
            var player = GameState.Instance?.PlayerTransform;
            if (player == null) return;
            var d = player.position - transform.position;
            bool inside = Mathf.Abs(d.x) <= HintReach.x && Mathf.Abs(d.y) <= HintReach.y;
            bool entered = inside && !_playerInHintArea;
            _playerInHintArea = inside;
            if (!entered || Time.time < _hintUntil || Time.time < s_globalHintUntil) return;

            _hintUntil = Time.time + HintCooldown;
            s_globalHintUntil = Time.time + GlobalHintCooldown;
            var gs = GameState.Instance;
            bool hasPickaxe = gs.GetUpgradeLevel(Upgrade.PickaxeUnlocked) > 0;
            string message = hasPickaxe
                ? (GameInput.TouchMode ? "Use your pickaxe (tap DIG) to break these blocks and progress!"
                                       : "Use your pickaxe (SPACE) to break these blocks and progress!")
                : "These blocks are blocking your path deeper! You'll need a pickaxe...";
            // destroyable_barier.gd:292: duration 4.0 instead of the default 0.8
            PopupText.Show(message, transform.position + Vector3.up, Color.yellow, 4f);
        }

        // destroyable_barier.gd uses staged crack overlays; here each stage darkens the crate.
        void UpdateCrackAppearance()
        {
            float damage = 1f - Mathf.Clamp01((float)Health / Mathf.Max(1, MaxHealth));
            int stage = Mathf.CeilToInt(damage * (CrackStageCount - 1));
            float shade = 1f - stage * 0.08f;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor("_Color", new Color(shade, shade, shade, 1f));
            foreach (var r in _renderers)
                if (r != null) r.SetPropertyBlock(_mpb);
        }

        /// <summary>destroyable_barier.gd give_random_reward().</summary>
        void GiveRandomReward()
        {
            var gs = GameState.Instance;
            if (gs == null) return;
            float depthMultiplier = Mathf.Max(1f, gs.Depth / 100f);
            Vector3 popup = transform.position + Vector3.up * 1.5f;
            float roll = Random.value;

            if (roll <= 0.65f)
            {
                int amount = (int)(Random.Range(5, 41) * depthMultiplier);
                gs.Money += amount;
                PopupText.Show($"+ {amount} Money!", popup, new Color(1f, 0.8f, 0f));
            }
            else if (roll <= 0.80f)
            {
                int amount = Random.Range(5, 16);
                gs.Heal(amount);
                PopupText.Show($"+ {amount} Health!", popup, new Color(0f, 1f, 0.3f));
            }
            else if (roll <= 0.95f)
            {
                // super_reward (15 %)
                int amount = (int)(Random.Range(50, 101) * depthMultiplier);
                gs.Money += amount;
                PopupText.Show($"SUPER REWARD: + {amount} Money!", popup, new Color(1f, 0.5f, 0f));
            }
            else
                GiveSpecialReward(gs, popup); // special (5 %)
        }

        /// <summary>
        /// destroyable_barier.gd:392-434 give_special_reward(). The free-pickaxe branch needs a crate
        /// broken without a pickaxe (only the boss's smash does that, and it gives no reward), so it
        /// is left out.
        /// </summary>
        static void GiveSpecialReward(GameState gs, Vector3 popup)
        {
            float depthLevel = gs.Depth / 100f;
            if (Random.value < 0.5f)
            {
                // +1.0 horizontal / +0.5 vertical speed for 15 s, run on the player (the crate is gone)
                var player = gs.Player;
                if (player != null)
                {
                    player.ApplySpeedBoost(1f, 0.5f, 15f);
                    PopupText.Show("SPEED BOOST for 15 seconds!", popup, new Color(0f, 0.8f, 1f));
                }
                return;
            }
            int huge = (int)(Random.Range(100, 201) * Mathf.Max(1f, depthLevel));
            gs.Money += huge;
            PopupText.Show($"JACKPOT: + {huge} Money!", popup, new Color(1f, 0.3f, 0.7f));
        }
    }
}
