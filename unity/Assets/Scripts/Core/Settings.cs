using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/settings_manager.gd: master/music/SFX volume, mute, environmental particles
    /// and the FPS counter, persisted between sessions (PlayerPrefs instead of user://settings.cfg).
    /// </summary>
    public static class Settings
    {
        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static bool Muted { get; private set; }
        public static bool ShowParticles { get; private set; } = true;
        public static bool ShowFps { get; private set; } = true;

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            MasterVolume = PlayerPrefs.GetFloat("audio.master_volume", 1f);
            MusicVolume = PlayerPrefs.GetFloat("audio.music_volume", 1f);
            SfxVolume = PlayerPrefs.GetFloat("audio.sfx_volume", 1f);
            Muted = PlayerPrefs.GetInt("audio.mute", 0) == 1;
            // Godot disables environmental particles by default on the web
            bool defaultParticles = Application.platform != RuntimePlatform.WebGLPlayer;
            ShowParticles = PlayerPrefs.GetInt("display.particles", defaultParticles ? 1 : 0) == 1;
            ShowFps = PlayerPrefs.GetInt("display.fps_counter", 1) == 1;
            Apply();
        }

        static void Save()
        {
            PlayerPrefs.SetFloat("audio.master_volume", MasterVolume);
            PlayerPrefs.SetFloat("audio.music_volume", MusicVolume);
            PlayerPrefs.SetFloat("audio.sfx_volume", SfxVolume);
            PlayerPrefs.SetInt("audio.mute", Muted ? 1 : 0);
            PlayerPrefs.SetInt("display.particles", ShowParticles ? 1 : 0);
            PlayerPrefs.SetInt("display.fps_counter", ShowFps ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void Apply()
        {
            AudioListener.volume = Muted ? 0f : MasterVolume;
        }

        public static void SetMasterVolume(float v) { MasterVolume = v; Apply(); Save(); }
        public static void SetMusicVolume(float v) { MusicVolume = v; Apply(); Save(); }
        public static void SetSfxVolume(float v) { SfxVolume = v; Apply(); Save(); }
        public static void SetMuted(bool m) { Muted = m; Apply(); Save(); }
        public static void SetShowParticles(bool s) { ShowParticles = s; Save(); }
        public static void SetShowFps(bool s) { ShowFps = s; Save(); }
    }
}
