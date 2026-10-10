using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>Godot <c>StyleBoxFlat</c>: background, uniform border, uniform corner radius, shadow.</summary>
    public struct StyleBox
    {
        public Color Bg;
        public int Border;
        public Color BorderColor;
        public int Radius;
        public Color ShadowColor;
        public int ShadowSize;

        public StyleBox(Color bg, int border = 0, Color? borderColor = null, int radius = 0, Color? shadow = null, int shadowSize = 0)
        {
            Bg = bg;
            Border = border;
            BorderColor = borderColor ?? Color.clear;
            Radius = radius;
            ShadowColor = shadow ?? Color.clear;
            ShadowSize = shadowSize;
        }
    }

    /// <summary>
    /// Builds uGUI elements that look like the Godot ones: StyleBoxFlat panels are rendered into
    /// small 9-sliced sprites, text uses Godot's default font (Open Sans SemiBold), and the canvas
    /// works in Godot's 1920×1080 viewport units, so sizes and font sizes are copied 1:1.
    /// </summary>
    public static class UiKit
    {
        static Font _font;
        static readonly Dictionary<string, Sprite> Sprites = new();
        static Sprite _circle;

        public static Font Font => _font ??= LoadFont();

        static Font LoadFont()
        {
            var f = Resources.Load<Font>("Fonts/OpenSans-SemiBold");
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ------------------------------------------------------------------ rect helpers

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Godot anchors + offsets (left, top, right, bottom), with Godot's y-down convention.</summary>
        public static RectTransform Place(RectTransform rt, float anchorLeft, float anchorTop, float anchorRight, float anchorBottom,
            float left, float top, float right, float bottom)
        {
            rt.anchorMin = new Vector2(anchorLeft, 1f - anchorBottom);
            rt.anchorMax = new Vector2(anchorRight, 1f - anchorTop);
            rt.offsetMin = new Vector2(left, -bottom);
            rt.offsetMax = new Vector2(right, -top);
            return rt;
        }

        /// <summary>Fills the parent, inset by the given margins.</summary>
        public static RectTransform Fill(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0) =>
            Place(rt, 0, 0, 1, 1, left, top, -right, -bottom);

        /// <summary>A fixed-size rect centred in the parent (Godot CenterContainer child).</summary>
        public static RectTransform Centered(RectTransform rt, float width, float height, float offsetX = 0, float offsetY = 0) =>
            Place(rt, 0.5f, 0.5f, 0.5f, 0.5f, -width / 2 + offsetX, -height / 2 + offsetY, width / 2 + offsetX, height / 2 + offsetY);

        /// <summary>Rect at (x, y) from the parent's top-left, Godot style.</summary>
        public static RectTransform TopLeft(RectTransform rt, float x, float y, float width, float height) =>
            Place(rt, 0, 0, 0, 0, x, y, x + width, y + height);

        // ------------------------------------------------------------------ panels

        public static Image Panel(Transform parent, string name, StyleBox style)
        {
            var rt = Rect(parent, name);
            if (style.ShadowSize > 0 && style.ShadowColor.a > 0f)
            {
                var shadow = Rect(rt, "Shadow").gameObject.AddComponent<Image>();
                shadow.sprite = SoftShadowSprite(style.Radius, style.ShadowSize);
                shadow.type = Image.Type.Sliced;
                shadow.color = style.ShadowColor;
                shadow.raycastTarget = false;
                var srt = shadow.rectTransform;
                Fill(srt, -style.ShadowSize, -style.ShadowSize, -style.ShadowSize, -style.ShadowSize);
                shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            }
            var img = rt.gameObject.AddComponent<Image>();
            ApplyStyle(img, style);
            img.raycastTarget = false; // menus opt in with BlockRaycasts
            return img;
        }

        public static void ApplyStyle(Image img, StyleBox style)
        {
            img.sprite = StyleSprite(style);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>The project theme's PanelContainer style (StyleBoxTexture, UI_element_background.png).</summary>
        public static Image ThemePanel(Transform parent, string name)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var sprite = GodotAssets.Sprite("textures/UI_element_background.png");
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            else
                ApplyStyle(img, new StyleBox(new Color(0.05f, 0.4f, 0.6f, 0.95f), 3, new Color(0.2f, 0.6f, 0.9f), 12));
            return img;
        }

        /// <summary>Renders a StyleBoxFlat into a 9-sliceable sprite (cached per style).</summary>
        public static Sprite StyleSprite(StyleBox s)
        {
            string key = $"{(Color32)s.Bg}|{s.Border}|{(Color32)s.BorderColor}|{s.Radius}";
            if (Sprites.TryGetValue(key, out var cached) && cached != null) return cached;

            int r = Mathf.Max(0, s.Radius);
            int b = Mathf.Max(0, s.Border);
            int edge = Mathf.Max(r, b) + 1;
            int size = edge * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Style " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float outer = RoundedRectCoverage(cx, cy, size, size, r);
                float inner = b > 0 ? RoundedRectCoverage(cx - b, cy - b, size - 2 * b, size - 2 * b, Mathf.Max(0, r - b)) : outer;
                Color c = s.BorderColor;
                c.a *= Mathf.Clamp01(outer - inner);
                Color fill = s.Bg;
                fill.a *= inner;
                px[y * size + x] = Over(fill, c);
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
            Sprites[key] = sprite;
            return sprite;
        }

        static Sprite SoftShadowSprite(int radius, int shadow)
        {
            string key = $"shadow|{radius}|{shadow}";
            if (Sprites.TryGetValue(key, out var cached) && cached != null) return cached;
            int edge = radius + shadow * 2 + 1;
            int size = edge * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f - shadow, y + 0.5f - shadow, size - 2 * shadow, size - 2 * shadow, radius);
                float a = 1f - Mathf.Clamp01((d + shadow) / (2f * shadow));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(edge, edge, edge, edge));
            Sprites[key] = sprite;
            return sprite;
        }

        public static Sprite Circle => _circle ??= BuildCircle(64);

        static Sprite BuildCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // Signed distance to a rounded rectangle occupying [0,w]x[0,h] (negative inside).
        static float RoundedRectDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2f) - (w / 2f - r);
            float qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float RoundedRectCoverage(float x, float y, float w, float h, float r)
        {
            if (w <= 0 || h <= 0) return 0f;
            return Mathf.Clamp01(0.5f - RoundedRectDistance(x, y, w, h, r));
        }

        static Color Over(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0f) return Color.clear;
            var c = (over * over.a + under * under.a * (1f - over.a)) / a;
            c.a = a;
            return c;
        }

        // ------------------------------------------------------------------ text

        public static Text Label(Transform parent, string name, string text, int fontSize, Color color,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Godot's default label colour (theme font_color).</summary>
        public static readonly Color DefaultText = new(0.875f, 0.875f, 0.875f);

        // ------------------------------------------------------------------ buttons

        public class ButtonStyles
        {
            public StyleBox Normal, Hover, Pressed;
            public StyleBox? Disabled;
            public Color Font = Color.white;
            public Color DisabledFont = new(0.875f, 0.875f, 0.875f, 0.5f);
        }

        /// <summary>Godot's default theme button (used where the scene does not override styles).</summary>
        public static readonly ButtonStyles DefaultButton = new()
        {
            Normal = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.6f), 0, null, 3),
            Hover = new StyleBox(new Color(0.225f, 0.225f, 0.225f, 0.6f), 0, null, 3),
            Pressed = new StyleBox(new Color(0f, 0f, 0f, 0.6f), 0, null, 3),
            Disabled = new StyleBox(new Color(0.1f, 0.1f, 0.1f, 0.3f), 0, null, 3),
            Font = DefaultText
        };

        /// <summary>The pause/save/settings button style (pause_menu.gd _style_buttons).</summary>
        public static readonly ButtonStyles MenuButton = new()
        {
            Normal = new StyleBox(new Color(0.1f, 0.25f, 0.4f, 0.9f), 2, new Color(0.3f, 0.5f, 0.7f), 8),
            Hover = new StyleBox(new Color(0.15f, 0.3f, 0.45f, 0.9f), 2, new Color(0.4f, 0.6f, 0.8f), 8),
            Pressed = new StyleBox(new Color(0.05f, 0.2f, 0.35f, 0.9f), 2, new Color(0.2f, 0.4f, 0.6f), 8),
            Font = DefaultText
        };

        /// <summary>The blue action button (inventory_menu.gd / fish_item.gd).</summary>
        public static readonly ButtonStyles BlueButton = new()
        {
            Normal = new StyleBox(new Color(0.2f, 0.4f, 0.7f, 0.9f), 1, new Color(0.4f, 0.6f, 0.9f), 5),
            Hover = new StyleBox(new Color(0.3f, 0.5f, 0.8f, 0.9f), 1, new Color(0.5f, 0.7f, 1f), 5),
            Pressed = new StyleBox(new Color(0.15f, 0.35f, 0.65f, 0.9f), 1, new Color(0.3f, 0.5f, 0.8f), 5),
            Font = Color.white
        };

        public static Button Button(Transform parent, string name, string text, int fontSize, ButtonStyles styles, UnityAction onClick)
        {
            var img = Panel(parent, name, styles.Normal);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            SetButtonStyles(button, styles);
            var label = Label(img.transform, "Label", text, fontSize, styles.Font, TextAnchor.MiddleCenter);
            Fill(label.rectTransform);
            if (onClick != null) button.onClick.AddListener(onClick);
            button.gameObject.AddComponent<ButtonFontColor>().Init(label, styles);
            return button;
        }

        public static void SetButtonStyles(Button button, ButtonStyles styles)
        {
            var img = (Image)button.targetGraphic ?? button.GetComponent<Image>();
            button.targetGraphic = img;
            ApplyStyle(img, styles.Normal);
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = StyleSprite(styles.Hover),
                selectedSprite = StyleSprite(styles.Normal),
                pressedSprite = StyleSprite(styles.Pressed),
                disabledSprite = StyleSprite(styles.Disabled ?? styles.Normal)
            };
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            var fc = button.GetComponent<ButtonFontColor>();
            if (fc != null) fc.Styles = styles;
        }

        /// <summary>Keeps a button label at the style's font colour (dimmed when disabled).</summary>
        public class ButtonFontColor : MonoBehaviour
        {
            Text _label;
            Button _button;
            public ButtonStyles Styles;

            public void Init(Text label, ButtonStyles styles)
            {
                _label = label;
                Styles = styles;
                _button = GetComponent<Button>();
            }

            void LateUpdate()
            {
                if (_label == null || _button == null || Styles == null) return;
                _label.color = _button.interactable ? Styles.Font : Styles.DisabledFont;
            }
        }

        // ------------------------------------------------------------------ progress bar

        /// <summary>Godot ProgressBar: background style + fill style, value 0..1.</summary>
        public class ProgressBar
        {
            public RectTransform Root;
            public Image Background, Fill;
            StyleBox _fillStyle;

            public float Value
            {
                set
                {
                    float v = Mathf.Clamp01(value);
                    Fill.rectTransform.anchorMax = new Vector2(v, 1f);
                    Fill.enabled = v > 0.001f;
                }
            }

            public void SetFillColor(Color c)
            {
                if (_fillStyle.Bg == c) return;
                _fillStyle.Bg = c;
                ApplyStyle(Fill, _fillStyle);
            }

            public ProgressBar(Transform parent, string name, StyleBox background, StyleBox fill)
            {
                Background = Panel(parent, name, background);
                Root = Background.rectTransform;
                _fillStyle = fill;
                Fill = Panel(Root, "Fill", fill);
                Fill.rectTransform.anchorMin = Vector2.zero;
                Fill.rectTransform.anchorMax = Vector2.one;
                Fill.rectTransform.offsetMin = Vector2.zero;
                Fill.rectTransform.offsetMax = Vector2.zero;
            }
        }

        // ------------------------------------------------------------------ misc

        public static Image Picture(Transform parent, string name, Sprite sprite, bool preserveAspect = true)
        {
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            img.enabled = sprite != null;
            return img;
        }

        public static Image ColorRect(Transform parent, string name, Color color)
        {
            var img = Rect(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Godot HSeparator: a 1 px line with the default theme colour.</summary>
        public static Image Separator(Transform parent) => ColorRect(parent, "HSeparator", new Color(0.375f, 0.375f, 0.375f, 0.6f));

        /// <summary>Blocks clicks from reaching the world (Godot Control mouse_filter STOP).</summary>
        public static void BlockRaycasts(Image img) => img.raycastTarget = true;

        public static bool PointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public static void Destroy(GameObject go)
        {
            if (go != null) Object.Destroy(go);
        }
    }
}
