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
