using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// The Monster's eyes.
    ///
    /// The results screen has always said "it has eyes now" past three quarters anger,
    /// and the pile has never actually grown any - the one moment the game promises a
    /// visible change, nothing changed. They open as the bar fills: shut and invisible
    /// on a calm day, wide and yellow when it is about to come for you.
    ///
    /// Built from primitives at runtime rather than modelled, because the pile is a
    /// generated mesh that gets scaled every frame by MonsterAnimator, and parenting
    /// two spheres to it is the only way the eyes stay on the face while it breathes.
    /// </summary>
    public class MonsterFace : MonoBehaviour
    {
        Transform _left, _right;
        Material _scleraL, _scleraR, _pupilL, _pupilR;

        float _blink = 3f;
        float _blinkTimer;
        float _open;          // 0 shut, 1 wide

        // Local to the pile, which runs y 0..1 before the root's 1.3x-2.9x scale.
        //
        // On the Monster's own head lump, on its forward slope - with the camera pitched
        // fifty degrees down that is the part of it actually pointing at the player, and
        // a face on the vertical front would be addressing the skirting board. The first
        // attempt used z -0.74, a long way outside the laundry entirely.
        static readonly Vector3 LeftEye = new Vector3(-0.19f, 0.82f, -0.30f);
        static readonly Vector3 RightEye = new Vector3(0.19f, 0.82f, -0.30f);

        // Built by MonsterArt and switched on with the eyes: the brows and the mouth.
        // Rotation and scale only - their COLOUR belongs to Highlighter, which took it
        // as the resting colour to tint from when the room was built, and two components
        // writing the same material every frame is a fight nobody wins.
        Transform _face, _browL, _browR, _mouth;

        // How the brows were built: tipped back to lie along the curve of the head. The
        // expression is rolled ON TOP of that, because writing a flat Euler would stand
        // them up off the face again every frame.
        Quaternion _browRestL = Quaternion.identity, _browRestR = Quaternion.identity;

        // Calm is a mild, slightly worried arch; furious drags the inner ends down. The
        // inner end of the left brow is its +x end, so dropping it is a negative roll,
        // and the right brow is the mirror.
        const float BrowCalm = 7f;
        const float BrowAngry = 27f;

        static readonly Color Calm = new Color(0.95f, 0.93f, 0.88f);
        static readonly Color Furious = new Color(1f, 0.82f, 0.18f);

        /// <summary>
        /// Built on the first frame rather than in Start, because Highlighter's Start
        /// walks every renderer under the Monster and takes its colour as the resting
        /// colour to tint from. Winning that race by one frame means two cream spheres
        /// permanently joining in with the highlight.
        /// </summary>
        void Ensure()
        {
            if (_left != null) return;
            _left = MakeEye("EyeL", LeftEye, out _scleraL, out _pupilL);
            _right = MakeEye("EyeR", RightEye, out _scleraR, out _pupilR);

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Face") _face = t;
                else if (t.name == "BrowL") _browL = t;
                else if (t.name == "BrowR") _browR = t;
                else if (t.name == "Mouth") _mouth = t;
            }
            if (_browL != null) _browRestL = _browL.localRotation;
            if (_browR != null) _browRestR = _browR.localRotation;
        }

        Transform MakeEye(string name, Vector3 at, out Material sclera, out Material pupil)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = at;

            var white = Prim.Make(PrimitiveType.Sphere);
            white.name = "Sclera";
            Strip(white);
            white.transform.SetParent(root, false);
            white.transform.localScale = new Vector3(0.19f, 0.19f, 0.19f);
            sclera = Unlit(white, Calm);

            var dot = Prim.Make(PrimitiveType.Sphere);
            dot.name = "Pupil";
            Strip(dot);
            dot.transform.SetParent(root, false);
            // Forward of the sclera so it is never swallowed by it at any scale.
            dot.transform.localPosition = new Vector3(0f, 0f, -0.075f);
            dot.transform.localScale = new Vector3(0.10f, 0.10f, 0.10f);
            pupil = Unlit(dot, new Color(0.06f, 0.05f, 0.08f));

            root.gameObject.SetActive(false);
            return root;
        }

        static void Strip(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>
        /// Unlit, so the eyes stay the colour they are told to be. Under the room's four
        /// warm lamps a lit sclera goes the same cream as the pile it sits on.
        /// </summary>
        static Material Unlit(GameObject go, Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var rend = go.GetComponent<Renderer>();
            var mat = shader != null ? new Material(shader) : new Material(rend.sharedMaterial);
            mat.color = c;
            rend.sharedMaterial = mat;
            return mat;
        }

        void Update()
        {
            Ensure();

            var dir = GameDirector.Instance;
            float anger = dir != null ? Mathf.Clamp01(dir.AngerFraction) : 0f;

            // Nothing below a quarter: the eyes arriving should be an event, and on a
            // clean day the pile is just laundry.
            float want = Mathf.InverseLerp(0.25f, 0.9f, anger);

            // A blink, so they read as alive rather than as two painted dots.
            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer <= 0f)
            {
                _blinkTimer = _blink + Random.Range(-1.2f, 2.5f);
                _blinkDown = 0.16f;
            }
            if (_blinkDown > 0f) _blinkDown -= Time.deltaTime;

            _open = Mathf.Lerp(_open, want, 1f - Mathf.Exp(-4f * Time.deltaTime));

            bool show = _open > 0.02f;
            if (_left.gameObject.activeSelf != show)
            {
                _left.gameObject.SetActive(show);
                _right.gameObject.SetActive(show);
                if (_face != null) _face.gameObject.SetActive(show);
            }
            if (!show) return;

            // Wide open when furious, and squeezed to a slit mid-blink.
            float lid = _blinkDown > 0f ? 0.12f : 1f;
            float size = Mathf.Lerp(0.55f, 1.25f, _open);
            var scale = new Vector3(size, size * lid, size);
            _left.localScale = scale;
            _right.localScale = scale;

            // Calm cream through to a furious yellow, with a flicker at the top end.
            var tint = Color.Lerp(Calm, Furious, _open);
            if (anger > 0.75f) tint = Color.Lerp(tint, Color.white, UiKit.Pulse(9f) * 0.3f);
            _scleraL.color = tint;
            _scleraR.color = tint;

            // The brows do most of the work. Two sock-shaped boxes rolling thirty-odd
            // degrees say "furious" more plainly than any amount of colour on the eyes,
            // and they cost two rotations a frame.
            float brow = Mathf.Lerp(BrowCalm, -BrowAngry, _open);
            if (_browL != null) _browL.localRotation = _browRestL * Quaternion.Euler(0f, 0f, brow);
            if (_browR != null) _browR.localRotation = _browRestR * Quaternion.Euler(0f, 0f, -brow);

            // And the mouth opens: a flat line on a calm day, a gape at the top end,
            // with a snarl on it once the anger is past three quarters.
            if (_mouth != null)
            {
                float gape = Mathf.Lerp(0.45f, 1.7f, _open);
                if (anger > 0.75f) gape += UiKit.Pulse(7f) * 0.22f;
                _mouth.localScale = new Vector3(1f, gape, 1f);
            }

            // Angry eyes look at you; calm ones wander.
            float wander = Mathf.Sin(Time.time * 0.9f) * (1f - _open) * 0.03f;
            var look = new Vector3(wander, 0f, -0.075f);
            _pupilL.color = _pupilR.color = new Color(0.06f, 0.05f, 0.08f);
            if (_left.childCount > 1) _left.GetChild(1).localPosition = look;
            if (_right.childCount > 1) _right.GetChild(1).localPosition = look;
        }

        float _blinkDown;
    }
}
