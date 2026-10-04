using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// A tiny world-space bar built from two quads. Deliberately font-free so it
    /// cannot be broken by font/TMP setup, and the camera is fixed so no billboarding.
    /// </summary>
    public class ProgressBar : MonoBehaviour
    {
        Transform _fill;
        Renderer _fillRend;
        Renderer _bgRend;
        Material _fillMat;

        public static ProgressBar Attach(Transform parent, Vector3 localOffset)
        {
            var root = new GameObject("ProgressBar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localOffset;
            root.transform.localRotation = Quaternion.Euler(50f, 0f, 0f); // match the fixed camera pitch

            var bar = root.AddComponent<ProgressBar>();
            bar.Build();
            return bar;
        }

        void Build()
        {
            _bgRend = MakeQuad("BG", new Vector3(0f, 0f, 0f), new Vector3(1.3f, 0.22f, 1f),
                               new Color(0.08f, 0.08f, 0.10f));

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            _fill = fillGo.transform;

            var quad = MakeQuadOn(fillGo, new Vector3(1.24f, 0.16f, 1f), Color.white);
            _fillRend = quad;
            _fillMat = quad.material;

            // Pivot the fill on its left edge so it grows left-to-right.
            quad.transform.localPosition = new Vector3(0.5f, 0f, -0.01f);
            Hide();
        }

        Renderer MakeQuad(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var r = MakeQuadOn(go, scale, color);
            return r;
        }

        Renderer MakeQuadOn(GameObject go, Vector3 scale, Color color)
        {
            var quad = Prim.Make(PrimitiveType.Quad);
            quad.name = "quad";
            quad.transform.SetParent(go.transform, false);
            quad.transform.localScale = scale;

            var rend = quad.GetComponent<Renderer>();

            // Shader.Find only sees shaders the build actually kept. Nothing in the scene
            // references Unlit, so the stripper removes it and this returns null in a
            // player - which threw ArgumentNullException and left every bar magenta.
            // The pipeline's own default material is always included, so it is the
            // one safe fallback.
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
            }
            else
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var fallback = rp != null ? rp.defaultMaterial : null;
                if (fallback == null)
                {
                    Debug.LogError("ProgressBar: no usable shader; bar will not render.");
                    return rend;
                }
                mat = new Material(fallback);
            }
            mat.color = color;
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return rend;
        }

        public void Set(float fraction, Color color)
        {
            fraction = Mathf.Clamp01(fraction);
            if (_bgRend != null) _bgRend.enabled = true;
            if (_fillRend != null) _fillRend.enabled = true;

            if (_fill != null)
                _fill.localScale = new Vector3(Mathf.Max(fraction, 0.001f), 1f, 1f);

            // The fill is pivoted at x=+0.5 inside a left-anchored parent.
            if (_fill != null)
                _fill.localPosition = new Vector3(-0.62f, 0f, 0f);

            if (_fillMat != null) _fillMat.color = color;
        }

        public void Hide()
        {
            if (_bgRend != null) _bgRend.enabled = false;
            if (_fillRend != null) _fillRend.enabled = false;
        }
    }
}
