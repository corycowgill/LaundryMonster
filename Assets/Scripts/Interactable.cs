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

        /// <summary>What pressing E would do right now; empty means nothing useful.</summary>
        public abstract string ActionPrompt(PlayerController p);

        /// <summary>&gt; 0 turns this into a hold-to-use action.</summary>
        public virtual float HoldSeconds(PlayerController p) => 0f;

        public abstract void Interact(PlayerController p);
    }
}
