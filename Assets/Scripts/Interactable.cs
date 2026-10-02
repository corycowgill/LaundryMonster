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

        /// <summary>How long the hold takes. Must be &gt; 0 for a hold action to exist.</summary>
        public virtual float HoldSeconds(PlayerController p) => 0f;

        public virtual void HoldInteract(PlayerController p) { }
    }
}
