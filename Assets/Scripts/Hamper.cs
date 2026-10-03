using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>Where dirty laundry arrives. The source of all suffering.</summary>
    public class Hamper : Interactable
    {
        public readonly List<Garment> Waiting = new List<Garment>();

        public override string Label => "Hamper";
        public override string Status => Waiting.Count.ToString();

        public override string ActionPrompt(PlayerController p)
        {
            if (Waiting.Count == 0 || p.FreeSlots <= 0) return "";
            return "Pick up laundry";
        }

        public override string BlockedReason(PlayerController p) =>
            Waiting.Count > 0 && p.FreeSlots <= 0
                ? "Hands full - fold or dump something first" : "";

        public override void Interact(PlayerController p)
        {
            while (Waiting.Count > 0 && p.FreeSlots > 0)
            {
                var g = Waiting[0];
                Waiting.RemoveAt(0);
                g.gameObject.SetActive(true);
                p.Take(g);
                SfxPlayer.Play(Sfx.PickUp, 0.8f);
                var ha = p.GetComponent<HeroAnimator>();
                if (ha != null) ha.Grab();
            }
        }

        public void Add(Garment g)
        {
            Waiting.Add(g);
            g.gameObject.SetActive(false);
            g.DecayMultiplier = 0f; // dirty laundry does not get worse; that is the joke
        }
    }
}
