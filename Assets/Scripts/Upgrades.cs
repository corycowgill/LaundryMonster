using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    public enum UpgradeId { None, Basket, FoldingBoard, WrinkleSpray }

    /// <summary>
    /// What the player has bought this run.
    ///
    /// Every upgrade is a trade, not a straight gift: the basket doubles what you can
    /// carry and slows you down, because walking is the real cost in this game and an
    /// upgrade that only removed cost would flatten it. The run owns this object, so
    /// starting a new run starts from nothing.
    /// </summary>
    public class Upgrades
    {
        readonly HashSet<UpgradeId> _owned = new HashSet<UpgradeId>();

        /// <summary>Wrinkle Spray charges left today. Refilled at the start of each day.</summary>
        public int SprayCharges { get; private set; }

        public bool Has(UpgradeId id) => _owned.Contains(id);
        public IEnumerable<UpgradeId> Owned => _owned;
        public int Count => _owned.Count;

        public void Grant(UpgradeId id)
        {
            if (id == UpgradeId.None) return;
            _owned.Add(id);
            if (id == UpgradeId.WrinkleSpray) SprayCharges = Tuning.SprayUsesPerDay;
        }

        /// <summary>A new run owns nothing. Called from StartRun.</summary>
        public void ResetForRun()
        {
            _owned.Clear();
            SprayCharges = 0;
        }

        /// <summary>Per-day refill.</summary>
        public void BeginDay()
        {
            SprayCharges = Has(UpgradeId.WrinkleSpray) ? Tuning.SprayUsesPerDay : 0;
        }

        public bool SpendSpray()
        {
            if (SprayCharges <= 0) return false;
            SprayCharges--;
            return true;
        }

        // ---- effects, read by the systems they modify ----

        public int CarryBonus => Has(UpgradeId.Basket) ? Tuning.BasketCarryBonus : 0;

        public float MoveSpeedMultiplier =>
            Has(UpgradeId.Basket) ? Tuning.BasketSpeedMultiplier : 1f;

        public float FoldSeconds =>
            Tuning.FoldHold * (Has(UpgradeId.FoldingBoard) ? Tuning.FoldingBoardMultiplier : 1f);

        public bool CanSpray => Has(UpgradeId.WrinkleSpray) && SprayCharges > 0;

        // ---- the shop ----

        public struct Info
        {
            public string Name;
            public string Effect;
            public string Tradeoff;
        }

        /// <summary>
        /// The displayed description. Written from the tuning constants rather than
        /// repeating them as prose, so the card can never promise a number the game does
        /// not deliver.
        /// </summary>
        public static Info Describe(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.Basket:
                    return new Info
                    {
                        Name = "LAUNDRY BASKET",
                        Effect = "Carry " + (Tuning.CarryCapacity + Tuning.BasketCarryBonus)
                               + " garments instead of " + Tuning.CarryCapacity + ".",
                        Tradeoff = "You walk "
                                 + Mathf.RoundToInt((1f - Tuning.BasketSpeedMultiplier) * 100f)
                                 + "% slower carrying it.",
                    };

                case UpgradeId.FoldingBoard:
                    return new Info
                    {
                        Name = "FOLDING BOARD",
                        Effect = "Folding takes "
                               + (Tuning.FoldHold * Tuning.FoldingBoardMultiplier).ToString("0.0")
                               + "s instead of " + Tuning.FoldHold.ToString("0.0") + "s.",
                        Tradeoff = "No downside. Someone had to stock it.",
                    };

                case UpgradeId.WrinkleSpray:
                    return new Info
                    {
                        Name = "WRINKLE SPRAY",
                        Effect = Tuning.SprayUsesPerDay + " use per day: restore one carried "
                               + "wrinkled garment to clean and dry, with a fresh timer.",
                        Tradeoff = "One charge. It refills tomorrow, not today.",
                    };
            }
            return new Info { Name = "", Effect = "", Tradeoff = "" };
        }

        /// <summary>
        /// Up to <paramref name="count"/> upgrades the player does not already own.
        ///
        /// None of these stack, so owning one removes it from the pool entirely rather
        /// than offering a second copy that would do nothing.
        /// </summary>
        public List<UpgradeId> Offer(int count)
        {
            var pool = new List<UpgradeId>();
            foreach (UpgradeId id in System.Enum.GetValues(typeof(UpgradeId)))
                if (id != UpgradeId.None && !Has(id)) pool.Add(id);

            // Shuffle, then take. With three upgrades and three slots this is cosmetic
            // today, and correct the moment a fourth is added.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (pool.Count > count) pool.RemoveRange(count, pool.Count - count);
            return pool;
        }

        /// <summary>Anything left to buy?</summary>
        public bool AnyAvailable()
        {
            foreach (UpgradeId id in System.Enum.GetValues(typeof(UpgradeId)))
                if (id != UpgradeId.None && !Has(id)) return true;
            return false;
        }
    }
}
