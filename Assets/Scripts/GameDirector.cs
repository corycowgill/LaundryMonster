using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    public enum Phase { Intro, Title, Help, Credits, Playing, DaySummary, RunOver }

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
        public float Score;
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
        public float Target => Tuning.TargetForDay(Day);
        public float TimeLeft => Mathf.Max(0f, DayLength - DayTimer);

        /// <summary>No more laundry is coming; what is left is the finishing period.</summary>
        public bool ArrivalsDone => _spawnIndex >= _spawnTimes.Count;

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
            CurrentPhase = Phase.Playing;
            DayTimer = 0f;
            DayLength = Tuning.DayLength(day);
            Score = 0f;
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

            foreach (var m in Object.FindObjectsByType<LaundryMachine>())
            {
                m.Contents.Clear();
                m.Running = false;
                m.Timer = 0f;
                m.OfflineTimer = 0f;          // a burnt dryer cools off overnight
                if (day == 1) m.Lint = 0;     // but lint is run-long debt
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
            int count = Tuning.GarmentsForDay(day);
            // Arrivals stop one full pipeline before the end, so the last garment of the
            // day can actually be washed, dried, folded and delivered. What remains is the
            // finishing period.
            float window = Tuning.ArrivalWindow(day);
            for (int i = 0; i < count; i++)
                _spawnTimes.Add(window * (i / (float)Mathf.Max(1, count - 1)));
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
                    else if (TutorialPressed()) StartTutorial();
                    else if (CreditsPressed()) OpenCredits();
                    break;

                case Phase.Credits:
                    if (go || back) CurrentPhase = Phase.Title;
                    break;

                // (the title's own confirm is handled by TitleConfirm below)

                case Phase.Help:
                    if (go || back || help) CurrentPhase = Phase.Title;
                    else if (TutorialPressed()) StartTutorial();
                    break;

                case Phase.DaySummary:
                    if (go) BeginDay(Day + 1);
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
            if (GameInput.Active == GameInput.Scheme.Touch)
            {
                var hud = Object.FindAnyObjectByType<HUD>();
                int btn = hud != null ? hud.TitleButtonAt(GameInput.LastTapScreen) : -1;
                switch (btn)
                {
                    case 1: StartTutorial(); return;
                    case 2: CurrentPhase = Phase.Help; return;
                    case 3: OpenCredits(); return;
                    case 0: StartRun(); return;
                    default: StartRun(); return;   // a tap anywhere else still starts
                }
            }
            StartRun();
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

            RunScore += Score;
            RunDelivered += Delivered;

            float frac = Target <= 0f ? 1f : Score / Target;
            if (frac >= 1f) Stars = 3;
            else if (frac >= Tuning.StarTwoFraction) Stars = 2;
            else if (frac >= Tuning.StarOneFraction) Stars = 1;
            else Stars = 0;

            RunStars += Stars;

            // The closing charge can be what finally finishes a run.
            if (Monster >= Tuning.MonsterMax) { EndRun(); return; }

            // Earning a star is worth a cheer; scraping through on zero is not.
            if (Stars > 0)
            {
                var hero = FindFirstObjectByType<HeroAnimator>();
                if (hero != null) hero.Celebrate();
            }
        }

        int _nextPairId;

        void SpawnGarment()
        {
            var kind = (GarmentKind)Random.Range(0, 5);

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
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Garment_" + kind;
                go.transform.localScale = kind == GarmentKind.Sock
                    ? new Vector3(0.22f, 0.10f, 0.30f)
                    : new Vector3(0.46f, 0.13f, 0.36f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
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
            g.HasPockets = kind == GarmentKind.Pants || Random.value < Tuning.PocketChanceOnNonPants;

            _all.Add(g);
            if (Hamper != null) Hamper.Add(g);
            return g;
        }

        public void Deliver(Garment g)
        {
            Score += g.FoldedWrinkled ? Tuning.PointsWrinkled : Tuning.PointsClean;
            Delivered++;
            _all.Remove(g);
        }

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
