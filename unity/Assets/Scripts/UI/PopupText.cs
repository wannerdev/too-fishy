using UnityEngine;

namespace TooFishy
{
    public class PopupText : MonoBehaviour
    {
        TextMesh _tm;
        float _life = 1.2f;
        Vector3 _vel;

        public static void Show(string text, Vector3 worldPos) => Show(text, worldPos, Color.white);

        public static void Show(string text, Vector3 worldPos, Color color)
        {
            var go = new GameObject("Popup");
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            // A TextMesh renders nothing without a font and the font's material
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var p = go.AddComponent<PopupText>();
            p._tm = tm;
            p._vel = Vector3.up * 1.5f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            transform.position += _vel * dt;
            _life -= dt;
            if (_tm != null)
            {
                var c = _tm.color;
                c.a = Mathf.Clamp01(_life);
                _tm.color = c;
            }
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
