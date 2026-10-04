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

        // Set by NextStopGuide: this station is where something in the player's arms
        // needs to go. Kept separate from _on so standing at a station always looks the
        // same whether or not you happen to be carrying something for it.
        bool _wanted;
        Color _wantedTint = Color.white;

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

            var disc = Prim.Make(PrimitiveType.Cylinder);
            disc.name = "HighlightPad";

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
            _actionable = actionable;
            Apply();
        }

        /// <summary>
        /// Mark this station as the destination for something the player is carrying.
        /// Drawn slower and dimmer than the standing-here highlight, and in the
        /// garment's own destination colour, so the two never read as the same signal.
        /// </summary>
        public void SetWanted(bool on, Color tint)
        {
            if (_wanted == on && (!on || _wantedTint == tint)) return;
            _wanted = on;
            _wantedTint = tint;
            Apply();
        }

        bool _actionable;

        void Apply()
        {
            bool showPad = _on || _wanted;

            if (_pad != null)
            {
                if (_pad.gameObject.activeSelf != showPad) _pad.gameObject.SetActive(showPad);
                if (showPad && _padMat != null) _padLive = true;
            }

            for (int i = 0; i < _mats.Count; i++)
            {
                if (_mats[i] == null) continue;
                _mats[i].color = _on
                    ? Color.Lerp(_baseColors[i], Color.white, 0.22f)
                    : _wanted
                        ? Color.Lerp(_baseColors[i], _wantedTint, 0.18f)
                        : _baseColors[i];
            }
        }

        bool _padLive;

        /// <summary>
        /// The pulse runs here rather than inside SetHighlighted, which only fired on the
        /// station the player was standing at. A destination across the room gets no such
        /// call, so without this its pad would sit there frozen and look like scenery.
        /// </summary>
        void Update()
        {
            if (_pad == null || _padMat == null || !_padLive) return;
            if (!_pad.gameObject.activeSelf) { _padLive = false; return; }

            if (_on)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                _padMat.color = Color.Lerp(_actionable ? PadBusy : PadIdle, Color.white, pulse * 0.45f);
                float s = PadSize + pulse * 0.16f;
                _pad.localScale = new Vector3(s, 0.012f, s);
            }
            else
            {
                // Half the speed and a smaller pad: a hint, not an instruction.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.6f);
                _padMat.color = Color.Lerp(_wantedTint * 0.85f, Color.white, pulse * 0.30f);
                float s = PadSize * 0.78f + pulse * 0.10f;
                _pad.localScale = new Vector3(s, 0.012f, s);
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
