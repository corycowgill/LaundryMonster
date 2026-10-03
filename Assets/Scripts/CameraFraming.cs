using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Keeps the whole room in frame whatever shape the window is.
    ///
    /// A camera has a fixed VERTICAL field of view, so narrowing the window narrows what
    /// you can see sideways. The game ships to WebGL, where the canvas is whatever shape
    /// the browser happens to be, and the room is much wider than it is deep - so a
    /// portrait-ish window quietly cut the closet off the right-hand edge. Pulling the
    /// camera back far enough to survive that by luck is how it ended up a metre of empty
    /// floor away from the game.
    ///
    /// Instead: name the points that must stay on screen, and solve for the field of view
    /// that contains all of them at the current aspect. Wide windows get the close framing
    /// the room was composed for; narrow ones widen out just enough and no further.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFraming : MonoBehaviour
    {
        [Tooltip("World points that must stay inside the picture.")]
        public Vector3[] MustSee = new Vector3[0];

        [Tooltip("Breathing room, as a fraction of the half-angle each point needs.")]
        public float Margin = 0.07f;

        public float MinFov = 30f;
        public float MaxFov = 74f;

        Camera _cam;
        float _lastAspect = -1f;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (_cam == null || MustSee.Length == 0) return;

            // Only on a real change: the solve is cheap but the camera is not something
            // to be rewritten sixty times a second for no reason.
            float aspect = _cam.aspect;
            if (Mathf.Abs(aspect - _lastAspect) < 0.0005f) return;
            _lastAspect = aspect;

            Apply(aspect);
        }

        void Apply(float aspect)
        {
            float needed = 0f;

            foreach (var p in MustSee)
            {
                // Camera space: z is depth down the lens axis, x and y are the offsets
                // the frustum has to cover at that depth.
                var local = transform.InverseTransformPoint(p);
                if (local.z <= 0.05f) continue;

                float byHeight = Mathf.Atan2(Mathf.Abs(local.y), local.z);

                // A horizontal requirement becomes a vertical one by dividing through
                // the aspect, because vertical fov is the only dial the camera has.
                float byWidth = Mathf.Atan2(Mathf.Abs(local.x) / Mathf.Max(0.01f, aspect), local.z);

                needed = Mathf.Max(needed, Mathf.Max(byHeight, byWidth));
            }

            if (needed <= 0f) return;
            _cam.fieldOfView = Mathf.Clamp(2f * needed * Mathf.Rad2Deg * (1f + Margin),
                                           MinFov, MaxFov);
        }

        /// <summary>Recompute now, for the editor-side room build and for tests.</summary>
        public void Refresh()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            _lastAspect = -1f;
            if (_cam != null) Apply(_cam.aspect);
        }
    }
}
