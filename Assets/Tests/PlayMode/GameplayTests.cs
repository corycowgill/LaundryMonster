using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LaundryMonster.Tests
{
    /// <summary>
    /// The regressions that need a running game: machine unloading, sock matching on one
    /// carry slot, the world freezing outside play, the day-boundary charge, and run
    /// resets.
    ///
    /// Every one of these is here because it was broken once. They are written against
    /// the real scene rather than mocks, because each bug lived in the wiring between
    /// objects rather than inside any one of them.
    /// </summary>
    public class GameplayTests
    {
        GameDirector _dir;
        PlayerController _player;

        [TearDown]
        public void ForgetAnySavedRun() => RunSave.Clear();

        [UnitySetUp]
        public IEnumerator LoadTheRoom()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;          // let Awake/Start run
            yield return null;

            _dir = GameDirector.Instance;
            _player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(_dir, "no GameDirector in the scene");
            Assert.IsNotNull(_player, "no PlayerController in the scene");

            _dir.StartRun();
            _dir.BeginDay(6);           // past every unlock, so nothing is gated off
            _dir.CurrentPhase = Phase.Playing;
            _dir.SetPaused(false);
            _player.Carried.Clear();
            _player.CarryPenalty = 0;
        }

        static Garment MakeGarment(GarmentState state, GarmentKind kind = GarmentKind.Shirt)
        {
            var g = new GameObject("TestGarment").AddComponent<Garment>();
            g.Kind = kind;
            g.SetState(state);
            return g;
        }

        LaundryMachine FindMachine(LaundryMachine.Mode mode)
        {
            foreach (var m in Object.FindObjectsByType<LaundryMachine>(FindObjectsInactive.Include))
                if (m.MachineMode == mode) return m;
            return null;
        }

        static void ForceCycleComplete(LaundryMachine m)
        {
            typeof(LaundryMachine).GetProperty("CycleComplete")
                .GetSetMethod(true).Invoke(m, new object[] { true });
        }

        // ================= machine unloading =================

        [UnityTest]
        public IEnumerator AFinishedLoadStaysUnloadableAfterItSpoils()
        {
            // The original bug: HasFinishedLoad was derived from the garment's state, so a
            // load that spoiled in the drum stopped counting as finished and could never
            // be taken out again.
            var washer = FindMachine(LaundryMachine.Mode.Washer);
            var g = MakeGarment(GarmentState.Wet);

            washer.Contents.Clear();
            washer.Contents.Add(g);
            ForceCycleComplete(washer);
            Assert.IsTrue(washer.HasFinishedLoad, "a completed cycle should be unloadable");

            g.SetState(GarmentState.Mildewed);
            yield return null;

            Assert.IsTrue(washer.HasFinishedLoad,
                "a spoiled load is still a completed cycle and must still come out");
            washer.Contents.Clear();
        }

        [UnityTest]
        public IEnumerator ASpoiledLoadCanBeUnloadedAndRecovered()
        {
            var washer = FindMachine(LaundryMachine.Mode.Washer);
            var g = MakeGarment(GarmentState.Mildewed);

            washer.Contents.Clear();
            washer.Contents.Add(g);
            ForceCycleComplete(washer);

            washer.Interact(_player);
            yield return null;

            Assert.AreEqual(1, _player.Carried.Count, "the spoiled load never came out");
            Assert.AreEqual(0, washer.Contents.Count);

            // ...and a washer will take it back, which is the recovery path.
            washer.Interact(_player);
            yield return null;
            Assert.AreEqual(1, washer.Contents.Count, "a mildewed garment should be re-washable");
            washer.Contents.Clear();
        }

        // ================= sock matching on one slot =================

        [UnityTest]
        public IEnumerator SocksMatchWithASingleCarrySlot()
        {
            // The AirPods disaster permanently drops capacity to one. Before the fix that
            // made matching impossible rather than harder, because matching required both
            // socks in hand at the same moment.
            var station = Object.FindAnyObjectByType<SockStation>();
            station.Waiting.Clear();
            station.Orphans.Clear();
            station.ReadyPairs.Clear();

            _player.CarryPenalty = 99;      // worse than any real run
            Assert.AreEqual(Tuning.MinCarryCapacity, _player.CarryCapacity);

            var a = MakeGarment(GarmentState.Folded, GarmentKind.Sock);
            var b = MakeGarment(GarmentState.Folded, GarmentKind.Sock);
            a.PairId = b.PairId = 31337;

            _player.Take(a);
            station.Interact(_player);                       // leave the first
            Assert.AreEqual(1, station.Waiting.Count, "the first sock was not left waiting");
            Assert.AreEqual(0, station.Orphans.Count, "a sock awaiting a partner is not an orphan");

            _player.Take(b);
            station.Interact(_player);                       // its partner arrives
            yield return null;

            Assert.AreEqual(1, station.ReadyPairs.Count, "the pair never matched");
            Assert.AreEqual(0, station.Waiting.Count);

            _player.Carried.Clear();
            station.Interact(_player);                       // collect it
            Assert.AreEqual(1, _player.Carried.Count);
            Assert.IsTrue(_player.Carried[0].Paired, "what came back was not a pair");

            _player.CarryPenalty = 0;
        }

        [UnityTest]
        public IEnumerator CarryingNeverExceedsCapacity()
        {
            for (int i = 0; i < 8; i++) _player.Take(MakeGarment(GarmentState.Folded));
            yield return null;

            Assert.AreEqual(_player.CarryCapacity, _player.Carried.Count,
                "Take ignored the computed capacity");
        }

        // ================= the world freezes outside play =================

        [UnityTest]
        public IEnumerator MachineCyclesDoNotRunOnTheDaySummary()
        {
            var washer = FindMachine(LaundryMachine.Mode.Washer);
            washer.Contents.Clear();
            washer.Contents.Add(MakeGarment(GarmentState.Dirty));

            typeof(LaundryMachine).GetField("_cycleLength",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(washer, 600f);
            washer.Running = true;
            washer.Timer = 0f;

            _dir.CurrentPhase = Phase.DaySummary;
            for (int i = 0; i < 30; i++) yield return null;

            Assert.AreEqual(0f, washer.Timer, 0.0001f, "a cycle advanced while on the summary");

            _dir.CurrentPhase = Phase.Playing;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.Greater(washer.Timer, 0f, "the cycle did not resume when play did");

            washer.Running = false;
            washer.Contents.Clear();
        }

        [UnityTest]
        public IEnumerator PausingStopsTheWorld()
        {
            var washer = FindMachine(LaundryMachine.Mode.Washer);
            washer.Contents.Clear();
            washer.Contents.Add(MakeGarment(GarmentState.Dirty));
            typeof(LaundryMachine).GetField("_cycleLength",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(washer, 600f);
            washer.Running = true;
            washer.Timer = 0f;

            for (int i = 0; i < 20; i++) yield return null;
            float running = washer.Timer;
            Assert.Greater(running, 0f);

            _dir.SetPaused(true);
            for (int i = 0; i < 30; i++) yield return null;

            Assert.AreEqual(running, washer.Timer, 0.0001f, "a cycle advanced while paused");
            Assert.IsFalse(_dir.IsRunning);

            _dir.SetPaused(false);
            washer.Running = false;
            washer.Contents.Clear();
        }

        [UnityTest]
        public IEnumerator TheMonsterDoesNotAttackOutsidePlay()
        {
            var attack = Object.FindAnyObjectByType<MonsterAttack>();
            var chair = Object.FindAnyObjectByType<Chair>();
            if (attack == null || chair == null) Assert.Ignore("no Monster in this scene");

            chair.Pile.Clear();
            chair.Pile.Add(MakeGarment(GarmentState.CleanDry));
            _dir.Monster = Tuning.MonsterMax;        // as angry as it gets
            _dir.CurrentPhase = Phase.DaySummary;

            for (int i = 0; i < 60; i++) yield return null;

            Assert.IsFalse(attack.Attacking, "the Monster attacked during a summary screen");
            chair.Pile.Clear();
            _dir.CurrentPhase = Phase.Playing;
        }

        // ================= the day boundary =================

        [UnityTest]
        public IEnumerator UnfinishedLaundryIsChargedOnceAtClosing()
        {
            var spawn = typeof(GameDirector).GetMethod("SpawnGarment",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var endDay = typeof(GameDirector).GetMethod("EndDay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            _dir.BeginDay(6);
            _dir.CurrentPhase = Phase.Playing;
            for (int i = 0; i < 4; i++) spawn.Invoke(_dir, null);
            yield return null;

            float before = _dir.Monster;
            endDay.Invoke(_dir, null);

            Assert.Greater(_dir.UnfinishedAtClose, 0, "nothing was counted as unfinished");
            Assert.Greater(_dir.Monster, before, "unfinished laundry cost nothing");
            Assert.LessOrEqual(_dir.ClosingPenalty, Tuning.MonsterClosingCap, "the charge is uncapped");

            // Closing again must not bill the same garments twice.
            float after = _dir.Monster;
            endDay.Invoke(_dir, null);
            Assert.AreEqual(after, _dir.Monster, 0.0001f, "garments were charged twice");
        }

        [UnityTest]
        public IEnumerator ADayThatIntroducesSomethingOpensOnItsBriefing()
        {
            _dir.BeginDay(Tuning.DayLint);
            yield return null;

            Assert.AreEqual(Phase.Briefing, _dir.CurrentPhase);
            Assert.AreEqual(Tuning.Unlock.Lint, _dir.PendingUnlock);
            Assert.IsFalse(_dir.IsRunning, "the world should be frozen behind a briefing");

            // A day with genuinely nothing new starts immediately. That is day one: from
            // day six every day carries a modifier, so there is no quiet day after the
            // tutorial any more.
            _dir.BeginDay(1);
            yield return null;
            Assert.AreEqual(Phase.Playing, _dir.CurrentPhase);
            Assert.AreEqual(DayModifiers.Modifier.None, _dir.Today.Mod);
        }

        [UnityTest]
        public IEnumerator ADayWithAModifierOpensOnItsCardToo()
        {
            // The modifier is the day's rule, and a rule the player is not told is just
            // the game behaving oddly. It gets the same card, and the same frozen world
            // behind it, that a system unlock gets.
            _dir.StartRun();
            _dir.BeginDay(DayModifiers.FirstDay);
            yield return null;

            Assert.AreNotEqual(DayModifiers.Modifier.None, _dir.Today.Mod,
                "the first modifier day arrived without a modifier");
            Assert.AreEqual(Tuning.Unlock.None, _dir.PendingUnlock,
                "a modifier day must never also be teaching a new system");
            Assert.AreEqual(Phase.Briefing, _dir.CurrentPhase,
                "the day's twist should be explained before the clock starts");
            Assert.IsFalse(_dir.IsRunning, "the world should be frozen behind the card");
        }

        // ================= a dryer that is dead for the day =================

        [UnityTest]
        public IEnumerator ADeadDryerSaysSoAndDoesNotPromiseToComeBack()
        {
            // The dead dryer shares Offline with the burnt-out one, and every prompt that
            // keyed on Offline told the player it was on fire and would come back by
            // itself. For the day's modifier neither is true, and "wait for it" is the
            // one plan that cannot work.
            var dryer = FindMachine(LaundryMachine.Mode.Dryer);
            dryer.Contents.Clear();
            dryer.OutOfOrder = true;

            _player.Carried.Clear();
            _player.Take(MakeGarment(GarmentState.Wet));
            yield return null;

            Assert.IsEmpty(dryer.ActionPrompt(_player), "a dead dryer offered an action");
            var why = dryer.BlockedReason(_player).ToLowerInvariant();
            StringAssert.DoesNotContain("fire", why, "a dead dryer claims to be on fire");
            StringAssert.DoesNotContain("comes back", why, "a dead dryer promises to recover");
            StringAssert.Contains("dead", why, "a dead dryer does not say what it is");

            dryer.OutOfOrder = false;
            _player.Carried.Clear();
        }

        // ================= tomorrow's forecast =================

        [UnityTest]
        public IEnumerator TheForecastIsWhatTomorrowTurnsOutToBe()
        {
            // The picker shows tomorrow's modifier so the pick can be a plan. A forecast
            // that is ever wrong is worse than none: the player picks for a day that does
            // not come. Across a whole cycle, what Tomorrow said must be what BeginDay
            // then dealt - including the first modifier day, forecast from the last
            // tutorial day, and the seam between one bag and the next.
            _dir.StartRun();
            for (int day = DayModifiers.FirstDay - 1; day < DayModifiers.FirstDay + 7; day++)
            {
                _dir.BeginDay(day);
                var said = _dir.Tomorrow;
                _dir.BeginDay(day + 1);
                yield return null;
                Assert.AreEqual(said, _dir.Today.Mod,
                    $"on day {day} the forecast said {said}, and day {day + 1} was {_dir.Today.Mod}");
            }
        }

        // ================= the Monster's reach =================

        [UnityTest]
        public IEnumerator TheMonsterVisiblyReachesDuringASnatchAndStopsAfter()
        {
            // The snatch is the one thing the Monster does on purpose, and the arm is
            // its only telegraph that works without reading. It is found by name at
            // runtime, which is exactly the kind of wiring that breaks silently: rename
            // the pivot in MonsterArt and the Monster goes back to a four-degree lean
            // while every other test stays green.
            var pile = GameObject.Find("MonsterPile");
            Assert.IsNotNull(pile, "no MonsterPile in the scene");
            var anim = pile.GetComponent<MonsterAnimator>();
            Assert.IsNotNull(anim, "the Monster has no animator");

            Transform arm = null;
            foreach (var t in pile.GetComponentsInChildren<Transform>(true))
                if (t.name == "ArmPivot") { arm = t; break; }
            Assert.IsNotNull(arm, "the Monster has no ArmPivot - MonsterAnimator finds it by this name");

            float rest = MonsterArm.RestScale.x;
            float reach = MonsterArm.ReachScale.x;
            Assert.Less(rest, reach, "the arm has nowhere to reach to");

            // At rest, tucked in.
            yield return null;
            Assert.AreEqual(rest, arm.localScale.x, 0.05f, "the arm is not tucked in at rest");

            // Halfway through a three-second warning it should be well on its way out.
            anim.Reach(3f);
            float t0 = Time.time;
            while (Time.time - t0 < 1.5f) yield return null;
            Assert.Greater(arm.localScale.x, rest + (reach - rest) * 0.5f,
                $"1.5s into a 3s snatch the arm is at {arm.localScale.x:0.00}, barely past rest {rest:0.00}");

            // And once the warning is over it goes back.
            t0 = Time.time;
            while (Time.time - t0 < 2.5f) yield return null;
            Assert.Less(arm.localScale.x, rest + (reach - rest) * 0.2f,
                $"well after the snatch the arm is still out at {arm.localScale.x:0.00}");
        }

        // ================= continuing a run =================

        [UnityTest]
        public IEnumerator AResumedRunIsTheRunYouLeft()
        {
            // The checkpoint is the start of a day. Everything that defines a run at
            // that moment - the day, the kit, the Monster, the lint, the score, and the
            // seed the modifiers are dealt from - has to come back exactly, or the
            // player resumes into a run that is subtly not theirs. The seed is the one
            // most easily forgotten and the one with the widest consequences: lose it
            // and every remaining day's modifier changes.
            _dir.StartRun();
            _dir.Kit.Grant(UpgradeId.Basket);
            _dir.Monster = 12.5f;
            _dir.RunScore = 41f;
            _dir.RunDelivered = 37;
            _dir.RunStars = 5;
            _dir.VoidedSocks = 3;
            _player.CarryPenalty = 1;
            var dryer = FindMachine(LaundryMachine.Mode.Dryer);
            dryer.Lint = 5;

            _dir.BeginDay(7);                       // writes the checkpoint
            var day = _dir.Day;
            var modifier = _dir.Today.Mod;
            var forecast = _dir.Tomorrow;
            Assert.IsTrue(_dir.HasSavedRun, "a day past the first should leave a save");
            Assert.AreEqual(7, _dir.SavedDay);

            // Wreck the live state, as a reload would - INCLUDING the seed, which is
            // private. Without scrambling it, a resume that forgot the seed would still
            // pass, because the seed it forgot was still sitting in the live object.
            var seedField = typeof(GameDirector).GetField("_runSeed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int savedSeed = (int)seedField.GetValue(_dir);
            seedField.SetValue(_dir, savedSeed + 977);
            _dir.Kit.ResetForRun();
            _dir.Monster = 0f;
            _dir.RunScore = 0f;
            _dir.RunDelivered = 0;
            _dir.RunStars = 0;
            _dir.VoidedSocks = 0;
            _player.CarryPenalty = 0;
            dryer.Lint = 0;
            _dir.Day = 1;
            yield return null;

            _dir.ContinueRun();
            yield return null;

            Assert.AreEqual(day, _dir.Day, "resumed on the wrong day");
            Assert.AreEqual(savedSeed, (int)seedField.GetValue(_dir),
                "the run seed was not restored - every remaining day's modifier would change");
            Assert.AreEqual(modifier, _dir.Today.Mod, "the day's modifier changed on resume - the seed was not restored");
            Assert.AreEqual(forecast, _dir.Tomorrow, "tomorrow's forecast changed on resume");
            Assert.IsTrue(_dir.Kit.Has(UpgradeId.Basket), "the kit did not come back");
            Assert.AreEqual(12.5f, _dir.Monster, 0.001f, "the Monster's anger did not come back");
            Assert.AreEqual(41f, _dir.RunScore, 0.001f);
            Assert.AreEqual(37, _dir.RunDelivered);
            Assert.AreEqual(5, _dir.RunStars);
            Assert.AreEqual(3, _dir.VoidedSocks);
            Assert.AreEqual(1, _player.CarryPenalty, "the AirPods loss did not come back");
            Assert.AreEqual(5, dryer.Lint, "the lint did not come back");
            Assert.IsTrue(_dir.HasSavedRun, "resuming should leave the checkpoint in place");
        }

        [UnityTest]
        public IEnumerator LosingTheRunForgetsIt()
        {
            _dir.StartRun();
            _dir.BeginDay(3);
            Assert.IsTrue(_dir.HasSavedRun);

            _dir.EndRun();
            yield return null;

            Assert.IsFalse(_dir.HasSavedRun, "a lost run is still offered on the title");
        }

        [UnityTest]
        public IEnumerator StartingOverDiscardsTheOldRun()
        {
            _dir.StartRun();
            _dir.BeginDay(4);
            Assert.IsTrue(_dir.HasSavedRun);

            _dir.StartRun();
            yield return null;

            Assert.IsFalse(_dir.HasSavedRun, "a new run should abandon the saved one");
            Assert.AreEqual(1, _dir.Day);
        }

        [UnityTest]
        public IEnumerator DayOneIsNeverACheckpoint()
        {
            // There is nothing to resume on day one that a fresh run does not give you.
            _dir.StartRun();
            yield return null;
            Assert.IsFalse(_dir.HasSavedRun, "a run that has only just started was saved");
        }

        // ================= the game-over card =================

        [UnityTest]
        public IEnumerator LosingTheRunShowsTheGameOverCardNotYesterdays()
        {
            // The summary panel served both the day summary and the game-over screen,
            // and the HUD only refreshed it inside the "playing" branch - which RunOver
            // is not. So the panel appeared on a lost run with whatever text it had last
            // been given: the previous day's DAY N COMPLETE, or nothing at all on a day
            // one loss. THE LAUNDRY WON was never once on screen.
            var hud = Object.FindAnyObjectByType<HUD>();
            var title = (UnityEngine.UI.Text)typeof(HUD)
                .GetField("_summaryTitle", System.Reflection.BindingFlags.NonPublic
                                           | System.Reflection.BindingFlags.Instance)
                .GetValue(hud);

            // Yesterday's card, written the normal way.
            _dir.CurrentPhase = Phase.DaySummary;
            yield return null;
            yield return null;
            StringAssert.Contains("COMPLETE", title.text, "the day summary did not render");

            // Then the run is lost.
            _dir.CurrentPhase = Phase.RunOver;
            yield return null;
            yield return null;

            Assert.AreEqual("THE LAUNDRY WON", title.text,
                "a lost run is showing the previous day's summary instead of the game-over card");
        }

        // ================= scoring =================

        [UnityTest]
        public IEnumerator StarsIgnoreTheStreakBonus()
        {
            _dir.BeginDay(6);
            _dir.CurrentPhase = Phase.Playing;

            for (int i = 0; i < Tuning.StreakMin + 2; i++)
                _dir.Deliver(MakeGarment(GarmentState.Folded));
            yield return null;

            Assert.Greater(_dir.BonusScore, 0f, "a long clean streak paid nothing");
            Assert.AreEqual((Tuning.StreakMin + 2) * Tuning.PointsClean, _dir.Score, 0.0001f,
                "the bonus leaked into the base score that stars are measured on");
            Assert.LessOrEqual(_dir.BonusScore, Tuning.StreakBonusCap);
        }

        [UnityTest]
        public IEnumerator AWrinkledDeliveryBreaksTheStreak()
        {
            _dir.BeginDay(6);
            _dir.CurrentPhase = Phase.Playing;

            for (int i = 0; i < Tuning.StreakMin; i++)
                _dir.Deliver(MakeGarment(GarmentState.Folded));
            Assert.AreEqual(Tuning.StreakMin, _dir.CleanStreak);

            var wrinkled = MakeGarment(GarmentState.Folded);
            wrinkled.FoldedWrinkled = true;
            _dir.Deliver(wrinkled);
            yield return null;

            Assert.AreEqual(0, _dir.CleanStreak, "a wrinkled delivery should break the streak");
        }

        // ================= run resets =================

        [UnityTest]
        public IEnumerator StartingARunClearsEverythingFromTheLastOne()
        {
            _dir.Kit.Grant(UpgradeId.Basket);
            _dir.Kit.Grant(UpgradeId.WrinkleSpray);
            _dir.Monster = Tuning.MonsterMax * 0.8f;
            _player.CarryPenalty = 1;
            _dir.RunScore = 123f;
            _dir.RunDelivered = 45;
            yield return null;

            _dir.StartRun();
            yield return null;

            Assert.AreEqual(0, _dir.Kit.Count, "upgrades survived a new run");
            Assert.AreEqual(0f, _dir.Monster, 0.0001f, "the Monster survived a new run");
            Assert.AreEqual(0f, _dir.RunScore, 0.0001f);
            Assert.AreEqual(0, _dir.RunDelivered);
            Assert.AreEqual(1, _dir.Day, "a new run should start on day one");
            Assert.AreEqual(Tuning.CarryCapacity, _player.CarryCapacity,
                "the AirPods penalty survived a new run");
        }
    }
}
