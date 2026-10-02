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
        public float StepsPerSecond = 2.6f;
        public float BobHeight = 0.085f;
        public float RollDegrees = 6.5f;
        public float LeanDegrees = 11f;

        [Header("Squash and stretch")]
        public float Stretch = 0.07f;

        [Header("Idle")]
        public float BreathSpeed = 1.7f;
        public float BreathAmount = 0.022f;

        PlayerController _player;
        Transform _model;
        Vector3 _modelBasePos;
        Vector3 _modelBaseScale;

        float _phase;          // walk cycle, radians
        float _speed01;        // 0..1 smoothed movement amount
        float _lean;           // smoothed lean angle
        Vector3 _lastPos;
        bool _stepFlag;        // so each footfall fires once

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

            _model.localPosition = _modelBasePos + new Vector3(0f, bob, 0f);
            _model.localRotation = Quaternion.Euler(_lean + carryLean, 0f, roll);
            _model.localScale = new Vector3(
                _modelBaseScale.x * (1f - sq * 0.5f + breath * 0.5f),
                _modelBaseScale.y * (1f + sq + breath),
                _modelBaseScale.z * (1f - sq * 0.5f + breath * 0.5f));

            Footsteps(bobSin);
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
