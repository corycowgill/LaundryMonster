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
