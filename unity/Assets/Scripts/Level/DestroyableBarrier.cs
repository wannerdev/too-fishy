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
            return barrier;
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            UpdateCrackAppearance();
            if (Health <= 0)
            {
                GiveRandomReward();
                GameState.Instance?.DestroyedBarriers.Add(_key);
                Destroy(gameObject);
            }
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
            else
            {
                // super_reward (15 %) and the rare special reward (5 %)
                int amount = (int)(Random.Range(50, 101) * depthMultiplier);
                gs.Money += amount;
                PopupText.Show($"SUPER REWARD: + {amount} Money!", popup, new Color(1f, 0.5f, 0f));
            }
        }
    }
}
