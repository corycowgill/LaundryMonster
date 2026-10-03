using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// A scale punch on a prop, so an interaction is visible at the prop rather than
    /// only in the bar at the bottom of the screen.
    ///
    /// Until now every station was completely inert: you pressed E, a counter in the
    /// corner changed, and the washer you were standing in front of looked exactly as
    /// it had a moment before. The sound told you it worked and nothing else did.
    ///
    /// Overshoots once and settles, conserving volume, which is what makes a rigid box
    /// read as reacting rather than resizing.
    /// </summary>
    public class Squash : MonoBehaviour
    {
        Vector3 _base;
        float _amount, _phase;
        bool _have;

        /// <summary>Punch whatever this sits on. Safe to call every frame.</summary>
        public void Hit(float amount)
        {
            Capture();
            _amount = Mathf.Min(0.35f, _amount + amount);
            _phase = 0f;
        }

        void Capture()
        {
            if (_have) return;
            _base = transform.localScale;
            _have = true;
        }

        void Update()
        {
            if (!_have || _amount <= 0.0001f) return;

            _phase += Time.deltaTime * 13f;
            float w = _amount * Mathf.Sin(_phase) * Mathf.Exp(-_phase * 0.7f);
            _amount = Mathf.Max(0f, _amount - Time.deltaTime * 0.7f);

            transform.localScale = new Vector3(_base.x * (1f - w * 0.5f),
                                               _base.y * (1f + w),
                                               _base.z * (1f - w * 0.5f));

            if (_amount <= 0.0001f) transform.localScale = _base;
        }

        // ---------- helpers ----------

        /// <summary>
        /// Punch a station. Skips anything already animating its own scale - the Monster
        /// runs MonsterAnimator every frame and a second writer just fights it.
        /// </summary>
        public static void Pop(Component at, float amount = 0.16f)
        {
            if (at == null) return;
            if (at.GetComponent<MonsterAnimator>() != null) return;

            var s = at.GetComponent<Squash>();
            if (s == null) s = at.gameObject.AddComponent<Squash>();
            s.Hit(amount);
        }
    }

    /// <summary>
    /// A handful of tumbling cubes thrown out of a point in the world.
    ///
    /// Opaque, unlit, and deleted on a timer: no transparency and no particle system,
    /// for the same reason the highlight pads are built this way - nothing here can be
    /// stripped out of a WebGL build or go pink under a pipeline it did not expect.
    /// </summary>
    public class Confetti : MonoBehaviour
    {
        Vector3 _vel, _spin;
        float _life = 0.9f;

        public static void Burst(Vector3 at, Color tint, int count = 10)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");

            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Confetti";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);

                var rend = go.GetComponent<Renderer>();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                var mat = shader != null ? new Material(shader) : new Material(rend.sharedMaterial);
                // Each scrap a slightly different shade, so the burst does not read as
                // one object that shattered.
                mat.color = Color.Lerp(tint, Color.white, Random.value * 0.45f);
                rend.sharedMaterial = mat;

                go.transform.position = at + Random.insideUnitSphere * 0.12f;
                go.transform.localScale = Vector3.one * Random.Range(0.05f, 0.11f);
                go.transform.rotation = Random.rotation;

                var c = go.AddComponent<Confetti>();
                var dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.8f;          // always upward
                c._vel = dir.normalized * Random.Range(2.2f, 3.6f);
                c._spin = Random.insideUnitSphere * 540f;
                c._life = Random.Range(0.7f, 1.1f);
            }
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }

            _vel.y -= 11f * Time.deltaTime;
            transform.position += _vel * Time.deltaTime;
            transform.Rotate(_spin * Time.deltaTime, Space.World);

            // Shrink out instead of fading out, because fading needs a transparent
            // material and this one is deliberately opaque.
            transform.localScale = Vector3.one * Mathf.Clamp01(_life) * 0.11f;
        }
    }
}
