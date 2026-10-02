using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Procedural animation for the hero. The generated mesh has no skeleton, so instead
    /// of skinned animation this drives the model child's local transform: bob, roll,
    /// lean and squash-and-stretch.
    ///
    /// It only ever touches the MODEL CHILD. The root transform stays exactly where the
    /// controller put it, so interaction distances and the carry anchor are unaffected.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class CharacterAnimator : MonoBehaviour
    {
        [Header("Walk")]
        public float StepsPerSecond = 2.1f;
        public float BobHeight = 0.020f;
        public float RollDegrees = 2.0f;
        public float LeanDegrees = 4.5f;

        [Header("Squash and stretch")]
        public float Stretch = 0.010f;

        [Header("Idle")]
        public float BreathSpeed = 1.7f;
        public float BreathAmount = 0.012f;

        PlayerController _player;
        Transform _model;
        Vector3 _modelBasePos;
        Vector3 _modelBaseScale;

        float _phase;          // walk cycle, radians
        float _speed01;        // 0..1 smoothed movement amount
        float _lean;           // smoothed lean angle
        float _reach;          // decaying grab impulse
        float _reachPhase;
        float _work;           // smoothed "doing a timed action" amount
        Vector3 _lastPos;
        bool _stepFlag;        // so each footfall fires once

        // Skeleton. Null when the model is unrigged, in which case only the body
        // bob runs and nothing here throws.
        Transform _chest, _head;
        Transform _upperArmL, _lowerArmL, _upperArmR, _lowerArmR;
        Transform _upperLegL, _lowerLegL, _upperLegR, _lowerLegR;
        Quaternion _restChest, _restHead;
        Quaternion _restUAL, _restLAL, _restUAR, _restLAR;
        Quaternion _restULL, _restLLL, _restULR, _restLLR;
        bool _rigged;

        [Header("Limbs (degrees)")]
        public float ArmSwing = 32f;
        public float LegSwing = 26f;
        public float ElbowBend = 18f;
        public float GrabReach = 62f;
        public float CarryArmLift = 48f;

        void Start()
        {
            _player = GetComponent<PlayerController>();

            // The generated model, or the primitive body if the model is missing.
            _model = transform.Find("Model");
            if (_model == null)
            {
                var r = GetComponentInChildren<Renderer>();
                if (r != null) _model = r.transform;
            }
            if (_model == null) { enabled = false; return; }

            _modelBasePos = _model.localPosition;
            _modelBaseScale = _model.localScale;
            _lastPos = transform.position;

            BindSkeleton();
        }

        Transform FindBone(string n)
        {
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                if (t.name == n) return t;
            return null;
        }

        void BindSkeleton()
        {
            _chest = FindBone("Chest");      _head = FindBone("Head");
            _upperArmL = FindBone("UpperArmL"); _lowerArmL = FindBone("LowerArmL");
            _upperArmR = FindBone("UpperArmR"); _lowerArmR = FindBone("LowerArmR");
            _upperLegL = FindBone("UpperLegL"); _lowerLegL = FindBone("LowerLegL");
            _upperLegR = FindBone("UpperLegR"); _lowerLegR = FindBone("LowerLegR");

            _rigged = _upperArmL != null && _upperArmR != null;
            if (!_rigged) return;

            // Remember the bind pose; every rotation below is relative to it.
            if (_chest) _restChest = _chest.localRotation;
            if (_head) _restHead = _head.localRotation;
            _restUAL = _upperArmL.localRotation; _restLAL = _lowerArmL ? _lowerArmL.localRotation : Quaternion.identity;
            _restUAR = _upperArmR.localRotation; _restLAR = _lowerArmR ? _lowerArmR.localRotation : Quaternion.identity;
            if (_upperLegL) _restULL = _upperLegL.localRotation;
            if (_lowerLegL) _restLLL = _lowerLegL.localRotation;
            if (_upperLegR) _restULR = _upperLegR.localRotation;
            if (_lowerLegR) _restLLR = _lowerLegR.localRotation;
        }

        static void Swing(Transform bone, Quaternion rest, float degrees)
        {
            if (bone != null) bone.localRotation = rest * Quaternion.Euler(degrees, 0f, 0f);
        }

        /// <summary>Called when the player actually grabs or operates something.</summary>
        public void Grab()
        {
            _reach = 1f;
            _reachPhase = 0f;
        }

        void LateUpdate()
        {
            // Measure actual movement rather than reading input, so this stays correct
            // no matter what moves the player.
            var delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;

            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            float target = Mathf.Clamp01(speed / Mathf.Max(0.01f, _player.MoveSpeed));
            _speed01 = Mathf.Lerp(_speed01, target, 1f - Mathf.Exp(-12f * Time.deltaTime));

            _phase += Time.deltaTime * StepsPerSecond * Mathf.PI * 2f * Mathf.Max(_speed01, 0.0001f);

            float bobSin = Mathf.Sin(_phase);
            float bob = Mathf.Abs(bobSin) * BobHeight * _speed01;     // two footfalls per cycle
            float roll = Mathf.Sin(_phase * 0.5f) * RollDegrees * _speed01;

            // Lean into travel, in the body's own frame.
            float leanTarget = LeanDegrees * _speed01;
            _lean = Mathf.Lerp(_lean, leanTarget, 1f - Mathf.Exp(-9f * Time.deltaTime));

            // Carrying two things leans you back a little; it should look like effort.
            float carry = _player.Carried.Count / (float)Mathf.Max(1, _player.CarryCapacity);
            float carryLean = -5f * carry;

            // Squash at the bottom of the step, stretch at the top.
            float sq = bobSin * Stretch * _speed01;
            // Idle breathing takes over when standing still.
            float breath = Mathf.Sin(Time.time * BreathSpeed) * BreathAmount * (1f - _speed01);

            // Grab: a quick damped dip forward, so picking things up reads as an action.
            _reachPhase += Time.deltaTime * 9f;
            float reach = _reach * Mathf.Sin(_reachPhase) * Mathf.Exp(-_reachPhase * 0.9f);
            _reach = Mathf.Max(0f, _reach - Time.deltaTime * 1.6f);

            // Working: folding or checking pockets holds a leaned-in pose with a small
            // repetitive motion, so a hold looks like effort rather than standing still.
            bool holding = _player.HoldProgress > 0f;
            _work = Mathf.Lerp(_work, holding ? 1f : 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));
            float workBob = Mathf.Sin(Time.time * 9f) * 0.012f * _work;
            float workLean = 13f * _work;

            _model.localPosition = _modelBasePos + new Vector3(0f, bob + workBob, 0f);
            _model.localRotation = Quaternion.Euler(
                _lean + carryLean + reach * 16f + workLean, 0f, roll);
            _model.localScale = new Vector3(
                _modelBaseScale.x * (1f - sq * 0.5f + breath * 0.5f),
                _modelBaseScale.y * (1f + sq + breath),
                _modelBaseScale.z * (1f - sq * 0.5f + breath * 0.5f));

            AnimateLimbs(bobSin, reach);
            Footsteps(bobSin);
        }

        /// <summary>
        /// Arms and legs swing in opposition on the walk cycle; grabbing swings both
        /// arms forward sharply; carrying holds them up in front.
        /// </summary>
        void AnimateLimbs(float bobSin, float reach)
        {
            if (!_rigged) return;

            float swing = Mathf.Sin(_phase * 0.5f);           // one full stride per cycle
            float arm = swing * ArmSwing * _speed01;
            float leg = swing * LegSwing * _speed01;

            float carry = _player.Carried.Count / (float)Mathf.Max(1, _player.CarryCapacity);
            float hold = CarryArmLift * carry;                 // both arms up, holding the load
            float grab = Mathf.Clamp01(reach) * GrabReach;     // a reach forward

            // Arms swing opposite each other, and opposite the legs on the same side.
            Swing(_upperArmL, _restUAL, -arm - hold - grab);
            Swing(_upperArmR, _restUAR, arm - hold - grab);
            Swing(_lowerArmL, _restLAL, -ElbowBend * (0.4f + carry) - grab * 0.35f);
            Swing(_lowerArmR, _restLAR, -ElbowBend * (0.4f + carry) - grab * 0.35f);

            Swing(_upperLegL, _restULL, leg);
            Swing(_upperLegR, _restULR, -leg);
            // Knees only bend backwards, on the leg that is trailing.
            Swing(_lowerLegL, _restLLL, Mathf.Max(0f, -leg) * 0.9f);
            Swing(_lowerLegR, _restLLR, Mathf.Max(0f, leg) * 0.9f);

            if (_chest != null)
                _chest.localRotation = _restChest * Quaternion.Euler(_work * 10f + grab * 0.12f, 0f, 0f);
            if (_head != null)
                _head.localRotation = _restHead * Quaternion.Euler(-_work * 6f, 0f, 0f);
        }

        void Footsteps(float bobSin)
        {
            // Fire on the downbeat of each half cycle, once.
            bool low = bobSin < 0.15f && bobSin > -0.15f;
            if (_speed01 < 0.25f) { _stepFlag = false; return; }

            if (low && !_stepFlag)
            {
                _stepFlag = true;
                SfxPlayer.Play(Sfx.Step, 0.16f * _speed01, Random.Range(0.92f, 1.1f));
            }
            else if (!low)
            {
                _stepFlag = false;
            }
        }
    }
}
