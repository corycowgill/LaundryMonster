using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// The sock drawer. Hold E to match carried socks into pairs; tap E to dump the
    /// hopeless ones. Orphans pile up because their partners went to the Void, and a
    /// full drawer feeds the Monster.
    ///
    /// Three orphans make a dust rag, which wipes every lint trap clean. It is the
    /// only use anyone has ever found for odd socks, in this game or anywhere.
    /// </summary>
    public class SockStation : Interactable
    {
        /// <summary>Partner confirmed gone to the Void. These make rags.</summary>
        public readonly List<Garment> Orphans = new List<Garment>();

        /// <summary>Dropped off, partner still out there somewhere. These can still match.</summary>
        public readonly List<Garment> Waiting = new List<Garment>();

        /// <summary>Matched here and waiting to be collected.</summary>
        public readonly List<Garment> ReadyPairs = new List<Garment>();

        public int Rags;

        public override string Label => "Sock Drawer";

        public override string Status
        {
            get
            {
                var parts = new List<string>();
                if (ReadyPairs.Count > 0) parts.Add(ReadyPairs.Count + " pair ready");
                if (Waiting.Count > 0) parts.Add(Waiting.Count + " waiting");
                parts.Add(Orphans.Count + "/" + Tuning.OrphanDrawerCapacity + " orphans");
                if (Rags > 0) parts.Add(Rags + " rag" + (Rags == 1 ? "" : "s"));
                return string.Join(", ", parts);
            }
        }

        public bool Overflowing => Orphans.Count > Tuning.OrphanDrawerCapacity;

        /// <summary>Two carried socks from the same pair, ready to be matched.</summary>
        bool FindMatch(PlayerController p, out Garment a, out Garment b)
        {
            a = null; b = null;
            for (int i = 0; i < p.Carried.Count; i++)
            {
                var x = p.Carried[i];
                if (!x.IsSock || x.Paired || x.Orphan) continue;
                for (int j = i + 1; j < p.Carried.Count; j++)
                {
                    var y = p.Carried[j];
                    if (!y.IsSock || y.Paired || y.Orphan) continue;
                    if (x.PairId == y.PairId) { a = x; b = y; return true; }
                }
            }
            return false;
        }

        int LoneSocksCarried(PlayerController p)
        {
            int n = 0;
            foreach (var g in p.Carried) if (g.NeedsPartner) n++;
            return n;
        }

        // ---- hold: match a pair, or make a rag ----

        public override float HoldSeconds(PlayerController p)
        {
            Garment a, b;
            if (FindMatch(p, out a, out b)) return Tuning.SockMatchHold;
            if (p.Carried.Count == 0 && Orphans.Count >= Tuning.OrphansPerRag) return Tuning.SockMatchHold;
            return 0f;
        }

        public override string HoldPrompt(PlayerController p)
        {
            Garment a, b;
            if (FindMatch(p, out a, out b)) return "Match the pair";
            if (p.Carried.Count == 0 && Orphans.Count >= Tuning.OrphansPerRag)
                return "Make a dust rag from " + Tuning.OrphansPerRag + " orphans";
            return "";
        }

        public override void HoldInteract(PlayerController p)
        {
            Garment a, b;
            if (FindMatch(p, out a, out b))
            {
                // One sock becomes the pair; the other is absorbed into it.
                a.Paired = true;
                p.Release(b);
                Destroy(b.gameObject);
                GameDirector.Instance?.NoteSockMerged(b);

                a.transform.localScale = new Vector3(0.40f, 0.13f, 0.34f);  // now pair-sized
                SfxPlayer.Play(Sfx.Fold, 1f, 1.25f);
                GameDirector.Instance?.Flash("a matching pair. savour it.", new Color(0.7f, 0.9f, 0.75f));
                return;
            }

            if (p.Carried.Count == 0 && Orphans.Count >= Tuning.OrphansPerRag)
            {
                for (int i = 0; i < Tuning.OrphansPerRag; i++)
                {
                    var g = Orphans[Orphans.Count - 1];
                    Orphans.RemoveAt(Orphans.Count - 1);
                    GameDirector.Instance?.NoteSockMerged(g);
                    if (g != null) Destroy(g.gameObject);
                }
                Rags++;
                SfxPlayer.Play(Sfx.LintClear, 1f);
                GameDirector.Instance?.Flash("dust rag made. it will wipe every lint trap.",
                                             new Color(0.75f, 0.85f, 0.7f));
            }
        }

        // ---- tap: dump lone socks, or spend a rag ----

        public override string ActionPrompt(PlayerController p)
        {
            if (LoneSocksCarried(p) > 0)
                return WouldMatch(p) ? "Match it with the sock waiting here"
                                     : "Leave the sock here for its partner";
            if (ReadyPairs.Count > 0 && p.FreeSlots > 0) return "Take a matched pair";
            if (Rags > 0 && AnyLint()) return "Use a dust rag on every lint trap";
            return "";
        }

        /// <summary>A carried lone sock whose partner is already sitting on the drawer.</summary>
        bool WouldMatch(PlayerController p)
        {
            foreach (var c in p.Carried)
            {
                if (c == null || !c.NeedsPartner || c.Orphan) continue;
                foreach (var w in Waiting)
                    if (w != null && w.PairId == c.PairId) return true;
            }
            return false;
        }

        /// <summary>
        /// Join two socks into one pair object. The survivor becomes the pair and is
        /// scaled up; the other is absorbed, the same as matching in hand.
        /// </summary>
        Garment MakePair(Garment keep, Garment absorb)
        {
            keep.Paired = true;
            keep.transform.localScale = new Vector3(0.40f, 0.13f, 0.34f);
            GameDirector.Instance?.NoteSockMerged(absorb);
            if (absorb != null) Destroy(absorb.gameObject);
            SfxPlayer.Play(Sfx.Fold, 1f, 1.25f);
            return keep;
        }

        public override void Interact(PlayerController p)
        {
            Squash.Pop(this, 0.14f);
            if (LoneSocksCarried(p) > 0)
            {
                bool matched = false;
                for (int i = p.Carried.Count - 1; i >= 0; i--)
                {
                    var g = p.Carried[i];
                    if (g == null || !g.NeedsPartner) continue;

                    p.Release(g);
                    g.DecayMultiplier = 0f;

                    // Known orphan: straight to the drawer, it will become a rag.
                    if (g.Orphan)
                    {
                        g.gameObject.SetActive(false);
                        Orphans.Add(g);
                        continue;
                    }

                    // Does its partner happen to be waiting here already?
                    Garment partner = null;
                    foreach (var w in Waiting)
                        if (w != null && w.PairId == g.PairId) { partner = w; break; }

                    if (partner != null)
                    {
                        Waiting.Remove(partner);
                        partner.gameObject.SetActive(false);
                        ReadyPairs.Add(MakePair(partner, g));
                        matched = true;
                    }
                    else
                    {
                        g.gameObject.SetActive(false);
                        Waiting.Add(g);
                    }
                }

                if (matched)
                    GameDirector.Instance?.Flash("a matching pair. savour it.",
                                                 new Color(0.7f, 0.9f, 0.75f));
                else
                    SfxPlayer.Play(Sfx.Drop, 0.9f);
                return;
            }

            // Collect finished pairs, as many as will fit.
            if (ReadyPairs.Count > 0 && p.FreeSlots > 0)
            {
                while (ReadyPairs.Count > 0 && p.FreeSlots > 0)
                {
                    var pair = ReadyPairs[0];
                    ReadyPairs.RemoveAt(0);
                    if (pair == null) continue;
                    pair.gameObject.SetActive(true);
                    p.Take(pair);
                }
                SfxPlayer.Play(Sfx.PickUp, 0.9f);
                return;
            }

            if (Rags > 0 && AnyLint())
            {
                Rags--;
                foreach (var m in Object.FindObjectsByType<LaundryMachine>())
                    if (m.MachineMode == LaundryMachine.Mode.Dryer) m.Lint = 0;
                SfxPlayer.Play(Sfx.LintClear, 1f);
                GameDirector.Instance?.Flash("every lint trap wiped. by socks. it is something.",
                                             new Color(0.8f, 0.9f, 0.8f));
            }
        }

        static bool AnyLint()
        {
            foreach (var m in Object.FindObjectsByType<LaundryMachine>())
                if (m.MachineMode == LaundryMachine.Mode.Dryer && m.Lint > 0) return true;
            return false;
        }

        void Update()
        {
            // Nothing ticks outside active play: not cycles, not decay, not
            // the Monster. A results screen is not playtime.
            var dir = GameDirector.Instance;
            if (dir != null && !dir.IsRunning) return;

            // A sock waiting here becomes a confirmed orphan the moment the Void takes
            // its partner. Sweeping here keeps VoidSock from needing to know about the
            // drawer's internals.
            for (int i = Waiting.Count - 1; i >= 0; i--)
            {
                var w = Waiting[i];
                if (w == null) { Waiting.RemoveAt(i); continue; }
                if (!w.Orphan) continue;
                Waiting.RemoveAt(i);
                Orphans.Add(w);
            }

            if (Overflowing && GameDirector.Instance != null)
                GameDirector.Instance.AddMonster(
                    Tuning.MonsterPerChairOverflowSecond * Time.deltaTime *
                    (Orphans.Count - Tuning.OrphanDrawerCapacity));
        }
    }
}
