using UnityEngine;
using UnityEngine.InputSystem;

namespace LaundryMonster
{
    /// <summary>
    /// One place that answers "what is the player asking for", whatever they are holding.
    ///
    /// Keyboard, Xbox pad and touch all land here. Before this existed each caller read
    /// Keyboard.current directly, and GameDirector's menus gave up entirely when there was
    /// no keyboard - so on a phone or a pad-only machine the title screen was a dead end
    /// no matter how many buttons you pressed.
    ///
    /// Touch is pushed in by TouchControls rather than polled, because the on-screen stick
    /// owns the geometry of where a finger counts as which control.
    /// </summary>
    public static class GameInput
    {
        public enum Scheme { Keyboard, Gamepad, Touch }

        /// <summary>Whichever device was used most recently. Drives the on-screen prompts.</summary>
        public static Scheme Active { get; private set; } = Scheme.Keyboard;

        // ---- pushed in by TouchControls ----
        internal static Vector2 TouchMove;
        internal static bool TouchActHeld;
        internal static int TouchActDownFrame = -100;
        internal static int TouchActUpFrame = -100;
        internal static int TouchConfirmFrame = -100;
        internal static int TouchHelpFrame = -100;
        internal static int TouchBackFrame = -100;
        internal static int TouchPauseFrame = -100;
        internal static int TouchSprayFrame = -100;
        internal static bool TouchPresent;

        /// <summary>Screen position of the most recent menu tap, for hit-testing buttons.</summary>
        public static Vector2 LastTapScreen;

        /// <summary>
        /// Did this one-shot fire recently, and claim it if so.
        ///
        /// The window is two frames because script execution order is undefined: a flag
        /// TouchControls sets in its Update may not be read until the next frame. It has
        /// to be CLAIMED rather than merely tested, though - left standing for both
        /// frames, a single tap on the help button opened the help screen and then closed
        /// it again on the very next frame, so the title screen never appeared to change.
        /// </summary>
        static bool Claim(ref int frame)
        {
            if (frame != Time.frameCount && frame != Time.frameCount - 1) return false;
            frame = -100;
            return true;
        }

        /// <summary>Test without claiming, for the catch-all checks.</summary>
        static bool TouchFired(int frame) => frame == Time.frameCount || frame == Time.frameCount - 1;

        // ---- movement ----

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;

                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                }

                var gp = Gamepad.current;
                if (gp != null)
                {
                    var stick = gp.leftStick.ReadValue();
                    // The stick processor already deadzones, but a worn pad still drifts
                    // enough to creep the hero across the room while nobody is touching it.
                    if (stick.sqrMagnitude > 0.04f) v += stick;
                    v += gp.dpad.ReadValue();
                }

                v += TouchMove;

                if (v.sqrMagnitude > 1f) v.Normalize();
                return v;
            }
        }

        // ---- interact (tap and hold) ----

        public static bool InteractHeld()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.isPressed) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.isPressed || gp.rightShoulder.isPressed)) return true;
            return TouchActHeld;
        }

        public static bool InteractPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame
                               || gp.rightShoulder.wasPressedThisFrame)) return true;
            return Claim(ref TouchActDownFrame);
        }

        public static bool InteractReleased()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.eKey.wasReleasedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasReleasedThisFrame
                               || gp.rightShoulder.wasReleasedThisFrame)) return true;
            return Claim(ref TouchActUpFrame);
        }

        // ---- front end ----

        /// <summary>Start, continue, dismiss. Space / Enter / A / Start / a tap.</summary>
        public static bool ConfirmPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame
                               || kb.enterKey.wasPressedThisFrame)) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame
                               || gp.startButton.wasPressedThisFrame)) return true;
            return Claim(ref TouchConfirmFrame);
        }

        public static bool HelpPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonNorth.wasPressedThisFrame) return true;
            return Claim(ref TouchHelpFrame);
        }

        public static bool BackPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonEast.wasPressedThisFrame
                               || gp.selectButton.wasPressedThisFrame)) return true;
            return Claim(ref TouchBackFrame);
        }

        /// <summary>Use the Wrinkle Spray: Q, X on a pad, or the on-screen button.</summary>
        public static bool SprayPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.qKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.buttonWest.wasPressedThisFrame) return true;
            return Claim(ref TouchSprayFrame);
        }

        public static string SprayGlyph => Active switch
        {
            Scheme.Gamepad => "(X)",
            Scheme.Touch => "SPRAY",
            _ => "Q",
        };

        /// <summary>The on-screen pause button was tapped.</summary>
        public static bool PauseTapped() => Claim(ref TouchPauseFrame);

        /// <summary>Anything at all, for skipping the intro.</summary>
        public static bool AnyPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame
                               || gp.startButton.wasPressedThisFrame
                               || gp.buttonEast.wasPressedThisFrame)) return true;
            var ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame) return true;
            return TouchFired(TouchConfirmFrame) || TouchFired(TouchActDownFrame);
        }

        // ---- prompt glyphs, so the UI never tells a phone to press E ----

        // Short, because these get dropped into sentences like "<glyph> to interact."
        public static string InteractGlyph => Active switch
        {
            Scheme.Gamepad => "(A)",
            // Not "ACT" any more: the button's face now carries the verb itself, so
            // the word in the sentence should describe the gesture instead.
            Scheme.Touch => "TAP",
            _ => "E",
        };

        public static string ConfirmGlyph => Active switch
        {
            Scheme.Gamepad => "(A)",
            Scheme.Touch => "TAP",
            _ => "SPACE",
        };

        public static string HelpGlyph => Active switch
        {
            Scheme.Gamepad => "(Y)",
            Scheme.Touch => "?",
            _ => "H",
        };

        // Phrased so that "<glyph> to move." reads as a sentence in every case.
        public static string MoveGlyph => Active switch
        {
            Scheme.Gamepad => "Left stick",
            Scheme.Touch => "Drag the left side",
            _ => "WASD",
        };

        /// <summary>"hold E" / "hold (A)" / "press and hold"</summary>
        public static string HoldGlyph => Active switch
        {
            Scheme.Gamepad => "hold (A)",
            Scheme.Touch => "press and hold",
            _ => "hold E",
        };

        // ---- scheme tracking ----

        static void Refresh()
        {
            // Most recent wins, so a player who picks up a pad mid-run sees pad prompts.
            if (TouchPresent)
            {
                Active = Scheme.Touch;
                return;
            }

            var gp = Gamepad.current;
            if (gp != null && (gp.leftStick.ReadValue().sqrMagnitude > 0.09f
                               || gp.dpad.ReadValue().sqrMagnitude > 0.09f
                               || gp.buttonSouth.wasPressedThisFrame
                               || gp.buttonNorth.wasPressedThisFrame
                               || gp.buttonEast.wasPressedThisFrame
                               || gp.startButton.wasPressedThisFrame))
            {
                Active = Scheme.Gamepad;
                return;
            }

            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) Active = Scheme.Keyboard;
        }

        /// <summary>
        /// Self-installing driver. Nothing has to be wired in a scene and nothing breaks
        /// if a scene is rebuilt, which matters because RoomBuilder regenerates the scene
        /// from scratch.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("~GameInput") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<Driver>();
            Object.DontDestroyOnLoad(go);
        }

        class Driver : MonoBehaviour
        {
            void Update() => Refresh();
        }
    }
}
