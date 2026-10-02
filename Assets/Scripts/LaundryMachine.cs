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

        /// <summary>Dryers only. Debt that survives the end of the day.</summary>
        public int Lint;

        /// <summary>Seconds this machine is unusable after a fire.</summary>
        public float OfflineTimer;
        public bool Offline => OfflineTimer > 0f;

        ProgressBar _bar;
        AudioSource _audio;
        float _nagTimer;
        const float NagInterval = 2.6f;

        public override string Label => MachineMode == Mode.Washer ? "Washer" : "Dryer";

        void Awake()
        {
            _bar = ProgressBar.Attach(transform, new Vector3(0f, 1.5f, 0f));
            _audio = SfxPlayer.Spatial(gameObject);
        }

        void Nag(AudioClip clip, float volume)
        {
            if (_audio == null) SfxPlayer.Play(clip, volume);
            else _audio.PlayOneShot(clip, volume);
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
                if (Offline) return "BURNT OUT " + Mathf.CeilToInt(OfflineTimer) + "s";

                string lint = MachineMode == Mode.Dryer && Lint > 0
                    ? "  lint " + Lint + "/" + Tuning.LintMax + (Lint >= Tuning.LintFireThreshold
                        ? " DANGER" : Lint >= Tuning.LintSlowThreshold ? " slow" : "")
                    : "";

                if (Running) return Mathf.CeilToInt(_cycleLength - Timer) + "s" + lint;
                if (HasFinishedLoad) return "DONE " + Contents.Count + lint;
                if (Contents.Count > 0) return Contents.Count + "/" + Tuning.MachineCapacity + lint;
                return "empty" + lint;
            }
        }

        public override string ActionPrompt(PlayerController p)
        {
            if (Offline || Running) return "";
            if (HasFinishedLoad) return p.FreeSlots > 0 ? "unload" : "hands full";

            int loadable = 0;
            foreach (var g in p.Carried) if (Accepts(g)) loadable++;
            if (loadable > 0 && Contents.Count < Tuning.MachineCapacity) return "load";

            if (HasUnstartedLoad) return "start";
            return "";
        }

        public override void Interact(PlayerController p)
        {
            if (Offline || Running) return;

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

        // ---------- pocket roulette ----------

        /// <summary>Garments the player is carrying that this machine wants and that still have unchecked pockets.</summary>
        int UncheckedPocketsCarried(PlayerController p)
        {
            int n = 0;
            foreach (var g in p.Carried)
                if (Accepts(g) && g.HasPockets && !g.PocketsChecked && !g.PocketsResolved) n++;
            return n;
        }

        public override float HoldSeconds(PlayerController p)
        {
            if (Offline || Running) return 0f;

            if (MachineMode == Mode.Dryer)
                return Lint > 0 ? Tuning.LintClearHold : 0f;

            if (Contents.Count >= Tuning.MachineCapacity) return 0f;
            return UncheckedPocketsCarried(p) > 0 ? Tuning.PocketCheckHold : 0f;
        }

        public override string HoldPrompt(PlayerController p)
        {
            if (HoldSeconds(p) <= 0f) return "";
            if (MachineMode == Mode.Dryer) return "clean the lint trap (" + Lint + "/" + Tuning.LintMax + ")";
            return "check pockets, then load";
        }

        public override void HoldInteract(PlayerController p)
        {
            if (MachineMode == Mode.Dryer)
            {
                Lint = 0;
                SfxPlayer.Play(Sfx.LintClear, 0.9f);
                GameDirector.Instance?.Flash("lint trap emptied", new Color(0.7f, 0.8f, 0.7f));
                UpdateBar();
                return;
            }

            foreach (var g in p.Carried)
                if (g.HasPockets) g.PocketsChecked = true;

            SfxPlayer.Play(Sfx.Safe, 0.9f);
            GameDirector.Instance?.Flash("pockets checked", new Color(0.6f, 0.85f, 1f));
            Interact(p);   // then load as normal
        }

        /// <summary>
        /// Rolls for every unchecked pocketed garment in the drum.
        /// Returns false if the load cannot run at all (tissue).
        /// </summary>
        bool ResolvePockets()
        {
            if (MachineMode != Mode.Washer) return true;

            var gambling = new List<Garment>();
            foreach (var g in Contents)
                if (g.HasPockets && !g.PocketsChecked && !g.PocketsResolved) gambling.Add(g);

            if (gambling.Count == 0) return true;

            var dir = GameDirector.Instance;
            foreach (var g in gambling) g.PocketsResolved = true;

            foreach (var g in gambling)
            {
                float r = Random.value;
                float cut = Tuning.OddsNothing;

                if (r < cut)
                    continue;                                   // empty pockets, got away with it

                cut += Tuning.OddsTissue;
                if (r < cut)
                {
                    // A tissue shreds over everything, and that fluff has to go somewhere.
                    foreach (var c in Contents) c.SetState(GarmentState.Dirty);
                    foreach (var dry in Object.FindObjectsByType<LaundryMachine>())
                        if (dry.MachineMode == Mode.Dryer)
                            dry.Lint = Mathf.Min(Tuning.LintMax, dry.Lint + Tuning.LintFromTissue);
                    SfxPlayer.Play(Sfx.Disaster, 1f);
                    dir?.Flash("A TISSUE. Re-wash the load, and every lint trap just got worse.",
                               new Color(1f, 0.85f, 0.4f));
                    return false;
                }

                cut += Tuning.OddsWallet;
                if (r < cut)
                {
                    float lost = dir != null ? dir.Score * Tuning.WalletScorePenalty : 0f;
                    if (dir != null) dir.Score -= lost;
                    SfxPlayer.Play(Sfx.Disaster, 1f);
                    dir?.Flash("Your WALLET was in there. -" + lost.ToString("0.#") + " score.",
                               new Color(1f, 0.6f, 0.3f));
                    continue;
                }

                cut += Tuning.OddsChapstick;
                if (r < cut)
                {
                    RuinSome(Tuning.ChapstickRuins);
                    SfxPlayer.Play(Sfx.Disaster, 1f);
                    dir?.Flash("CHAPSTICK. Grease on " + Tuning.ChapstickRuins + " garments.",
                               new Color(1f, 0.5f, 0.3f));
                    return Contents.Count > 0;
                }

                cut += Tuning.OddsCrayon;
                if (r < cut)
                {
                    RuinSome(Contents.Count);
                    SfxPlayer.Play(Sfx.Disaster, 1f);
                    dir?.Flash("A CRAYON. The entire load is ruined.", new Color(1f, 0.35f, 0.3f));
                    return false;
                }

                // AirPods: the only permanent loss in the game.
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (player != null) player.CarryPenalty++;
                SfxPlayer.Play(Sfx.Disaster, 1f);
                    dir?.Flash("YOUR AIRPODS. Gone. You can carry one less for the rest of the run.",
                           new Color(1f, 0.3f, 0.45f));
            }

            return Contents.Count > 0;
        }

        void RuinSome(int count)
        {
            var dir = GameDirector.Instance;
            for (int i = 0; i < count && Contents.Count > 0; i++)
            {
                var g = Contents[0];
                Contents.RemoveAt(0);
                dir?.Ruin(g);
            }
        }

        void StartCycle()
        {
            // The gamble resolves the moment you commit the load.
            if (!ResolvePockets())
            {
                UpdateBar();
                return;
            }

            // A load of wrinkled garments is a quick refresh, not a full cycle.
            bool allWrinkled = MachineMode == Mode.Dryer;
            foreach (var g in Contents)
                if (g.State != GarmentState.Wrinkled) { allWrinkled = false; break; }

            _cycleLength = allWrinkled
                ? Tuning.ReDryCycle
                : (MachineMode == Mode.Washer ? Tuning.WashCycle : Tuning.DryCycle);

            // A clogged dryer is slow, and eventually it is worse than slow.
            if (MachineMode == Mode.Dryer)
            {
                if (Lint >= Tuning.LintFireThreshold &&
                    (Lint >= Tuning.LintMax || Random.value < Tuning.LintFireChance))
                {
                    CatchFire();
                    return;
                }
                if (Lint >= Tuning.LintSlowThreshold) _cycleLength *= Tuning.LintSlowMultiplier;
            }

            Running = true;
            Timer = 0f;
            Nag(Sfx.Start, 0.7f);
            foreach (var g in Contents) g.DecayMultiplier = 0f; // nothing decays mid-cycle
        }

        /// <summary>
        /// Lint debt coming due. Ruins the load, takes the dryer out for a while, and
        /// burns itself clean - at ten it ends the run outright.
        /// </summary>
        void CatchFire()
        {
            var dir = GameDirector.Instance;
            bool terminal = Lint >= Tuning.LintMax;

            Running = false;
            Timer = 0f;
            RuinSome(Contents.Count);
            Nag(Sfx.Fire, 1f);

            if (terminal)
            {
                dir?.Flash("THE DRYER IS ON FIRE. That is the run.", new Color(1f, 0.3f, 0.2f));
                dir?.EndRun();
                Lint = 0;
                OfflineTimer = Tuning.FireOfflineSeconds;
                return;
            }

            Lint = 0;                                   // the fire cleared the trap for you
            OfflineTimer = Tuning.FireOfflineSeconds;
            dir?.AddMonster(Tuning.MonsterPerRuined);
            dir?.Flash("DRYER FIRE. Load gone, dryer out for "
                       + Mathf.RoundToInt(Tuning.FireOfflineSeconds) + "s.",
                       new Color(1f, 0.4f, 0.25f));
        }

        void Update()
        {
            if (OfflineTimer > 0f)
            {
                OfflineTimer -= Time.deltaTime;
                UpdateBar();
                return;
            }

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
                    if (MachineMode == Mode.Dryer)
                    {
                        Lint = Mathf.Min(Tuning.LintMax, Lint + Tuning.LintPerDryCycle);
                        if (Lint == Tuning.LintSlowThreshold)
                            GameDirector.Instance?.Flash("the dryer is getting slow. check the lint trap.",
                                                         new Color(0.9f, 0.85f, 0.5f));
                        else if (Lint == Tuning.LintFireThreshold)
                            GameDirector.Instance?.Flash("THAT LINT TRAP IS A FIRE HAZARD.",
                                                         new Color(1f, 0.55f, 0.3f));
                    }
                    Nag(MachineMode == Mode.Washer ? Sfx.WasherDone : Sfx.DryerDone, 1f);
                    _nagTimer = NagInterval;
                }
            }

            // A finished machine keeps asking. This is the pressure loop.
            if (HasFinishedLoad)
            {
                _nagTimer -= Time.deltaTime;
                if (_nagTimer <= 0f)
                {
                    Nag(MachineMode == Mode.Washer ? Sfx.WasherDone : Sfx.DryerDone, 0.8f);
                    _nagTimer = NagInterval;
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
            else if (Offline)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
                _bar.Set(Mathf.Clamp01(OfflineTimer / Tuning.FireOfflineSeconds),
                         Color.Lerp(new Color(0.6f, 0.1f, 0.05f), new Color(1f, 0.45f, 0.1f), pulse));
            }
            else if (Contents.Count > 0)
            {
                _bar.Set(Contents.Count / (float)Tuning.MachineCapacity, new Color(0.5f, 0.5f, 0.55f));
            }
            else if (MachineMode == Mode.Dryer && Lint > 0)
            {
                // Idle dryers show their lint debt, so it is never invisible.
                float f = Lint / (float)Tuning.LintMax;
                _bar.Set(f, Color.Lerp(new Color(0.45f, 0.40f, 0.30f),
                                       new Color(0.95f, 0.35f, 0.15f), f));
            }
            else
            {
                _bar.Hide();
            }
        }
    }
}
