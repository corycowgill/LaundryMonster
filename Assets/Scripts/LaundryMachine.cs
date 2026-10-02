using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>A washer or a dryer. Same machine, different input and output states.</summary>
    public class LaundryMachine : Interactable
    {
        public enum Mode { Washer, Dryer }

        public Mode MachineMode = Mode.Washer;
        public readonly List<Garment> Contents = new List<Garment>();

        public bool Running;
        public float Timer;
        float _cycleLength;

        ProgressBar _bar;

        public override string Label => MachineMode == Mode.Washer ? "Washer" : "Dryer";

        void Awake()
        {
            _bar = ProgressBar.Attach(transform, new Vector3(0f, 1.5f, 0f));
        }

        bool Accepts(Garment g)
        {
            if (MachineMode == Mode.Washer)
                return g.State == GarmentState.Dirty || g.State == GarmentState.Mildewed;
            return g.State == GarmentState.Wet || g.State == GarmentState.Wrinkled;
        }

        GarmentState OutputState =>
            MachineMode == Mode.Washer ? GarmentState.Wet : GarmentState.CleanDry;

        /// <summary>True when the cycle has finished and the contents are waiting to be taken out.</summary>
        public bool HasFinishedLoad =>
            !Running && Contents.Count > 0 && Contents[0].State == OutputState;

        bool HasUnstartedLoad
        {
            get
            {
                if (Running || Contents.Count == 0) return false;
                foreach (var g in Contents) if (!Accepts(g)) return false;
                return true;
            }
        }

        public override string Status
        {
            get
            {
                if (Running) return Mathf.CeilToInt(_cycleLength - Timer) + "s";
                if (HasFinishedLoad) return "DONE " + Contents.Count;
                if (Contents.Count > 0) return Contents.Count + "/" + Tuning.MachineCapacity;
                return "empty";
            }
        }

        public override string ActionPrompt(PlayerController p)
        {
            if (Running) return "";
            if (HasFinishedLoad) return p.FreeSlots > 0 ? "unload" : "hands full";

            int loadable = 0;
            foreach (var g in p.Carried) if (Accepts(g)) loadable++;
            if (loadable > 0 && Contents.Count < Tuning.MachineCapacity) return "load";

            if (HasUnstartedLoad) return "start";
            return "";
        }

        public override void Interact(PlayerController p)
        {
            if (Running) return;

            // 1. Finished load waiting? Take it out.
            if (HasFinishedLoad)
            {
                while (Contents.Count > 0 && p.FreeSlots > 0)
                {
                    var g = Contents[0];
                    Contents.RemoveAt(0);
                    p.Take(g);
                }
                UpdateBar();
                return;
            }

            // 2. Carrying something this machine wants? Load it.
            bool loaded = false;
            for (int i = p.Carried.Count - 1; i >= 0; i--)
            {
                if (Contents.Count >= Tuning.MachineCapacity) break;
                var g = p.Carried[i];
                if (!Accepts(g)) continue;

                p.Release(g);
                Contents.Add(g);
                g.DecayMultiplier = 1f;
                g.gameObject.SetActive(false);
                loaded = true;
            }
            if (loaded)
            {
                // Auto-start on a full drum, the way you actually use a machine.
                if (Contents.Count >= Tuning.MachineCapacity) StartCycle();
                UpdateBar();
                return;
            }

            // 3. Otherwise start what's already in there.
            if (HasUnstartedLoad) StartCycle();
            UpdateBar();
        }

        void StartCycle()
        {
            // A load of wrinkled garments is a quick refresh, not a full cycle.
            bool allWrinkled = MachineMode == Mode.Dryer;
            foreach (var g in Contents)
                if (g.State != GarmentState.Wrinkled) { allWrinkled = false; break; }

            _cycleLength = allWrinkled
                ? Tuning.ReDryCycle
                : (MachineMode == Mode.Washer ? Tuning.WashCycle : Tuning.DryCycle);

            Running = true;
            Timer = 0f;
            foreach (var g in Contents) g.DecayMultiplier = 0f; // nothing decays mid-cycle
        }

        void Update()
        {
            if (Running)
            {
                Timer += Time.deltaTime;
                if (Timer >= _cycleLength)
                {
                    Running = false;
                    Timer = 0f;
                    foreach (var g in Contents)
                    {
                        g.SetState(OutputState);
                        g.DecayMultiplier = 1f; // the clock starts the moment the cycle ends
                    }
                }
            }
            UpdateBar();
        }

        void UpdateBar()
        {
            if (_bar == null) return;

            if (Running)
            {
                _bar.Set(Timer / _cycleLength, new Color(0.35f, 0.65f, 0.95f));
            }
            else if (HasFinishedLoad)
            {
                // Pulse: a finished machine should nag you.
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                _bar.Set(1f, Color.Lerp(new Color(0.95f, 0.75f, 0.1f), Color.white, pulse));
            }
            else if (Contents.Count > 0)
            {
                _bar.Set(Contents.Count / (float)Tuning.MachineCapacity, new Color(0.5f, 0.5f, 0.55f));
            }
            else
            {
                _bar.Hide();
            }
        }
    }
}
