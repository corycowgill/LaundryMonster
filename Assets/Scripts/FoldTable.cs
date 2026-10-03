using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Where the game is actually won. Hold E to fold one carried garment at a time.
    /// Wrinkled garments can be folded too, for half value - the player's call.
    /// </summary>
    public class FoldTable : Interactable
    {
        public override string Label => "Fold Table";

        public static bool CanFold(Garment g) =>
            (g.State == GarmentState.CleanDry || g.State == GarmentState.Wrinkled)
            && !g.NeedsPartner;   // a lone sock is not foldable. that is the whole problem.

        Garment FirstFoldable(PlayerController p)
        {
            foreach (var g in p.Carried) if (CanFold(g)) return g;
            return null;
        }

        // Folding is a hold, and there is no tap action here.
        public override string ActionPrompt(PlayerController p) => "";
        public override void Interact(PlayerController p) { }

        public override float HoldSeconds(PlayerController p) =>
            FirstFoldable(p) != null
                ? (GameDirector.Instance != null ? GameDirector.Instance.Kit.FoldSeconds
                                                 : Tuning.FoldHold)
                : 0f;

        public override string HoldPrompt(PlayerController p)
        {
            var g = FirstFoldable(p);
            if (g == null)
            {
                foreach (var c in p.Carried)
                    if (c.NeedsPartner) return "";   // tell them at the drawer, not here
                return "";
            }
            return g.State == GarmentState.Wrinkled ? "fold (wrinkled, half value)" : "fold";
        }

        public override void HoldInteract(PlayerController p)
        {
            var g = FirstFoldable(p);
            if (g == null) return;

            SfxPlayer.Play(Sfx.Fold, 1f, g.State == GarmentState.Wrinkled ? 0.75f : 1f);
            g.FoldedWrinkled = g.State == GarmentState.Wrinkled;
            g.SetState(GarmentState.Folded);
            g.DecayMultiplier = 0f; // folded laundry is safe. briefly.
        }
    }
}
