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
            foreach (var g in p.Carried) if (g.State == GarmentState.Folded) n++;
            return n;
        }

        public override string ActionPrompt(PlayerController p) =>
            FoldedCount(p) > 0 ? "put away" : "";

        public override void Interact(PlayerController p)
        {
            for (int i = p.Carried.Count - 1; i >= 0; i--)
            {
                var g = p.Carried[i];
                if (g.State != GarmentState.Folded) continue;

                p.Release(g);
                GameDirector.Instance?.Deliver(g);
                Destroy(g.gameObject);
            }
        }
    }
}
