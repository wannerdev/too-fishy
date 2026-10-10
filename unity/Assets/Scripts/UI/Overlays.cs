using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/cooldown_visualization.gd + cooldown_ring.gd: small rings near the screen
    /// centre (behind the rest of the UI). Harpoon at centre + (100, 160), drone/buoy at
    /// centre + (0, 50); a ring is only shown when its item is owned.
    /// </summary>
    public class CooldownRings : MonoBehaviour
    {
        static readonly Color Ready = new(0f, 0.36f, 0.83f, 0.8f);
        static readonly Color Active = new(0.918f, 0.525f, 0.212f, 0.82f);
        static readonly Color Background = new(0.05f, 0.05f, 0.05f, 0.3f);
        static readonly Color HarpoonBright = new(0.2f, 0.6f, 1f, 0.9f);

        class Ring
        {
            public GameObject Root;
            public Image Fill;
        }

        Ring _harpoon, _buoy, _drone;

        public static CooldownRings Create(RectTransform canvas)
        {
            var root = UiKit.Fill(UiKit.Rect(canvas, "CooldownVisualization"));
            var c = root.gameObject.AddComponent<CooldownRings>();
            // Godot draws harpoon and drone on top of the (bigger) AK47 and buoy rings
            c._buoy = c.MakeRing(root, "buoy_ring", 12f, 5f, new Vector2(0, 50));
            c._harpoon = c.MakeRing(root, "harpoon_ring", 8f, 4f, new Vector2(100, 160));
            c._drone = c.MakeRing(root, "drone_ring", 8f, 4f, new Vector2(0, 50));
            return c;
        }

        Ring MakeRing(RectTransform parent, string name, float radius, float thickness, Vector2 offset)
        {
            float size = (radius + thickness / 2f + 1f) * 2f;
            var rt = UiKit.Rect(parent, name);
            UiKit.Place(rt, 0.5f, 0.5f, 0.5f, 0.5f, offset.x - size / 2, offset.y - size / 2, offset.x + size / 2, offset.y + size / 2);

            var bg = UiKit.Picture(rt, "Background", AnnulusSprite(radius, thickness + 2f, size), false);
            UiKit.Fill(bg.rectTransform);
            bg.color = Background;

            var fill = UiKit.Picture(rt, "Progress", AnnulusSprite(radius, thickness, size), false);
            UiKit.Fill(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            return new Ring { Root = rt.gameObject, Fill = fill };
        }

        static readonly Dictionary<string, Sprite> Cache = new();

        static Sprite AnnulusSprite(float radius, float thickness, float size)
        {
            string key = $"{radius}|{thickness}|{size}";
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            const int scale = 4;
            int px = Mathf.CeilToInt(size * scale);
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[px * px];
            float c = px / 2f, r = radius * scale, half = thickness * scale / 2f;
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c)) - r);
                pixels[y * px + x] = new Color(1, 1, 1, Mathf.Clamp01(half - d + 0.5f));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f * scale);
            Cache[key] = s;
            return s;
        }

        void Update()
        {
            var gs = GameState.Instance;
            var player = gs?.Player;
            if (player == null) return;

            Show(_harpoon, true, 1f - player.HarpoonCdRemaining / PlayerController.HarpoonCooldown,
                gs.GetUpgradeLevel(Upgrade.HarpoonRotation) > 0 ? HarpoonBright : Ready);
            Show(_buoy, gs.GetUpgradeLevel(Upgrade.SurfaceBuoy) > 0, 1f - player.BuoyCdRemaining / PlayerController.BuoyCooldown, Ready);
            Show(_drone, gs.GetUpgradeLevel(Upgrade.DroneSelling) > 0, 1f - player.DroneCdRemaining / PlayerController.DroneCooldown, Ready);
        }

        static void Show(Ring ring, bool available, float progress, Color readyColor)
        {
            if (ring.Root.activeSelf != available) ring.Root.SetActive(available);
            if (!available) return;
            progress = Mathf.Clamp01(progress);
            ring.Fill.fillAmount = progress;
            ring.Fill.color = progress >= 1f ? readyColor : Active;
        }
    }

    /// <summary>Port of scenes/ui/boss_health_bar.tscn + boss_health_bar.gd (main_scene placement).</summary>
    public class BossHealthBar : MonoBehaviour
    {
        UiKit.ProgressBar _bar;
        GameObject _root;

        public static BossHealthBar Create(RectTransform canvas)
        {
            var root = UiKit.Rect(canvas, "BossHealthBar");
            UiKit.Place(root, 0.5f, 1f, 0.5f, 1f, -753, -969, 762, -939);
            var b = canvas.gameObject.AddComponent<BossHealthBar>();
            b._root = root.gameObject;
            b._bar = new UiKit.ProgressBar(root, "ProgressBar",
                new StyleBox(new Color(0.6f, 0f, 0f, 0.7f), 1, new Color(1f, 0f, 0f, 0.3f), 4),
                new StyleBox(new Color(1f, 0f, 0f, 0.8f), 0, null, 4));
            UiKit.Fill(b._bar.Root);
            UiKit.Fill(UiKit.Label(b._bar.Root, "Label", "Blobfish", 16, UiKit.DefaultText, TextAnchor.UpperCenter).rectTransform);
            root.gameObject.SetActive(false);
            return b;
        }

        void Update()
        {
            var boss = BossController.Current;
            bool show = boss != null;
            if (_root.activeSelf != show) _root.SetActive(show);
            if (show) _bar.Value = (float)boss.Health / boss.MaxHealth;
        }
    }

    /// <summary>
    /// Port of scenes/damage_effects.tscn + scripts/damage_effects.gd: red flash and screen cracks
    /// when hurt, growing red tint and cracks while taking pressure damage.
    /// </summary>
    public class DamageEffects : MonoBehaviour
    {
        public static DamageEffects Instance { get; private set; }

        Image _redFlash;
        RectTransform _root;
        float _flashAlpha;
        bool _flashing;
        bool _underPressure;
        float _pressureTimer;
        readonly List<Crack> _cracks = new();
        bool _webgl;

        class Crack
        {
            public Image Image;
            public float Age;
            public bool Fading;
            public float StartAlpha;
        }

        int MaxCracks => _webgl ? 3 : 5;
        float CrackFadeTime => _webgl ? 2f : 3f;
        float PressureCrackInterval => _webgl ? 2f : 1f;

        public static DamageEffects Create(RectTransform canvas)
        {
            var root = UiKit.Fill(UiKit.Rect(canvas, "DamageEffects"));
            var d = root.gameObject.AddComponent<DamageEffects>();
            d._root = root;
            d._redFlash = UiKit.ColorRect(root, "RedFlash", new Color(1, 0, 0, 0));
            UiKit.Fill(d._redFlash.rectTransform);
            d._webgl = Application.platform == RuntimePlatform.WebGLPlayer;
            Instance = d;
            return d;
        }

        /// <summary>show_damage_effects(): 0.6 red flash fading over 0.5 s plus one crack.</summary>
        public void ShowDamage()
        {
            _flashAlpha = 0.6f;
            _flashing = true;
            if (_cracks.Count < MaxCracks) AddCrack();
        }

        /// <summary>process_pressure_damage(): red tint up to 40 % after 3 s, a crack every second.</summary>
        public void ProcessPressure(float dt)
        {
            if (!_underPressure)
            {
                _underPressure = true;
                _pressureTimer = 0f;
            }
            _pressureTimer += dt;
            _flashing = false;
            _redFlash.color = new Color(1, 0, 0, Mathf.Min(_pressureTimer / 3f, 1f) * 0.4f);
            if (_pressureTimer > PressureCrackInterval && _cracks.Count < MaxCracks)
            {
                AddCrack();
                _pressureTimer = 0f;
            }
        }

        public void ResetPressure()
        {
            if (!_underPressure) return;
            _underPressure = false;
            _pressureTimer = 0f;
            foreach (var c in _cracks) c.Age = 0f;
            _redFlash.color = new Color(1, 0, 0, 0);
        }

        void AddCrack()
        {
            var sprite = GodotAssets.Sprite("textures/effects/screen_crack.png");
            if (sprite == null) return;
            float baseSize = _webgl ? 100f : 200f;
            float size = baseSize * Random.Range(0.5f, 1.5f);
            var rect = _root.rect;
            var img = UiKit.Picture(_root, "CrackLayer", sprite);
            float x = Random.Range(0f, Mathf.Max(0f, rect.width - size));
            float y = Random.Range(0f, Mathf.Max(0f, rect.height - size));
            UiKit.TopLeft(img.rectTransform, x, y, size, size);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.value * 360f);
            float alpha = (_webgl ? 0.9f : 0.8f) - (_cracks.Count + 1) * (_webgl ? 0.15f : 0.1f);
            img.color = new Color(1, 1, 1, alpha);
            _cracks.Add(new Crack { Image = img, StartAlpha = alpha });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_flashing)
            {
                _flashAlpha = Mathf.MoveTowards(_flashAlpha, 0f, dt * 0.6f / 0.5f);
                _redFlash.color = new Color(1, 0, 0, _flashAlpha);
                if (_flashAlpha <= 0f) _flashing = false;
            }

            // Cracks wait crack_fade_time (not while under pressure), then fade out over 1 s
            for (int i = _cracks.Count - 1; i >= 0; i--)
            {
                var c = _cracks[i];
                if (_underPressure) continue;
                c.Age += dt;
                if (c.Age > CrackFadeTime)
                {
                    float t = Mathf.Clamp01(c.Age - CrackFadeTime);
                    c.Image.color = new Color(1, 1, 1, Mathf.Lerp(c.StartAlpha, 0f, t));
                    if (t >= 1f)
                    {
                        Destroy(c.Image.gameObject);
                        _cracks.RemoveAt(i);
                    }
                }
            }

            var gs = GameState.Instance;
            if (gs == null) return;
            bool pressure = gs.Headroom < 0f && !gs.IsDocked && !gs.DeathScreen && Time.timeScale > 0f;
            if (pressure) ProcessPressure(Time.deltaTime);
            else ResetPressure();
        }
    }
}
