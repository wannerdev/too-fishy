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
            public RectTransform Rect;
            public Image Fill;
            public Vector2 Offset;
            public float Size;
        }

        Ring _harpoon, _buoy, _drone, _ak47;
        float _appliedFov = -1f;

        /// <summary>Camera field of view the Godot offsets were made for (UnderwaterCamera.BaseFov).</summary>
        const float ReferenceFov = 75f;

        public static CooldownRings Create(RectTransform canvas)
        {
            var root = UiKit.Fill(UiKit.Rect(canvas, "CooldownVisualization"));
            var c = root.gameObject.AddComponent<CooldownRings>();
            // Godot draws harpoon and drone on top of the (bigger) AK47 and buoy rings
            c._ak47 = c.MakeRing(root, "ak47_ring", 12f, 5f, new Vector2(100, 160));
            c._buoy = c.MakeRing(root, "buoy_ring", 12f, 5f, new Vector2(0, 50));
            c._harpoon = c.MakeRing(root, "harpoon_ring", 8f, 4f, new Vector2(100, 160));
            c._drone = c.MakeRing(root, "drone_ring", 8f, 4f, new Vector2(0, 50));
            c.Layout();
            UiScale.Changed += c.Layout;
            return c;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        /// <summary>
        /// Godot's offsets are screen pixels at 1080 p next to the submarine, so on a scaled-up canvas
        /// (<see cref="UiScale.Factor"/> &gt; 1) they shrink with the canvas and follow the camera's
        /// wider FOV on long phones to stay at the same spot of the sub; the rings themselves only
        /// shrink as far as world popups do (<see cref="UiScale.WorldTextScale"/>) to stay visible.
        /// Factor 1 (desktop) keeps the Godot offsets and sizes unchanged.
        /// </summary>
        void Layout()
        {
            float fov = CameraFov();
            _appliedFov = fov;
            float offsetScale = 1f, sizeScale = 1f;
            if (UiScale.Factor > 1f)
            {
                float fovCorrection = Mathf.Tan(ReferenceFov * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                offsetScale = UiScale.CanvasSize.y / UiScale.RefHeight * fovCorrection;
                sizeScale = UiScale.WorldTextScale / UiScale.Factor;
            }
            foreach (var ring in new[] { _ak47, _buoy, _harpoon, _drone })
            {
                Vector2 o = ring.Offset * offsetScale;
                float size = ring.Size;
                UiKit.Place(ring.Rect, 0.5f, 0.5f, 0.5f, 0.5f, o.x - size / 2, o.y - size / 2, o.x + size / 2, o.y + size / 2);
                ring.Rect.localScale = new Vector3(sizeScale, sizeScale, 1f);
            }
        }

        static float CameraFov()
        {
            var cam = Camera.main;
            return cam != null && !cam.orthographic ? cam.fieldOfView : ReferenceFov;
        }

        Ring MakeRing(RectTransform parent, string name, float radius, float thickness, Vector2 offset)
        {
            float size = (radius + thickness / 2f + 1f) * 2f;
            var rt = UiKit.Rect(parent, name);

            var bg = UiKit.Picture(rt, "Background", AnnulusSprite(radius, thickness + 2f, size), false);
            UiKit.Fill(bg.rectTransform);
            bg.color = Background;

            var fill = UiKit.Picture(rt, "Progress", AnnulusSprite(radius, thickness, size), false);
            UiKit.Fill(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            return new Ring { Root = rt.gameObject, Rect = rt, Fill = fill, Offset = offset, Size = size };
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
            // UnderwaterCamera widens its FOV after a resize; follow it on scaled canvases
            if (UiScale.Factor > 1f && !Mathf.Approximately(CameraFov(), _appliedFov)) Layout();

            var gs = GameState.Instance;
            var player = gs?.Player;
            if (player == null) return;

            Show(_harpoon, true, 1f - player.HarpoonCdRemaining / PlayerController.HarpoonCooldown,
                gs.GetUpgradeLevel(Upgrade.HarpoonRotation) > 0 ? HarpoonBright : Ready);
            Show(_ak47, gs.GetUpgradeLevel(Upgrade.Ak47) > 0, Ak47.ReloadProgress, Ready);
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

    /// <summary>
    /// Port of scenes/ui/boss_health_bar.tscn + boss_health_bar.gd (main_scene placement: 1515 wide,
    /// 969..939 above the bottom edge, i.e. y 111..141 on a 1080-tall canvas). A canvas where that
    /// does not fit (tablet) gets the bar at y 111..141 between the inventory panel and the
    /// achievement column; a phone-sized canvas gets y 44..62 from x 186 to W-186.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        UiKit.ProgressBar _bar;
        GameObject _root;
        RectTransform _rect;
        Text _label;

        public static BossHealthBar Create(RectTransform canvas)
        {
            var root = UiKit.Rect(canvas, "BossHealthBar");
            var b = canvas.gameObject.AddComponent<BossHealthBar>();
            b._root = root.gameObject;
            b._rect = root;
            b._bar = new UiKit.ProgressBar(root, "ProgressBar",
                new StyleBox(new Color(0.6f, 0f, 0f, 0.7f), 1, new Color(1f, 0f, 0f, 0.3f), 4),
                new StyleBox(new Color(1f, 0f, 0f, 0.8f), 0, null, 4));
            UiKit.Fill(b._bar.Root);
            // boss_health_bar.tscn Label: horizontal and vertical alignment centred
            b._label = UiKit.Label(b._bar.Root, "Label", "Blobfish", 16, UiKit.DefaultText, TextAnchor.MiddleCenter);
            UiKit.Fill(b._label.rectTransform);
            root.gameObject.SetActive(false);
            b.Layout();
            UiScale.Changed += b.Layout;
            return b;
        }

        void OnDestroy() => UiScale.Changed -= Layout;

        /// <summary>Sizes for the current canvas; runs at creation and on <see cref="UiScale.Changed"/>.</summary>
        void Layout()
        {
            Vector2 canvas = UiScale.CanvasSize;
            if (UiScale.Compact)
            {
                // Phone map: between the inventory panel (x ..180) and the achievements (x W-176..)
                UiKit.Place(_rect, 0, 0, 1, 0, 186, 44, -186, 62);
                _label.fontSize = 14;
            }
            else if (canvas.x >= 1515f + 16f && canvas.y - 969f >= 105f)
            {
                // Godot main_scene.tscn placement (desktop)
                UiKit.Place(_rect, 0.5f, 1f, 0.5f, 1f, -753, -969, 762, -939);
                _label.fontSize = 16;
            }
            else
            {
                // Tablet: same height below the 105-unit HUD, stretched between inv_ui (..198) and
                // the achievement column (W-190..), so it never leaves a narrower canvas
                UiKit.Place(_rect, 0, 0, 1, 0, 206, 111, -198, 141);
                _label.fontSize = 16;
            }
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
        // damage_effects.gd crack_fade_time (3 s, 2 s on WebGL) is never reached: player.gd:523-526
        // calls reset_pressure_damage() every frame out of pressure, and its start_all_cracks_fade()
        // (damage_effects.gd:276-284) fades every crack over 2 s right away and removes it.
        const float CrackFadeAll = 2f;
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
            // start_all_cracks_fade(): cracks added under pressure start their 2 s fade now
            foreach (var c in _cracks)
                if (!c.Fading)
                {
                    c.Fading = true;
                    c.Age = 0f;
                }
            _redFlash.color = new Color(1, 0, 0, 0);
        }

        void AddCrack()
        {
            var sprite = GodotAssets.Sprite("textures/effects/screen_crack.png");
            if (sprite == null) return;
            float baseSize = _webgl ? 100f : 200f;
            // Godot sizes are pixels of a 1080 p screen: on a scaled-up (phone) canvas a crack keeps
            // the same share of the screen instead of growing with the UI (Factor 1: unchanged)
            float size = baseSize * Random.Range(0.5f, 1.5f) / UiScale.Factor;
            var rect = _root.rect;
            var img = UiKit.Picture(_root, "CrackLayer", sprite);
            float x = Random.Range(0f, Mathf.Max(0f, rect.width - size));
            float y = Random.Range(0f, Mathf.Max(0f, rect.height - size));
            UiKit.TopLeft(img.rectTransform, x, y, size, size);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, Random.value * 360f);
            float alpha = (_webgl ? 0.9f : 0.8f) - (_cracks.Count + 1) * (_webgl ? 0.15f : 0.1f);
            img.color = new Color(1, 1, 1, alpha);
            // Out of pressure the next reset_pressure_damage() starts its fade at once
            _cracks.Add(new Crack { Image = img, StartAlpha = alpha, Fading = !_underPressure, Age = 0f });
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

            // Fading cracks go from their alpha to 0 over 2 s, also under pressure (the tweens keep
            // running); cracks added under pressure hold until it ends (damage_effects.gd:146-154, 276-284)
            for (int i = _cracks.Count - 1; i >= 0; i--)
            {
                var c = _cracks[i];
                if (!c.Fading) continue;
                c.Age += dt;
                float t = Mathf.Clamp01(c.Age / CrackFadeAll);
                c.Image.color = new Color(1, 1, 1, Mathf.Lerp(c.StartAlpha, 0f, t));
                if (t >= 1f)
                {
                    Destroy(c.Image.gameObject);
                    _cracks.RemoveAt(i);
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
