using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LaundryMonster
{
    /// <summary>
    /// On-screen controls for phones: a floating stick on the left, an ACT button on the
    /// right, and a help button in the front end.
    ///
    /// Touches are read straight from Touchscreen rather than through uGUI buttons. The
    /// stick has to decide for itself which finger belongs to it and where that finger
    /// started, which a Button cannot express, and doing it this way means the game needs
    /// no EventSystem - one less thing RoomBuilder has to remember to create.
    ///
    /// The stick FLOATS: it appears wherever the thumb lands rather than sitting in a fixed
    /// corner. On a phone held two-handed there is no one correct corner, and a fixed stick
    /// is permanently slightly wrong for most hands.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        [Tooltip("Travel from the stick's origin, in reference-resolution units, for full tilt.")]
        public float StickRange = 170f;

        [Tooltip("Radius of the ACT button, in reference-resolution units.")]
        public float ActRadius = 150f;

        [Tooltip("Radius of the front-end help button, in reference-resolution units.")]
        public float HelpRadius = 85f;

        [Tooltip("Radius of the in-game pause button, in reference-resolution units.")]
        public float PauseRadius = 62f;

        const float RefWidth = 1920f;
        const float RefHeight = 1080f;

        Canvas _canvas;
        CanvasScaler _scaler;
        RectTransform _stickRing, _stickKnob, _actButton, _helpButton, _pauseButton;
        RectTransform _stickHome, _stickHomeKnob;
        Text _actLabel, _helpLabel, _pauseLabel, _stickHomeLabel;
        Sprite _disc;

        GameDirector _dir;

        int _moveTouchId = -1;
        Vector2 _moveOrigin;
        int _actTouchId = -1;
        readonly System.Collections.Generic.HashSet<int> _menuClaimed = new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.HashSet<int> _seenThisFrame = new System.Collections.Generic.HashSet<int>();
        float _lastTouchTime = -99f;

        void Start()
        {
            _dir = GameDirector.Instance;
            _disc = MakeDiscSprite(96);
            Build();
        }

        // ---------- construction ----------

        void Build()
        {
            var go = new GameObject("Touch Canvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD: the thumb controls must never end up behind a prompt.
            _canvas.sortingOrder = 20;

            _scaler = go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            _scaler.matchWidthOrHeight = 0.5f;

            // The resting stick: a target to aim the thumb at. The live stick still
            // floats to wherever the thumb actually lands, because no fixed corner is
            // right for every hand - but an invisible control is not a control.
            _stickHome = MakeDisc(go.transform, "StickHome", StickRange * 1.05f,
                                  new Color(1f, 1f, 1f, 0.10f));
            _stickHomeKnob = MakeDisc(go.transform, "StickHomeKnob", StickRange * 0.46f,
                                      new Color(1f, 1f, 1f, 0.22f));
            // Centred in the ring rather than slung underneath it. Underneath put it
            // straight through the Monster card in the same corner, and a label is only
            // ever read while the stick is at rest - the moment a thumb lands the whole
            // resting stick hands over to the live one and the word goes with it.
            _stickHomeLabel = MakeLabel(_stickHome, "WALK", 34);

            _stickRing = MakeDisc(go.transform, "StickRing", StickRange * 1.05f,
                                  new Color(1f, 1f, 1f, 0.13f));
            _stickKnob = MakeDisc(go.transform, "StickKnob", StickRange * 0.46f,
                                  new Color(1f, 1f, 1f, 0.30f));

            _actButton = MakeDisc(go.transform, "Act", ActRadius * 2f,
                                  new Color(0.15f, 0.85f, 0.65f, 0.30f));
            _actLabel = MakeLabel(_actButton, "ACT", 54);

            _helpButton = MakeDisc(go.transform, "Help", HelpRadius * 2f,
                                   new Color(1f, 1f, 1f, 0.16f));
            _helpLabel = MakeLabel(_helpButton, "?", 72);

            _pauseButton = MakeDisc(go.transform, "Pause", PauseRadius * 2f,
                                    new Color(1f, 1f, 1f, 0.20f));
            _pauseLabel = MakeLabel(_pauseButton, "II", 44);

            SetVisible(false);
        }

        RectTransform MakeDisc(Transform parent, string name, float diameter, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // positioned from bottom-left
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(diameter, diameter);
            var img = go.AddComponent<Image>();
            img.sprite = _disc;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        Text MakeLabel(RectTransform parent, string text, int size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 1f, 1f, 0.85f);
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A filled circle with a soft edge, so the controls are not jagged squares.</summary>
        static Sprite MakeDiscSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01((r - d) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
        }

        // ---------- per frame ----------

        void Update()
        {
            var ts = Touchscreen.current;
            if (ts == null)
            {
                // No touchscreen at all: stay completely out of the way.
                GameInput.TouchPresent = false;
                GameInput.TouchMove = Vector2.zero;
                GameInput.TouchActHeld = false;
                SetVisible(false);
                return;
            }

            bool anyTouch = ReadTouches(ts);
            if (anyTouch) _lastTouchTime = Time.unscaledTime;

            // Stay in touch mode briefly after the last contact, so prompts do not flip
            // back to "press E" between every tap.
            GameInput.TouchPresent = Time.unscaledTime - _lastTouchTime < 4f;

            SetVisible(GameInput.TouchPresent);
        }

        bool ReadTouches(Touchscreen ts)
        {
            bool playing = _dir == null || _dir.AcceptsInput;
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

            Vector2 actCentreScreen = ActCentre() * scale;
            float actRadiusScreen = ActRadius * scale * 1.15f;   // a little forgiveness
            Vector2 helpCentreScreen = HelpCentre() * scale;
            float helpRadiusScreen = HelpRadius * scale * 1.25f;
            Vector2 pauseCentreScreen = PauseCentre() * scale;
            float pauseRadiusScreen = PauseRadius * scale * 1.3f;

            bool any = false;
            bool moveStillDown = false;
            bool actStillDown = false;

            var seen = _seenThisFrame;
            seen.Clear();

            foreach (var t in ts.touches)
            {
                var phase = t.phase.ReadValue();
                bool down = phase == UnityEngine.InputSystem.TouchPhase.Began
                            || phase == UnityEngine.InputSystem.TouchPhase.Moved
                            || phase == UnityEngine.InputSystem.TouchPhase.Stationary;
                bool began = phase == UnityEngine.InputSystem.TouchPhase.Began;
                bool ended = phase == UnityEngine.InputSystem.TouchPhase.Ended
                             || phase == UnityEngine.InputSystem.TouchPhase.Canceled;

                if (!down && !ended) continue;
                any |= down;

                int id = t.touchId.ReadValue();
                Vector2 pos = t.position.ReadValue();

                // --- front end: a tap anywhere starts or continues ---
                if (!playing)
                {
                    // One finger, one menu action, tracked per finger.
                    //
                    // A contact can keep reporting Began for several frames, and menus
                    // advance on a single press, so an unguarded tap walks through two or
                    // three screens at once. Remembering only the LAST finger is not
                    // enough either: with two of them the guard alternates and fires every
                    // frame, which is how one tap on the help button ended up starting a run.
                    seen.Add(id);
                    if (began && !_menuClaimed.Contains(id))
                    {
                        _menuClaimed.Add(id);
                        GameInput.LastTapScreen = pos;
                        if ((pos - helpCentreScreen).sqrMagnitude <= helpRadiusScreen * helpRadiusScreen)
                            GameInput.TouchHelpFrame = Time.frameCount;
                        else
                            GameInput.TouchConfirmFrame = Time.frameCount;
                    }
                    continue;
                }

                // --- pause, which is a tap rather than a hold ---
                if (began && (pos - pauseCentreScreen).sqrMagnitude
                             <= pauseRadiusScreen * pauseRadiusScreen)
                {
                    GameInput.TouchPauseFrame = Time.frameCount;
                    continue;
                }

                // --- ACT button ---
                if (id == _actTouchId)
                {
                    if (ended) { GameInput.TouchActUpFrame = Time.frameCount; _actTouchId = -1; }
                    else actStillDown = true;
                    continue;
                }

                // --- stick ---
                if (id == _moveTouchId)
                {
                    if (ended) { _moveTouchId = -1; }
                    else
                    {
                        moveStillDown = true;
                        Vector2 delta = (pos - _moveOrigin) / scale;
                        float range = Mathf.Max(StickRange, 1f);
                        Vector2 v = delta / range;
                        if (v.sqrMagnitude > 1f) v.Normalize();
                        GameInput.TouchMove = v;
                        _stickKnob.anchoredPosition = _moveOrigin / scale + v * range;
                    }
                    continue;
                }

                if (!began) continue;

                // A new finger. Claim it for whichever control it landed on.
                if ((pos - actCentreScreen).sqrMagnitude <= actRadiusScreen * actRadiusScreen
                    && _actTouchId < 0)
                {
                    _actTouchId = id;
                    actStillDown = true;
                    GameInput.TouchActDownFrame = Time.frameCount;
                }
                else if (_moveTouchId < 0 && pos.x < Screen.width * 0.62f)
                {
                    _moveTouchId = id;
                    _moveOrigin = pos;
                    GameInput.TouchMove = Vector2.zero;
                    _stickRing.anchoredPosition = pos / scale;
                    _stickKnob.anchoredPosition = pos / scale;
                    moveStillDown = true;
                }
            }

            // Forget fingers that have lifted, so the same id can tap again later.
            _menuClaimed.RemoveWhere(id => !seen.Contains(id));

            if (!moveStillDown)
            {
                _moveTouchId = -1;
                GameInput.TouchMove = Vector2.zero;
            }
            if (!actStillDown && _actTouchId >= 0)
            {
                GameInput.TouchActUpFrame = Time.frameCount;
                _actTouchId = -1;
            }
            GameInput.TouchActHeld = actStillDown;

            return any;
        }

        /// <summary>
        /// The button says what pressing it will do. On a phone there is no key cap and
        /// no hover, so a button reading "ACT" is the only control in the game whose
        /// meaning the player has to reconstruct from the room every single time.
        /// </summary>
        void UpdateActLabel()
        {
            if (_player == null) _player = Object.FindAnyObjectByType<PlayerController>();
            string face = "";
            if (_player != null && _player.Nearest != null)
                face = _player.Nearest.ButtonLabel(_player);

            if (string.IsNullOrEmpty(face))
            {
                _actLabel.text = "ACT";
                _actLabel.fontSize = 54;
                _actButton.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.16f);
                return;
            }

            _actLabel.text = face;
            // Long verbs ("MATCH THE", "PUT AWAY") would otherwise spill off the disc.
            _actLabel.fontSize = face.Length > 9 ? 34 : face.Length > 6 ? 42 : 54;
            _actButton.GetComponent<Image>().color = new Color(0.15f, 0.85f, 0.65f, 0.42f);
        }

        PlayerController _player;

        /// <summary>
        /// The usable rectangle in reference-resolution units.
        ///
        /// Screen.safeArea is where the operating system promises nothing of its own
        /// will be drawn: the notch, the rounded corners, the home indicator. Measuring
        /// the controls from the physical edge instead put the pause button under the
        /// status bar on a notched phone and the stick under the gesture bar.
        /// </summary>
        Rect Safe()
        {
            if (Screen.width <= 0 || Screen.height <= 0)
                return new Rect(0f, 0f, RefWidth, RefHeight);

            // Divide by the canvas's own scale factor, NOT by RefWidth/Screen.width.
            // The scaler blends the two axes, so the canvas is 1920 units wide only on
            // a 16:9 screen; on a 19.5:9 phone it is nearer 2120 and anything measured
            // from the constant ends up a hundred units inboard of the edge it was
            // supposed to hug. Scaling x and y separately was wrong for the same reason,
            // and would have skewed a circular button into an ellipse of hit area.
            float sf = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var sa = Screen.safeArea;
            return new Rect(sa.x / sf, sa.y / sf, sa.width / sf, sa.height / sf);
        }

        Vector2 ActCentre()
        {
            var s = Safe();
            return new Vector2(s.xMax - ActRadius - 90f, s.yMin + ActRadius + 90f);
        }

        Vector2 HelpCentre()
        {
            var s = Safe();
            return new Vector2(s.xMax - HelpRadius - 56f, s.yMin + HelpRadius + 56f);
        }

        /// <summary>Top left, well away from the stick and the action button.</summary>
        Vector2 PauseCentre()
        {
            var s = Safe();
            return new Vector2(s.xMin + PauseRadius + 40f, s.yMax - PauseRadius - 40f);
        }

        /// <summary>
        /// Where the resting stick sits: low and inboard, under a left thumb holding the
        /// phone, and clear of the action bar that runs across the middle bottom.
        /// </summary>
        Vector2 StickHomeCentre()
        {
            var s = Safe();
            // High enough to clear the Monster card, which occupies the same corner of
            // the HUD: its top edge is at 140 and the ring's radius is StickRange * 1.05.
            const float HudBottomLeftTop = 140f;
            float y = Mathf.Max(s.yMin + StickRange + 70f,
                                s.yMin + HudBottomLeftTop + StickRange * 1.05f + 16f);
            return new Vector2(s.xMin + StickRange + 70f, y);
        }

        void SetVisible(bool on)
        {
            if (_stickRing == null) return;

            bool playing = _dir == null || _dir.AcceptsInput;
            bool stick = on && playing && _moveTouchId >= 0;

            _stickRing.gameObject.SetActive(stick);
            _stickKnob.gameObject.SetActive(stick);

            // The resting stick hands over to the live one the moment a thumb lands.
            bool home = on && playing && !stick;
            _stickHome.gameObject.SetActive(home);
            _stickHomeKnob.gameObject.SetActive(home);
            if (home)
            {
                var c = StickHomeCentre();
                _stickHome.anchoredPosition = c;
                _stickHomeKnob.anchoredPosition = c;
            }

            _actButton.gameObject.SetActive(on && playing);
            if (on && playing)
            {
                _actButton.anchoredPosition = ActCentre();
                UpdateActLabel();
            }

            _helpButton.gameObject.SetActive(on && !playing);
            if (on && !playing) _helpButton.anchoredPosition = HelpCentre();

            _pauseButton.gameObject.SetActive(on && playing);
            if (on && playing) _pauseButton.anchoredPosition = PauseCentre();
        }
    }
}
