using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Drives the hero's Animator from what the controller is actually doing.
    ///
    /// Replaces the old procedural bob. The clips are real skeletal animation - text
    /// prompts through NVIDIA Kimodo, retargeted onto our rig - so this component only
    /// has to translate movement into the handful of parameters the controller reads.
    ///
    /// The Animator lives on the MODEL CHILD, never the root. Root motion stays off: the
    /// clips are in-place and PlayerController owns position, so interaction distances
    /// and the carry anchor are unaffected by whatever the animation does.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class HeroAnimator : MonoBehaviour
    {
        [Header("Playback")]
        [Tooltip("Speed the walk clip was authored at, in metres per second.")]
        public float ClipWalkSpeed = 1.35f;

        [Tooltip("Ceiling on clip playback rate. The hero outruns a real walk by a long "
               + "way, and past about 2x the legs read as a blur rather than as steps.")]
        public float MaxPlaybackRate = 2.1f;

        [Tooltip("How quickly the idle/walk blend responds, in units per second.")]
        public float BlendResponse = 6f;

        static readonly int PSpeed = Animator.StringToHash("Speed");
        static readonly int PLocoSpeed = Animator.StringToHash("LocoSpeed");
        static readonly int PCarrying = Animator.StringToHash("Carrying");
        static readonly int PGrab = Animator.StringToHash("Grab");
        static readonly int PCelebrate = Animator.StringToHash("Celebrate");

        PlayerController _player;
        Animator _anim;
        Vector3 _lastPos;
        float _speed01;

        void Start()
        {
            _player = GetComponent<PlayerController>();
            _lastPos = transform.position;

            _anim = GetComponentInChildren<Animator>();
            if (_anim == null)
            {
                // Nothing rigged in the scene (primitive fallback hero). Everything below
                // degrades to a no-op rather than throwing every frame.
                enabled = false;
                return;
            }
            _anim.applyRootMotion = false;
            _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void Update()
        {
            var pos = transform.position;
            var delta = pos - _lastPos;
            delta.y = 0f;
            _lastPos = pos;

            float dt = Mathf.Max(Time.deltaTime, 1e-5f);
            float speed = delta.magnitude / dt;

            // Measured speed is noisy at the clamp edges of the room, so it feeds a
            // smoothed 0..1 rather than driving the blend directly.
            float target = Mathf.Clamp01(speed / Mathf.Max(_player.MoveSpeed, 0.01f));
            _speed01 = Mathf.MoveTowards(_speed01, target, BlendResponse * Time.deltaTime);

            _anim.SetFloat(PSpeed, _speed01);
            _anim.SetBool(PCarrying, _player.Carried.Count > 0);

            // Step rate follows real speed so the feet stay roughly planted, up to the cap.
            float rate = speed > 0.05f
                ? Mathf.Clamp(speed / Mathf.Max(ClipWalkSpeed, 0.01f), 0.6f, MaxPlaybackRate)
                : 1f;
            _anim.SetFloat(PLocoSpeed, rate);
        }

        /// <summary>Play the pick-up one-shot. Called when an interaction lands.</summary>
        public void Grab()
        {
            if (_anim != null) _anim.SetTrigger(PGrab);
        }

        /// <summary>Play the cheer. For the end of a successful day.</summary>
        public void Celebrate()
        {
            if (_anim != null) _anim.SetTrigger(PCelebrate);
        }
    }
}
