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
        }

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
    }
}
