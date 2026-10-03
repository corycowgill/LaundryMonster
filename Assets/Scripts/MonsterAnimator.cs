using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Gives the Monster life. It is a pile of laundry that eats your neglect, and it
    /// should be funny rather than frightening: it breathes, it sways, and when it eats
    /// something it does a delighted little bounce.
    ///
    /// GameDirector sets TargetScale; everything here is presentation. Growth is eased
    /// rather than snapped so that getting bigger is a visible event you can catch out
    /// of the corner of your eye.
    /// </summary>
    public class MonsterAnimator : MonoBehaviour
    {
        [Header("Set by GameDirector")]
        public float TargetScale = 0.45f;

        [Header("Idle life")]
        public float BreathSpeed = 1.25f;
        public float BreathAmount = 0.05f;
        public float SwaySpeed = 0.8f;
        public float SwayDegrees = 4.5f;

        [Header("Reaction")]
        public float GrowPop = 0.35f;
        public float EaseSpeed = 2.6f;

        float _current = 0.45f;
        float _pop;              // decaying squash-and-stretch impulse
        float _popPhase;
        float _lastTarget;

        void Start()
        {
            _current = TargetScale;
            _lastTarget = TargetScale;
        }

        float _reach;          // seconds of reaching left
        float _reachTotal;

        /// <summary>
        /// Lean out toward The Chair for the duration of a snatch attempt. Purely a
        /// telegraph: the attack is decided by MonsterAttack, this just makes it obvious
        /// that something is happening and roughly how far along it is.
        /// </summary>
        public void Reach(float seconds)
        {
            _reach = seconds;
            _reachTotal = Mathf.Max(0.01f, seconds);
        }

        /// <summary>Call when the Monster has just been fed, for a visible reaction.</summary>
        public void React(float amount)
        {
            _pop = Mathf.Min(1f, _pop + Mathf.Clamp01(amount / 3f) * GrowPop + 0.12f);
            _popPhase = 0f;
        }

        void Update()
        {
            // Ease toward the size the game says we should be.
            if (TargetScale > _lastTarget + 0.0001f) React(TargetScale - _lastTarget);
            _lastTarget = TargetScale;

            _current = Mathf.Lerp(_current, TargetScale, 1f - Mathf.Exp(-EaseSpeed * Time.deltaTime));

            // The pop is a damped wobble, so it overshoots once and settles.
            _popPhase += Time.deltaTime * 11f;
            float pop = _pop * Mathf.Sin(_popPhase) * Mathf.Exp(-_popPhase * 0.55f);
            _pop = Mathf.Max(0f, _pop - Time.deltaTime * 0.35f);

            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount;

            // Squash and stretch conserve volume, which is what sells it as soft.
            float y = _current * (1f + breath + pop);
            float xz = _current * (1f - (breath + pop) * 0.45f);
            transform.localScale = new Vector3(xz, y, xz);

            // A lazy sway, plus a quicker wobble while it is reacting.
            float sway = Mathf.Sin(Time.time * SwaySpeed) * SwayDegrees;
            float wobble = pop * 22f;

            // Reaching: lean further the closer it is to taking something, with a shiver
            // on top so it reads as straining rather than merely tilted.
            float lean = 0f;
            if (_reach > 0f)
            {
                _reach = Mathf.Max(0f, _reach - Time.deltaTime);
                float t = 1f - (_reach / _reachTotal);
                lean = Mathf.Lerp(6f, 26f, t) + Mathf.Sin(Time.time * 26f) * 2.5f * t;
            }

            transform.localRotation = Quaternion.Euler(lean * 0.35f, sway * 0.6f, sway + wobble + lean);
        }
    }
}
