using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// One piece of laundry. Owns its own decay clock; the holder (machine, chair,
    /// player) sets DecayMultiplier to say how fast that clock runs.
    /// </summary>
    public class Garment : MonoBehaviour
    {
        public GarmentKind Kind;
        public GarmentState State = GarmentState.Dirty;

        /// <summary>Has pockets worth checking. Pants always do.</summary>
        public bool HasPockets;

        /// <summary>Checked before loading, so the roulette is skipped for this garment.</summary>
        public bool PocketsChecked;

        /// <summary>True once this garment has gambled and survived, so it never rolls twice.</summary>
        public bool PocketsResolved;

        /// <summary>Seconds spent in the current decaying state.</summary>
        public float StateTimer;

        /// <summary>0 = frozen (inside a running machine), 1 = normal, 2 = on The Chair.</summary>
        public float DecayMultiplier = 1f;

        /// <summary>Folded while wrinkled, so it only scores half.</summary>
        public bool FoldedWrinkled;

        /// <summary>Socks only: which pair this belongs to. -1 for everything else.</summary>
        public int PairId = -1;

        /// <summary>Socks only: matched with its partner, so it can finally be folded.</summary>
        public bool Paired;

        /// <summary>Socks only: its partner went to the Void. It can never be matched.</summary>
        public bool Orphan;

        /// <summary>This garment's own colour. State modulates it rather than replacing it,
        /// so a red shirt stays recognisably a red shirt whether it is dirty, wet or folded.</summary>
        public Color BaseColor = Color.white;

        /// <summary>The laundry palette. Deliberately a believable wash load.</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.95f, 0.95f, 0.93f),  // white
            new Color(0.93f, 0.88f, 0.76f),  // cream
            new Color(0.45f, 0.62f, 0.85f),  // light blue
            new Color(0.20f, 0.28f, 0.52f),  // navy
            new Color(0.80f, 0.30f, 0.30f),  // red
            new Color(0.35f, 0.62f, 0.42f),  // green
            new Color(0.93f, 0.78f, 0.32f),  // yellow
            new Color(0.88f, 0.58f, 0.70f),  // pink
            new Color(0.45f, 0.45f, 0.50f),  // grey
            new Color(0.55f, 0.42f, 0.72f),  // purple
            new Color(0.75f, 0.45f, 0.25f),  // rust
            new Color(0.25f, 0.55f, 0.58f),  // teal
        };

        public bool IsSock => Kind == GarmentKind.Sock;

        /// <summary>A lone sock cannot be folded or put away. That is the whole problem.</summary>
        public bool NeedsPartner => IsSock && !Paired;

        Renderer _rend;
        Material _mat;

        static readonly Color DirtyColor    = new Color(0.42f, 0.38f, 0.26f);
        static readonly Color WetColor      = new Color(0.20f, 0.34f, 0.52f);
        static readonly Color MildewColor   = new Color(0.33f, 0.44f, 0.24f);
        static readonly Color CleanColor    = new Color(0.94f, 0.95f, 0.97f);
        static readonly Color WrinkledColor = new Color(0.72f, 0.66f, 0.52f);
        static readonly Color FoldedColor   = new Color(0.80f, 0.88f, 0.96f);
        static readonly Color RuinedColor   = new Color(0.30f, 0.10f, 0.12f);

        void Awake()
        {
            _rend = GetComponentInChildren<Renderer>();
            if (_rend != null)
            {
                _mat = new Material(_rend.sharedMaterial);
                _rend.sharedMaterial = _mat;
            }
        }

        void Start() => RefreshVisual();

        public bool IsDecaying => State == GarmentState.Wet || State == GarmentState.CleanDry;

        /// <summary>How long this state has before it degrades, or 0 if it doesn't.</summary>
        public float DecayLimit
        {
            get
            {
                if (State == GarmentState.Wet) return Tuning.MildewGrace;
                if (State == GarmentState.CleanDry) return Tuning.WrinkleGrace;
                return 0f;
            }
        }

        /// <summary>0..1, how far along the decay is. Drives the warning colour.</summary>
        public float DecayFraction
        {
            get
            {
                float limit = DecayLimit;
                return limit <= 0f ? 0f : Mathf.Clamp01(StateTimer / limit);
            }
        }

        public void SetState(GarmentState s)
        {
            State = s;
            StateTimer = 0f;
            RefreshVisual();
        }

        public void Tick(float dt)
        {
            if (!IsDecaying || DecayMultiplier <= 0f) return;

            StateTimer += dt * DecayMultiplier;

            if (State == GarmentState.Wet && StateTimer >= Tuning.MildewGrace)
            {
                SetState(GarmentState.Mildewed);
                SfxPlayer.Play(Sfx.Wrinkled, 0.9f, 0.7f);
                GameDirector.Instance?.OnGarmentSpoiled(this, GarmentState.Mildewed);
            }
            else if (State == GarmentState.CleanDry && StateTimer >= Tuning.WrinkleGrace)
            {
                SetState(GarmentState.Wrinkled);
                SfxPlayer.Play(Sfx.Wrinkled, 0.9f);
                GameDirector.Instance?.OnGarmentSpoiled(this, GarmentState.Wrinkled);
            }
            else
            {
                RefreshVisual();
            }
        }

        public void RefreshVisual()
        {
            if (_mat == null) return;

            Color c = BaseColor;
            switch (State)
            {
                // Grimy and flattened, but still the same garment underneath.
                case GarmentState.Dirty:    c = Mix(c, new Color(0.34f, 0.30f, 0.22f), 0.50f); break;
                case GarmentState.Wet:      c = Mix(Darken(c, 0.72f), new Color(0.16f, 0.26f, 0.42f), 0.35f); break;
                case GarmentState.Mildewed: c = Mix(Darken(c, 0.65f), new Color(0.26f, 0.36f, 0.18f), 0.60f); break;
                case GarmentState.CleanDry: c = Darken(c, 1.06f); break;
                case GarmentState.Wrinkled: c = Mix(Desaturate(c, 0.45f), new Color(0.70f, 0.61f, 0.45f), 0.30f); break;
                case GarmentState.Folded:   c = Darken(c, 1.10f); break;
                default:                    c = Mix(c, new Color(0.17f, 0.06f, 0.07f), 0.80f); break;
            }

            // As a decay clock runs out the garment flushes toward warning amber,
            // so you can read danger off the garment itself without looking at a bar.
            if (IsDecaying)
            {
                float f = DecayFraction;
                if (f > 0.5f)
                {
                    float t = Mathf.InverseLerp(0.5f, 1f, f);
                    c = Color.Lerp(c, new Color(0.95f, 0.45f, 0.10f), t * 0.75f);
                }
            }

            _mat.color = c;
        }

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        static Color Darken(Color c, float f) =>
            new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);

        static Color Desaturate(Color c, float t)
        {
            float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return Color.Lerp(c, new Color(g, g, g), t);
        }

        public static string StateLabel(GarmentState s)
        {
            switch (s)
            {
                case GarmentState.Dirty:    return "dirty";
                case GarmentState.Wet:      return "wet";
                case GarmentState.Mildewed: return "MILDEWED";
                case GarmentState.CleanDry: return "clean";
                case GarmentState.Wrinkled: return "WRINKLED";
                case GarmentState.Folded:   return "folded";
                default:                    return "ruined";
            }
        }
    }
}
