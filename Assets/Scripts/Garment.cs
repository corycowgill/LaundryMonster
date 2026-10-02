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
        public bool HasPockets;

        /// <summary>Seconds spent in the current decaying state.</summary>
        public float StateTimer;

        /// <summary>0 = frozen (inside a running machine), 1 = normal, 2 = on The Chair.</summary>
        public float DecayMultiplier = 1f;

        /// <summary>Folded while wrinkled, so it only scores half.</summary>
        public bool FoldedWrinkled;

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
                GameDirector.Instance?.OnGarmentSpoiled(this, GarmentState.Mildewed);
            }
            else if (State == GarmentState.CleanDry && StateTimer >= Tuning.WrinkleGrace)
            {
                SetState(GarmentState.Wrinkled);
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

            Color c;
            switch (State)
            {
                case GarmentState.Dirty:    c = DirtyColor;    break;
                case GarmentState.Wet:      c = WetColor;      break;
                case GarmentState.Mildewed: c = MildewColor;   break;
                case GarmentState.CleanDry: c = CleanColor;    break;
                case GarmentState.Wrinkled: c = WrinkledColor; break;
                case GarmentState.Folded:   c = FoldedColor;   break;
                default:                    c = RuinedColor;   break;
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
