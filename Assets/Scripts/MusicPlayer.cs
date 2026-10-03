using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    /// <summary>
    /// Background music, with crossfades between the three beds.
    ///
    /// Two AudioSources rather than one, swapped turn and turn about: changing the clip on
    /// a single source cuts the current bar off dead, and in a game where the track changes
    /// every time the day gets tight that cut is far more noticeable than the music.
    ///
    /// The track follows the pressure the player is actually under - the clock running down
    /// or the Monster getting big - rather than just the phase, so the music tightens at the
    /// same moment the game does.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        public AudioClip TitleTrack, DayTrack, RushTrack;

        [Range(0f, 1f)] public float Volume = 0.38f;
        public float FadeSeconds = 1.6f;

        [Tooltip("Seconds left in the day at or below which the music switches to Rush.")]
        public float RushTimeLeft = 25f;

        [Tooltip("Monster fullness (0-1) at or above which the music switches to Rush.")]
        public float RushMonster = 0.72f;

        const string MuteKey = "lm_music_muted";

        AudioSource _a, _b;
        AudioSource _current;          // the one currently audible
        AudioClip _wanted;
        float _fade = 1f;              // 0..1 progress of the active crossfade
        bool _muted;

        public bool Muted => _muted;

        void Awake()
        {
            _a = Make("Music A");
            _b = Make("Music B");
            _current = _a;
            _muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        }

        AudioSource Make(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f;      // music is not in the room, it is on top of it
            src.ignoreListenerPause = true;
            return src;
        }

        void Update()
        {
            HandleMuteKey();

            var want = Choose();
            if (want != _wanted)
            {
                _wanted = want;
                StartCrossfade(want);
            }

            _fade = FadeSeconds > 0f
                ? Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / FadeSeconds)
                : 1f;

            float target = _muted ? 0f : Volume;
            var other = _current == _a ? _b : _a;
            _current.volume = target * _fade;
            other.volume = target * (1f - _fade);

            if (_fade >= 1f && other.isPlaying) other.Stop();
        }

        void HandleMuteKey()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) SetMuted(!_muted);
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        }

        /// <summary>Which bed the moment calls for.</summary>
        AudioClip Choose()
        {
            var dir = GameDirector.Instance;
            if (dir == null) return TitleTrack;

            switch (dir.CurrentPhase)
            {
                case Phase.Playing:
                    return UnderPressure(dir) ? RushTrack : DayTrack;

                case Phase.DaySummary:
                    return DayTrack;

                default:
                    // Intro, Title, Help, RunOver all sit on the calm bed.
                    return TitleTrack;
            }
        }

        bool UnderPressure(GameDirector dir)
        {
            if (RushTrack == null) return false;

            if (dir.TimeLeft <= RushTimeLeft) return true;

            if (dir.Hamper == null || Tuning.MonsterFullPile <= 0) return false;
            float fullness = dir.Hamper.Waiting.Count / (float)Tuning.MonsterFullPile;
            return fullness >= RushMonster;
        }

        void StartCrossfade(AudioClip clip)
        {
            if (clip == null) return;

            var next = _current == _a ? _b : _a;
            next.clip = clip;
            next.time = 0f;
            next.Play();

            _current = next;
            _fade = 0f;
        }
    }
}
