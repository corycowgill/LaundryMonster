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

        public int FreeSlots => Tuning.CarryCapacity - Carried.Count;

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
                Nearest = best;
                _holdTimer = 0f;
                HoldProgress = 0f;
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

        void HandleInteract()
        {
            if (Nearest == null || string.IsNullOrEmpty(Nearest.ActionPrompt(this)))
            {
                _holdTimer = 0f;
                HoldProgress = 0f;
                return;
            }

            float need = Nearest.HoldSeconds(this);

            if (need <= 0f)
            {
                HoldProgress = 0f;
                if (InteractPressed()) Nearest.Interact(this);
                return;
            }

            if (InteractHeld())
            {
                _holdTimer += Time.deltaTime;
                HoldProgress = Mathf.Clamp01(_holdTimer / need);
                if (_holdTimer >= need)
                {
                    Nearest.Interact(this);
                    _holdTimer = 0f;   // keep holding to fold the next one
                    HoldProgress = 0f;
                }
            }
            else
            {
                _holdTimer = 0f;       // releasing early cancels
                HoldProgress = 0f;
            }
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
