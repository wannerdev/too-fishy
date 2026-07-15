using UnityEngine;

namespace TooFishy
{
    public class PopupText : MonoBehaviour
    {
        TextMesh _tm;
        float _life = 1.2f;
        Vector3 _vel;

        public static void Show(string text, Vector3 worldPos)
        {
            var go = new GameObject("Popup");
            go.transform.position = worldPos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
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
