using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Wall of blocks that seals a stage transition until the pickaxe breaks it. Spans the whole
    /// play area (x -14..6) so it cannot be swum around.
    /// </summary>
    public class DestroyableBarrier : MonoBehaviour
    {
        public int Health = 3;
        int _max;
        int _key;
        Renderer[] _renderers;

        static readonly Color IntactColor = new(0.4f, 0.35f, 0.3f);
        static readonly Color BrokenColor = new(0.8f, 0.2f, 0.1f);

        public static DestroyableBarrier Create(Transform parent, float y, int hp, int key)
        {
            var root = new GameObject("Barrier");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(-4f, y + 10f, 0f);
            root.tag = "Barrier";

            var barrier = root.AddComponent<DestroyableBarrier>();
            barrier.Health = hp;
            barrier._max = hp;
            barrier._key = key;

            const int cols = 9, rows = 2;
            var renderers = new Renderer[cols * rows];
            var mat = Materials.Opaque(IntactColor);
            for (int i = 0; i < renderers.Length; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Block_{i}";
                box.transform.SetParent(root.transform, false);
                int row = i / cols;
                int col = i % cols;
                box.transform.localPosition = new Vector3((col - (cols - 1) / 2f) * 2.2f, (row - (rows - 1) / 2f) * 2.2f, 0f);
                box.transform.localScale = new Vector3(2.1f, 2.1f, 1.5f);
                var r = box.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                renderers[i] = r;
            }
            barrier._renderers = renderers;
            return barrier;
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            float t = 1f - (float)Health / _max;
            // One shared material per damage step instead of a new instance per block per hit
            var damaged = Materials.Opaque(Color.Lerp(IntactColor, BrokenColor, t));
            foreach (var r in _renderers)
            {
                if (r != null) r.sharedMaterial = damaged;
            }
            PopupText.Show("!", transform.position);
            if (Health <= 0)
            {
                PopupText.Show("Barrier destroyed!", transform.position);
                GameState.Instance?.DestroyedBarriers.Add(_key);
                Destroy(gameObject);
            }
        }
    }
}
