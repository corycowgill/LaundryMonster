using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Plays sounds. Builds its own AudioListener and a small pool of 2D sources on
    /// demand, so nothing needs wiring in the scene.
    ///
    /// Machines get their own positional source instead (see Spatial), because a
    /// dryer nagging you from across the room is the whole pressure loop - a bar
    /// pulsing silently cannot do that job.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        const int PoolSize = 8;
        AudioSource[] _pool;
        int _next;

        public static SfxPlayer Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("SfxPlayer");
            Instance = go.AddComponent<SfxPlayer>();
            Instance.Build();
            return Instance;
        }

        void Awake()
        {
            if (Instance == null) { Instance = this; Build(); }
        }

        void Build()
        {
            if (_pool != null) return;

            // The camera needs a listener or nothing is audible at all.
            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                var cam = Camera.main;
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
                else gameObject.AddComponent<AudioListener>();
            }

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;    // UI / feedback sounds are flat
                _pool[i] = src;
            }
        }

        /// <summary>Fire a non-positional sound.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var p = Ensure();
            if (p._pool == null) return;

            var src = p._pool[p._next];
            p._next = (p._next + 1) % PoolSize;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        /// <summary>Attach a positional source to something in the world.</summary>
        public static AudioSource Spatial(GameObject host, float maxDistance = 22f)
        {
            Ensure();
            var src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;              // fully 3D, so direction and distance read
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2.5f;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0f;
            return src;
        }
    }
}
