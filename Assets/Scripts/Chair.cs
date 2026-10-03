using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// The Chair. Where clean laundry goes to be forgotten. Everything on it wrinkles
    /// twice as fast, and once it is over capacity it starts feeding the Monster.
    /// </summary>
    public class Chair : Interactable
    {
        public readonly List<Garment> Pile = new List<Garment>();

        public override string Label => "The Chair";
        public override string Status => Pile.Count + "/" + Tuning.ChairCapacity;

        public bool Overflowing => Pile.Count > Tuning.ChairCapacity;

        public override string ActionPrompt(PlayerController p)
        {
            if (p.Carried.Count > 0)
                return p.Carried.Count > 1 ? "Dump " + p.Carried.Count + " on the chair"
                                           : "Dump it on the chair";
            if (Pile.Count > 0 && p.FreeSlots > 0) return "Take from the chair";
            return "";
        }

        public override void Interact(PlayerController p)
        {
            Squash.Pop(this, 0.18f);
            if (p.Carried.Count > 0)
            {
                for (int i = p.Carried.Count - 1; i >= 0; i--)
                {
                    var g = p.Carried[i];
                    p.Release(g);
                    Pile.Add(g);
                    g.DecayMultiplier = Tuning.ChairDecayMultiplier;
                }
                SfxPlayer.Play(Sfx.Drop, 0.9f);
                Restack();
                return;
            }

            while (Pile.Count > 0 && p.FreeSlots > 0)
            {
                var g = Pile[Pile.Count - 1];
                Pile.RemoveAt(Pile.Count - 1);
                p.Take(g);
                SfxPlayer.Play(Sfx.PickUp, 0.8f);
            }
            Restack();
        }

        void Restack()
        {
            for (int i = 0; i < Pile.Count; i++)
            {
                var g = Pile[i];
                g.gameObject.SetActive(true);
                g.transform.SetParent(transform, false);
                // Sprawl, not a neat stack. It should look like a mess.
                g.transform.localPosition = new Vector3(
                    Mathf.Sin(i * 2.4f) * 0.22f,
                    0.55f + i * 0.11f,
                    Mathf.Cos(i * 1.7f) * 0.18f);
                g.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            }
        }

        void Update()
        {
            // Nothing ticks outside active play: not cycles, not decay, not
            // the Monster. A results screen is not playtime.
            var dir = GameDirector.Instance;
            if (dir != null && !dir.IsRunning) return;

            if (Overflowing && GameDirector.Instance != null)
            {
                GameDirector.Instance.AddMonster(
                    Tuning.MonsterPerChairOverflowSecond * Time.deltaTime *
                    (Pile.Count - Tuning.ChairCapacity));
            }
        }
    }
}
