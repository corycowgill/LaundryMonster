using NUnit.Framework;

namespace LaundryMonster.Tests
{
    /// <summary>
    /// Daily modifiers: what is dealt, and what each one is allowed to do.
    ///
    /// The dealing rules matter more than they look. A modifier that can repeat on
    /// consecutive days, or that can land on a tutorial day, turns the feature from
    /// "every day has a character" into "the game is being arbitrary at me".
    /// </summary>
    public class ModifierTests
    {
        [Test]
        public void NoModifierWhileTheGameIsStillTeaching()
        {
            // Days one to five introduce the four systems. A new rule and a twist on that
            // rule arriving together is how a player concludes the game is unfair rather
            // than learning the rule.
            for (int seed = 1; seed <= 40; seed++)
            for (int day = 1; day < DayModifiers.FirstDay; day++)
                Assert.AreEqual(DayModifiers.Modifier.None, DayModifiers.For(day, seed),
                    $"day {day} is still teaching and must not also carry a modifier");
        }

        [Test]
        public void EveryDayFromSixOnwardsHasOne()
        {
            for (int seed = 1; seed <= 40; seed++)
            for (int day = DayModifiers.FirstDay; day < DayModifiers.FirstDay + 30; day++)
                Assert.AreNotEqual(DayModifiers.Modifier.None, DayModifiers.For(day, seed),
                    $"day {day} came back with no modifier at all");
        }

        [Test]
        public void TheSameModifierNeverLandsTwoDaysRunning()
        {
            // The bag is shuffled per cycle, so the join between one cycle and the next
            // is the place this can go wrong, and it is the reason For() checks it.
            for (int seed = 1; seed <= 200; seed++)
            {
                var prev = DayModifiers.Modifier.None;
                for (int day = DayModifiers.FirstDay; day < DayModifiers.FirstDay + 40; day++)
                {
                    var m = DayModifiers.For(day, seed);
                    Assert.AreNotEqual(prev, m,
                        $"seed {seed}: {m} landed on day {day} and on day {day - 1}");
                    prev = m;
                }
            }
        }

        [Test]
        public void EveryModifierIsSeenBeforeAnyIsSeenTwice()
        {
            // Dealt from a bag, not rolled. Six days should be six different modifiers.
            for (int seed = 1; seed <= 60; seed++)
            {
                var seen = new System.Collections.Generic.HashSet<DayModifiers.Modifier>();
                for (int i = 0; i < 6; i++)
                    seen.Add(DayModifiers.For(DayModifiers.FirstDay + i, seed));

                Assert.AreEqual(6, seen.Count,
                    $"seed {seed}: the first six modifier days held only {seen.Count} "
                    + "different modifiers - they are being rolled rather than dealt");
            }
        }

        [Test]
        public void TwoRunsDoNotGetTheSameOrder()
        {
            // Not a guarantee for any particular pair, but across a spread of seeds the
            // order has to actually vary or the seed is doing nothing.
            var orders = new System.Collections.Generic.HashSet<string>();
            for (int seed = 1; seed <= 50; seed++)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 6; i++)
                    sb.Append((int)DayModifiers.For(DayModifiers.FirstDay + i, seed)).Append(',');
                orders.Add(sb.ToString());
            }
            Assert.Greater(orders.Count, 10,
                "fifty runs produced almost the same modifier order - the seed is not reaching the shuffle");
        }

        [Test]
        public void AnOrdinaryDayChangesNothing()
        {
            var p = DayModifiers.PlanFor(DayModifiers.Modifier.None);
            Assert.AreEqual(1f, p.LengthScale);
            Assert.AreEqual(1f, p.CountScale);
            Assert.AreEqual(1f, p.TargetScale);
            Assert.AreEqual(1f, p.WashScale);
            Assert.AreEqual(1f, p.DryScale);
            Assert.AreEqual(Tuning.PointsWrinkled, p.WrinkledPoints);
            Assert.AreEqual(0f, p.SockBias);
            Assert.AreEqual(0, p.DryersOffline);
        }

        [Test]
        public void NoModifierCanMakeADayImpossible()
        {
            // Every modifier is a different question, not a harder one. These are the
            // bounds that keep it that way: nothing may halve the day, double the work,
            // or ask for more than everything that arrives.
            foreach (DayModifiers.Modifier m in System.Enum.GetValues(typeof(DayModifiers.Modifier)))
            {
                var p = DayModifiers.PlanFor(m);

                Assert.GreaterOrEqual(p.LengthScale, 0.8f, $"{m} cuts the day too short");
                Assert.LessOrEqual(p.CountScale, 1.3f, $"{m} brings too much laundry");
                Assert.LessOrEqual(p.WashScale, 1.6f, $"{m} makes washing too slow");
                Assert.LessOrEqual(p.DryScale, 1.6f, $"{m} makes drying too slow");
                Assert.LessOrEqual(p.DryersOffline, 1, $"{m} takes out more than one dryer");

                Assert.Greater(p.TargetScale, 0f, $"{m} has no target at all");

                // And the one that actually bit: the target has to be reachable with the
                // laundry the day brings. A quiet morning cuts the arrivals to 60% and
                // raises the share asked for, and while the target was still derived from
                // the day NUMBER that came to twenty-three points out of fourteen
                // garments - unreachable by delivering every single one of them cleanly.
                for (int day = DayModifiers.FirstDay; day < DayModifiers.FirstDay + 20; day++)
                {
                    int garments = System.Math.Max(1, (int)System.Math.Round(
                        Tuning.GarmentsForDay(day) * p.CountScale));
                    float target = Tuning.TargetForGarments(garments) * p.TargetScale;

                    Assert.LessOrEqual(target, garments * Tuning.PointsClean,
                        $"{m} on day {day} asks for {target:0.0} points from {garments} "
                        + "garments - there is not that much laundry");

                    // And it should still be a day's work, not a formality.
                    Assert.GreaterOrEqual(target, garments * 0.4f,
                        $"{m} on day {day} asks for almost nothing");
                }
            }
        }

        [Test]
        public void EveryModifierSaysWhatItIs()
        {
            foreach (DayModifiers.Modifier m in System.Enum.GetValues(typeof(DayModifiers.Modifier)))
            {
                if (m == DayModifiers.Modifier.None) continue;

                var card = DayModifiers.Describe(m);
                Assert.IsNotEmpty(card.Title, $"{m} has no briefing title");
                Assert.IsNotEmpty(card.Body, $"{m} does not say what it does");
                Assert.IsNotEmpty(card.Advice, $"{m} tells the player nothing to do about it");
                Assert.IsNotEmpty(DayModifiers.ShortName(m),
                    $"{m} has no short label, so the HUD cannot remind anyone it is on");
            }
        }

        [Test]
        public void TheGuestDayRemovesHalfMarksAndPaysForIt()
        {
            // The one modifier that changes a scoring rule rather than a quantity. If it
            // ever stops lowering the target it becomes simply harder, which is the thing
            // these are not supposed to be.
            var p = DayModifiers.PlanFor(DayModifiers.Modifier.GuestComing);
            Assert.AreEqual(0f, p.WrinkledPoints, "a wrinkled delivery should be worth nothing");
            Assert.Less(p.TargetScale, 1f, "and the target should come down to pay for it");
        }
    }
}
