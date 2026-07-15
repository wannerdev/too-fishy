using UnityEngine;

namespace TooFishy
{
    public class DestroyableBarrier : MonoBehaviour
    {
        public int Health = 3;
        int _max;
        Renderer[] _renderers;

        public static DestroyableBarrier Create(Transform parent, float y, int hp)
        {
            var root = new GameObject("Barrier");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(-4f, y + 10f, 0f);
            root.tag = "Barrier";

            var barrier = root.AddComponent<DestroyableBarrier>();
            barrier.Health = hp;
            barrier._max = hp;

            var mats = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < 9; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Block_{i}";
                box.transform.SetParent(root.transform, false);
                int row = i / 3;
                int col = i % 3;
                box.transform.localPosition = new Vector3((col - 1) * 2.2f, (row - 1) * 2.2f, 0f);
                box.transform.localScale = new Vector3(2f, 2f, 1.5f);
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.4f, 0.35f, 0.3f);
                box.GetComponent<Renderer>().material = mat;
                mats.Add(box.GetComponent<Renderer>());
            }
            barrier._renderers = mats.ToArray();
            return barrier;
        }

        public void TakeDamage(int amount)
        {
            Health -= amount;
            float t = 1f - (float)Health / _max;
            foreach (var r in _renderers)
            {
                if (r != null)
                    r.material.color = Color.Lerp(new Color(0.4f, 0.35f, 0.3f), new Color(0.8f, 0.2f, 0.1f), t);
            }
            PopupText.Show("!", transform.position);
            if (Health <= 0)
            {
                PopupText.Show("Barrier destroyed!", transform.position);
                Destroy(gameObject);
            }
        }
    }
}
