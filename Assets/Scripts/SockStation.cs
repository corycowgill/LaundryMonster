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
        public readonly List<Garment> Orphans = new List<Garment>();
        public int Rags;

        public override string Label => "Sock Drawer";

        public override string Status
        {
            get
            {
                string s = Orphans.Count + "/" + Tuning.OrphanDrawerCapacity + " orphans";
                if (Rags > 0) s += ", " + Rags + " rag" + (Rags == 1 ? "" : "s");
                return s;
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
            if (FindMatch(p, out a, out b)) return "match the pair";
            if (p.Carried.Count == 0 && Orphans.Count >= Tuning.OrphansPerRag)
                return "make a dust rag from " + Tuning.OrphansPerRag + " orphans";
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
            if (LoneSocksCarried(p) > 0) return "put lone socks in the drawer";
            if (Rags > 0 && AnyLint()) return "use a dust rag on every lint trap";
            return "";
        }

        public override void Interact(PlayerController p)
        {
            if (LoneSocksCarried(p) > 0)
            {
                for (int i = p.Carried.Count - 1; i >= 0; i--)
                {
                    var g = p.Carried[i];
                    if (!g.NeedsPartner) continue;
                    p.Release(g);
                    g.gameObject.SetActive(false);
                    g.DecayMultiplier = 0f;
                    Orphans.Add(g);
                }
                SfxPlayer.Play(Sfx.Drop, 0.9f);
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
            if (Overflowing && GameDirector.Instance != null)
                GameDirector.Instance.AddMonster(
                    Tuning.MonsterPerChairOverflowSecond * Time.deltaTime *
                    (Orphans.Count - Tuning.OrphanDrawerCapacity));
        }
    }
}
