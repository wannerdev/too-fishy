using UnityEngine;

namespace TooFishy
{
    public class BossController : MonoBehaviour
    {
        public static bool IsDefeated { get; private set; }
        public static bool HasSpawned { get; private set; }
        /// <summary>The living boss, if any (boss health bar).</summary>
        public static BossController Current { get; private set; }

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
            if (gs == null || gs.MaxDepthReached <= 500) return; // boss.gd boss_spawn_height

            HasSpawned = true;
            gs.BossEncountered = true;

            // scenes/boss.tscn, placed like level.gd spawnBoss(): x -5, 25 m below the max depth
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
            Current = boss;
            // boss.gd setBossSpawned()
            Dialogs.SetStage(DialogSection.BossIntro);
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
                // Boss sphere (radius 1.51, centred 1.97 above the origin) plus the submarine radius (0.5)
                float dist = Vector3.Distance(transform.position + Vector3.up * 1.97357f, _player.position);
                if (dist < 2.01f && _playerController != null)
                    _playerController.Hurt(2);
            }
        }

        public void TakeDamage(int amount)
        {
            Health = Mathf.Max(0, Health - amount);
            PopupText.Show($"-{amount}", transform.position + Vector3.up * 3f);
            SoundPlayer.Play("urrgh");

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
                // boss.gd defeat_boss()
                Dialogs.SetStage(DialogSection.BossDefeated);
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public static void ResetFlags()
        {
            IsDefeated = false;
            HasSpawned = false;
        }
    }
}
