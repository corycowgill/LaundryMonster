namespace LaundryMonster
{
    /// <summary>
    /// What is different about today.
    ///
    /// Up to day five the game is still teaching: each day introduces exactly one new
    /// rule, and that is quite enough to be going on with. After that the systems are all
    /// in play and the only thing that changed from one day to the next was that the
    /// numbers got bigger - the same day, more of it, until you lost. That is the point
    /// where a run stops being interesting.
    ///
    /// A modifier changes the SHAPE of a day rather than its size. Each one makes a
    /// different thing scarce, so the right plan is different: a rainy day makes dryer
    /// time the bottleneck and wash time nearly free, a guest makes promptness matter
    /// more than throughput, cheap electricity makes the machines fast and your legs the
    /// limit. None of them is simply "harder"; each is a different question.
    ///
    /// Deliberately NOT stacked with the tutorial days. A rule you have never seen and a
    /// twist on that rule arriving together is how a player learns that the game is
    /// unfair rather than learning the rule.
    /// </summary>
    public static class DayModifiers
    {
        public enum Modifier
        {
            None,
            RainyWeekend,
            GuestComing,
            CheapElectricity,
            SockAvalanche,
            DryerDown,
            QuietMorning,
        }

        /// <summary>The first day that gets one. Everything is taught by day five.</summary>
        public const int FirstDay = 6;

        /// <summary>How many real modifiers there are, for the shuffle.</summary>
        const int Kinds = 6;

        /// <summary>
        /// Everything a modifier changes, gathered in one place.
        ///
        /// Scales rather than absolutes, so a modifier cannot quietly undo the day
        /// progression: a long day is still longer than a short one with the same
        /// modifier on it.
        /// </summary>
        public struct Plan
        {
            public Modifier Mod;

            public float LengthScale;
            public float CountScale;
            public float TargetScale;
            public float WashScale;
            public float DryScale;

            /// <summary>What a wrinkled delivery is worth today.</summary>
            public float WrinkledPoints;

            /// <summary>Extra chance, 0..1, that any given arrival is a sock.</summary>
            public float SockBias;

            /// <summary>Dryers out of order for the whole day.</summary>
            public int DryersOffline;

            public static Plan Normal => new Plan
            {
                Mod = Modifier.None,
                LengthScale = 1f,
                CountScale = 1f,
                TargetScale = 1f,
                WashScale = 1f,
                DryScale = 1f,
                WrinkledPoints = Tuning.PointsWrinkled,
                SockBias = 0f,
                DryersOffline = 0,
            };
        }

        public static Plan PlanFor(Modifier m)
        {
            var p = Plan.Normal;
            p.Mod = m;
            switch (m)
            {
                // Nothing dries on a line, so every single thing goes through a dryer and
                // the dryers cannot keep up. Washing is suddenly the easy half: the skill
                // is keeping both dryers fed and never letting one sit finished.
                case Modifier.RainyWeekend:
                    p.DryScale = 1.45f;
                    p.LengthScale = 1.15f;
                    break;

                // Somebody is coming. Half marks for a wrinkled garment is the usual
                // mercy, and today there is none - so folding promptly stops being the
                // tidy option and becomes the only one. The target drops to pay for it.
                case Modifier.GuestComing:
                    p.WrinkledPoints = 0f;
                    p.TargetScale = 0.85f;
                    break;

                // Everything runs quickly and there is more of it in less time. The
                // machines stop being the constraint and the walking starts.
                case Modifier.CheapElectricity:
                    p.WashScale = 0.65f;
                    p.DryScale = 0.65f;
                    p.LengthScale = 0.82f;
                    p.CountScale = 1.25f;
                    break;

                // Someone turned the sock drawer out. Far more of the day arrives as
                // things that cannot be folded until they find a partner.
                case Modifier.SockAvalanche:
                    p.SockBias = 0.45f;
                    p.CountScale = 1.15f;
                    p.TargetScale = 0.9f;
                    break;

                // One dryer is dead. Half the drying capacity, every load through the
                // same machine, and its lint trap filling twice as fast.
                case Modifier.DryerDown:
                    p.DryersOffline = 1;
                    p.LengthScale = 1.1f;
                    p.TargetScale = 0.85f;
                    break;

                // Hardly anything arrives - and the target is nearly all of it, so the
                // slack is in time rather than in margin. The day to clear your lint.
                case Modifier.QuietMorning:
                    p.CountScale = 0.6f;
                    p.TargetScale = 1.2f;
                    p.LengthScale = 0.9f;
                    break;
            }
            return p;
        }

        /// <summary>
        /// Today's modifier.
        ///
        /// Dealt from a shuffled bag rather than rolled, so every modifier is seen once
        /// before any is seen twice and the same one cannot land two days running. The
        /// shuffle is seeded per run, so two runs do not get the same order.
        /// </summary>
        public static Modifier For(int day, int runSeed)
        {
            if (day < FirstDay) return Modifier.None;

            int index = day - FirstDay;
            return (Modifier)(CycleOrder(runSeed, index / Kinds)[index % Kinds] + 1);
        }

        /// <summary>
        /// The order this cycle deals in, with the join to the previous cycle fixed.
        ///
        /// A shuffle cannot repeat inside itself, so the only place the same modifier can
        /// land twice running is the seam between one bag and the next. If the new bag
        /// opens with whatever the last one ended on, it is rotated once - which is enough
        /// on its own, because the six values are distinct and so exactly one of them can
        /// be the offender.
        ///
        /// Built forward from cycle zero rather than by asking the previous cycle, so a
        /// rotation applies to EVERY day of its cycle. Deciding it only on the cycle's
        /// first day was the original fault: day one of the cycle answered from a rotated
        /// bag while day two answered from the unrotated one, and the two agreed - which
        /// produced the repeat it was written to prevent.
        /// </summary>
        static int[] CycleOrder(int runSeed, int cycle)
        {
            var order = Shuffle(runSeed, 0);
            for (int c = 1; c <= cycle; c++)
            {
                var next = Shuffle(runSeed, c);
                if (next[0] == order[Kinds - 1])
                {
                    var moved = next[0];
                    for (int i = 0; i < Kinds - 1; i++) next[i] = next[i + 1];
                    next[Kinds - 1] = moved;
                }
                order = next;
            }
            return order;
        }

        /// <summary>Fisher-Yates over 0..Kinds-1, from a small deterministic generator.</summary>
        static int[] Shuffle(int runSeed, int cycle)
        {
            var a = new int[Kinds];
            for (int i = 0; i < Kinds; i++) a[i] = i;

            uint s = (uint)(runSeed * 73856093 ^ (cycle + 1) * 19349663);
            if (s == 0) s = 2463534242u;

            for (int i = Kinds - 1; i > 0; i--)
            {
                // xorshift: more than enough mixing to deal six cards.
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }

        // ---------- what the player is told ----------

        public struct Card
        {
            public string Title;
            public string Body;
            public string Advice;
        }

        public static Card Describe(Modifier m)
        {
            switch (m)
            {
                case Modifier.RainyWeekend:
                    return new Card
                    {
                        Title = "RAINY WEEKEND",
                        Body = "Nothing is drying outside today, so every single thing has to "
                             + "go through a dryer - and they are running slow in the damp.",
                        Advice = "Washing is the easy half today. Keep both dryers fed and never "
                               + "let a finished one stand there wrinkling.",
                    };

                case Modifier.GuestComing:
                    return new Card
                    {
                        Title = "SOMEONE IS COMING OVER",
                        Body = "It all has to look right. A wrinkled garment is worth NOTHING "
                             + "today instead of half.",
                        Advice = "Fold the moment a dryer stops. There is less to deliver than "
                               + "usual, so take the time and get it right.",
                    };

                case Modifier.CheapElectricity:
                    return new Card
                    {
                        Title = "CHEAP ELECTRICITY",
                        Body = "Run everything now. Washes and dries are much quicker, there is "
                             + "more laundry than usual, and less of the day to do it in.",
                        Advice = "The machines will not be what holds you up. Your legs will - "
                               + "plan the walking.",
                    };

                case Modifier.SockAvalanche:
                    return new Card
                    {
                        Title = "THE SOCK DRAWER WENT OVER",
                        Body = "Most of what arrives today is socks, and a lone sock cannot be "
                             + "folded until it finds its partner.",
                        Advice = "Work the sock drawer hard. Leaving orphans there is better "
                               + "than carrying them around all day.",
                    };

                case Modifier.DryerDown:
                    return new Card
                    {
                        Title = "ONE DRYER IS DEAD",
                        Body = "Half your drying capacity is gone for the day. Everything has to "
                             + "go through the one that still works.",
                        Advice = "That trap is now filling twice as fast. Clear it early, while "
                               + "you still have a moment.",
                    };

                case Modifier.QuietMorning:
                    return new Card
                    {
                        Title = "A QUIET MORNING",
                        Body = "Hardly anything is coming today - but you are asked for nearly "
                             + "all of it, so there is no room to lose any.",
                        Advice = "This is the day to empty the lint traps and catch up on "
                               + "whatever the Monster is still holding.",
                    };

                default:
                    return new Card { Title = "", Body = "", Advice = "" };
            }
        }

        /// <summary>Short label for the HUD, so the day's rule is never more than a glance away.</summary>
        public static string ShortName(Modifier m) => m switch
        {
            Modifier.RainyWeekend => "RAINY: DRYERS SLOW",
            Modifier.GuestComing => "GUEST: NO HALF MARKS",
            Modifier.CheapElectricity => "CHEAP POWER: FAST + BUSY",
            Modifier.SockAvalanche => "SOCKS EVERYWHERE",
            Modifier.DryerDown => "ONE DRYER DEAD",
            Modifier.QuietMorning => "QUIET: DELIVER NEARLY ALL",
            _ => "",
        };
    }
}
