using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    public enum Phase { Intro, Title, Help, Credits, Briefing, UpgradePick, Playing, DaySummary, RunOver }

    /// <summary>
    /// Runs the day: spawns laundry, ticks every decay clock, scores deliveries,
    /// and grows the Monster out of whatever the player failed to finish.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("Scene wiring")]
        public Hamper Hamper;
        public Transform MonsterPile;
        public Transform GarmentSpawnParent;

        /// <summary>Generated folded-laundry mesh. Falls back to a cube when unset.</summary>
        public Mesh GarmentMesh;

        /// <summary>One mesh per GarmentKind, in enum order: Shirt, Pants, Towel, Sock, Delicate.
        /// Any null entry falls back to GarmentMesh, then to a cube.</summary>
        public Mesh[] GarmentMeshes = new Mesh[5];

        [Header("Run state")]
        public int Day = 1;
        public Phase CurrentPhase = Phase.Playing;

        public float DayTimer;
        public float DayLength;
        /// <summary>Base delivery points. Stars are measured against this alone.</summary>
        public float Score;

        /// <summary>Streak bonus, kept apart so it cannot move the day's pass mark.</summary>
        public float BonusScore;

        /// <summary>Consecutive clean deliveries. One wrinkled garment ends it.</summary>
        public int CleanStreak;
        public int BestStreakToday;

        /// <summary>Set briefly when a delivery lands, for the HUD to react to.</summary>
        public float LastDeliveryTime { get; private set; } = -99f;
        public float LastDeliveryPoints { get; private set; }
        public float LastDeliveryBonus { get; private set; }
        public Vector3 LastDeliveryWorld { get; private set; }
        public int Delivered;
        public int WrinkledCount;
        public int MildewedCount;
        public int RuinedCount;
        public float Monster;

        public int Stars { get; private set; }

        /// <summary>Score accumulated across every day of this run, for the high score table.</summary>
        public float RunScore;
        public int RunDelivered;
        public int RunStars;
        public bool NewRecord;

        /// <summary>Short on-screen message. A gamble the player cannot read teaches nothing.</summary>
        public string FlashMessage = "";
        public float FlashTimer;
        public Color FlashColor = Color.white;
        public const float FlashDuration = 3.5f;

        public void Flash(string msg, Color color)
        {
            FlashMessage = msg;
            FlashColor = color;
            FlashTimer = FlashDuration;
        }

        /// <summary>End the run immediately. A ten-lint dryer fire does this.</summary>
        public void EndRun()
        {
            if (CurrentPhase == Phase.RunOver) return;
            CurrentPhase = Phase.RunOver;
            RunSave.Clear();
            SfxPlayer.Play(Sfx.RunOver, 1f);

            // The day in progress still counts toward the run.
            RunScore += Score;
            RunDelivered += Delivered;
            NewRecord = HighScores.SubmitRun(RunScore, Day, RunDelivered, RunStars);
        }

        /// <summary>A sock was absorbed into its pair, or made into a rag. Not a loss.</summary>
        public void NoteSockMerged(Garment g)
        {
            if (g != null) _all.Remove(g);
        }

        /// <summary>A sock went to the Void. Its partner is an orphan forever.</summary>
        public int VoidedSocks;

        public void VoidSock(Garment sock)
        {
            if (sock == null) return;

            // Whatever shares its pair id is now an orphan.
            foreach (var other in _all)
                if (other != null && other != sock && other.IsSock && other.PairId == sock.PairId)
                    other.Orphan = true;

            VoidedSocks++;
            _all.Remove(sock);
            Destroy(sock.gameObject);
            SfxPlayer.Play(Sfx.Wrinkled, 0.7f, 1.4f);
            Flash("a sock is simply gone. nobody knows where.", new Color(0.65f, 0.7f, 0.85f));
        }

        /// <summary>Destroy a garment outright. Only pocket disasters do this.</summary>
        public void Ruin(Garment g)
        {
            if (g == null) return;
            g.SetState(GarmentState.Ruined);
            OnGarmentSpoiled(g, GarmentState.Ruined);
            _all.Remove(g);
            Destroy(g.gameObject);
        }

        readonly List<Garment> _all = new List<Garment>();
        readonly List<float> _spawnTimes = new List<float>();
        int _spawnIndex;
        Material _monsterMat;

        /// <summary>Paused by the player. Separate from phase so it can be toggled back.</summary>
        public bool Paused { get; private set; }

        public void TogglePause() => Paused = !Paused;
        public void SetPaused(bool p) => Paused = p;

        /// <summary>
        /// The single authority for "is the world ticking".
        ///
        /// Machines, the Chair and the sock drawer all run their own Update, and all of
        /// them used to keep running during the help screen, the day summary and game
        /// over - so a dryer finished, laundry wrinkled and an overflowing Chair kept
        /// feeding the Monster while the player was reading a results screen.
        /// </summary>
        public bool IsRunning => CurrentPhase == Phase.Playing && !Paused;

        public bool AcceptsInput => CurrentPhase == Phase.Playing && !Paused;
        /// <summary>How many garments today actually brings, after any modifier.</summary>
        public int GarmentsToday { get; private set; }

        /// <summary>
        /// Points needed to pass the day.
        ///
        /// Measured against what arrives TODAY, not against what an unmodified day
        /// of this number would have brought - otherwise a modifier that reduces the
        /// laundry leaves the target where it was and asks for more than exists.
        /// </summary>
        public float Target => Tuning.TargetForGarments(GarmentsToday) * Today.TargetScale;
        public float TimeLeft => Mathf.Max(0f, DayLength - DayTimer);

        /// <summary>No more laundry is coming; what is left is the finishing period.</summary>
        public bool ArrivalsDone => _spawnIndex >= _spawnTimes.Count;

        /// <summary>The system being introduced on the briefing card, if one is up.</summary>
        public Tuning.Unlock PendingUnlock { get; private set; }

        /// <summary>
        /// What is different about today, and everything it changes.
        ///
        /// Read by the machines for their cycle times, by scoring for what a wrinkled
        /// garment is worth, and by the HUD so the rule is never more than a glance away.
        /// Always valid: outside the modifier days it is Plan.Normal, which changes
        /// nothing, so no caller has to ask whether there is one.
        /// </summary>
        public DayModifiers.Plan Today { get; private set; } = DayModifiers.Plan.Normal;

        /// <summary>
        /// What tomorrow will be, known today.
        ///
        /// The modifiers are dealt from a seeded bag, so tomorrow is not a secret - and
        /// the one place the player makes a decision between days is the upgrade picker.
        /// A basket is worth more before a day of walking; a spray before a guest. Shown
        /// there, the forecast turns a pick from a guess into a plan.
        /// </summary>
        public DayModifiers.Modifier Tomorrow => DayModifiers.For(Day + 1, _runSeed);

        /// <summary>
        /// Seeds the order modifiers are dealt in, so two runs are not the same run.
        /// Set once when a run starts and left alone, which keeps a day reproducible
        /// within its own run - the day does not change shape because you paused.
        /// </summary>
        int _runSeed;

        /// <summary>Everything bought this run. Owned by the run, cleared when one starts.</summary>
        public readonly Upgrades Kit = new Upgrades();

        /// <summary>The three on offer right now, while the picker is up.</summary>
        public readonly List<UpgradeId> Offered = new List<UpgradeId>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (MonsterPile != null)
            {
                var r = MonsterPile.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    _monsterMat = new Material(r.sharedMaterial);
                    r.sharedMaterial = _monsterMat;
                }
            }
            CurrentPhase = Phase.Title;
        }

        /// <summary>Start a brand new run from the title screen.</summary>
        public void StartRun()
        {
            // A new run is a decision to abandon the old one.
            RunSave.Clear();

            Kit.ResetForRun();
            Offered.Clear();
            _runSeed = Random.Range(1, int.MaxValue);
            Monster = 0f;
            RunScore = 0f;
            RunDelivered = 0;
            RunStars = 0;
            NewRecord = false;
            BeginDay(1);
        }

        public void BeginDay(int day)
        {
            Day = day;

            // A day that introduces something opens on its briefing. IsRunning is false
            // there, so nothing ticks while the player reads it.
            Kit.BeginDay();
            PendingUnlock = Tuning.UnlockFor(day);

            // Today's twist. Modifiers only start once every system has been taught, so
            // an unlock day and a modifier day can never be the same day and the player
            // is never handed a new rule and a twist on it at once.
            Today = DayModifiers.PlanFor(DayModifiers.For(day, _runSeed));

            // Either a new system or a new twist opens on a card. IsRunning is false
            // there, so nothing ticks while it is being read.
            bool briefing = PendingUnlock != Tuning.Unlock.None
                            || Today.Mod != DayModifiers.Modifier.None;
            CurrentPhase = briefing ? Phase.Briefing : Phase.Playing;
            DayTimer = 0f;
            DayLength = Tuning.DayLength(day) * Today.LengthScale;
            Score = 0f;
            BonusScore = 0f;
            CleanStreak = 0;
            BestStreakToday = 0;
            Delivered = 0;
            WrinkledCount = 0;
            MildewedCount = 0;
            RuinedCount = 0;
            Stars = 0;

            // Clear anything left over from the previous day. Stations hold their own
            // references, so they must be emptied too or day 2 works from dead objects.
            foreach (var g in _all) if (g != null) Destroy(g.gameObject);
            _all.Clear();

            if (Hamper != null) Hamper.Waiting.Clear();

            // One dryer can be dead for the day. Always the LAST dryer rather than a
            // random one, so the player learns where the dead machine will be instead of
            // having to rediscover it, and because a dryer at the end of the row is a
            // longer walk than one in the middle - the modifier should cost capacity, not
            // cost the player a surprise halfway through a load.
            int dryersToKill = Today.DryersOffline;
            var machines = Object.FindObjectsByType<LaundryMachine>();
            System.Array.Sort(machines, (a, b) => b.transform.position.x.CompareTo(a.transform.position.x));

            foreach (var m in machines)
            {
                m.Contents.Clear();
                m.Running = false;
                m.Timer = 0f;
                m.OfflineTimer = 0f;          // a burnt dryer cools off overnight
                m.OutOfOrder = false;
                if (day == 1) m.Lint = 0;     // but lint is run-long debt

                if (dryersToKill > 0 && m.MachineMode == LaundryMachine.Mode.Dryer)
                {
                    m.OutOfOrder = true;
                    dryersToKill--;
                }
            }

            foreach (var c in Object.FindObjectsByType<Chair>())
                c.Pile.Clear();

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.Carried.Clear();
                if (day == 1) player.CarryPenalty = 0;   // AirPods loss lasts the whole run
            }

            // Spread the day's laundry across the first 70% of it, so the back half
            // is about finishing rather than starting.
            _spawnTimes.Clear();
            _spawnIndex = 0;
            if (day == 1) { VoidedSocks = 0; _nextPairId = 0; }

            foreach (var st in Object.FindObjectsByType<SockStation>())
            {
                st.Orphans.Clear();
                st.Waiting.Clear();
                st.ReadyPairs.Clear();
                if (day == 1) st.Rags = 0;
            }
            int count = Mathf.Max(1, Mathf.RoundToInt(Tuning.GarmentsForDay(day) * Today.CountScale));
            GarmentsToday = count;
            // Arrivals stop one full pipeline before the end, so the last garment of the
            // day can actually be washed, dried, folded and delivered. What remains is the
            // finishing period.
            // The day's own length and cycle speeds, not the day number's: a modifier
            // may have changed both, and arrivals have to stop one REAL pipeline before
            // the end or the last of them cannot be finished.
            float window = Tuning.ArrivalWindow(DayLength, Today.WashScale, Today.DryScale);
            for (int i = 0; i < count; i++)
                _spawnTimes.Add(window * (i / (float)Mathf.Max(1, count - 1)));

            // The checkpoint. Everything above is now the state of "the start of this
            // day", which is exactly what a resume puts back.
            if (day >= 2) SaveRun();
        }

        // ---------- continue ----------

        /// <summary>Is there a run to pick up from the title screen?</summary>
        public bool HasSavedRun => RunSave.Exists;

        /// <summary>The day that run would resume on.</summary>
        public int SavedDay => RunSave.SavedDay;

        /// <summary>
        /// Dryers in the order the save stores their lint: right to left, which is also
        /// the order BeginDay walks them to pick the dead one.
        /// </summary>
        LaundryMachine[] DryersRightToLeft()
        {
            var all = Object.FindObjectsByType<LaundryMachine>();
            var list = new List<LaundryMachine>();
            foreach (var m in all) if (m.MachineMode == LaundryMachine.Mode.Dryer) list.Add(m);
            list.Sort((a, b) => b.transform.position.x.CompareTo(a.transform.position.x));
            return list.ToArray();
        }

        /// <summary>Start-of-day checkpoint: resume by replaying this day.</summary>
        void SaveRun()
        {
            var d = Snapshot();
            d.day = Day;
            d.atSummary = false;
            RunSave.Save(d);
        }

        /// <summary>
        /// End-of-day checkpoint: resume on this day's summary, totals already banked,
        /// and go on to the pick and the next day from there.
        /// </summary>
        void SaveAtSummary()
        {
            var d = Snapshot();
            d.day = Day + 1;
            d.atSummary = true;
            d.completedDay = Day;
            d.garmentsToday = GarmentsToday;
            d.dayScore = Score;
            d.dayBonus = BonusScore;
            d.dayDelivered = Delivered;
            d.dayWrinkled = WrinkledCount;
            d.dayMildewed = MildewedCount;
            d.dayBestStreak = BestStreakToday;
            d.dayStars = Stars;
            RunSave.Save(d);
        }

        /// <summary>Everything a run is, apart from which day and where in it.</summary>
        RunSave.Data Snapshot()
        {
            var d = new RunSave.Data
            {
                seed = _runSeed,
                monster = Monster,
                runScore = RunScore,
                runDelivered = RunDelivered,
                runStars = RunStars,
                voidedSocks = VoidedSocks,
                nextPairId = _nextPairId,
            };

            var player = Object.FindAnyObjectByType<PlayerController>();
            d.carryPenalty = player != null ? player.CarryPenalty : 0;

            var dryers = DryersRightToLeft();
            d.dryerLint = new int[dryers.Length];
            for (int i = 0; i < dryers.Length; i++) d.dryerLint[i] = dryers[i].Lint;

            var owned = new List<int>();
            foreach (var id in Kit.Owned) owned.Add((int)id);
            d.owned = owned.ToArray();

            return d;
        }

        /// <summary>
        /// Pick the saved run back up at the start of its day. Falls back to a fresh run
        /// if there is nothing to pick up, so the title's primary action always does
        /// something.
        /// </summary>
        public void ContinueRun()
        {
            var d = RunSave.Load();
            if (d == null) { StartRun(); return; }

            Kit.ResetForRun();
            foreach (var id in d.owned) Kit.Grant((UpgradeId)id);
            Offered.Clear();

            _runSeed = d.seed;
            Monster = d.monster;
            RunScore = d.runScore;
            RunDelivered = d.runDelivered;
            RunStars = d.runStars;
            NewRecord = false;
            VoidedSocks = d.voidedSocks;
            _nextPairId = d.nextPairId;

            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) player.CarryPenalty = d.carryPenalty;

            // Lint goes on BEFORE BeginDay: a day past the first leaves lint alone, and
            // BeginDay writes the checkpoint, which has to see the restored traps.
            var dryers = DryersRightToLeft();
            for (int i = 0; i < dryers.Length && i < d.dryerLint.Length; i++)
                dryers[i].Lint = d.dryerLint[i];

            if (!d.atSummary)
            {
                BeginDay(d.day);
                return;
            }

            // Back onto the results screen of the day that was finished. The totals are
            // already in the run; this is the day's own numbers, so the card reads as it
            // did, and the ordinary flow takes over from the confirm - the pick, then
            // BeginDay for the next day, which writes the next checkpoint.
            Day = d.completedDay;
            Today = DayModifiers.PlanFor(DayModifiers.For(Day, _runSeed));
            GarmentsToday = d.garmentsToday;
            Score = d.dayScore;
            BonusScore = d.dayBonus;
            Delivered = d.dayDelivered;
            WrinkledCount = d.dayWrinkled;
            MildewedCount = d.dayMildewed;
            BestStreakToday = d.dayBestStreak;
            Stars = d.dayStars;
            DayTimer = DayLength = Tuning.DayLength(Day) * Today.LengthScale;
            CurrentPhase = Phase.DaySummary;
        }

        void Update()
        {
            if (FlashTimer > 0f) FlashTimer -= Time.deltaTime;

            if (CurrentPhase == Phase.Playing)
            {
                if (PausePressed()) TogglePause();
                if (!Paused) TickDay();
                return;
            }

            // Every scheme, not just the keyboard. This used to return early when
            // Keyboard.current was null, which made the title screen a dead end on a
            // phone or a pad-only machine: the game was running and unreachable.
            bool go = GameInput.ConfirmPressed();
            bool help = GameInput.HelpPressed();
            bool back = GameInput.BackPressed();

            switch (CurrentPhase)
            {
                case Phase.Intro:
                    // Anything skips the intro.
                    if (go || help || back || GameInput.AnyPressed()) CurrentPhase = Phase.Title;
                    break;

                case Phase.Title:
                    if (go) TitleConfirm();
                    else if (help) CurrentPhase = Phase.Help;
                    else if (NewRunPressed() && HasSavedRun) StartRun();
                    else if (TutorialPressed()) StartTutorial();
                    else if (CreditsPressed()) OpenCredits();
                    break;

                case Phase.Credits:
                    if (go || back) CurrentPhase = Phase.Title;
                    break;

                case Phase.Briefing:
                    if (go || back) { PendingUnlock = Tuning.Unlock.None; CurrentPhase = Phase.Playing; }
                    break;

                // (the title's own confirm is handled by TitleConfirm below)

                case Phase.Help:
                    if (go || back || help) CurrentPhase = Phase.Title;
                    else if (TutorialPressed()) StartTutorial();
                    break;

                case Phase.DaySummary:
                    if (go) AdvanceFromSummary();
                    break;

                case Phase.UpgradePick:
                    // `go` is passed in rather than re-read. Reading the confirm twice in
                    // one frame is free on a keyboard and destroys it on a touchscreen.
                    HandleUpgradePick(go);
                    break;

                case Phase.RunOver:
                    if (go) CurrentPhase = Phase.Title;
                    break;
            }
        }

        /// <summary>
        /// Confirm on the title screen. A keyboard or pad confirm means "start"; a tap
        /// means whichever button it landed on, because there is no EventSystem to do
        /// that for us and the buttons would otherwise be unreachable on a phone.
        /// </summary>
        void TitleConfirm()
        {
            if (GameInput.ConfirmWasTap)
            {
                var hud = Object.FindAnyObjectByType<HUD>();
                int btn = hud != null ? hud.TitleButtonAt(GameInput.LastTapScreen) : -1;
                switch (btn)
                {
                    case 1: StartTutorial(); return;
                    case 2: CurrentPhase = Phase.Help; return;
                    case 3: OpenCredits(); return;
                    case 4: ContinueRun(); return;
                    case 0: StartRun(); return;
                    default: TitlePrimary(); return;   // a tap anywhere else still goes
                }
            }
            TitlePrimary();
        }

        /// <summary>
        /// What SPACE, (A) and a stray tap mean on the title: carry on if there is a
        /// run to carry on, otherwise start one. Starting over while a save exists is a
        /// deliberate act with its own button and key.
        /// </summary>
        void TitlePrimary()
        {
            if (HasSavedRun) ContinueRun();
            else StartRun();
        }

        /// <summary>N, or (X) on a pad: start a new run even though one is saved.</summary>
        static bool NewRunPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.nKey.wasPressedThisFrame) return true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return gp != null && gp.buttonWest.wasPressedThisFrame;
        }

        void OpenCredits()
        {
            var hud = Object.FindAnyObjectByType<HUD>();
            if (hud != null) hud.RewindCredits();
            CurrentPhase = Phase.Credits;
        }

        /// <summary>Pause: Escape or P, Start on a pad, or the on-screen pause button.</summary>
        static bool PausePressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
                return true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null && gp.startButton.wasPressedThisFrame) return true;
            return GameInput.PauseTapped();
        }

        /// <summary>
        /// Leaving the day summary. A day worth at least one star earns a pick, provided
        /// there is anything left to buy.
        /// </summary>
        void AdvanceFromSummary()
        {
            if (Stars > 0 && Kit.AnyAvailable())
            {
                Offered.Clear();
                Offered.AddRange(Kit.Offer(Tuning.UpgradeChoices));
                if (Offered.Count > 0) { CurrentPhase = Phase.UpgradePick; return; }
            }
            BeginDay(Day + 1);
        }

        void HandleUpgradePick(bool confirmed)
        {
            int choice = UpgradeChoicePressed(confirmed);
            if (choice < 0 || choice >= Offered.Count) return;

            Kit.Grant(Offered[choice]);
            var info = Upgrades.Describe(Offered[choice]);
            Offered.Clear();
            SfxPlayer.Play(Sfx.Deliver, 1f);
            BeginDay(Day + 1);
            Flash(info.Name + " acquired", new Color(0.95f, 0.8f, 0.3f));
        }

        /// <summary>
        /// Which of the three was chosen, or -1. Number keys, face buttons, or a tap on
        /// the card itself - the picker has to be usable on whatever is in your hands.
        /// </summary>
        static int UpgradeChoicePressed(bool confirmed)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) return 0;
                if (kb.digit2Key.wasPressedThisFrame) return 1;
                if (kb.digit3Key.wasPressedThisFrame) return 2;
            }

            var gp = UnityEngine.InputSystem.Gamepad.current;
            if (gp != null)
            {
                if (gp.buttonWest.wasPressedThisFrame) return 0;
                if (gp.buttonNorth.wasPressedThisFrame) return 1;
                if (gp.buttonEast.wasPressedThisFrame) return 2;
            }

            // The tap was already claimed by the caller; `confirmed` is that claim
            // handed down, not a second read of it. ConfirmWasTap, not Active: the
            // question is where THIS confirm came from, not which device the player
            // happens to have touched most recently.
            if (confirmed && GameInput.ConfirmWasTap)
            {
                var hud = Object.FindAnyObjectByType<HUD>();
                if (hud != null) return hud.UpgradeCardAt(GameInput.LastTapScreen);
            }
            return -1;
        }

        static bool TutorialPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tKey.wasPressedThisFrame) return true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return gp != null && gp.leftShoulder.wasPressedThisFrame;
        }

        static bool CreditsPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) return true;
            var gp = UnityEngine.InputSystem.Gamepad.current;
            return gp != null && gp.rightShoulder.wasPressedThisFrame;
        }

        /// <summary>
        /// Start a run with the tutorial driving it.
        ///
        /// It is a real run, not a sandbox: the same room, the same machines, the same
        /// scoring. Only the pressure is held off, by TutorialRunning below.
        /// </summary>
        public void StartTutorial()
        {
            StartRun();
            var tut = Object.FindAnyObjectByType<Tutorial>();
            if (tut != null) tut.Begin();
        }

        /// <summary>True while a tutorial run is in progress and the gloves are off.</summary>
        public bool TutorialRunning
        {
            get
            {
                if (_tutorial == null) _tutorial = Object.FindAnyObjectByType<Tutorial>();
                return _tutorial != null && _tutorial.Running;
            }
        }

        Tutorial _tutorial;

        void TickDay()
        {
            float dt = Time.deltaTime;

            // The tutorial holds every clock. A player still working out which lid is
            // which should not lose laundry to mildew while they read the instruction,
            // and the whole point of the pressure is that you meet it once you can cope.
            bool teaching = TutorialRunning;
            if (!teaching) DayTimer += dt;
            float decayDt = teaching ? 0f : dt;

            while (_spawnIndex < _spawnTimes.Count && DayTimer >= _spawnTimes[_spawnIndex])
            {
                SpawnGarment();
                _spawnIndex++;
            }

            // Tick every clock, and find the one closest to expiring.
            float mostUrgent = float.MaxValue;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var g = _all[i];
                if (g == null) { _all.RemoveAt(i); continue; }
                g.Tick(decayDt);

                if (g.IsDecaying && g.DecayMultiplier > 0f)
                {
                    // Seconds of real time left, which The Chair halves.
                    float left = (g.DecayLimit - g.StateTimer) / g.DecayMultiplier;
                    if (left < mostUrgent) mostUrgent = left;
                }
            }

            TickWarning(mostUrgent, decayDt);
            UpdateMonsterVisual();

            if (Monster >= Tuning.MonsterMax) { EndRun(); return; }

            if (!teaching && DayTimer >= DayLength) EndDay();
        }

        /// <summary>
        /// A quickening tick for whatever is closest to spoiling. Rate and pitch both
        /// rise as it runs out, so you can hear trouble without looking for it.
        /// </summary>
        const float TickWindow = 6f;
        float _tickTimer;

        void TickWarning(float secondsLeft, float dt)
        {
            if (secondsLeft >= TickWindow || secondsLeft <= 0f)
            {
                _tickTimer = 0f;
                return;
            }

            _tickTimer -= dt;
            if (_tickTimer > 0f) return;

            float urgency = 1f - Mathf.Clamp01(secondsLeft / TickWindow); // 0 far, 1 imminent
            _tickTimer = Mathf.Lerp(0.55f, 0.11f, urgency);
            SfxPlayer.Play(Sfx.Tick, Mathf.Lerp(0.25f, 0.6f, urgency), Mathf.Lerp(0.9f, 1.5f, urgency));
        }

        /// <summary>Unfinished garments counted at the last closing, for the summary.</summary>
        public int UnfinishedAtClose;
        public float ClosingPenalty;

        /// <summary>
        /// Charge for everything still lying around, then let the day be cleared.
        ///
        /// Without this the day boundary was an amnesty: dirty laundry never decays, so an
        /// idle day spoiled nothing and grew nothing, and BeginDay quietly destroyed the
        /// evidence. A player could do nothing forever and never lose.
        ///
        /// A garment that already fed the Monster by spoiling is not charged again - it
        /// has been paid for - and the total is capped so one terrible day cannot end a
        /// run outright.
        /// </summary>
        void ChargeForUnfinished()
        {
            int counted = 0;
            foreach (var g in _all)
            {
                if (g == null || g.MonsterCharged) continue;
                g.MonsterCharged = true;
                counted++;
            }

            UnfinishedAtClose = counted;
            ClosingPenalty = Mathf.Min(counted * Tuning.MonsterPerUnfinished,
                                       Tuning.MonsterClosingCap);
            if (ClosingPenalty > 0f) AddMonster(ClosingPenalty);
        }

        void EndDay()
        {
            CurrentPhase = Phase.DaySummary;
            SfxPlayer.Play(Sfx.DayEnd, 0.9f);

            ChargeForUnfinished();

            // The run total counts the bonus; the star rating deliberately does not.
            RunScore += Score + BonusScore;
            RunDelivered += Delivered;

            float frac = Target <= 0f ? 1f : Score / Target;
            if (frac >= 1f) Stars = 3;
            else if (frac >= Tuning.StarTwoFraction) Stars = 2;
            else if (frac >= Tuning.StarOneFraction) Stars = 1;
            else Stars = 0;

            RunStars += Stars;

            // The likeliest moment to close the tab is the results screen. Checkpoint
            // here as well, so coming back lands on this summary rather than replaying
            // the day that was just finished.
            SaveAtSummary();

            // The closing charge can be what finally finishes a run.
            if (Monster >= Tuning.MonsterMax) { EndRun(); return; }

            // Earning a star is worth a cheer; scraping through on zero is not.
            if (Stars > 0)
            {
                var hero = FindAnyObjectByType<HeroAnimator>();
                if (hero != null) hero.Celebrate();
            }
        }

        int _nextPairId;

        void SpawnGarment()
        {
            var kind = RollKind();

            // Socks arrive two at a time, sharing a pair id. They rarely leave that way.
            if (kind == GarmentKind.Sock)
            {
                // Both socks of a pair share a colour. You match socks by looking at
                // them, so two socks that belong together have to look like they do.
                int pair = _nextPairId++;
                var colour = Garment.Palette[Random.Range(0, Garment.Palette.Length)];

                var first = MakeGarment(kind);
                first.PairId = pair;
                first.BaseColor = colour;
                first.RefreshVisual();

                var second = MakeGarment(kind);
                second.PairId = pair;
                second.BaseColor = colour;
                second.RefreshVisual();
                return;
            }
            MakeGarment(kind);
        }

        /// <summary>
        /// Pick a garment kind from the ones this day knows about.
        ///
        /// Socks are excluded until the day they are introduced - not hidden, not made
        /// rare, simply not dealt. A player who has never been told about pairing should
        /// not be holding an unmatchable sock.
        /// </summary>
        GarmentKind RollKind()
        {
            if (Tuning.SocksActive(Day))
            {
                // A sock avalanche tips the mix rather than replacing it, so the day is
                // still recognisably a laundry day with far too many socks in it.
                if (Today.SockBias > 0f && Random.value < Today.SockBias) return GarmentKind.Sock;
                return (GarmentKind)Random.Range(0, 5);
            }

            // Shirt, Pants, Towel, Delicate - every kind except Sock. Delicates have no
            // special handling yet, so they are an ordinary garment with a different mesh.
            var kind = (GarmentKind)Random.Range(0, 4);
            return kind == GarmentKind.Sock ? GarmentKind.Delicate : kind;
        }

        Garment MakeGarment(GarmentKind kind)
        {

            GameObject go;
            Renderer rend;

            var kindMesh = GarmentMeshes != null && (int)kind < GarmentMeshes.Length
                ? GarmentMeshes[(int)kind] : null;
            if (kindMesh == null) kindMesh = GarmentMesh;

            if (kindMesh != null)
            {
                go = new GameObject("Garment_" + kind);
                go.AddComponent<MeshFilter>().sharedMesh = kindMesh;
                rend = go.AddComponent<MeshRenderer>();
                // The mesh ships untextured on purpose: Garment tints the material to show
                // state, and a photographic texture underneath would muddy those colours.
                go.transform.localScale = Vector3.one;
            }
            else
            {
                go = Prim.Make(PrimitiveType.Cube);
                go.name = "Garment_" + kind;
                go.transform.localScale = kind == GarmentKind.Sock
                    ? new Vector3(0.22f, 0.10f, 0.30f)
                    : new Vector3(0.46f, 0.13f, 0.36f);
                rend = go.GetComponent<Renderer>();
            }

            if (GarmentSpawnParent != null) go.transform.SetParent(GarmentSpawnParent, false);

            // Same trap ProgressBar documents: RenderPipelineAsset.defaultMaterial is
            // editor-only and comes back null in a player, so this guard silently left
            // every spawned garment with no material at all. Builder forces URP/Lit into
            // Always Included Shaders, which is what makes Shader.Find survive the build.
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                rend.sharedMaterial = new Material(litShader);
            }
            else
            {
                var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var fallback = rp != null ? rp.defaultMaterial : null;
                if (fallback != null) rend.sharedMaterial = new Material(fallback);
                else Debug.LogError("GameDirector: no usable shader for garments.");
            }
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var g = go.AddComponent<Garment>();
            g.Kind = kind;
            g.BaseColor = Garment.Palette[Random.Range(0, Garment.Palette.Length)];
            g.State = GarmentState.Dirty;
            // Before pocket day nothing has pockets, so the prompt never offers a check
            // and the roulette has nothing to spin.
            g.HasPockets = Tuning.PocketsActive(Day)
                && (kind == GarmentKind.Pants || Random.value < Tuning.PocketChanceOnNonPants);

            _all.Add(g);
            if (Hamper != null) Hamper.Add(g);
            return g;
        }

        public void Deliver(Garment g)
        {
            // What a wrinkled delivery is worth is the day's business, not a constant:
            // with a guest coming it is worth nothing at all.
            float basePoints = g.FoldedWrinkled ? Today.WrinkledPoints : Tuning.PointsClean;
            Score += basePoints;
            Delivered++;
            _all.Remove(g);

            // A wrinkled delivery breaks the streak. It still scores, it just does not
            // count as keeping up.
            float bonus = 0f;
            if (g.FoldedWrinkled)
            {
                CleanStreak = 0;
            }
            else
            {
                CleanStreak++;
                BestStreakToday = Mathf.Max(BestStreakToday, CleanStreak);
                if (CleanStreak >= Tuning.StreakMin)
                {
                    float earned = (CleanStreak - Tuning.StreakMin + 1) * Tuning.StreakBonusEach;
                    bonus = Mathf.Min(earned, Mathf.Max(0f, Tuning.StreakBonusCap - BonusScore));
                    BonusScore += bonus;
                }
            }

            LastDeliveryTime = Time.time;
            LastDeliveryPoints = basePoints;
            LastDeliveryBonus = bonus;
            LastDeliveryWorld = g.transform.position;

            // A clean delivery calms the Monster. A wrinkled one does not - it is laundry
            // you let spoil, so finishing it late is not an apology. This is the only way
            // anger goes down, which is what makes "finish things properly" the answer to
            // the question the Monster is asking.
            if (!g.FoldedWrinkled) AddMonster(-Tuning.AngerPerCleanDelivery);
        }

        /// <summary>Anger as a 0..1 fraction. What the Monster attacks on, and loses on.</summary>
        public float AngerFraction => Tuning.MonsterMax <= 0f ? 0f : Monster / Tuning.MonsterMax;

        /// <summary>Dirty laundry waiting. What the Monster is SIZED by.</summary>
        public int Backlog => Hamper != null ? Hamper.Waiting.Count : 0;

        public void OnGarmentSpoiled(Garment g, GarmentState newState)
        {
            // Spoiling is the charge for this garment. Closing will not bill it again.
            if (g != null) g.MonsterCharged = true;

            if (newState == GarmentState.Wrinkled)
            {
                WrinkledCount++;
                AddMonster(Tuning.MonsterPerWrinkled);
            }
            else if (newState == GarmentState.Mildewed)
            {
                MildewedCount++;
                AddMonster(Tuning.MonsterPerMildewed);
            }
            else if (newState == GarmentState.Ruined)
            {
                RuinedCount++;
                AddMonster(Tuning.MonsterPerRuined);
            }
        }

        public void AddMonster(float amount)
        {
            Monster = Mathf.Clamp(Monster + amount, 0f, Tuning.MonsterMax);
        }

        void UpdateMonsterVisual()
        {
            if (MonsterPile == null) return;

            // Two things make it big: how much you have neglected, and how much dirty
            // laundry is still piled on it. Taking clothes off it visibly shrinks it.
            float neglect = Monster / Tuning.MonsterMax;
            float pending = Hamper == null
                ? 0f
                : Mathf.Clamp01(Hamper.Waiting.Count / (float)Tuning.MonsterFullPile);
            float f = Mathf.Clamp01(neglect * 0.6f + pending * 0.55f);
            float s = Mathf.Lerp(Tuning.MonsterMinScale, Tuning.MonsterMaxScale, f);

            // Hand the size to the animator, which eases and adds the wobble. Setting
            // localScale here would fight it every frame.
            var anim = MonsterPile.GetComponent<MonsterAnimator>();
            if (anim != null) anim.TargetScale = s;
            else MonsterPile.localScale = new Vector3(s, s, s);

            if (_monsterMat != null)
            {
                // Keep it bright. It is a silly laundry blob, not a horror; size and
                // wobble carry the threat, not a drain of colour.
                _monsterMat.color = Color.Lerp(
                    Color.white,
                    new Color(1f, 0.92f, 0.88f), f);
            }
        }
    }
}
