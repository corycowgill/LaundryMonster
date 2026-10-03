namespace LaundryMonster
{
    /// <summary>
    /// The one card shown when a system unlocks.
    ///
    /// Every briefing answers the same three questions in the same order, because a rule
    /// the player cannot act on is just a warning label: what goes wrong, what it costs,
    /// and what you do about it. Control names are resolved at display time from
    /// GameInput, so a pad player is never told to hold a key they do not have.
    /// </summary>
    public static class Briefings
    {
        public struct Card
        {
            public string Title;
            public string Warning;
            public string Consequence;
            public string Recovery;
        }

        public static Card For(Tuning.Unlock unlock)
        {
            string act = GameInput.InteractGlyph;
            string hold = GameInput.HoldGlyph;

            switch (unlock)
            {
                case Tuning.Unlock.Spoilage:
                    return new Card
                    {
                        Title = "LAUNDRY DOES NOT WAIT",
                        Warning = "Dry laundry starts wrinkling the moment the dryer stops, "
                                + "and wet laundry mildews if you leave it in the washer.",
                        Consequence = "You have " + Tuning.WrinkleGrace + "s to fold something dry, and "
                                    + Tuning.MildewGrace + "s to move something wet. "
                                    + "A wrinkled garment is worth half. A mildewed one is worth nothing until it is washed again.",
                        Recovery = "Nothing is lost. Put a wrinkled load back in a dryer for "
                                 + Tuning.ReDryCycle + "s, or a mildewed one back in a washer.",
                    };

                case Tuning.Unlock.Lint:
                    return new Card
                    {
                        Title = "THE LINT TRAP",
                        Warning = "Every dry cycle clogs the trap a little further, and it never clears itself.",
                        Consequence = "At " + Tuning.LintSlowThreshold + " the dryer runs slow. At "
                                    + Tuning.LintFireThreshold + " it can catch fire and ruin the load. At "
                                    + Tuning.LintMax + " the run ends.",
                        Recovery = hold.ToUpper() + " at a dryer to empty the trap. It never helps the load "
                                 + "in front of you - that is exactly why it gets skipped.",
                    };

                case Tuning.Unlock.Pockets:
                    return new Card
                    {
                        Title = "CHECK YOUR POCKETS",
                        Warning = "Pants always have pockets. Other things sometimes do.",
                        Consequence = "Most loads are fine. The rest cost you a tissue through the whole wash, "
                                    + "a crayon through the whole load, or your AirPods - and the AirPods cost "
                                    + "you a carry slot for the rest of the run.",
                        Recovery = hold.ToUpper() + " at a washer to check first. It takes "
                                 + Tuning.PocketCheckHold + "s. Tapping " + act + " loads without checking, "
                                 + "which is usually fine, which is the trap.",
                    };

                case Tuning.Unlock.Socks:
                    return new Card
                    {
                        Title = "SOCKS",
                        Warning = "Socks arrive in pairs and refuse to leave that way. "
                                + "Every wash may send one sock to the Void.",
                        Consequence = "A lone sock cannot be folded or put away. It scores nothing.",
                        Recovery = "Match pairs at the SOCK DRAWER - carry both, or leave one there and bring "
                                 + "its partner later. Three true orphans make a dust rag, which wipes every "
                                 + "lint trap at once.",
                    };
            }

            return new Card { Title = "", Warning = "", Consequence = "", Recovery = "" };
        }

        /// <summary>
        /// The short nudge shown on the objective card during a day, once the briefing is
        /// gone. It names the thing that is new today rather than repeating the basics.
        /// </summary>
        public static string Hint(int day, PlayerController player)
        {
            if (day < Tuning.DaySpoilage)
                return player != null && player.Carried.Count == 0
                    ? GameInput.MoveGlyph + " to move. Pull laundry off the Monster."
                    : "Washer, then dryer, then the fold table, then the closet.";

            if (day == Tuning.DaySpoilage) return "Dry laundry wrinkles. Fold it before the timer runs out.";
            if (day == Tuning.DayLint) return GameInput.HoldGlyph + " at a dryer to empty the lint trap.";
            if (day == Tuning.DayPockets) return GameInput.HoldGlyph + " at a washer to check pockets first.";
            if (day == Tuning.DaySocks) return "Match socks at the drawer. One at a time is fine.";
            return "";
        }
    }
}
