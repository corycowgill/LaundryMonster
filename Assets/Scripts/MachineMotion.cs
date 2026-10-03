using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Makes a running machine look like it is running.
    ///
    /// A washer mid-cycle and a washer sitting idle were pixel-identical: the only way
    /// to tell was to read the badge floating above it. In a game where you are crossing
    /// the room deciding which machine to go to, that is a question the room should be
    /// able to answer by itself.
    ///
    /// Three states, each with its own motion:
    ///   running  - a fast, small judder, stronger near the end of a spin cycle
    ///   finished - a slow nod, so a load waiting to be taken out waves at you
    ///   on fire  - a violent shake
    /// </summary>
    [RequireComponent(typeof(LaundryMachine))]
    public class MachineMotion : MonoBehaviour
    {
        LaundryMachine _m;
        Vector3 _base;
        Transform _band;
        Material _bandMat;
        Color _bandBase;

        void Start()
        {
            _m = GetComponent<LaundryMachine>();
            _base = transform.position;

            _band = transform.Find("FrontBand");
            if (_band != null)
            {
                var r = _band.GetComponent<Renderer>();
                if (r != null)
                {
                    // Its own material instance: Highlighter hands every renderer under
                    // the machine a copy already, but relying on that ordering is how the
                    // Monster's eyes ended up tinted.
                    _bandMat = new Material(r.sharedMaterial);
                    r.sharedMaterial = _bandMat;
                    _bandBase = _bandMat.color;
                }
            }
        }

        void Update()
        {
            if (_m == null) return;

            var dir = GameDirector.Instance;
            // A results screen is not playtime, and a room full of juddering machines
            // behind a summary card reads as a bug.
            bool live = dir == null || dir.IsRunning;

            float shakeX = 0f, shakeY = 0f;
            float glow = 0f;

            if (!live)
            {
                // nothing
            }
            else if (_m.Offline)
            {
                shakeX = Mathf.Sin(Time.time * 41f) * 0.045f;
                shakeY = Mathf.Sin(Time.time * 53f) * 0.030f;
                glow = 1f;
            }
            else if (_m.Running)
            {
                // Builds through the cycle: the spin is the loud part, and it gives the
                // player a read on "nearly done" from across the room.
                float t = Mathf.Clamp01(_m.CycleFraction);
                float amp = Mathf.Lerp(0.006f, 0.022f, t * t);
                shakeX = Mathf.Sin(Time.time * 34f) * amp;
                shakeY = Mathf.Abs(Mathf.Sin(Time.time * 17f)) * amp * 0.8f;
                glow = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 3.4f));
            }
            else if (_m.HasFinishedLoad)
            {
                // A slow nod rather than a shake: it is not working, it is waiting.
                shakeY = (Mathf.Sin(Time.time * 3.1f) * 0.5f + 0.5f) * 0.035f;
                glow = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.2f);
            }

            transform.position = _base + new Vector3(shakeX, shakeY, 0f);

            if (_bandMat != null)
                _bandMat.color = Color.Lerp(_bandBase, Color.white, glow * 0.45f);
        }

        /// <summary>The machine has been moved by a rebuild; take the new spot as home.</summary>
        public void Rebase() => _base = transform.position;
    }
}
