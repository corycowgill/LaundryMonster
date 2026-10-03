using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    /// <summary>
    /// Move, carry two garments, press or hold E. Deliberately no physics: the room
    /// is flat and bounded, so transform movement is simpler and never tunnels.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public float MoveSpeed = 6.5f;
        public Vector2 RoomMin = new Vector2(-7.2f, -4.2f);
        public Vector2 RoomMax = new Vector2(7.2f, 6.2f);

        public readonly List<Garment> Carried = new List<Garment>();
        public Transform CarryAnchor;

        public Interactable Nearest { get; private set; }
        public float HoldProgress { get; private set; }

        float _holdTimer;

        /// <summary>A completed hold already acted, so releasing must not also tap.</summary>
        bool _holdConsumed;

        /// <summary>Permanent carry slots lost this run, e.g. to the AirPods disaster.</summary>
        public int CarryPenalty;

        static Upgrades Kit => GameDirector.Instance != null ? GameDirector.Instance.Kit : null;

        /// <summary>
        /// Base capacity, plus whatever the basket adds, minus whatever the AirPods cost.
        /// The penalty applies on top of the upgrade rather than instead of it, so buying
        /// a basket does not quietly undo a permanent loss.
        /// </summary>
        public int CarryCapacity =>
            Mathf.Max(Tuning.MinCarryCapacity,
                      Tuning.CarryCapacity + (Kit != null ? Kit.CarryBonus : 0) - CarryPenalty);

        /// <summary>Walking speed after upgrades. The basket is heavy.</summary>
        public float EffectiveSpeed => MoveSpeed * (Kit != null ? Kit.MoveSpeedMultiplier : 1f);

        public int FreeSlots => CarryCapacity - Carried.Count;

        void Awake()
        {
            if (CarryAnchor == null)
            {
                var anchor = new GameObject("CarryAnchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(0f, 1.15f, 0.35f);
                CarryAnchor = anchor.transform;
            }
        }

        void Update()
        {
            if (GameDirector.Instance != null && !GameDirector.Instance.AcceptsInput)
            {
                HoldProgress = 0f;
                _holdTimer = 0f;
                return;
            }

            Move();
            FindNearest();
            HandleInteract();
            HandleSpray();
        }

        void Move()
        {
            Vector2 input = GameInput.Move;

            var pos = transform.position;
            float speed = EffectiveSpeed;
            pos.x = Mathf.Clamp(pos.x + input.x * speed * Time.deltaTime, RoomMin.x, RoomMax.x);
            pos.z = Mathf.Clamp(pos.z + input.y * speed * Time.deltaTime, RoomMin.y, RoomMax.y);
            transform.position = pos;

            if (input.sqrMagnitude > 0.01f)
            {
                var face = new Vector3(input.x, 0f, input.y);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(face), 15f * Time.deltaTime);
            }
        }

        void FindNearest()
        {
            Interactable best = null;
            float bestDist = float.MaxValue;

            foreach (var it in Interactable.All)
            {
                if (it == null) continue;
                float d = Vector3.Distance(transform.position, it.transform.position);
                if (d > it.InteractRadius || d >= bestDist) continue;
                bestDist = d;
                best = it;
            }

            if (best != Nearest)
            {
                // Drop the old highlight before taking the new one.
                if (Nearest != null)
                {
                    var prev = Nearest.GetComponent<Highlighter>();
                    if (prev != null) prev.SetHighlighted(false, false);
                }

                Nearest = best;
                _holdTimer = 0f;
                HoldProgress = 0f;
            }

            if (Nearest != null)
            {
                var h = Nearest.GetComponent<Highlighter>();
                if (h != null)
                {
                    bool actionable = !string.IsNullOrEmpty(Nearest.ActionPrompt(this))
                                   || !string.IsNullOrEmpty(Nearest.HoldPrompt(this));
                    h.SetHighlighted(true, actionable);
                }
            }
        }

        bool InteractHeld() => GameInput.InteractHeld();

        bool InteractPressed() => GameInput.InteractPressed();

        bool InteractReleased() => GameInput.InteractReleased();

        void HandleInteract()
        {
            if (Nearest == null)
            {
                _holdTimer = 0f;
                HoldProgress = 0f;
                return;
            }

            bool hasTap = !string.IsNullOrEmpty(Nearest.ActionPrompt(this));
            float need = Nearest.HoldSeconds(this);
            bool hasHold = need > 0f && !string.IsNullOrEmpty(Nearest.HoldPrompt(this));

            // Hold first: a station offering both resolves the hold, and the tap only
            // fires on release if the hold never completed.
            if (hasHold && InteractHeld())
            {
                _holdTimer += Time.deltaTime;
                HoldProgress = Mathf.Clamp01(_holdTimer / need);
                if (_holdTimer >= need)
                {
                    Nearest.HoldInteract(this);
                    var ha = GetComponent<HeroAnimator>();
                    if (ha != null) ha.Grab();
                    _holdTimer = 0f;        // keep holding to repeat
                    HoldProgress = 0f;
                    _holdConsumed = true;   // do not also fire the tap on release
                }
                return;
            }

            if (hasTap && InteractPressed() && !hasHold)
            {
                Nearest.Interact(this);
            }
            else if (hasTap && hasHold && InteractReleased())
            {
                // Tapped and let go before the hold completed -> treat it as the tap action.
                if (!_holdConsumed && _holdTimer > 0f)
                {
                    Nearest.Interact(this);
                    var ta = GetComponent<HeroAnimator>();
                    if (ta != null) ta.Grab();
                }
            }

            if (InteractReleased()) _holdConsumed = false;

            _holdTimer = 0f;
            HoldProgress = 0f;
        }

        /// <summary>
        /// Wrinkle Spray: rescue one carried wrinkled garment back to clean and dry, with
        /// a fresh wrinkle timer. One charge a day, so it is a decision rather than a
        /// repair tool - the point is choosing WHICH garment is worth it.
        /// </summary>
        void HandleSpray()
        {
            var kit = GameDirector.Instance != null ? GameDirector.Instance.Kit : null;
            if (kit == null || !kit.CanSpray) return;
            if (!GameInput.SprayPressed()) return;

            Garment target = null;
            foreach (var g in Carried)
                if (g != null && g.State == GarmentState.Wrinkled) { target = g; break; }

            if (target == null)
            {
                GameDirector.Instance.Flash("nothing wrinkled in your arms to spray.",
                                            new Color(0.8f, 0.8f, 0.85f));
                return;
            }

            if (!kit.SpendSpray()) return;

            target.SetState(GarmentState.CleanDry);   // SetState resets the decay clock
            target.FoldedWrinkled = false;
            SfxPlayer.Play(Sfx.Safe, 0.95f);
            GameDirector.Instance.Flash("wrinkle spray. good as new, for now.",
                                        new Color(0.6f, 0.9f, 0.8f));
        }

        public void Take(Garment g)
        {
            // CarryCapacity, not the raw constant: this gate ignored both the AirPods
            // penalty and the basket, so a one-slot player could still pick up two.
            if (Carried.Count >= CarryCapacity) return;

            Carried.Add(g);
            g.gameObject.SetActive(true);
            g.transform.SetParent(CarryAnchor, false);
            g.transform.localRotation = Quaternion.identity;
            // The garment itself jumps into your arms rather than appearing there.
            Squash.Pop(g, 0.28f);

            if (g.State != GarmentState.Dirty && g.State != GarmentState.Folded)
                g.DecayMultiplier = 1f;

            Restack();
        }

        public void Release(Garment g)
        {
            Carried.Remove(g);
            g.transform.SetParent(null, true);
            Restack();
        }

        void Restack()
        {
            for (int i = 0; i < Carried.Count; i++)
                Carried[i].transform.localPosition = new Vector3(0f, 0.16f * i, 0f);
        }
    }
}
