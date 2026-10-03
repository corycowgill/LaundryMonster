using UnityEngine;

namespace LaundryMonster
{
    /// <summary>Put folded laundry away. This is the only thing that scores.</summary>
    public class Closet : Interactable
    {
        public override string Label => "Closet";

        int FoldedCount(PlayerController p)
        {
            int n = 0;
            foreach (var g in p.Carried) if (g.State == GarmentState.Folded && !g.NeedsPartner) n++;
            return n;
        }

        public override string ActionPrompt(PlayerController p)
        {
            int n = FoldedCount(p);
            if (n == 0) return "";
            return n > 1 ? "Put away " + n + " folded" : "Put away folded laundry";
        }

        /// <summary>
        /// Standing at the closet holding something it will not take is the commonest
        /// dead end in the game, and silence here reads as a broken button.
        /// </summary>
        public override string BlockedReason(PlayerController p)
        {
            if (FoldedCount(p) > 0) return "";
            foreach (var g in p.Carried)
            {
                if (g == null) continue;
                if (g.NeedsPartner) return "That sock needs its partner - try the drawer";
                if (g.State == GarmentState.CleanDry || g.State == GarmentState.Wrinkled)
                    return "Fold it first - the closet only takes folded";
            }
            return "";
        }

        public override void Interact(PlayerController p)
        {
            int n = 0;
            for (int i = p.Carried.Count - 1; i >= 0; i--)
            {
                var g = p.Carried[i];
                if (g.State != GarmentState.Folded || g.NeedsPartner) continue;

                p.Release(g);
                var tint = g.DisplayColor;
                GameDirector.Instance?.Deliver(g);
                SfxPlayer.Play(Sfx.Deliver, 1f, 1f + n * 0.12f);
                n++;
                Destroy(g.gameObject);

                // In the garment's own colour, so the burst is visibly that shirt
                // rather than a generic firework.
                Confetti.Burst(transform.position + new Vector3(0f, 1.3f, -0.4f), tint, 9);
            }

            if (n > 0) Squash.Pop(this, 0.12f + n * 0.04f);
        }
    }
}
