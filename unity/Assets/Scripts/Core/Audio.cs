using UnityEngine;

namespace TooFishy
{
    /// <summary>
    /// Port of scripts/soundplayer.gd: one player node whose stream is replaced on every call,
    /// so a new sound cuts off the previous one exactly like the Godot version.
    /// </summary>
    public static class SoundPlayer
    {
        static AudioSource _source;

        public static void Play(string soundName)
        {
            string path = soundName switch
            {
                "bupp" => "sounds/bupp.wav",
                "urrgh" => "sounds/urrgh.wav",
                "ughhh" => "sounds/ughhh.wav",
                "ouugh" => "sounds/Ouugh.wav",
                "bup" => "sounds/bup.wav",
                "harp" => "sounds/harp.wav",
                "harp2" => "sounds/harp2.wav",
                "harp3" => "sounds/harp3.wav",
                "coins" => "sounds/coins.wav",
                "save" => "sounds/harp2.wav",
                _ => null
            };
            if (path == null)
            {
                Debug.LogWarning($"[SoundPlayer] Unknown sound: {soundName}");
                return;
            }
            var clip = GodotAssets.Audio(path);
            if (clip == null) return;

            if (_source == null)
            {
                var go = new GameObject("SoundPlayer");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            // main_scene.tscn: SoundPlayer/player volume_db = -11.876, then the SFX setting
            _source.volume = Mathf.Pow(10f, -11.876f / 20f) * Settings.SfxVolume;
            _source.clip = clip;
            _source.Play();
        }
    }

    /// <summary>
    /// Port of scripts/musicplayer.gd: two looping players, 2 s crossfade (linear in dB from -15 to
    /// -80) whenever the stage's track changes.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const float BaseVolumeDb = -15f;
        const float SilentDb = -80f;
        const float FadeDuration = 2f;

        AudioSource _player1, _player2;
        string _currentTrack, _nextTrack;
        bool _crossfading;
        float _fadeTimer;

        void Start()
        {
            _player1 = NewSource();
            _player2 = NewSource();
            _currentTrack = "music/surface.mp3";
            _player1.clip = GodotAssets.Audio(_currentTrack);
            _player1.volume = DbToLinear(BaseVolumeDb) * Settings.MusicVolume;
            _player2.volume = 0f;
            if (_player1.clip != null) _player1.Play();
        }

        AudioSource NewSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            if (_player1 == null) return;
            float music = Settings.MusicVolume;

            if (_crossfading)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                float t = Mathf.Min(_fadeTimer / FadeDuration, 1f);
                _player1.volume = DbToLinear(Mathf.Lerp(BaseVolumeDb, SilentDb, t)) * music;
                _player2.volume = DbToLinear(Mathf.Lerp(SilentDb, BaseVolumeDb, t)) * music;
                if (_fadeTimer >= FadeDuration)
                {
                    _crossfading = false;
                    _player1.Stop();
                    (_player1, _player2) = (_player2, _player1);
                    _currentTrack = _nextTrack;
                }
                return;
            }

            var gs = GameState.Instance;
            if (gs == null) return;
            string expected = TrackFor(gs.PlayerInStage);
            if (expected != _currentTrack)
            {
                var clip = GodotAssets.Audio(expected);
                if (clip == null) return;
                _nextTrack = expected;
                _player2.clip = clip;
                _player2.volume = 0f;
                _player2.Play();
                _fadeTimer = 0f;
                _crossfading = true;
            }
            else
            {
                _player1.volume = DbToLinear(BaseVolumeDb) * music;
                if (!_player1.isPlaying && _player1.clip != null) _player1.Play();
            }
        }

        static string TrackFor(Stage stage) => stage switch
        {
            Stage.Surface => "music/surface.mp3",
            Stage.Deep or Stage.Deeper or Stage.SuperDeep => "music/deep.mp3",
            Stage.Hot or Stage.Lava => "music/hotzone.mp3",
            Stage.Void => "music/bossfight.mp3",
            _ => "music/surface.mp3"
        };

        static float DbToLinear(float db) => db <= SilentDb ? 0f : Mathf.Pow(10f, db / 20f);
    }
}
