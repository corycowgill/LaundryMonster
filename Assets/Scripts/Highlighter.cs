using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Marks the station the player is standing at. Until now the only cue was a line
    /// of text at the bottom of the screen, which means reading instead of seeing -
    /// in a game about panicking, that is the wrong place to put the information.
    ///
    /// Draws a lit pad on the floor under the station and lifts the station's own
    /// colour. Opaque geometry only: no transparency, no emission keywords, so
    /// nothing here can be stripped out of a build.
    /// </summary>
    public class Highlighter : MonoBehaviour
    {
        readonly List<Renderer> _rends = new List<Renderer>();
        readonly List<Color> _baseColors = new List<Color>();
        readonly List<Material> _mats = new List<Material>();

        Transform _pad;
        Material _padMat;
        bool _on;

        const float PadSize = 2.9f;

        static readonly Color PadIdle = new Color(0.35f, 0.75f, 0.95f);
        static readonly Color PadBusy = new Color(0.95f, 0.72f, 0.25f);

        void Start()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<ProgressBar>() != null) continue;  // leave bars alone
                var m = new Material(r.sharedMaterial);
                r.sharedMaterial = m;
                _rends.Add(r);
                _mats.Add(m);
                _baseColors.Add(m.color);
            }
            BuildPad();
        }

        void BuildPad()
        {
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");

            Material mat;
            if (shader != null) mat = new Material(shader);
            else if (rp != null && rp.defaultMaterial != null) mat = new Material(rp.defaultMaterial);
            else return;

            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "HighlightPad";
            var col = disc.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _pad = disc.transform;
            // Sibling, not child: the Monster is scaled and rotated every frame by its
            // animator, and a child pad would be dragged around with it.
            _pad.SetParent(transform.parent, true);
            _pad.position = new Vector3(transform.position.x, 0.03f, transform.position.z);
            _pad.localRotation = Quaternion.identity;
            // Must be wider than the prop itself or the prop hides it completely.
            _pad.localScale = new Vector3(PadSize, 0.012f, PadSize);

            _padMat = mat;
            _padMat.color = PadIdle;
            var rend = disc.GetComponent<Renderer>();
            rend.sharedMaterial = _padMat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            disc.SetActive(false);
        }

        /// <summary>Highlight, and say whether the station currently wants something doing.</summary>
        public void SetHighlighted(bool on, bool actionable)
        {
            _on = on;

            if (_pad != null)
            {
                if (_pad.gameObject.activeSelf != on) _pad.gameObject.SetActive(on);
                if (on && _padMat != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                    _padMat.color = Color.Lerp(actionable ? PadBusy : PadIdle, Color.white, pulse * 0.45f);
                    float s = PadSize + pulse * 0.16f;
                    _pad.localScale = new Vector3(s, 0.012f, s);
                }
            }

            for (int i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] == null) continue;
                _mats[i].color = on
                    ? Color.Lerp(_baseColors[i], Color.white, 0.22f)
                    : _baseColors[i];
            }
        }

        /// <summary>Re-read the current colours as the new resting state (for the Monster, which recolours itself).</summary>
        public void RefreshBaseColors()
        {
            if (_on) return;
            for (int i = 0; i < _mats.Count; i++)
                if (_mats[i] != null) _baseColors[i] = _mats[i].color;
        }
    }
}
