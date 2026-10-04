using NUnit.Framework;
using UnityEngine;

namespace LaundryMonster.Tests
{
    /// <summary>
    /// The rules that are pure arithmetic: the teaching schedule, the day's shape, the
    /// upgrade kit and the briefing copy. None of this needs a scene, so none of it is
    /// allowed to be slow.
    ///
    /// These exist because each one encodes a decision that is easy to break by accident
    /// while tuning something adjacent - the day one hazard lockout in particular looks
    /// like a cosmetic constant and is actually the difference between a tutorial and an
    /// ambush.
    /// </summary>
    public class RulesTests
    {
        // ---------- the teaching schedule ----------

        [Test]
        public void DayOne_HasNoHazardsAtAll()
        {
            Assert.IsFalse(Tuning.SpoilageActive(1), "nothing should spoil on day one");
            Assert.IsFalse(Tuning.LintActive(1), "dry cycles should add no lint on day one");
            Assert.IsFalse(Tuning.PocketsActive(1), "no pockets on day one");
            Assert.IsFalse(Tuning.SocksActive(1), "no socks on day one");
        }

        [Test]
        public void EachSystemIsLiveOnItsOwnDayAndNotBefore()
        {
            Assert.IsFalse(Tuning.SpoilageActive(Tuning.DaySpoilage - 1));
            Assert.IsTrue(Tuning.SpoilageActive(Tuning.DaySpoilage));

            Assert.IsFalse(Tuning.LintActive(Tuning.DayLint - 1));
            Assert.IsTrue(Tuning.LintActive(Tuning.DayLint));

            Assert.IsFalse(Tuning.PocketsActive(Tuning.DayPockets - 1));
            Assert.IsTrue(Tuning.PocketsActive(Tuning.DayPockets));

            Assert.IsFalse(Tuning.SocksActive(Tuning.DaySocks - 1));
            Assert.IsTrue(Tuning.SocksActive(Tuning.DaySocks));
        }

        [Test]
        public void EveryUnlockDayAnnouncesExactlyOneSystem()
        {
            Assert.AreEqual(Tuning.Unlock.Spoilage, Tuning.UnlockFor(Tuning.DaySpoilage));
            Assert.AreEqual(Tuning.Unlock.Lint, Tuning.UnlockFor(Tuning.DayLint));
            Assert.AreEqual(Tuning.Unlock.Pockets, Tuning.UnlockFor(Tuning.DayPockets));
            Assert.AreEqual(Tuning.Unlock.Socks, Tuning.UnlockFor(Tuning.DaySocks));
            Assert.AreEqual(Tuning.Unlock.None, Tuning.UnlockFor(1), "day one introduces the loop itself");
            Assert.AreEqual(Tuning.Unlock.None, Tuning.UnlockFor(Tuning.DaySocks + 1));
        }

        [Test]
        public void EveryBriefing_SaysWhatGoesWrongWhatItCostsAndHowToRecover()
        {
            foreach (Tuning.Unlock u in System.Enum.GetValues(typeof(Tuning.Unlock)))
            {
                if (u == Tuning.Unlock.None) continue;
                var c = Briefings.For(u);
                Assert.IsNotEmpty(c.Title, $"{u} has no title");
                Assert.IsNotEmpty(c.Warning, $"{u} never says what goes wrong");
                Assert.IsNotEmpty(c.Consequence, $"{u} never says what it costs");
                Assert.IsNotEmpty(c.Recovery, $"{u} never says how to recover");
            }
        }

        // ---------- the shape of a day ----------

        [Test]
        public void TheLastArrivalCanStillBeFinished()
        {
            // A garment arriving at the cutoff has to survive a wash, a dry and a fold,
            // with walking in between. If this fails the day is unwinnable by design.
            //
            // Checked under every modifier, and for the WALKING allowance too, not merely
            // for the pipeline: the rainy weekend ran dryers at 1.45x against a cutoff
            // computed from the unmodified dry time, and the last arrivals lost a third
            // of their walking slack. The pipeline still fit, which is exactly why a test
            // that only checked the pipeline could not notice.
            foreach (DayModifiers.Modifier m in System.Enum.GetValues(typeof(DayModifiers.Modifier)))
            {
                var p = DayModifiers.PlanFor(m);
                float pipeline = Tuning.WashCycle * p.WashScale
                               + Tuning.DryCycle * p.DryScale
                               + Tuning.FoldHold;

                for (int day = 1; day <= 12; day++)
                {
                    float length = Tuning.DayLength(day) * p.LengthScale;
                    float finishing = length - Tuning.ArrivalWindow(length, p.WashScale, p.DryScale);
                    Assert.GreaterOrEqual(finishing, pipeline + Tuning.TravelAllowance - 0.01f,
                        $"{m} on day {day} leaves {finishing:0.0}s to finish a "
                        + $"{pipeline:0.0}s pipeline plus {Tuning.TravelAllowance:0}s of walking");
                }
            }
        }

        [Test]
        public void ArrivalsNeverOccupyTheWholeDay()
        {
            for (int day = 1; day <= 12; day++)
                Assert.Less(Tuning.ArrivalWindow(day), Tuning.DayLength(day),
                    $"day {day} never stops dealing laundry");
        }

