using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scenes/popup_text.tscn + scripts/popup_text.gd (via Popup_Manager.gd): a billboard
    /// label (font size 42, dark outline, drawn on top) that pops in over 0.2 s with a 1.1×
    /// overshoot, rises 1.2 units with a quintic ease-out and fades over the last 40 %.
    /// </summary>
    public class PopupText : MonoBehaviour
    {
        const float MoveAmount = 1.2f;
        const float Duration = 0.8f;
        const float HorizontalDrift = 0.15f;
        static readonly Color Outline = new(0.05f, 0.08f, 0.12f, 0.5f);

        TextMesh[] _meshes; // [0] = text, rest = outline copies
        Color _color;
        Vector3 _start, _target;
        float _t;

        public static void Show(string text, Vector3 worldPos) => Show(text, worldPos, Color.white);

        public static void Show(string text, Vector3 worldPos, Color color)
        {
            var go = new GameObject("PopupText");
            var p = go.AddComponent<PopupText>();
            p._color = color;
            p._start = worldPos + new Vector3(Random.Range(-HorizontalDrift, HorizontalDrift), 0f, 0f);
            p._target = p._start + Vector3.up * MoveAmount;
            go.transform.position = p._start;

            // Label3D pixel_size 0.005 × font 42 ≈ 0.21 units per line
            var offsets = new[] { Vector2.zero, new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
                                  new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f) };
            p._meshes = new TextMesh[offsets.Length];
            for (int i = offsets.Length - 1; i >= 0; i--)
            {
                var child = new GameObject(i == 0 ? "Text" : "Outline");
                child.transform.SetParent(go.transform, false);
                // outline_size 6 px at pixel_size 0.005 = 0.03 units; drawn slightly behind the text
                child.transform.localPosition = new Vector3(offsets[i].x * 0.015f, offsets[i].y * 0.015f, i == 0 ? 0f : 0.001f);
                var tm = child.AddComponent<TextMesh>();
                tm.font = UiKit.Font;
                var r = child.GetComponent<MeshRenderer>();
                r.sharedMaterial = UiKit.Font.material;
                r.sortingOrder = i == 0 ? 1 : 0; // render_priority 1 over outline_render_priority 0
                tm.text = text;
                tm.fontSize = 84;
                tm.characterSize = 0.025f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.clear;
                p._meshes[i] = tm;
            }
        }

        void LateUpdate()
        {
            _t += Time.unscaledDeltaTime;
            const float popIn = 0.2f, settle = 0.15f;
            float moveDuration = Duration - settle;
            float fadeStart = moveDuration * 0.6f;

            float alpha, scale;
            Vector3 pos = _start;
            if (_t < popIn)
            {
                float k = EaseOutQuint(_t / popIn);
                alpha = k;
                scale = Mathf.Lerp(1f, 1.1f, k);
            }
            else
            {
                float t2 = _t - popIn;
                scale = Mathf.Lerp(1.1f, 1f, EaseOutQuint(Mathf.Clamp01(t2 / settle)));
                pos = Vector3.Lerp(_start, _target, EaseOutQuint(Mathf.Clamp01(t2 / moveDuration)));
                alpha = t2 < fadeStart ? 1f : 1f - EaseOutQuint(Mathf.Clamp01((t2 - fadeStart) / (moveDuration - fadeStart)));
                if (t2 >= moveDuration)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            transform.position = pos;
            transform.localScale = Vector3.one * scale;
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation; // billboard

            for (int i = 0; i < _meshes.Length; i++)
            {
                var c = i == 0 ? _color : Outline;
                c.a *= alpha;
                _meshes[i].color = c;
            }
        }

        static float EaseOutQuint(float x) => 1f - Mathf.Pow(1f - x, 5f);
    }
}
