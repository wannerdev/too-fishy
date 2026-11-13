using System.Runtime.InteropServices;
using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Is the browser running on a touch device? Android and iOS builds never ask. In WebGL the
    /// page (Assets/WebGLTemplates/TooFishy) reports a coarse primary pointer or a real touch
    /// event through Assets/Plugins/WebGL/TooFishyTouch.jslib; everywhere else it is false.
    /// </summary>
    public static class WebTouch
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int TF_TouchDetected();
        static float _next;
        static bool _cached;

        public static bool Detected
        {
            get
            {
                if (_cached) return true; // a device that has been touched stays a touch device
                if (Time.unscaledTime < _next) return false;
                _next = Time.unscaledTime + 0.25f;
                _cached = TF_TouchDetected() != 0;
                return _cached;
            }
        }
#else
        public static bool Detected => false;
#endif
    }

    /// <summary>Shows or hides the touch canvas as WebTouch changes (it must stay on an active object).</summary>
    public class TouchCanvasToggle : MonoBehaviour
    {
        public GameObject Target;

        void Update()
        {
            if (Target == null) return;
            bool on = GameInput.TouchMode;
            if (Target.activeSelf != on) Target.SetActive(on);
        }
    }
}