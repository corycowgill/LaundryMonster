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

        public int CarryCapacity =>
            Mathf.Max(Tuning.MinCarryCapacity, Tuning.CarryCapacity - CarryPenalty);

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
        }

        void Move()
        {
            Vector2 input = Vector2.zero;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            }

            var gp = Gamepad.current;
            if (gp != null) input += gp.leftStick.ReadValue();

            if (input.sqrMagnitude > 1f) input.Normalize();

            var pos = transform.position;
            pos.x = Mathf.Clamp(pos.x + input.x * MoveSpeed * Time.deltaTime, RoomMin.x, RoomMax.x);
            pos.z = Mathf.Clamp(pos.z + input.y * MoveSpeed * Time.deltaTime, RoomMin.y, RoomMax.y);
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

        bool InteractHeld()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.isPressed) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonSouth.isPressed;
        }

        bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasPressedThisFrame;
        }

        bool InteractReleased()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasReleasedThisFrame) return true;
            var gp = Gamepad.current;
            return gp != null && gp.buttonSouth.wasReleasedThisFrame;
        }

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
                if (!_holdConsumed && _holdTimer > 0f) Nearest.Interact(this);
            }

            if (InteractReleased()) _holdConsumed = false;

            _holdTimer = 0f;
            HoldProgress = 0f;
        }

        public void Take(Garment g)
        {
            if (Carried.Count >= Tuning.CarryCapacity) return;

            Carried.Add(g);
            g.gameObject.SetActive(true);
            g.transform.SetParent(CarryAnchor, false);
            g.transform.localRotation = Quaternion.identity;

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
