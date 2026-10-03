using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LaundryMonster.Tests
{
    /// <summary>
    /// The room has to fit inside the picture, and the picture has to leave room for the
    /// interface drawn on top of it.
    ///
    /// Both of these come from the same report: on an iPhone held sideways the machines
    /// were drawn underneath the tutorial card. The camera solves its field of view from
    /// the aspect ratio, so a shape of screen nobody tried is a shape of screen nobody
    /// checked - which is every shape, since development happens at 16:9.
    /// </summary>
    public class LayoutTests
    {
        Camera _cam;
        CameraFraming _framing;
        HUD _hud;

        // The shapes real screens actually come in, widest to squarest.
        static readonly (string name, float aspect)[] Screens =
        {
            ("ultrawide 21:9", 21f / 9f),
            ("iPhone landscape", 2532f / 1170f),
            ("desktop 16:9", 16f / 9f),
            ("laptop 16:10", 16f / 10f),
            ("iPad 4:3", 4f / 3f),
        };

        [UnitySetUp]
        public IEnumerator LoadTheRoom()
        {
            SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _cam = Camera.main;
            _hud = Object.FindAnyObjectByType<HUD>();
            Assert.IsNotNull(_cam, "no main camera");
            Assert.IsNotNull(_hud, "no HUD");

            _framing = _cam.GetComponent<CameraFraming>();
            Assert.IsNotNull(_framing, "the camera has no CameraFraming");

            var dir = GameDirector.Instance;
            dir.StartRun();
            dir.BeginDay(3);
            dir.CurrentPhase = Phase.Playing;
            dir.SetPaused(false);
            yield return null;          // let the HUD lay the objective card out
        }

        [TearDown]
        public void GiveTheCameraItsScreenBack()
        {
            if (_cam != null) _cam.ResetAspect();
            if (_framing != null) _framing.Refresh();
        }

        RectTransform HudCard(string name)
        {
            var gameplay = (GameObject)typeof(HUD)
                .GetField("_gameplay", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_hud);
            foreach (Transform c in gameplay.transform)
                if (c.name == name) return c as RectTransform;
            return null;
        }

        static float BottomEdge(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var a = RectTransformUtility.WorldToScreenPoint(null, c[0]);
            var b = RectTransformUtility.WorldToScreenPoint(null, c[2]);
            return Mathf.Min(a.y, b.y);
        }

        [UnityTest]
        public IEnumerator TheMachinesAreNeverDrawnUnderTheObjectiveCard()
        {
            var objective = HudCard("Objective");
            Assert.IsNotNull(objective, "no objective card in the HUD");

            float cardBottom = BottomEdge(objective);

            foreach (var screen in Screens)
            {
                _cam.aspect = screen.aspect;
                _framing.Refresh();
                yield return null;

                foreach (var m in Object.FindObjectsByType<LaundryMachine>(FindObjectsInactive.Exclude))
                {
                    // The top of the machine's body, which is the part that gets hidden.
                    var top = m.transform.position + new Vector3(0f, 1.45f, 0f);
                    var sp = _cam.WorldToScreenPoint(top);

                    Assert.Less(sp.y, cardBottom,
                        $"on a {screen.name} screen the top of {m.name} is at y {sp.y:0}, "
                        + $"above the objective card's bottom edge at {cardBottom:0} - "
                        + "the machine is drawn behind the card");
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryStationStaysInFrameOnEveryShapeOfScreen()
        {
            foreach (var screen in Screens)
            {
                _cam.aspect = screen.aspect;
                _framing.Refresh();
                yield return null;

                foreach (var it in Interactable.All)
                {
                    var v = _cam.WorldToViewportPoint(it.transform.position + Vector3.up);
                    Assert.Greater(v.z, 0f, $"{it.Label} is behind the camera on {screen.name}");
                    Assert.That(v.x, Is.InRange(0f, 1f),
                        $"{it.Label} is off the side of a {screen.name} screen");
                    Assert.That(v.y, Is.InRange(0f, 1f),
                        $"{it.Label} is off the top or bottom of a {screen.name} screen");
                }
            }
        }

        [UnityTest]
        public IEnumerator ThePlayerCannotWalkOutOfShot()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            Assert.IsNotNull(player);

            foreach (var screen in Screens)
            {
                _cam.aspect = screen.aspect;
                _framing.Refresh();
                yield return null;

                foreach (var x in new[] { player.RoomMin.x, 0f, player.RoomMax.x })
                foreach (var z in new[] { player.RoomMin.y, 0f, player.RoomMax.y })
                {
                    // Measured at head height: down in the near corners the head projects
                    // further out than the feet, and that is the corner that fell off.
                    var v = _cam.WorldToViewportPoint(new Vector3(x, 1.8f, z));
                    Assert.Greater(v.z, 0f);
                    Assert.That(v.x, Is.InRange(0f, 1f),
                        $"standing at ({x:0.0}, {z:0.0}) puts the player off the side "
                        + $"of a {screen.name} screen");
                    Assert.That(v.y, Is.InRange(0f, 1f),
                        $"standing at ({x:0.0}, {z:0.0}) puts the player off the top or "
                        + $"bottom of a {screen.name} screen");
                }
            }
        }
    }
}