        [Test]
        public void DaysGetLongerAndTheWorkloadGrowsFaster()
        {
            // The game is supposed to tighten. If garments ever stop outgrowing the clock
            // the escalation has quietly been removed.
            float earlyPerGarment = Tuning.DayLength(1) / Tuning.GarmentsForDay(1);
            float latePerGarment = Tuning.DayLength(10) / Tuning.GarmentsForDay(10);

            Assert.Greater(Tuning.DayLength(10), Tuning.DayLength(1), "days should get longer");
            Assert.Less(latePerGarment, earlyPerGarment, "but time per garment should tighten");
        }

        [Test]
        public void DoingNothingEventuallyEndsARun()
        {
            // An idle day charges for everything left lying around. If that charge were
            // ever zero or uncapped-to-nothing, a player could idle forever.
            float perIdleDay = Mathf.Min(Tuning.MonsterClosingCap,
                                         Tuning.GarmentsForDay(1) * Tuning.MonsterPerUnfinished);
            Assert.Greater(perIdleDay, 0f, "an idle day must cost something");

            int days = Mathf.CeilToInt(Tuning.MonsterMax / perIdleDay);
            Assert.Less(days, 20, $"idling takes {days} days to lose, which is forever");
        }

        [Test]
        public void TheClosingChargeCannotEndARunOnItsOwn()
        {
            Assert.Less(Tuning.MonsterClosingCap, Tuning.MonsterMax,
                "one bad day should never be an instant loss");
        }

        // ---------- the upgrade kit ----------

        [Test]
        public void ANewKitOwnsNothing()
        {
            var kit = new Upgrades();
            Assert.AreEqual(0, kit.Count);
            Assert.AreEqual(0, kit.CarryBonus);
            Assert.AreEqual(1f, kit.MoveSpeedMultiplier);
            Assert.AreEqual(Tuning.FoldHold, kit.FoldSeconds);
            Assert.IsFalse(kit.CanSpray);
        }

        [Test]
        public void EveryUpgradeDoesWhatItsCardSays()
        {
            var kit = new Upgrades();

            kit.Grant(UpgradeId.Basket);
            Assert.AreEqual(Tuning.BasketCarryBonus, kit.CarryBonus);
            Assert.AreEqual(Tuning.BasketSpeedMultiplier, kit.MoveSpeedMultiplier);

            kit.Grant(UpgradeId.FoldingBoard);
            Assert.AreEqual(Tuning.FoldHold * Tuning.FoldingBoardMultiplier, kit.FoldSeconds, 0.0001f);

            kit.Grant(UpgradeId.WrinkleSpray);
            kit.BeginDay();
            Assert.IsTrue(kit.CanSpray);
            Assert.IsTrue(kit.SpendSpray());
            Assert.IsFalse(kit.CanSpray, "the spray is one use a day");
        }

        [Test]
        public void TheSprayRefillsEachDayButDoesNotStockpile()
        {
            var kit = new Upgrades();
            kit.Grant(UpgradeId.WrinkleSpray);

            kit.BeginDay();
            kit.SpendSpray();
            kit.BeginDay();
            Assert.IsTrue(kit.CanSpray, "it should refill overnight");

            kit.BeginDay();
            Assert.AreEqual(Tuning.SprayUsesPerDay, kit.SprayCharges, "charges must not accumulate");
        }

        [Test]
        public void OwnedUpgradesAreNeverOfferedAgain()
        {
            var kit = new Upgrades();
            kit.Grant(UpgradeId.Basket);

            foreach (var id in kit.Offer(Tuning.UpgradeChoices))
                Assert.AreNotEqual(UpgradeId.Basket, id, "a duplicate was offered");

            kit.Grant(UpgradeId.FoldingBoard);
            kit.Grant(UpgradeId.WrinkleSpray);
            Assert.IsFalse(kit.AnyAvailable());
            Assert.AreEqual(0, kit.Offer(Tuning.UpgradeChoices).Count);
        }

        [Test]
        public void ResettingForARunClearsEverything()
        {
            var kit = new Upgrades();
            kit.Grant(UpgradeId.Basket);
            kit.Grant(UpgradeId.WrinkleSpray);
            kit.BeginDay();

            kit.ResetForRun();

            Assert.AreEqual(0, kit.Count);
            Assert.AreEqual(0, kit.CarryBonus);
            Assert.IsFalse(kit.CanSpray);
            Assert.IsTrue(kit.AnyAvailable());
        }

        [Test]
        public void EveryUpgradeCardStatesBothAGiveAndATake()
        {
            foreach (UpgradeId id in System.Enum.GetValues(typeof(UpgradeId)))
            {
                if (id == UpgradeId.None) continue;
                var info = Upgrades.Describe(id);
                Assert.IsNotEmpty(info.Name, $"{id} has no name");
                Assert.IsNotEmpty(info.Effect, $"{id} never says what it gives");
                Assert.IsNotEmpty(info.Tradeoff, $"{id} never says what it costs");
            }
        }

        // ---------- scoring ----------

        [Test]
        public void TheStreakBonusIsSmallerThanADaysTarget()
        {
            // The bonus is flavour on top of the work, not a way to skip it.
            Assert.Less(Tuning.StreakBonusCap, Tuning.TargetForDay(1),
                "the streak bonus should never rival a day's target");
        }

        [Test]
        public void AWrinkledDeliveryIsWorthLessThanACleanOne()
        {
            Assert.Less(Tuning.PointsWrinkled, Tuning.PointsClean);
        }
    }
}
