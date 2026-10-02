namespace LaundryMonster
{
    public enum GarmentKind { Shirt, Pants, Towel, Sock, Delicate }

    public enum GarmentState
    {
        Dirty,      // straight from the hamper
        Wet,        // out of the washer, mildew clock running
        Mildewed,   // left wet too long -> must re-wash
        CleanDry,   // out of the dryer, WRINKLE CLOCK RUNNING - the core mechanic
        Wrinkled,   // left dry too long -> re-dry for full value, or fold for half
        Folded,     // ready to deliver
        Ruined      // pocket disaster only; feeds the Monster
    }

    /// <summary>
    /// Every number that decides how the game feels, in one place.
    /// These are first-pass values from DESIGN.md section 4 and are meant to be argued with.
    /// </summary>
    public static class Tuning
    {
        // --- cycles ---
        public const float WashCycle = 12f;
        public const float DryCycle = 15f;
        public const float ReDryCycle = 8f;    // recovering a wrinkled garment

        // --- decay clocks ---
        public const float MildewGrace = 20f;  // wet -> mildewed
        public const float WrinkleGrace = 15f; // clean&dry -> wrinkled   <-- THE number to playtest
        public const float ChairDecayMultiplier = 2f;

        // --- holds ---
        public const float FoldHold = 2.5f;
        public const float PocketCheckHold = 1.2f;

        // --- pocket roulette ---
        // Skipping the check is free most of the time. That is the point: players
        // learn to check by getting burned, not by being told.
        public const float PocketChanceOnNonPants = 0.15f;  // pants always have pockets
        public const float OddsNothing   = 0.70f;
        public const float OddsTissue    = 0.10f;  // load must be re-washed
        public const float OddsWallet    = 0.08f;  // lose a quarter of the day's score
        public const float OddsChapstick = 0.05f;  // 2 garments ruined
        public const float OddsCrayon    = 0.04f;  // whole load ruined
        public const float OddsAirPods   = 0.03f;  // permanent: -1 carry slot for the run
        public const float WalletScorePenalty = 0.25f;
        public const int ChapstickRuins = 2;
        public const int MinCarryCapacity = 1;

        // --- capacities ---
        public const int MachineCapacity = 4;
        public const int CarryCapacity = 2;    // walking is the cost
        public const int ChairCapacity = 8;

        // --- day shape ---
        public const float DayBaseLength = 90f;
        public const float DayLengthPerExtraDay = 10f;
        public const int SpawnBase = 6;
        public const int SpawnPerDay = 2;

        // --- scoring ---
        public const float PointsClean = 1f;
        public const float PointsWrinkled = 0.5f;
        public const float StarOneFraction = 0.60f;
        public const float StarTwoFraction = 0.85f;

        // --- lint: debt, not damage ---
        // Clearing the trap never helps the load in front of you. It only ever pays off
        // later, which is the whole lesson.
        public const int LintMax = 10;
        public const int LintPerDryCycle = 1;
        public const int LintFromTissue = 3;
        public const int LintSlowThreshold = 5;        // dry cycles start dragging
        public const float LintSlowMultiplier = 1.5f;
        public const int LintFireThreshold = 8;        // now it can catch fire
        public const float LintFireChance = 0.20f;
        public const float FireOfflineSeconds = 30f;
        public const float LintClearHold = 2.0f;

        // --- socks: the unwinnable subsystem ---
        // Socks arrive in pairs and leave in odd numbers. You can never reach zero,
        // which is the joke and also true.
        public const float SockVoidChance = 0.15f;   // per wash, one sock may simply go
        public const int OrphanDrawerCapacity = 6;
        public const int OrphansPerRag = 3;
        public const float SockMatchHold = 1.0f;

        // --- monster ---
        public const float MonsterPerWrinkled = 1f;
        public const float MonsterPerMildewed = 1.5f;
        public const float MonsterPerRuined = 3f;
        public const float MonsterPerChairOverflowSecond = 0.5f;
        public const float MonsterMax = 30f;

        public static float DayLength(int day) => DayBaseLength + DayLengthPerExtraDay * (day - 1);

        public static int GarmentsForDay(int day) => SpawnBase + SpawnPerDay * day;

        /// <summary>Delivery target in points for the day.</summary>
        public static float TargetForDay(int day) => GarmentsForDay(day) * 0.8f;
    }
}
