using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TooFishy
{
    /// <summary>
    /// Device-aware UI size. Godot draws the UI for a 1920x1080 canvas, which is right on a desktop
    /// monitor but makes a 16 px label about 1 mm tall on a 6" phone. On phones and tablets (and touch
    /// browsers) the canvas is shrunk until one canvas unit is one Android dp (1/160 inch), so a 16-unit
    /// label is 16 sp like native body text. Desktop only grows on very dense screens, so a normal
    /// monitor keeps Godot's exact 1920x1080 layout (Factor 1).
    ///
    /// Layout code reads <see cref="CanvasSize"/> / <see cref="Compact"/> and re-lays out on
    /// <see cref="Changed"/> (a touch browser switches to the mobile size after its first touch).
    /// </summary>
    public static class UiScale
    {
        public const float RefWidth = 1920f, RefHeight = 1080f;
        /// <summary>One Android dp in mm.</summary>
        const float DpMm = 25.4f / 160f;
        /// <summary>Desktop: only grow when one canvas unit would be smaller than this (very dense screens).</summary>
        const float DesktopMinMm = 0.17f;
        /// <summary>A phone browser does not report a real dpi; its short side counts as this many mm.</summary>
        const float PhoneShortSideMm = 68f;
        /// <summary>Never shrink the canvas below this many units on the short side.</summary>
        const float MinCanvasShortSide = 360f;

        /// <summary>Dev override: <c>-tfdpi 269</c> makes a desktop build size its UI like a phone of that dpi.</summary>
        static readonly float s_dpiOverride = ReadDpiArg();

        static readonly List<CanvasScaler> s_scalers = new();
        static Vector4 s_key = new(-1f, -1f, -1f, -1f);

        /// <summary>Canvas units per reference unit: 1 on a desktop monitor, about 2.5 on a 6" phone.</summary>
        public static float Factor { get; private set; } = 1f;
        /// <summary>Size of the UI canvas in canvas units (1920x1080 on desktop, about 960x432 on a 20:9 phone).</summary>
        public static Vector2 CanvasSize { get; private set; } = new(RefWidth, RefHeight);
        /// <summary>Physical size of one canvas unit, or 0 when the screen size is unknown.</summary>
        public static float MmPerUnit { get; private set; }
        /// <summary>Phone-sized canvas: menus made for 1080 units of height must use their compact layout.</summary>
        public static bool Compact => CanvasSize.y < 760f;
        /// <summary>Scale for world-space texts (popups) so they grow with the UI, but less.</summary>
        public static float WorldTextScale => Mathf.Clamp(Mathf.Sqrt(Factor), 1f, 1.8f);

        public static event Action Changed;

        public static void Register(CanvasScaler scaler)
        {
            if (scaler == null || s_scalers.Contains(scaler)) return;
            s_scalers.Add(scaler);
            Refresh(force: true);
        }

        /// <summary>Called every frame by the UI; recomputes only when the screen, dpi or input mode changed.</summary>
        public static void Refresh(bool force = false)
        {
            bool mobile = Application.isMobilePlatform || GameInput.TouchMode;
            float dpi = EffectiveDpi(mobile);
            var key = new Vector4(Screen.width, Screen.height, dpi, mobile ? 1f : 0f);
            if (!force && key == s_key) return;
            bool changed = key != s_key;
            s_key = key;

            float w = Mathf.Max(1f, Screen.width), h = Mathf.Max(1f, Screen.height);
            float unitPx = Mathf.Min(w / RefWidth, h / RefHeight); // CanvasScaler "Expand" at factor 1
            float factor = 1f;
            float mmAt1 = dpi > 0f ? unitPx * 25.4f / dpi : 0f;
            if (mmAt1 > 0f)
            {
                float target = mobile ? DpMm : DesktopMinMm;
                float maxFactor = Mathf.Min(w, h) / (unitPx * MinCanvasShortSide);
                factor = Mathf.Clamp(target / mmAt1, 1f, Mathf.Max(1f, Mathf.Min(3f, maxFactor)));
            }
            Factor = factor;
            CanvasSize = new Vector2(w, h) / (unitPx * factor);
            MmPerUnit = mmAt1 * factor;

            for (int i = s_scalers.Count - 1; i >= 0; i--)
            {
                var s = s_scalers[i];
                if (s == null) { s_scalers.RemoveAt(i); continue; }
                s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                s.referenceResolution = new Vector2(RefWidth, RefHeight) / factor;
            }
            if (changed) Changed?.Invoke();
        }

        static float EffectiveDpi(bool mobile)
        {
            if (s_dpiOverride > 0f) return s_dpiOverride;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return mobile ? Mathf.Min(Screen.width, Screen.height) / (PhoneShortSideMm / 25.4f) : 0f;
            return Screen.dpi;
        }

        static float ReadDpiArg()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-tfdpi");
            if (i >= 0 && i + 1 < args.Length &&
                float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                return v;
            return 0f;
        }
    }

    /// <summary>Keeps <see cref="UiScale"/> current (window resize, WebGL switching to touch).</summary>
    public class UiScaleDriver : MonoBehaviour
    {
        void Update() => UiScale.Refresh();
    }
}
