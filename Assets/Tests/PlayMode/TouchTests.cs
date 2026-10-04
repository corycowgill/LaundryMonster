using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LaundryMonster.Tests
{
    /// <summary>
    /// The touch path, which is the one nobody developing on a desktop ever exercises.
    ///
    /// Both of these are here because of the same report: on an iPhone, the upgrade cards
    /// after a day could not be tapped at all. Two separate faults produced it, and each
    /// one is invisible at 16:9 with a keyboard attached.
    /// </summary>
    public class TouchTests
    {
        GameDirector _dir;
        HUD _hud;

        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [UnitySetUp]
        public IEnumerator LoadTheRoom()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _dir = GameDirector.Instance;
            _hud = Object.FindAnyObjectByType<HUD>();
            Assert.IsNotNull(_dir, "no GameDirector in the scene");
            Assert.IsNotNull(_hud, "no HUD in the scene");
        }

        [TearDown]
        public void StopPretendingToBeAPhone()
        {
            SetScheme(GameInput.Scheme.Keyboard);
            typeof(GameInput).GetField("TouchPresent", HiddenStatic).SetValue(null, false);
            if (_screen != null)
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_screen);
                _screen = null;
            }
        }

        UnityEngine.InputSystem.Touchscreen _screen;

        /// <summary>A real touchscreen, so taps arrive through TouchControls rather than
        /// being poked into GameInput behind its back.</summary>
        UnityEngine.InputSystem.Touchscreen Phone()
        {
            if (_screen == null)
                _screen = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Touchscreen>();
            return _screen;
        }

        void Finger(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 at) =>
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(Phone(),
                new UnityEngine.InputSystem.LowLevel.TouchState
                {
                    touchId = id, phase = phase, position = at,
                });

        static void SetScheme(GameInput.Scheme s) =>
            typeof(GameInput).GetProperty("Active").GetSetMethod(true)
                .Invoke(null, new object[] { s });

        /// <summary>Put the game on the picker with three real cards laid out.</summary>
        IEnumerator ShowThePicker()
        {
            _dir.StartRun();
            _dir.Kit.ResetForRun();
            _dir.Offered.Clear();
            _dir.Offered.AddRange(_dir.Kit.Offer(Tuning.UpgradeChoices));
            _dir.CurrentPhase = Phase.UpgradePick;

            // A frame for the HUD to show the panel and lay the cards out.
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        RectTransform CardAt(int i)
        {
            var cards = (System.Collections.Generic.List<RectTransform>)
                typeof(HUD).GetField("_pickCards", Hidden).GetValue(_hud);
            return cards[i];
        }

        static Vector2 ScreenCentreOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
        }

        /// <summary>Hand the game a tap exactly as TouchControls would have.</summary>
        void ReportTap(Vector2 screenPoint)
        {
            SetScheme(GameInput.Scheme.Touch);
            typeof(GameInput).GetField("TouchPresent", HiddenStatic).SetValue(null, true);
            typeof(GameInput).GetField("LastTapScreen").SetValue(null, screenPoint);
            typeof(GameInput).GetField("TouchConfirmFrame", HiddenStatic)
                .SetValue(null, Time.frameCount);
        }

        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TappingAnUpgradeCardTakesThatUpgrade()
        {
            // The headline bug. GameDirector.Update reads the confirm once for every
            // non-playing phase, and on touch that read CLAIMS the tap and blanks it -
            // so the picker's own read came back empty and the card did nothing. A
            // keyboard never showed it, because reading a key twice costs nothing.
            for (int target = 0; target < Tuning.UpgradeChoices; target++)
            {
                yield return ShowThePicker();
                if (target >= _dir.Offered.Count) yield break;

                var want = _dir.Offered[target];
                ReportTap(ScreenCentreOf(CardAt(target)));

                yield return null;      // one real Update of the real director

                Assert.IsTrue(_dir.Kit.Has(want),
                    $"tapping card {target} ({want}) granted nothing - the tap was eaten "
                    + "before the picker could read it");
                Assert.AreNotEqual(Phase.UpgradePick, _dir.CurrentPhase,
                    "the picker should close once something is chosen");
            }
        }

        [UnityTest]
        public IEnumerator TappingBesideTheCardsChoosesNothing()
        {
            yield return ShowThePicker();
            int before = _dir.Kit.Count;

            ReportTap(new Vector2(4f, Screen.height - 4f));
            yield return null;

            Assert.AreEqual(before, _dir.Kit.Count, "a tap in the corner bought something");
            Assert.AreEqual(Phase.UpgradePick, _dir.CurrentPhase,
                "a tap that hit no card should leave the picker up");
        }

        [UnityTest]
        public IEnumerator CardHitBoxesFollowTheCardsOnAnyShapeOfScreen()
        {
            // The second fault. The hit boxes used to be rebuilt arithmetically around
            // 1920/2, but a CanvasScaler on match 0.5 only puts the centre at 960 when
            // the screen is 16:9. On a 19.5:9 phone the canvas is ~2300 units wide, so
            // every box sat a couple of hundred units left of its card: the inner part
            // still worked, the outer third was dead, and there was a live strip of
            // nothing beside the leftmost card.
            yield return ShowThePicker();

            var canvas = (Canvas)typeof(HUD).GetField("_canvas", Hidden).GetValue(_hud);
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var original = scaler.referenceResolution;

            // Reference resolutions that each give the canvas a different width.
            var shapes = new[]
            {
                new Vector2(1920f, 1080f),   // 16:9
                new Vector2(2532f, 1170f),   // iPhone, 19.5:9
                new Vector2(2048f, 1536f),   // iPad, 4:3
                new Vector2(1170f, 2532f),   // held upright
            };

            try
            {
                foreach (var shape in shapes)
                {
                    scaler.referenceResolution = shape;
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    for (int i = 0; i < _dir.Offered.Count; i++)
                    {
                        var rt = CardAt(i);
                        var corners = new Vector3[4];
                        rt.GetWorldCorners(corners);
                        var centre = (corners[0] + corners[2]) * 0.5f;

                        // The middle, and a point near each far corner of the card.
                        foreach (var t in new[] { 0f, 0.88f })
                        foreach (var corner in new[] { corners[0], corners[2] })
                        {
                            var probe = Vector3.Lerp(centre, corner, t);
                            var sp = RectTransformUtility.WorldToScreenPoint(null, probe);
                            Assert.AreEqual(i, _hud.UpgradeCardAt(sp),
                                $"at reference {shape.x}x{shape.y}, a point "
                                + $"{(t == 0f ? "in the middle of" : "near the corner of")} "
                                + $"card {i} did not hit card {i}");
                        }
                    }
                }
            }
            finally
            {
                scaler.referenceResolution = original;
                Canvas.ForceUpdateCanvases();
            }
        }

        // ------------------------------------------------------------------
        // Through TouchControls, not around it.
        //
        // Everything above hands GameInput a tap that TouchControls has already
        // interpreted, which tests the half of the path that was never broken. These
        // two start where a phone starts: at a finger on the glass.

        [UnityTest]
        public IEnumerator AQuickTapOnACardIsNotLost()
        {
            // The reported bug, and the reason the first fix did not fix it.
            //
            // A tap whose press and release reach the input system in the same update
            // leaves the touch slot reading Ended, and never Began. TouchControls only
            // started a menu tap on Began, so the tap was dropped before it became a
            // confirm at all - the player taps a card and the game does not so much as
            // flicker. It cannot happen in the editor, where frames are short enough that
            // a finger is always down for several of them; it is the normal case for a
            // WebGL build on a phone, which is where it was found.
            yield return ShowThePicker();
            var want = _dir.Offered[1];

            var at = ScreenCentreOf(CardAt(1));
            Finger(91, UnityEngine.InputSystem.TouchPhase.Began, at);
            Finger(91, UnityEngine.InputSystem.TouchPhase.Ended, at);

            yield return null;   // TouchControls reads the touch
            yield return null;   // GameDirector claims the confirm
            yield return null;

            Assert.IsTrue(_dir.Kit.Has(want),
                "a tap that began and ended inside one frame was dropped: on a phone that "
                + "is what an ordinary tap looks like, and the picker never answers");
        }

        [UnityTest]
        public IEnumerator AHeldTapOnACardStillWorks()
        {
            // The ordinary case, so the fix above cannot be made by breaking this one.
            yield return ShowThePicker();
            var want = _dir.Offered[2];

            var at = ScreenCentreOf(CardAt(2));
            Finger(92, UnityEngine.InputSystem.TouchPhase.Began, at);
            yield return null;
            yield return null;
            Finger(92, UnityEngine.InputSystem.TouchPhase.Ended, at);
            yield return null;

            Assert.IsTrue(_dir.Kit.Has(want), "a normal press-and-lift tap chose nothing");
        }

        [UnityTest]
        public IEnumerator AFingerThatLingersInEndedFiresOnlyOnce()
        {
            // The regression that arrived with the quick-tap fix.
            //
            // A touch slot does not go quiet the instant a finger lifts: it keeps
            // reporting Ended until the next input update clears it, which on a slow
            // device is several frames. The gameplay controls had no per-finger claim, so
            // ACT fired when the finger landed, again when it lifted, and once more for
            // every lingering frame after - an action repeating with nobody touching the
            // screen. Reported from a phone as "I do not have to tap any more".
            _dir.StartRun();
            _dir.CurrentPhase = Phase.Playing;

            // The hero reads the same one-shot once a frame and claims it, so he has to
            // sit this out or the count below is his, not ours.
            var hero = Object.FindAnyObjectByType<PlayerController>();
            if (hero != null) hero.enabled = false;

            yield return null;
            var act = ActButtonCentre();
            GameInput.InteractPressed();            // drain anything already pending

            int fired = 0;
            Finger(71, UnityEngine.InputSystem.TouchPhase.Began, act);
            yield return null;
            if (GameInput.InteractPressed()) fired++;

            Finger(71, UnityEngine.InputSystem.TouchPhase.Ended, act);
            yield return null;
            if (GameInput.InteractPressed()) fired++;

            // No further events. The slot simply keeps reporting what it last saw.
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                if (GameInput.InteractPressed()) fired++;
            }

            if (hero != null) hero.enabled = true;

            Assert.AreEqual(1, fired,
                $"one press of ACT produced {fired} interactions - a finger that has "
                + "lifted is still being counted on every frame the slot reports Ended");
        }

        /// <summary>Where TouchControls has actually put the ACT button, in screen pixels.</summary>
        Vector2 ActButtonCentre()
        {
            var tc = Object.FindAnyObjectByType<TouchControls>();
            var canvas = (Canvas)typeof(TouchControls).GetField("_canvas", Hidden).GetValue(tc);
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            var centre = (Vector2)typeof(TouchControls)
                .GetMethod("ActCentre", Hidden).Invoke(tc, null);
            return centre * scale;
        }
    }
}
