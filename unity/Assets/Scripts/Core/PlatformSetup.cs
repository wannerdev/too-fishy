using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Platform settings applied before the first scene loads.
    /// </summary>
    public static class PlatformSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            // project.godot has no display/window/energy_saving/keep_screen_on override, so Godot's
            // default (true) keeps the phone screen on; Unity's default follows the system timeout.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
