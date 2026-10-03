using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Lights the floor under every station the laundry in your arms is asking for.
    ///
    /// The carry chips already say "FOLD" and "DRAWER", but a word in a corner of the
    /// screen and a bench on the far side of the room are two different pieces of
    /// knowledge, and a new player has to build the mapping between them while the
    /// clock runs. This draws the mapping on the floor instead: pick something up and
    /// the place it belongs lights up in the same colour as its chip.
    ///
    /// Only destinations that can actually take the garment are lit. A running washer
    /// is not an answer to "where does this dirty shirt go", and sending the player to
    /// a machine that will refuse them is worse than saying nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NextStopGuide : MonoBehaviour
    {
        PlayerController _player;

        readonly Dictionary<Highlighter, Color> _wanted = new Dictionary<Highlighter, Color>();
        readonly List<Highlighter> _lastFrame = new List<Highlighter>();

        void Awake() => _player = GetComponent<PlayerController>();

        // LateUpdate so PlayerController has already chosen Nearest for this frame and
        // the proximity highlight wins cleanly over the destination glow.
        void LateUpdate()
        {
            foreach (var h in _lastFrame) if (h != null) h.SetWanted(false, Color.white);
            _lastFrame.Clear();
            _wanted.Clear();

            var dir = GameDirector.Instance;
            if (dir == null || !dir.IsRunning || _player == null) return;

            foreach (var g in _player.Carried)
            {
                if (g == null) continue;
                Mark(g);
            }

            foreach (var kv in _wanted)
            {
                kv.Key.SetWanted(true, kv.Value);
                _lastFrame.Add(kv.Key);
            }
        }

        void Mark(Garment g)
        {
            var tint = g.NextStopColor;

            switch (g.NextStop)
            {
                case "WASHER": MarkMachine(LaundryMachine.Mode.Washer, g, tint); break;
                case "DRYER":  MarkMachine(LaundryMachine.Mode.Dryer, g, tint);  break;
                case "FOLD":   MarkFirst<FoldTable>(tint);   break;
                case "CLOSET": MarkFirst<Closet>(tint);      break;
                case "DRAWER": MarkFirst<SockStation>(tint); break;
            }
        }

        /// <summary>
        /// Light every machine of the right kind that could take this right now. Several
        /// may qualify, and which one is nearest depends on where the player is standing,
        /// so that is their call to make rather than ours.
        /// </summary>
        void MarkMachine(LaundryMachine.Mode mode, Garment g, Color tint)
        {
            bool any = false;
            foreach (var it in Interactable.All)
            {
                var m = it as LaundryMachine;
                if (m == null || m.MachineMode != mode) continue;
                if (m.Offline || m.Running) continue;
                if (m.HasFinishedLoad) continue;              // it has to be emptied first
                if (m.Contents.Count >= Tuning.MachineCapacity) continue;
                any |= Add(m, tint);
            }

            // Everything of that kind is busy. Saying nothing would read as "this garment
            // has nowhere to go"; the chair is where it waits, so point at the chair.
            if (!any) MarkFirst<Chair>(tint);
        }

        void MarkFirst<T>(Color tint) where T : Interactable
        {
            foreach (var it in Interactable.All)
                if (it is T) { Add(it, tint); return; }
        }

        bool Add(Interactable it, Color tint)
        {
            var h = it.GetComponent<Highlighter>();
            if (h == null) return false;
            // Two garments wanting the same bench is normal; the first colour wins so the
            // pad does not flicker between two tints on alternating frames.
            if (!_wanted.ContainsKey(h)) _wanted.Add(h, tint);
            return true;
        }
    }
}
