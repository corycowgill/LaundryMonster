using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Anything the player can walk up to and press E on. Registers itself so the
    /// player can find the nearest one without a physics query.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public float InteractRadius = 2.4f;

        protected virtual void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>Name shown in the interaction prompt.</summary>
        public abstract string Label { get; }

        /// <summary>Right-hand status line, e.g. "washing 7s" or "3/8".</summary>
        public virtual string Status => "";

        // A station can offer a tap action, a hold action, or both. Offering both is
        // what makes "load it now" vs "check the pockets first" a real decision at
        // the moment you commit, with no detour to a separate station.

        /// <summary>What TAPPING E does right now; empty means nothing useful.</summary>
        public abstract string ActionPrompt(PlayerController p);

        public abstract void Interact(PlayerController p);

        /// <summary>What HOLDING E does right now; empty means holding does nothing.</summary>
        public virtual string HoldPrompt(PlayerController p) => "";

        /// <summary>
        /// Why there is nothing to press here, when the player is plainly trying.
        ///
        /// Kept apart from ActionPrompt on purpose. If "hands full" came back as an
        /// action the station would light up as actionable and grow a key cap, and the
        /// press would do nothing - which teaches the player that the prompt lies.
        /// </summary>
        public virtual string BlockedReason(PlayerController p) => "";

        /// <summary>
        /// The same action in at most two words, for the touch button's face.
        ///
        /// Derived from the prompt rather than written out a second time, so a station
        /// can never end up with a button that promises something the prompt does not.
        /// A phone player sees only this, so "ACT" was the one label guaranteed to be
        /// useless at the moment of pressing it.
        /// </summary>
        public virtual string ButtonLabel(PlayerController p)
        {
            string s = ActionPrompt(p);
            if (string.IsNullOrEmpty(s)) s = HoldPrompt(p);
            if (string.IsNullOrEmpty(s)) return "";

            int paren = s.IndexOf('(');
            if (paren > 0) s = s.Substring(0, paren);
            s = s.Trim();

            var words = s.Split(' ');
            if (words.Length > 2) s = words[0] + " " + words[1];
            return s.ToUpperInvariant();
        }

        /// <summary>How long the hold takes. Must be &gt; 0 for a hold action to exist.</summary>
        public virtual float HoldSeconds(PlayerController p) => 0f;

        public virtual void HoldInteract(PlayerController p) { }
    }
}
