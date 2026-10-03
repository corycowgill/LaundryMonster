using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LaundryMonster
{
    /// <summary>
    /// The whole interface, built in code: cream cards with heavy navy outlines, yellow
    /// for the one action that matters, and a status badge floating over every machine.
    ///
    /// Two rules run through all of it. Numbers come from game state, never from a layout
    /// constant - the reference art shows "01:08" and "4 / 8", and those have to be the
    /// real clock and the real delivery count or the HUD is decoration. And every control
    /// shows the key that works it, which is why the keycaps are built from GameInput
    /// rather than hard-coded to a keyboard.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Assigned by RoomBuilder")]
        public Sprite TitleArt;
        public Font HeadingFont;        // Lilita One
        public Font BodyFont;           // Nunito Sans

        [Tooltip("Credits roll speed, in reference units per second.")]
        public float CreditsSpeed = 95f;

        const float RefW = 1920f, RefH = 1080f;

        PlayerController _player;
        GameDirector _dir;
        Tutorial _tutorial;
        RectTransform _canvasRect;
        Canvas _canvas;

        GameObject _gameplay, _front, _summary, _credits;

        // top left
        Text _dayLabel, _clockText, _secondsText;
        Image _clockFill;

        // top centre
        RectTransform _objective;
        Text _objectiveTitle, _objectiveBody;
        readonly List<Image> _steps = new List<Image>();
        RectTransform _skipChip;
        Text _skipKey;

        // top right
        Text _deliveredText, _scoreText;

        // bottom left
        Text _monsterLabel;
        readonly List<Image> _monsterSegs = new List<Image>();

        // bottom centre
        RectTransform _actionBar;
        Text _actionText;
        RectTransform _actionKey;
        Text _actionKeyText;
        Image _holdFill;
        RectTransform _holdBar;
        readonly List<RectTransform> _carryChips = new List<RectTransform>();

        // machines
        readonly List<LaundryMachine> _machines = new List<LaundryMachine>();
        readonly List<MachineBadge> _badges = new List<MachineBadge>();

        Text _flashText;

        // front end
        Image _titleArtImage, _frontScrim;
        Text _frontTagline, _frontHeading, _frontBody;
        readonly List<RectTransform> _titleButtons = new List<RectTransform>();
        readonly List<Rect> _titleButtonRects = new List<Rect>();
        Text _bestDay, _bestScore, _bestDelivered;
        RectTransform _bestCard;

        // results
        Text _summaryTitle, _summaryStats, _summaryAction;
        readonly List<Image> _stars = new List<Image>();

        // credits
        Text _creditsText, _creditsHint;
        RectTransform _creditsScroll;
        float _creditsY;

        class MachineBadge
        {
            public LaundryMachine Machine;
            public RectTransform Root;
            public Text Label, Status;
            public Image Pill, RingBack, RingFill;
        }

        void Start()
        {
            _player = Object.FindAnyObjectByType<PlayerController>();
            _dir = GameDirector.Instance;
            if (HeadingFont == null) HeadingFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (BodyFont == null) BodyFont = HeadingFont;

            // A half-built HUD would otherwise throw from Update every frame, and the
            // flood buries the one exception that actually explains it.
            try
            {
                Build();
                _built = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[HUD] build failed; the interface is disabled.");
                Debug.LogException(e);
                enabled = false;
            }
        }

        bool _built;

        // ================= construction =================

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = canvasGo.GetComponent<RectTransform>();

            var root = canvasGo.transform;

            _gameplay = Panel(root, "Gameplay");
            BuildDayCard(_gameplay.transform);
            BuildObjective(_gameplay.transform);
            BuildDelivered(_gameplay.transform);
            BuildMonster(_gameplay.transform);
            BuildActionBar(_gameplay.transform);
            BuildFlash(_gameplay.transform);

            BuildFrontEnd(root);
            BuildSummary(root);
            BuildCredits(root);
        }

        GameObject Panel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Heading type: Lilita One, tight, navy.</summary>
        Text Head(Transform parent, string s, int size, TextAnchor align, Color? color = null)
        {
            return Label(parent, s, size, align, HeadingFont, color ?? UiKit.Navy);
        }

        /// <summary>Body type: Nunito Sans, for anything that is read rather than glanced at.</summary>
        Text Body(Transform parent, string s, int size, TextAnchor align, Color? color = null)
        {
            return Label(parent, s, size, align, BodyFont, color ?? UiKit.Navy);
        }

        Text Label(Transform parent, string s, int size, TextAnchor align, Font font, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.text = s;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>A keycap: the little outlined square that names the button to press.</summary>
        RectTransform Keycap(Transform parent, string key, float width = 92f, float height = 62f)
        {
            var cap = UiKit.Card(parent, "Keycap", UiKit.Cream, UiKit.Navy, 4f, UiKit.Card9, false);
            UiKit.Place(cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(width, height));
            Body(cap, key, 30, TextAnchor.MiddleCenter);
            return cap;
        }

        // ---------- top left: the day and the clock ----------

        void BuildDayCard(Transform parent)
        {
            var card = UiKit.Card(parent, "DayCard", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(36f, -30f), new Vector2(392f, 226f));

            _dayLabel = Head(card, "DAY 1", 44, TextAnchor.UpperLeft);
            _dayLabel.rectTransform.offsetMin = new Vector2(34f, 0f);
            _dayLabel.rectTransform.offsetMax = new Vector2(-24f, -18f);

            _clockText = Head(card, "02:00", 82, TextAnchor.UpperLeft);
            _clockText.rectTransform.offsetMin = new Vector2(30f, 0f);
            _clockText.rectTransform.offsetMax = new Vector2(-24f, -58f);

            var track = UiKit.Block(card, "Track", UiKit.Grey, UiKit.Bar);
            UiKit.Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(32f, 54f), new Vector2(326f, 22f));

            _clockFill = UiKit.Block(card, "Fill", UiKit.Blue, UiKit.Bar);
            UiKit.Place(_clockFill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(32f, 54f), new Vector2(326f, 22f));
            _clockFill.type = Image.Type.Filled;
            _clockFill.fillMethod = Image.FillMethod.Horizontal;
            _clockFill.fillOrigin = 0;

            _secondsText = Body(card, "", 26, TextAnchor.LowerLeft);
            _secondsText.rectTransform.offsetMin = new Vector2(34f, 18f);
            _secondsText.rectTransform.offsetMax = new Vector2(-24f, 0f);
        }

        // ---------- top centre: what to do next ----------

        void BuildObjective(Transform parent)
        {
            _objective = UiKit.Card(parent, "Objective", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(_objective, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -26f), new Vector2(720f, 152f));

            _objectiveTitle = Head(_objective, "", 38, TextAnchor.UpperCenter);
            _objectiveTitle.rectTransform.offsetMax = new Vector2(0f, -16f);

            _objectiveBody = Body(_objective, "", 26, TextAnchor.UpperCenter);
            _objectiveBody.rectTransform.offsetMax = new Vector2(0f, -62f);

            // Step dots: progress through the tutorial at a glance.
            var dots = new GameObject("Steps");
            dots.transform.SetParent(_objective, false);
            var drt = dots.AddComponent<RectTransform>();
            UiKit.Place(drt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(-54f, 22f), new Vector2(300f, 24f));

            int total = (int)Tutorial.Step.Done;
            for (int i = 0; i < total; i++)
            {
                var dot = UiKit.Block(dots.transform, "Dot", UiKit.Grey, UiKit.Disc);
                UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(i * 34f, 0f), new Vector2(20f, 20f));
                _steps.Add(dot);
            }

            _skipChip = UiKit.Card(_objective, "Skip", UiKit.CreamDim, UiKit.Navy, 3f, UiKit.Card9, false);
            UiKit.Place(_skipChip, new Vector2(1f, 0f), new Vector2(1f, 0f),
                        new Vector2(-26f, 16f), new Vector2(168f, 42f));
            _skipKey = Body(_skipChip, "Skip  H", 24, TextAnchor.MiddleCenter);
        }

        // ---------- top right: the score ----------

        void BuildDelivered(Transform parent)
        {
            var card = UiKit.Card(parent, "Delivered", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-36f, -30f), new Vector2(360f, 132f));

            _deliveredText = Head(card, "0", 58, TextAnchor.UpperRight);
            _deliveredText.rectTransform.offsetMin = new Vector2(20f, 0f);
            _deliveredText.rectTransform.offsetMax = new Vector2(-26f, -10f);

            var caption = Body(card, "DELIVERED", 24, TextAnchor.UpperLeft);
            caption.rectTransform.offsetMin = new Vector2(28f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-24f, -22f);

            _scoreText = Body(card, "", 26, TextAnchor.LowerRight);
            _scoreText.rectTransform.offsetMin = new Vector2(20f, 18f);
            _scoreText.rectTransform.offsetMax = new Vector2(-26f, -84f);
        }

        // ---------- bottom left: the Monster ----------

        void BuildMonster(Transform parent)
        {
            var card = UiKit.Card(parent, "Monster", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(36f, 36f), new Vector2(470f, 120f));

            _monsterLabel = Head(card, "MONSTER", 36, TextAnchor.UpperLeft);
            _monsterLabel.rectTransform.offsetMin = new Vector2(30f, 0f);
            _monsterLabel.rectTransform.offsetMax = new Vector2(-24f, -14f);

            // Segments, not a smooth bar: you can count how many you have left.
            var segs = new GameObject("Segments");
            segs.transform.SetParent(card, false);
            var srt = segs.AddComponent<RectTransform>();
            UiKit.Place(srt, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(30f, 22f), new Vector2(410f, 34f));

            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                var seg = UiKit.Block(segs.transform, "Seg", UiKit.Grey, UiKit.Card9);
                UiKit.Place(seg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(i * 58f, 0f), new Vector2(50f, 34f));
                _monsterSegs.Add(seg);
            }
        }

        // ---------- bottom centre: the contextual action ----------

        void BuildActionBar(Transform parent)
        {
            _actionBar = UiKit.Card(parent, "ActionBar", UiKit.NavyBar, UiKit.NavyDeep, 7f);
            UiKit.Place(_actionBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 44f), new Vector2(820f, 118f));

            _actionKey = Keycap(_actionBar, "E", 86f, 64f);
            UiKit.Place(_actionKey, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(28f, 0f), new Vector2(86f, 64f));
            _actionKeyText = _actionKey.GetComponentInChildren<Text>();

            _actionText = Body(_actionBar, "", 32, TextAnchor.MiddleLeft, UiKit.Cream);
            _actionText.rectTransform.offsetMin = new Vector2(142f, 0f);
            _actionText.rectTransform.offsetMax = new Vector2(-300f, 0f);

            // Hold progress runs along the bottom edge of the bar.
            _holdBar = UiKit.Block(_actionBar, "HoldTrack", new Color(1f, 1f, 1f, 0.18f), UiKit.Bar).rectTransform;
            UiKit.Place(_holdBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 14f), new Vector2(760f, 12f));

            _holdFill = UiKit.Block(_actionBar, "HoldFill", UiKit.Yellow, UiKit.Bar);
            UiKit.Place(_holdFill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 14f), new Vector2(760f, 12f));
            _holdFill.type = Image.Type.Filled;
            _holdFill.fillMethod = Image.FillMethod.Horizontal;
            _holdFill.fillOrigin = 0;
            _holdBar.gameObject.SetActive(false);
            _holdFill.gameObject.SetActive(false);

            // One chip per carry slot, filled left to right.
            for (int i = 0; i < Tuning.CarryCapacity; i++)
            {
                var chip = UiKit.Card(_actionBar, "Carry", UiKit.CreamDim, UiKit.Navy, 4f,
                                      UiKit.Card9, false);
                UiKit.Place(chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-28f - i * 96f, 2f), new Vector2(86f, 72f));
                _carryChips.Add(chip);
                chip.gameObject.SetActive(false);
            }
        }

        void BuildFlash(Transform parent)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            UiKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        new Vector2(0f, 210f), new Vector2(1200f, 80f));
            _flashText = Head(rt, "", 46, TextAnchor.MiddleCenter, UiKit.Cream);
            var outline = _flashText.gameObject.AddComponent<Outline>();
            outline.effectColor = UiKit.NavyDeep;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        // ---------- machine badges ----------

        void EnsureBadges()
        {
            if (_badges.Count > 0) return;

            _machines.Clear();
            _machines.AddRange(Object.FindObjectsByType<LaundryMachine>(FindObjectsSortMode.None));
            _machines.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            foreach (var m in _machines)
            {
                bool washer = m.MachineMode == LaundryMachine.Mode.Washer;
                var accent = washer ? UiKit.Blue : UiKit.Orange;

                var card = UiKit.Card(_gameplay.transform, "Badge", UiKit.Cream, accent, 6f);
                UiKit.Place(card, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(186f, 78f));

                var label = Body(card, washer ? "WASH" : "DRY", 24, TextAnchor.UpperLeft, UiKit.Navy);
                label.rectTransform.offsetMin = new Vector2(18f, 0f);
                label.rectTransform.offsetMax = new Vector2(-62f, -8f);

                var pill = UiKit.Block(card, "Pill", UiKit.Grey, UiKit.Card9);
                UiKit.Place(pill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(16f, 12f), new Vector2(104f, 32f));

                var status = Head(card, "", 32, TextAnchor.LowerLeft, UiKit.Navy);
                status.rectTransform.offsetMin = new Vector2(20f, 8f);
                status.rectTransform.offsetMax = new Vector2(-62f, -32f);

                var ringBack = UiKit.Block(card, "RingBack", UiKit.Grey, UiKit.Ring);
                UiKit.Place(ringBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-14f, 0f), new Vector2(44f, 44f));

                var ringFill = UiKit.Block(card, "RingFill", accent, UiKit.Ring);
                UiKit.Place(ringFill.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-14f, 0f), new Vector2(44f, 44f));
                ringFill.type = Image.Type.Filled;
                ringFill.fillMethod = Image.FillMethod.Radial360;
                ringFill.fillOrigin = (int)Image.Origin360.Top;
                ringFill.fillClockwise = true;

                _badges.Add(new MachineBadge
                {
                    Machine = m, Root = card, Label = label, Status = status,
                    Pill = pill, RingBack = ringBack, RingFill = ringFill,
                });
            }
        }

        // ================= front end =================

        void BuildFrontEnd(Transform root)
        {
            _front = Panel(root, "FrontEnd");

            if (TitleArt != null)
            {
                var art = new GameObject("Art");
                art.transform.SetParent(_front.transform, false);
                var art_rt = art.AddComponent<RectTransform>();
                UiKit.Place(art_rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(RefW, RefH));
                _titleArtImage = art.AddComponent<Image>();
                _titleArtImage.sprite = TitleArt;
                _titleArtImage.raycastTarget = false;

                var fit = art.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = TitleArt.rect.width / Mathf.Max(1f, TitleArt.rect.height);
            }

            _frontScrim = UiKit.Block(_front.transform, "Scrim",
                                      new Color(UiKit.NavyDeep.r, UiKit.NavyDeep.g,
                                                UiKit.NavyDeep.b, 0.35f), UiKit.White);
            _frontScrim.rectTransform.anchorMin = Vector2.zero;
            _frontScrim.rectTransform.anchorMax = Vector2.one;
            _frontScrim.rectTransform.offsetMin = Vector2.zero;
            _frontScrim.rectTransform.offsetMax = Vector2.zero;
            _frontScrim.type = Image.Type.Simple;

            // --- title screen controls, stacked up from the bottom left ---
            _bestCard = UiKit.Card(_front.transform, "Best", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(_bestCard, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(68f, 56f), new Vector2(820f, 168f));

            var crown = Head(_bestCard, "PERSONAL BEST", 26, TextAnchor.UpperLeft);
            crown.rectTransform.offsetMin = new Vector2(34f, 0f);
            crown.rectTransform.offsetMax = new Vector2(-24f, -16f);

            _bestDay = BestStat(_bestCard, 0, "Best Day");
            _bestScore = BestStat(_bestCard, 1, "Points");
            _bestDelivered = BestStat(_bestCard, 2, "Delivered");

            TitleButton(2, "HOW TO PLAY", GameInput.HelpGlyph, UiKit.Cream, UiKit.Navy,
                        new Vector2(68f, 252f), new Vector2(395f, 78f), 30);
            TitleButton(3, "CREDITS", "C", UiKit.Cream, UiKit.Navy,
                        new Vector2(493f, 252f), new Vector2(395f, 78f), 30);
            TitleButton(1, "LEARN TO PLAY", "T", UiKit.Blue, UiKit.NavyDeep,
                        new Vector2(68f, 348f), new Vector2(820f, 92f), 42, UiKit.Cream);
            TitleButton(0, "START LAUNDRY", GameInput.ConfirmGlyph, UiKit.Yellow, UiKit.Navy,
                        new Vector2(68f, 458f), new Vector2(820f, 104f), 48);

            _frontTagline = Head(_front.transform, "", 36, TextAnchor.LowerLeft, UiKit.Cream);
            UiKit.Place(_frontTagline.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(74f, 582f), new Vector2(900f, 50f));
            var tagOutline = _frontTagline.gameObject.AddComponent<Outline>();
            tagOutline.effectColor = UiKit.NavyDeep;
            tagOutline.effectDistance = new Vector2(2.5f, -2.5f);

            // --- the help page, which replaces all of the above ---
            _frontHeading = Head(_front.transform, "", 76, TextAnchor.UpperCenter, UiKit.Cream);
            UiKit.Place(_frontHeading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -56f), new Vector2(1600f, 110f));

            _frontBody = Body(_front.transform, "", 26, TextAnchor.UpperCenter, UiKit.Cream);
            UiKit.Place(_frontBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -178f), new Vector2(1560f, 820f));
        }

        Text BestStat(Transform card, int column, string caption)
        {
            float x = 36f + column * 268f;
            var value = Head(card, "0", 46, TextAnchor.UpperLeft);
            value.rectTransform.offsetMin = new Vector2(x, 0f);
            value.rectTransform.offsetMax = new Vector2(-24f, -62f);

            var cap = Body(card, caption, 22, TextAnchor.UpperLeft,
                           new Color(UiKit.Navy.r, UiKit.Navy.g, UiKit.Navy.b, 0.65f));
            cap.rectTransform.offsetMin = new Vector2(x, 0f);
            cap.rectTransform.offsetMax = new Vector2(-24f, -116f);
            return value;
        }

        void TitleButton(int index, string caption, string key, Color fill, Color outline,
                         Vector2 pos, Vector2 size, int fontSize, Color? textColor = null)
        {
            var card = UiKit.Card(_front.transform, "Btn_" + caption, fill, outline, 7f);
            UiKit.Place(card, new Vector2(0f, 0f), new Vector2(0f, 0f), pos, size);

            var label = Head(card, caption, fontSize, TextAnchor.MiddleCenter,
                             textColor ?? UiKit.Navy);
            label.rectTransform.offsetMin = new Vector2(24f, 0f);
            label.rectTransform.offsetMax = new Vector2(-120f, 0f);

            if (!string.IsNullOrEmpty(key))
            {
                var cap = Keycap(card, key, Mathf.Max(70f, 34f + key.Length * 20f), size.y - 30f);
                UiKit.Place(cap, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-18f, 0f), cap.sizeDelta);
            }

            while (_titleButtons.Count <= index) { _titleButtons.Add(null); _titleButtonRects.Add(default); }
            _titleButtons[index] = card;
            _titleButtonRects[index] = new Rect(pos.x, pos.y, size.x, size.y);
        }

        /// <summary>
        /// Which title button a screen-space point lands on, or -1.
        ///
        /// Touch needs this because the game has no EventSystem - the on-screen controls
        /// read raw touches - so a tap cannot find a Button by itself.
        /// 0 start, 1 tutorial, 2 help, 3 credits.
        /// </summary>
        public int TitleButtonAt(Vector2 screenPoint)
        {
            if (_canvas == null) return -1;
            float s = _canvas.scaleFactor;
            if (s <= 0f) return -1;

            var p = screenPoint / s;      // into reference units from the bottom left
            for (int i = 0; i < _titleButtonRects.Count; i++)
            {
                var r = _titleButtonRects[i];
                if (_titleButtons[i] == null) continue;
                if (p.x >= r.x && p.x <= r.x + r.width && p.y >= r.y && p.y <= r.y + r.height)
                    return i;
            }
            return -1;
        }

        // ================= results =================

        void BuildSummary(Transform root)
        {
            _summary = Panel(root, "Summary");

            var dim = UiKit.Block(_summary.transform, "Dim",
                                  new Color(UiKit.NavyDeep.r, UiKit.NavyDeep.g,
                                            UiKit.NavyDeep.b, 0.72f), UiKit.White);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero;
            dim.rectTransform.offsetMax = Vector2.zero;
            dim.type = Image.Type.Simple;

            var card = UiKit.Card(_summary.transform, "Card", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(840f, 640f));

            _summaryTitle = Head(card, "DAY 1 COMPLETE", 56, TextAnchor.UpperCenter);
            _summaryTitle.rectTransform.offsetMax = new Vector2(0f, -36f);

            var starRow = new GameObject("Stars");
            starRow.transform.SetParent(card, false);
            var srt = starRow.AddComponent<RectTransform>();
            UiKit.Place(srt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -130f), new Vector2(420f, 110f));

            for (int i = 0; i < 3; i++)
            {
                var holder = new GameObject("Star");
                holder.transform.SetParent(starRow.transform, false);
                var hrt = holder.AddComponent<RectTransform>();
                UiKit.Place(hrt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2((i - 1) * 130f, 0f), new Vector2(110f, 110f));
                var star = UiKit.Block(hrt, "Star", UiKit.Grey, UiKit.Star);
                star.type = Image.Type.Simple;
                star.rectTransform.anchorMin = Vector2.zero;
                star.rectTransform.anchorMax = Vector2.one;
                star.rectTransform.offsetMin = Vector2.zero;
                star.rectTransform.offsetMax = Vector2.zero;
                _stars.Add(star);
            }

            _summaryStats = Body(card, "", 30, TextAnchor.UpperCenter);
            _summaryStats.rectTransform.offsetMin = new Vector2(60f, 110f);
            _summaryStats.rectTransform.offsetMax = new Vector2(-60f, -250f);

            var action = UiKit.Card(card, "Next", UiKit.Yellow, UiKit.Navy, 5f);
            UiKit.Place(action, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 34f), new Vector2(560f, 86f));
            _summaryAction = Head(action, "", 36, TextAnchor.MiddleCenter);

            _summary.SetActive(false);
        }

        // ================= credits =================

        void BuildCredits(Transform root)
        {
            _credits = Panel(root, "Credits");

            var bg = UiKit.Block(_credits.transform, "Bg", UiKit.NavyDeep, UiKit.White);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;
            bg.type = Image.Type.Simple;

            var scroller = new GameObject("Scroll");
            scroller.transform.SetParent(_credits.transform, false);
            _creditsScroll = scroller.AddComponent<RectTransform>();
            // Top edge pinned to the bottom of the screen: at y=0 the roll is entirely
            // below the view, and raising y walks it up through frame.
            UiKit.Place(_creditsScroll, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                        Vector2.zero, new Vector2(1200f, 6000f));

            _creditsText = Body(scroller.transform, Credits.Build(), 28, TextAnchor.UpperCenter,
                                UiKit.Cream);
            _creditsText.lineSpacing = 1.3f;

            var band = UiKit.Block(_credits.transform, "Band", UiKit.NavyDeep, UiKit.White);
            UiKit.Place(band.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        Vector2.zero, new Vector2(4000f, 92f));
            band.type = Image.Type.Simple;

            var hintHolder = new GameObject("Hint");
            hintHolder.transform.SetParent(_credits.transform, false);
            var hrt2 = hintHolder.AddComponent<RectTransform>();
            UiKit.Place(hrt2, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 24f), new Vector2(900f, 44f));
            _creditsHint = Body(hrt2, "", 26, TextAnchor.MiddleCenter, UiKit.Yellow);

            _credits.SetActive(false);
        }

        // ================= per frame =================

        void Update()
        {
            if (!_built) return;
            if (_dir == null) _dir = GameDirector.Instance;
            if (_dir == null || _gameplay == null) return;
            if (_tutorial == null) _tutorial = Object.FindAnyObjectByType<Tutorial>();

            var ph = _dir.CurrentPhase;
            bool playing = ph == Phase.Playing || ph == Phase.DaySummary;
            bool front = ph == Phase.Intro || ph == Phase.Title || ph == Phase.Help;

            SetActive(_gameplay, playing);
            SetActive(_front, front);
            SetActive(_credits, ph == Phase.Credits);
            SetActive(_summary, ph == Phase.DaySummary || ph == Phase.RunOver);

            if (ph == Phase.Credits) UpdateCredits();
            if (front) UpdateFrontEnd(ph);
            if (!playing) return;

            UpdateClock();
            UpdateDelivered();
            UpdateMonster();
            UpdateObjective();
            UpdateBadges();
            UpdateActionBar();
            UpdateFlash();
            if (ph == Phase.DaySummary || ph == Phase.RunOver) UpdateSummary();
        }

        static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        void UpdateClock()
        {
            _dayLabel.text = "DAY " + _dir.Day;

            float left = _dir.TimeLeft;
            int mins = Mathf.FloorToInt(left / 60f);
            int secs = Mathf.CeilToInt(left - mins * 60f);
            if (secs == 60) { secs = 0; mins++; }
            _clockText.text = $"{mins:00}:{secs:00}";

            float frac = _dir.DayLength <= 0f ? 0f : left / _dir.DayLength;
            _clockFill.fillAmount = frac;
            var c = UiKit.Remaining(frac);
            _clockFill.color = c;

            // Under twenty percent the clock itself joins in, because a bar that is nearly
            // empty is easy to miss when the room is on fire.
            _clockText.color = frac <= 0.2f
                ? Color.Lerp(UiKit.Navy, UiKit.Red, 0.4f + 0.6f * UiKit.Pulse(9f))
                : UiKit.Navy;

            _secondsText.text = Mathf.CeilToInt(left) + "s left";
        }

        void UpdateDelivered()
        {
            _deliveredText.text = _dir.Delivered.ToString();
            _scoreText.text = _dir.Score.ToString("0.#") + " / " + _dir.Target.ToString("0.#");
            bool met = _dir.Score >= _dir.Target;
            _scoreText.color = met ? UiKit.Green
                : new Color(UiKit.Navy.r, UiKit.Navy.g, UiKit.Navy.b, 0.75f);
            _deliveredText.color = met ? UiKit.Green : UiKit.Navy;
        }

        void UpdateMonster()
        {
            float frac = Mathf.Clamp01(_dir.Monster / Tuning.MonsterMax);
            int lit = Mathf.CeilToInt(frac * _monsterSegs.Count);

            for (int i = 0; i < _monsterSegs.Count; i++)
            {
                bool on = i < lit;
                var c = !on ? UiKit.Grey
                      : frac > 0.75f ? Color.Lerp(UiKit.Red, UiKit.Yellow, UiKit.Pulse(7f) * 0.5f)
                      : UiKit.Red;
                _monsterSegs[i].color = c;
            }

            _monsterLabel.text = frac > 0.75f ? "MONSTER - it has eyes now" : "MONSTER";
            _monsterLabel.fontSize = frac > 0.75f ? 26 : 36;
        }

        void UpdateObjective()
        {
            bool teaching = _tutorial != null && _tutorial.Running;
            bool dayOneHint = !teaching && _dir.Day == 1 && _dir.FlashTimer <= 0f;

            SetActive(_objective.gameObject, teaching || dayOneHint);
            if (!teaching && !dayOneHint) return;

            SetActive(_skipChip.gameObject, teaching);
            foreach (var d in _steps) SetActive(d.gameObject, teaching);

            if (teaching)
            {
                _objectiveTitle.text = _tutorial.StepLabel();
                _objectiveBody.text = _tutorial.Instruction();
                _skipKey.text = "Skip  " + GameInput.HelpGlyph;

                int done = (int)_tutorial.Current;
                for (int i = 0; i < _steps.Count; i++)
                    _steps[i].color = i < done ? UiKit.Blue
                                    : i == done ? UiKit.Yellow : UiKit.Grey;

                // Flash the card as each step lands.
                float c = _tutorial.CelebrateTimer;
                UiKit.SetCardColors(_objective,
                                    c > 0f ? Color.Lerp(UiKit.Cream, UiKit.Green, Mathf.Clamp01(c)) : UiKit.Cream,
                                    UiKit.Navy);
            }
            else
            {
                _objectiveTitle.text = "GET STARTED";
                _objectiveBody.text = _player != null && _player.Carried.Count == 0
                    ? GameInput.MoveGlyph + " to move. Pull laundry off the Monster."
                    : "Wash it, dry it, fold it, put it away.";
                UiKit.SetCardColors(_objective, UiKit.Cream, UiKit.Navy);
            }
        }

        void UpdateBadges()
        {
            EnsureBadges();
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var b in _badges)
            {
                if (b.Machine == null) continue;

                var world = b.Machine.transform.position + new Vector3(0f, 1.42f, 0f);
                var sp = cam.WorldToScreenPoint(world);
                bool visible = sp.z > 0f;
                SetActive(b.Root.gameObject, visible);
                if (!visible) continue;
                b.Root.position = sp;

                var m = b.Machine;
                bool washer = m.MachineMode == LaundryMachine.Mode.Washer;
                var accent = washer ? UiKit.Blue : UiKit.Orange;

                if (m.Offline)
                {
                    b.Status.text = "FIRE";
                    b.Pill.color = UiKit.Red;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 0f;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, UiKit.Red);
                }
                else if (m.Running)
                {
                    b.Status.text = Mathf.CeilToInt(m.SecondsLeft) + "s";
                    SetActive(b.Pill.gameObject, false);
                    b.RingFill.fillAmount = m.CycleFraction;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, accent);
                }
                else if (m.HasFinishedLoad)
                {
                    b.Status.text = "READY";
                    b.Pill.color = UiKit.Green;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 1f;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, UiKit.Green);
                }
                else if (m.Contents.Count > 0)
                {
                    b.Status.text = "LOADED";
                    b.Pill.color = UiKit.Yellow;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 0f;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, accent);
                }
                else
                {
                    b.Status.text = "EMPTY";
                    b.Pill.color = UiKit.Grey;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 0f;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, accent);
                }

                // Lint is a dryer's own problem, and it is worth shouting about.
                if (!washer && m.Lint >= Tuning.LintFireThreshold && !m.Offline)
                    UiKit.SetCardColors(b.Root, UiKit.Cream,
                                        Color.Lerp(UiKit.Orange, UiKit.Red, UiKit.Pulse(8f)));
            }
        }

        void UpdateActionBar()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<PlayerController>();
                if (_player == null) return;
            }

            var near = _player.Nearest;
            string action = "";
            bool hold = false;

            if (near != null)
            {
                string tap = near.ActionPrompt(_player);
                string held = near.HoldPrompt(_player);
                if (!string.IsNullOrEmpty(held) && near.HoldSeconds(_player) > 0f)
                {
                    action = "Hold to " + held;
                    hold = true;
                }
                else if (!string.IsNullOrEmpty(tap))
                {
                    action = char.ToUpper(tap[0]) + tap.Substring(1);
                }

                if (!string.IsNullOrEmpty(action))
                {
                    string status = near.Status;
                    if (!string.IsNullOrEmpty(status)) action += "   (" + status + ")";
                }
            }

            bool show = !string.IsNullOrEmpty(action) || _player.Carried.Count > 0;
            SetActive(_actionBar.gameObject, show);
            if (!show) return;

            _actionText.text = string.IsNullOrEmpty(action)
                ? (_player.CarryPenalty > 0
                    ? "Carrying  (" + _player.CarryCapacity + " slot, AirPods lost)"
                    : "Carrying")
                : action;
            _actionKeyText.text = GameInput.InteractGlyph;
            SetActive(_actionKey.gameObject, !string.IsNullOrEmpty(action));

            bool holding = _player.HoldProgress > 0f;
            SetActive(_holdBar.gameObject, hold || holding);
            SetActive(_holdFill.gameObject, hold || holding);
            _holdFill.fillAmount = _player.HoldProgress;

            // One chip per carried garment, coloured by what it currently is.
            for (int i = 0; i < _carryChips.Count; i++)
            {
                bool used = i < _player.Carried.Count;
                SetActive(_carryChips[i].gameObject, used);
                if (!used) continue;

                var g = _player.Carried[i];
                UiKit.SetCardColors(_carryChips[i],
                                    g != null ? g.DisplayColor : UiKit.CreamDim, UiKit.Navy);
            }
        }

        void UpdateFlash()
        {
            if (_dir.FlashTimer <= 0f) { _flashText.text = ""; return; }
            _flashText.text = _dir.FlashMessage;
            var c = _dir.FlashColor;
            c.a = Mathf.Clamp01(_dir.FlashTimer / 0.8f);
            _flashText.color = c;
        }

        void UpdateSummary()
        {
            bool over = _dir.CurrentPhase == Phase.RunOver;
            _summaryTitle.text = over ? "THE LAUNDRY WON" : "DAY " + _dir.Day + " COMPLETE";

            for (int i = 0; i < _stars.Count; i++)
                _stars[i].color = i < _dir.Stars ? UiKit.Yellow : UiKit.Grey;

            if (over)
            {
                _summaryStats.text =
                    "score          " + _dir.RunScore.ToString("0.#") + "\n" +
                    "days survived  " + _dir.Day + "\n" +
                    "put away       " + _dir.RunDelivered + "\n" +
                    "stars earned   " + _dir.RunStars +
                    (_dir.NewRecord ? "\n\nA NEW PERSONAL BEST" : "");
                _summaryAction.text = GameInput.ConfirmGlyph + "   back to title";
            }
            else
            {
                _summaryStats.text =
                    "put away   " + _dir.Delivered + "\n" +
                    "score      " + _dir.Score.ToString("0.#") + " / " + _dir.Target.ToString("0.#") + "\n" +
                    "wrinkled   " + _dir.WrinkledCount + "\n" +
                    "mildewed   " + _dir.MildewedCount + "\n" +
                    "socks lost " + _dir.VoidedSocks;
                _summaryAction.text = GameInput.ConfirmGlyph + "   start day " + (_dir.Day + 1);
            }
        }

        void UpdateFrontEnd(Phase ph)
        {
            bool help = ph == Phase.Help;

            SetActive(_bestCard.gameObject, !help);
            foreach (var b in _titleButtons) if (b != null) SetActive(b.gameObject, !help);
            SetActive(_frontTagline.gameObject, !help);
            SetActive(_frontHeading.gameObject, help);
            SetActive(_frontBody.gameObject, help);

            if (_titleArtImage != null)
            {
                SetActive(_titleArtImage.gameObject, !help);

                // The art is 3:2 and the window is wider, so filling it overflows
                // vertically. Align the tops so the painted wordmark survives the crop.
                var art = _titleArtImage.rectTransform;
                float over = art.rect.height - _canvasRect.rect.height;
                art.anchoredPosition = new Vector2(0f, over > 0f ? -over * 0.5f : 0f);
            }
            _frontScrim.color = new Color(UiKit.NavyDeep.r, UiKit.NavyDeep.g, UiKit.NavyDeep.b,
                                          help ? 0.95f : 0.3f);

            if (help)
            {
                _frontHeading.text = "HOW TO PLAY";
                _frontBody.text = HelpText();
                return;
            }

            _frontTagline.text = ph == Phase.Intro
                ? "a Hallucinated Games production"
                : "The laundry never ends.";

            _bestDay.text = HighScores.BestDay.ToString();
            _bestScore.text = HighScores.BestScore.ToString("0.#");
            _bestDelivered.text = HighScores.BestDelivered.ToString();
        }

        string HelpText()
        {
            string act = GameInput.InteractGlyph;
            string hold = GameInput.HoldGlyph;
            return
                GameInput.MoveGlyph + " to move.   " + act + " to interact.   Some things need it HELD.\n\n" +
                "THE LOOP\n" +
                "  Pull dirty clothes off the MONSTER  ->  WASHER (blue)  ->  DRYER (orange)\n" +
                "  ->  " + hold.ToUpper() + " at the FOLD TABLE  ->  put it away in the CLOSET. Only the closet scores.\n\n" +
                "THE WRINKLE CLOCK\n" +
                "  The moment a dryer stops, its load starts wrinkling. Fold it in " + Tuning.WrinkleGrace + "s.\n" +
                "  Wet laundry mildews in " + Tuning.MildewGrace + "s. You will hear it ticking.\n\n" +
                "POCKETS\n" +
                "  Pants always have them. " + hold.ToUpper() + " at a washer to check first, for " + Tuning.PocketCheckHold + "s.\n" +
                "  Skip it and most loads are fine. The rest cost a wallet, a crayon, or your AirPods.\n\n" +
                "LINT\n" +
                "  Every dry cycle clogs the trap. " + hold.ToUpper() + " at a dryer to empty it.\n" +
                "  At 8 it can catch fire. At 10 the run ends.\n\n" +
                "SOCKS\n" +
                "  A lone sock cannot be folded. Match pairs at the SOCK DRAWER.\n" +
                "  Every wash may send one sock to the Void.\n\n" +
                "THE CHAIR wrinkles laundry twice as fast, and feeds the Monster when it overflows.\n\n" +
                "[ " + GameInput.ConfirmGlyph + " ] back";
        }

        void UpdateCredits()
        {
            _creditsY += CreditsSpeed * Time.unscaledDeltaTime;
            _creditsScroll.anchoredPosition = new Vector2(0f, _creditsY);
            _creditsHint.text = "[ " + GameInput.ConfirmGlyph + " ] back";

            if (_creditsY > _creditsText.preferredHeight + 220f)
            {
                _creditsY = 0f;
                _dir.CurrentPhase = Phase.Title;
            }
        }

        /// <summary>Reset the roll so it starts from the top next time it is opened.</summary>
        public void RewindCredits() => _creditsY = 0f;
    }
}
