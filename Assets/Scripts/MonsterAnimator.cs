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

        float _armT;           // 0 tucked in, 1 fully reaching
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

        Transform _arm;
        bool _lookedForArm;

        /// <summary>
        /// The sleeve that stretches toward the sorting basket during a snatch.
        ///
        /// Found lazily rather than wired, because RoomBuilder regenerates the scene and
        /// a serialised reference would be the one thing in here that goes stale.
        /// </summary>
        Transform Arm()
        {
            if (_lookedForArm) return _arm;
            _lookedForArm = true;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "ArmPivot") { _arm = t; break; }
            return _arm;
        }

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
            float reachT = 0f;
            if (_reach > 0f)
            {
                _reach = Mathf.Max(0f, _reach - Time.deltaTime);
                reachT = 1f - (_reach / _reachTotal);
                lean = Mathf.Lerp(6f, 26f, t: reachT) + Mathf.Sin(Time.time * 26f) * 2.5f * reachT;
            }

            // The arm. A twenty-six degree lean is not a threat anybody notices across a
            // busy room, and the snatch is the one thing the Monster does on purpose: it
            // has three seconds to be understood. So the sleeve unfolds and stretches out
            // toward the basket over that countdown, which is readable from anywhere and
            // needs no text. Eased so most of the travel happens early and the last of it
            // creeps, which is what makes the final second feel like it is about to land.
            var arm = Arm();
            if (arm != null)
            {
                float t = _reach > 0f ? 1f - Mathf.Pow(1f - reachT, 2.2f) : 0f;
                _armT = Mathf.Lerp(_armT, t, 1f - Mathf.Exp(-9f * Time.deltaTime));
                arm.localRotation = Quaternion.Slerp(Quaternion.Euler(MonsterArm.RestEuler),
                                                     Quaternion.Euler(MonsterArm.ReachEuler),
                                                     _armT);
                arm.localScale = Vector3.Lerp(MonsterArm.RestScale, MonsterArm.ReachScale, _armT);
            }

            transform.localRotation = Quaternion.Euler(lean * 0.35f, sway * 0.6f, sway + wobble + lean);
        }
    }
}
