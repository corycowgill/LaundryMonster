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
        RectTransform _modChip;
        Text _modText;
        readonly List<Image> _steps = new List<Image>();
        RectTransform _skipChip;
        Text _skipKey;

        // top right
        Text _deliveredText, _scoreText, _streakText;
        float _deliveredPop;

        // the floating "+1" that rises out of the closet
        RectTransform _pop;
        Text _popText;
        float _popAge = 99f;
        Vector3 _popWorld;

        // bottom left
        Text _monsterLabel, _backlogText, _monsterCaption;
        readonly List<Image> _monsterSegs = new List<Image>();

        // the snatch warning, pinned over The Chair
        RectTransform _snatchCard;
        Text _snatchText;
        Image _snatchRing;
        MonsterAttack _attack;
        Chair _chair;

        // bottom centre
        RectTransform _actionBar;
        Text _actionText;
        RectTransform _actionKey;
        Text _actionKeyText;
        Image _holdFill;
        RectTransform _holdBar;
        readonly List<RectTransform> _carryChips = new List<RectTransform>();
        readonly List<Image> _carryIcons = new List<Image>();
        readonly List<Text> _carryDest = new List<Text>();
        readonly List<Image> _carryDestBar = new List<Image>();

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

        // upgrades
        GameObject _picker;
        readonly List<RectTransform> _pickCards = new List<RectTransform>();
        readonly List<Text> _pickName = new List<Text>();
        readonly List<Text> _pickBody = new List<Text>();
        readonly List<Text> _pickKey = new List<Text>();

        Text _ownedStrip, _sprayText;
        RectTransform _sprayChip;

        // briefing
        GameObject _briefing;
        Text _briefTitle, _briefBody, _briefAction, _briefRibbon;

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
            BuildSnatchWarning(_gameplay.transform);
            BuildDeliveryPop(_gameplay.transform);
            BuildFlash(_gameplay.transform);

            BuildFrontEnd(root);
            BuildSummary(root);
            BuildBriefing(root);
            BuildPicker(root);
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
            // The day used to be 392x226 and printed the time twice, once as a clock
            // and once as "215s left" underneath it. One clock, in a card two thirds
            // the height, leaves the room visible behind it.
            var card = UiKit.Card(parent, "DayCard", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(28f, -24f), new Vector2(300f, 150f));

            _dayLabel = Head(card, "DAY 1", 28, TextAnchor.UpperLeft);
            _dayLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            _dayLabel.rectTransform.offsetMax = new Vector2(-20f, -12f);

            _clockText = Head(card, "02:00", 58, TextAnchor.UpperLeft);
            _clockText.rectTransform.offsetMin = new Vector2(22f, 0f);
            _clockText.rectTransform.offsetMax = new Vector2(-20f, -44f);

            var track = UiKit.Block(card, "Track", UiKit.Grey, UiKit.Bar);
            UiKit.Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(24f, 18f), new Vector2(252f, 14f));

            _clockFill = UiKit.Block(card, "Fill", UiKit.Blue, UiKit.Bar);
            UiKit.Place(_clockFill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(24f, 18f), new Vector2(252f, 14f));
            _clockFill.type = Image.Type.Filled;
            _clockFill.fillMethod = Image.FillMethod.Horizontal;
            _clockFill.fillOrigin = 0;

            // Kept for the one line it is still worth spending a row on, and parked
            // outside the card so the card does not have to be tall enough for it.
            _secondsText = Body(parent, "", 22, TextAnchor.UpperLeft, UiKit.Orange);
            UiKit.Place(_secondsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(32f, -180f), new Vector2(380f, 28f));
            var lo = _secondsText.gameObject.AddComponent<Outline>();
            lo.effectColor = UiKit.NavyDeep;
            lo.effectDistance = new Vector2(2f, -2f);
        }

        // ---------- top centre: what to do next ----------

        void BuildObjective(Transform parent)
        {
            // 940x104, not 720x152. The top of the screen is the scarcest space on a
            // landscape phone and the least scarce sideways, so the dots and the Skip
            // chip move out of the text's way rather than underneath it. A third of the
            // height back is the difference between this card clearing the machines and
            // sitting on them.
            _objective = UiKit.Card(parent, "Objective", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(_objective, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -14f), new Vector2(1000f, 116f));

            // What is different about today, directly under the objective and in a
            // colour nothing else in the HUD uses.
            //
            // The briefing card explains the modifier once, before the day starts,
            // and is then gone. Five minutes later, mid-panic, "why is this dryer so
            // slow" is a question the HUD should be able to answer without the player
            // having to remember a card they read before the clock started.
            _modChip = UiKit.Card(parent, "Modifier", UiKit.Yellow, UiKit.Navy, 5f);
            UiKit.Place(_modChip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -138f), new Vector2(520f, 44f));
            _modText = Head(_modChip, "", 24, TextAnchor.MiddleCenter);
            _modChip.gameObject.SetActive(false);

            // Left-aligned, because the text block no longer owns the whole card.
            _objectiveTitle = Head(_objective, "", 28, TextAnchor.UpperLeft);
            _objectiveTitle.rectTransform.offsetMin = new Vector2(28f, 0f);
            _objectiveTitle.rectTransform.offsetMax = new Vector2(-392f, -8f);

            // Two lines of 22 need 50 of the 72 this rect has. The first attempt gave it
            // 50 for 54 and cut the last word off the tutorial's very first instruction.
            _objectiveBody = Body(_objective, "", 22, TextAnchor.UpperLeft);
            _objectiveBody.rectTransform.offsetMin = new Vector2(28f, 8f);
            _objectiveBody.rectTransform.offsetMax = new Vector2(-392f, -44f);
            _objectiveBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _objectiveBody.verticalOverflow = VerticalWrapMode.Overflow;

            // Step dots: progress through the tutorial at a glance. Now a column's worth
            // of width on the right rather than a row's worth of height underneath.
            var dots = new GameObject("Steps");
            dots.transform.SetParent(_objective, false);
            var drt = dots.AddComponent<RectTransform>();
            UiKit.Place(drt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-196f, 0f), new Vector2(230f, 20f));

            int total = (int)Tutorial.Step.Done;
            for (int i = 0; i < total; i++)
            {
                var dot = UiKit.Block(dots.transform, "Dot", UiKit.Grey, UiKit.Disc);
                UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(i * 28f, 0f), new Vector2(18f, 18f));
                _steps.Add(dot);
            }

            _skipChip = UiKit.Card(_objective, "Skip", UiKit.CreamDim, UiKit.Navy, 3f, UiKit.Card9, false);
            UiKit.Place(_skipChip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-22f, 0f), new Vector2(146f, 44f));
            _skipKey = Body(_skipChip, "Skip  H", 22, TextAnchor.MiddleCenter);
        }

        // ---------- top right: the score ----------

        void BuildDelivered(Transform parent)
        {
            var card = UiKit.Card(parent, "Delivered", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-28f, -24f), new Vector2(300f, 118f));

            _deliveredText = Head(card, "0", 46, TextAnchor.UpperRight);
            _deliveredText.rectTransform.offsetMin = new Vector2(18f, 0f);
            _deliveredText.rectTransform.offsetMax = new Vector2(-22f, -8f);

            // "PUT AWAY", because that is now the words on the button at the closet.
            // It used to say DELIVERED while the prompt said "put away", which is two
            // names for the only action that scores.
            var caption = Body(card, "PUT AWAY", 22, TextAnchor.UpperLeft);
            caption.rectTransform.offsetMin = new Vector2(24f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-20f, -18f);

            // "0 / 6.4" told you nothing about what was being counted.
            _scoreText = Body(card, "", 22, TextAnchor.LowerRight);
            _scoreText.rectTransform.offsetMin = new Vector2(18f, 14f);
            _scoreText.rectTransform.offsetMax = new Vector2(-22f, -70f);

            // The streak lives under the card: it is a reward, not a requirement, and it
            // should not compete with the number that decides your stars.
            _streakText = Body(parent, "", 24, TextAnchor.UpperRight, UiKit.Yellow);
            UiKit.Place(_streakText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-32f, -150f), new Vector2(420f, 30f));
            var so = _streakText.gameObject.AddComponent<Outline>();
            so.effectColor = UiKit.NavyDeep;
            so.effectDistance = new Vector2(2f, -2f);
        }

        // ---------- bottom left: the Monster ----------

        void BuildMonster(Transform parent)
        {
            var card = UiKit.Card(parent, "Monster", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(28f, 28f), new Vector2(390f, 112f));

            _monsterLabel = Head(card, "MONSTER", 30, TextAnchor.UpperLeft);
            _monsterLabel.rectTransform.offsetMin = new Vector2(24f, 0f);
            _monsterLabel.rectTransform.offsetMax = new Vector2(-20f, -10f);

            // Backlog is a different quantity from anger and is labelled as one: it is
            // what makes the Monster BIG, where anger is what makes it dangerous.
            _backlogText = Body(card, "", 20, TextAnchor.UpperRight);
            _backlogText.rectTransform.offsetMin = new Vector2(24f, 0f);
            _backlogText.rectTransform.offsetMax = new Vector2(-22f, -16f);

            // The bar never said what filling it would do to you.
            _monsterCaption = Body(card, "", 17, TextAnchor.LowerLeft);
            _monsterCaption.rectTransform.offsetMin = new Vector2(25f, 9f);
            _monsterCaption.rectTransform.offsetMax = new Vector2(-20f, -84f);

            // What you are carrying this run, and what it is doing for you.
            _ownedStrip = Body(parent, "", 23, TextAnchor.LowerLeft, UiKit.Cream);
            UiKit.Place(_ownedStrip.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(30f, 150f), new Vector2(620f, 30f));
            var stripOutline = _ownedStrip.gameObject.AddComponent<Outline>();
            stripOutline.effectColor = UiKit.NavyDeep;
            stripOutline.effectDistance = new Vector2(2f, -2f);

            _sprayChip = UiKit.Card(parent, "Spray", UiKit.Mint(), UiKit.Navy, 5f);
            UiKit.Place(_sprayChip, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(30f, 188f), new Vector2(300f, 54f));
            _sprayText = Head(_sprayChip, "", 26, TextAnchor.MiddleCenter);
            _sprayChip.gameObject.SetActive(false);

            // Segments, not a smooth bar: you can count how many you have left.
            var segs = new GameObject("Segments");
            segs.transform.SetParent(card, false);
            var srt = segs.AddComponent<RectTransform>();
            UiKit.Place(srt, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(24f, 32f), new Vector2(342f, 26f));

            const int count = 7;
            for (int i = 0; i < count; i++)
            {
                var seg = UiKit.Block(segs.transform, "Seg", UiKit.Grey, UiKit.Card9);
                UiKit.Place(seg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(i * 49f, 0f), new Vector2(42f, 26f));
                _monsterSegs.Add(seg);
            }
        }

        // ---------- bottom centre: the contextual action ----------

        void BuildActionBar(Transform parent)
        {
            _actionBar = UiKit.Card(parent, "ActionBar", UiKit.NavyBar, UiKit.NavyDeep, 7f);
            UiKit.Place(_actionBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 44f), new Vector2(920f, 136f));

            _actionKey = Keycap(_actionBar, "E", 86f, 64f);
            UiKit.Place(_actionKey, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        new Vector2(28f, 0f), new Vector2(86f, 64f));
            _actionKeyText = _actionKey.GetComponentInChildren<Text>();

            _actionText = Body(_actionBar, "", 32, TextAnchor.MiddleLeft, UiKit.Cream);
            _actionText.rectTransform.offsetMin = new Vector2(142f, 0f);
            _actionText.rectTransform.offsetMax = new Vector2(-460f, 0f);

            // Hold progress runs along the bottom edge of the bar.
            _holdBar = UiKit.Block(_actionBar, "HoldTrack", new Color(1f, 1f, 1f, 0.18f), UiKit.Bar).rectTransform;
            UiKit.Place(_holdBar, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(28f, 12f), new Vector2(560f, 12f));

            _holdFill = UiKit.Block(_actionBar, "HoldFill", UiKit.Yellow, UiKit.Bar);
            UiKit.Place(_holdFill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                        new Vector2(28f, 12f), new Vector2(560f, 12f));
            _holdFill.type = Image.Type.Filled;
            _holdFill.fillMethod = Image.FillMethod.Horizontal;
            _holdFill.fillOrigin = 0;
            _holdBar.gameObject.SetActive(false);
            _holdFill.gameObject.SetActive(false);

            // One chip per carry slot, filled left to right. Enough for the biggest the
            // basket upgrade can make it, since capacity changes mid-run.
            int slots = Tuning.CarryCapacity + Tuning.BasketCarryBonus;
            for (int i = 0; i < slots; i++)
            {
                var chip = UiKit.Card(_actionBar, "Carry", UiKit.Cream, UiKit.Navy, 4f,
                                      UiKit.Card9, false);
                UiKit.Place(chip, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-24f - i * 104f, 2f), new Vector2(96f, 96f));

                // The silhouette answers "what am I holding".
                var icon = UiKit.Block(chip, "Icon", UiKit.Navy, UiKit.GarmentIcon(GarmentKind.Shirt));
                icon.type = Image.Type.Simple;
                UiKit.Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -8f), new Vector2(54f, 54f));
                _carryIcons.Add(icon);

                // The strip answers "where does it go", which is the actually useful half.
                var bar = UiKit.Block(chip, "DestBar", UiKit.Blue, UiKit.Card9);
                UiKit.Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 7f), new Vector2(84f, 26f));
                _carryDestBar.Add(bar);

                var dest = Body(bar.transform, "", 19, TextAnchor.MiddleCenter, UiKit.Cream);
                dest.rectTransform.anchorMin = Vector2.zero;
                dest.rectTransform.anchorMax = Vector2.one;
                dest.rectTransform.offsetMin = Vector2.zero;
                dest.rectTransform.offsetMax = Vector2.zero;
                _carryDest.Add(dest);

                _carryChips.Add(chip);
                chip.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The points that float up out of the closet when something is put away.
        ///
        /// It appears where the work happened rather than in the corner, because the
        /// corner is where the score already lives and the player is not looking at it -
        /// they are looking at the closet they just walked to.
        /// </summary>
        void BuildDeliveryPop(Transform parent)
        {
            var go = new GameObject("Pop");
            go.transform.SetParent(parent, false);
            _pop = go.AddComponent<RectTransform>();
            UiKit.Place(_pop, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(320f, 70f));

            _popText = Head(_pop, "", 46, TextAnchor.MiddleCenter, UiKit.Yellow);
            var o = _popText.gameObject.AddComponent<Outline>();
            o.effectColor = UiKit.NavyDeep;
            o.effectDistance = new Vector2(3f, -3f);
            _pop.gameObject.SetActive(false);
        }

        void UpdateDeliveryPop()
        {
            // A fresh delivery restarts the animation.
            if (_dir.LastDeliveryTime > 0f && Time.time - _dir.LastDeliveryTime < 0.05f && _popAge > 0.1f)
            {
                _popAge = 0f;
                _popWorld = _dir.LastDeliveryWorld;
                _deliveredPop = 1f;

                string txt = "+" + _dir.LastDeliveryPoints.ToString("0.#");
                if (_dir.LastDeliveryBonus > 0f)
                    txt += "  +" + _dir.LastDeliveryBonus.ToString("0.#") + " streak";
                _popText.text = txt;
                _popText.color = _dir.LastDeliveryBonus > 0f ? UiKit.Yellow : UiKit.Mint();
            }

            _popAge += Time.deltaTime;
            bool show = _popAge < 1.2f;
            SetActive(_pop.gameObject, show);
            if (!show) return;

            var cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(_popWorld + new Vector3(0f, 1.1f + _popAge * 1.3f, 0f));
            if (sp.z <= 0f) { SetActive(_pop.gameObject, false); return; }
            _pop.position = sp;

            float fade = 1f - Mathf.Clamp01(_popAge / 1.2f);
            var c = _popText.color; c.a = fade; _popText.color = c;
            float grow = 1f + Mathf.Clamp01(_popAge / 0.18f) * 0.25f;
            _pop.localScale = Vector3.one * grow;
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

                // These sat at 204x80 each - four of them across a row of four machines
                // covered the machines. The name moves onto the ring's left and the card
                // shrinks to the width of its own status word.
                var card = UiKit.Card(_gameplay.transform, "Badge", UiKit.Cream, accent, 5f);
                UiKit.Place(card, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(152f, 54f));

                var label = Body(card, washer ? "WASH" : "DRY", 16, TextAnchor.UpperLeft, UiKit.Navy);
                label.rectTransform.offsetMin = new Vector2(12f, 0f);
                label.rectTransform.offsetMax = new Vector2(-44f, -5f);

                var pill = UiKit.Block(card, "Pill", UiKit.Grey, UiKit.Card9);
                UiKit.Place(pill.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(10f, 6f), new Vector2(96f, 24f));

                var status = Head(card, "", 22, TextAnchor.LowerLeft, UiKit.Navy);
                status.rectTransform.offsetMin = new Vector2(14f, 6f);
                status.rectTransform.offsetMax = new Vector2(-44f, -24f);
                status.resizeTextForBestFit = true;
                status.resizeTextMinSize = 12;
                status.resizeTextMaxSize = 22;
                // Overflow, not Wrap: a wrapped second line has nowhere to go in a
                // 54px card and gets drawn over the first.
                status.horizontalOverflow = HorizontalWrapMode.Overflow;
                status.verticalOverflow = VerticalWrapMode.Truncate;

                var ringBack = UiKit.Block(card, "RingBack", UiKit.Grey, UiKit.Ring);
                UiKit.Place(ringBack.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-11f, 0f), new Vector2(32f, 32f));

                var ringFill = UiKit.Block(card, "RingFill", accent, UiKit.Ring);
                UiKit.Place(ringFill.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                            new Vector2(-11f, 0f), new Vector2(32f, 32f));
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

        /// <summary>
        /// The countdown over The Chair while the Monster is reaching for it.
        ///
        /// Pinned to the chair in world space rather than parked in a corner, because the
        /// player has to know WHICH thing is in danger and where to run. Three seconds is
        /// only generous if you can see where to go.
        /// </summary>
        void BuildSnatchWarning(Transform parent)
        {
            _snatchCard = UiKit.Card(parent, "Snatch", UiKit.Cream, UiKit.Red, 6f);
            UiKit.Place(_snatchCard, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(300f, 76f));

            _snatchText = Head(_snatchCard, "GRAB IT!", 30, TextAnchor.MiddleLeft, UiKit.Red);
            _snatchText.rectTransform.offsetMin = new Vector2(22f, 0f);
            _snatchText.rectTransform.offsetMax = new Vector2(-72f, 0f);

            var back = UiKit.Block(_snatchCard, "RingBack", UiKit.Grey, UiKit.Ring);
            UiKit.Place(back.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-16f, 0f), new Vector2(46f, 46f));

            _snatchRing = UiKit.Block(_snatchCard, "RingFill", UiKit.Red, UiKit.Ring);
            UiKit.Place(_snatchRing.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                        new Vector2(-16f, 0f), new Vector2(46f, 46f));
            _snatchRing.type = Image.Type.Filled;
            _snatchRing.fillMethod = Image.FillMethod.Radial360;
            _snatchRing.fillOrigin = (int)Image.Origin360.Top;
            _snatchRing.fillClockwise = false;

            _snatchCard.gameObject.SetActive(false);
        }

        void UpdateSnatchWarning()
        {
            if (_snatchCard == null) return;
            if (_attack == null) _attack = Object.FindAnyObjectByType<MonsterAttack>();
            if (_chair == null) _chair = Object.FindAnyObjectByType<Chair>();

            bool on = _attack != null && _attack.Attacking && _chair != null;
            SetActive(_snatchCard.gameObject, on);
            if (!on) return;

            var cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(_chair.transform.position + new Vector3(0f, 1.6f, 0f));
            if (sp.z <= 0f) { SetActive(_snatchCard.gameObject, false); return; }
            _snatchCard.position = sp;

            _snatchRing.fillAmount = 1f - _attack.Progress;
            _snatchText.text = "GRAB IT!  " + Mathf.CeilToInt(_attack.TimeLeft) + "s";

            // Flash the card as the time runs out.
            UiKit.SetCardColors(_snatchCard,
                                Color.Lerp(UiKit.Cream, UiKit.Red, UiKit.Pulse(10f) * 0.35f),
                                UiKit.Red);
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
                        // 720, not 640: the stats block can run to eleven lines - the streak and the
                        // equipment lines are conditional, and the day's modifier is one more -
                        // and at 640 the last of them sat under the action button.
                        Vector2.zero, new Vector2(840f, 720f));

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

            _summaryStats = Body(card, "", 28, TextAnchor.UpperCenter);
            _summaryStats.rectTransform.offsetMin = new Vector2(60f, 124f);
            _summaryStats.rectTransform.offsetMax = new Vector2(-60f, -238f);

            var action = UiKit.Card(card, "Next", UiKit.Yellow, UiKit.Navy, 5f);
            UiKit.Place(action, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 34f), new Vector2(560f, 86f));
            _summaryAction = Head(action, "", 36, TextAnchor.MiddleCenter);

            _summary.SetActive(false);
        }

        /// <summary>
        /// The card that introduces one system, on the day it unlocks. Deliberately a full
        /// stop rather than a toast: it is the only time the game explains a rule before
        /// the rule can hurt you, so it is worth a button press.
        /// </summary>
        void BuildBriefing(Transform root)
        {
            _briefing = Panel(root, "Briefing");

            var dim = UiKit.Block(_briefing.transform, "Dim",
                                  new Color(UiKit.NavyDeep.r, UiKit.NavyDeep.g,
                                            UiKit.NavyDeep.b, 0.82f), UiKit.White);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero;
            dim.rectTransform.offsetMax = Vector2.zero;
            dim.type = Image.Type.Simple;

            var card = UiKit.Card(_briefing.transform, "Card", UiKit.Cream, UiKit.Navy, 7f);
            UiKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(1020f, 620f));

            var ribbon = UiKit.Block(card, "Ribbon", UiKit.Yellow, UiKit.Card9);
            UiKit.Place(ribbon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -28f), new Vector2(330f, 50f));
            _briefRibbon = Body(ribbon.transform, "NEW TODAY", 26, TextAnchor.MiddleCenter);
            _briefRibbon.rectTransform.anchorMin = Vector2.zero;
            _briefRibbon.rectTransform.anchorMax = Vector2.one;

            _briefTitle = Head(card, "", 58, TextAnchor.UpperCenter);
            _briefTitle.rectTransform.offsetMax = new Vector2(0f, -92f);

            _briefBody = Body(card, "", 27, TextAnchor.UpperLeft);
            _briefBody.rectTransform.offsetMin = new Vector2(58f, 142f);
            _briefBody.rectTransform.offsetMax = new Vector2(-58f, -172f);
            // Prose, not a label: it has to wrap inside the card rather than run off it.
            _briefBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            var action = UiKit.Card(card, "Go", UiKit.Yellow, UiKit.Navy, 5f);
            UiKit.Place(action, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 32f), new Vector2(520f, 76f));
            _briefAction = Head(action, "", 34, TextAnchor.MiddleCenter);

            _briefing.SetActive(false);
        }

        void UpdateBriefing()
        {
            // Two kinds of card share this panel, and they are different promises.
            // A system briefing is a rule that holds for the rest of the run; a
            // modifier is true today and gone tomorrow. The ribbon says which,
            // because a player who files "wrinkled is worth nothing" as permanent
            // will play the next four days wrong.
            if (_dir.PendingUnlock != Tuning.Unlock.None)
            {
                var card = Briefings.For(_dir.PendingUnlock);
                _briefRibbon.text = "NEW TODAY";
                _briefTitle.text = card.Title;

                // Warning, consequence, recovery - in that order, always all three.
                _briefBody.text = card.Warning
                                + "\n\n" + card.Consequence
                                + "\n\n" + card.Recovery;
            }
            else
            {
                var mod = DayModifiers.Describe(_dir.Today.Mod);
                _briefRibbon.text = "JUST FOR TODAY";
                _briefTitle.text = mod.Title;
                _briefBody.text = mod.Body + "\n\n" + mod.Advice;
            }

            _briefAction.text = GameInput.ConfirmGlyph + "   start day " + _dir.Day;
        }

        /// <summary>
        /// The choice of three, offered after a day worth at least one star.
        ///
        /// Every card states what it gives AND what it costs, because an upgrade whose
        /// tradeoff is hidden is a trap rather than a decision.
        /// </summary>
        void BuildPicker(Transform root)
        {
            _picker = Panel(root, "Picker");

            var dim = UiKit.Block(_picker.transform, "Dim",
                                  new Color(UiKit.NavyDeep.r, UiKit.NavyDeep.g,
                                            UiKit.NavyDeep.b, 0.88f), UiKit.White);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = Vector2.zero;
            dim.rectTransform.offsetMax = Vector2.zero;
            dim.type = Image.Type.Simple;

            var heading = Head(_picker.transform, "PICK ONE", 64, TextAnchor.UpperCenter, UiKit.Cream);
            UiKit.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -70f), new Vector2(1200f, 90f));

            var sub2 = Body(_picker.transform, "a good day earns one piece of equipment",
                            28, TextAnchor.UpperCenter, UiKit.Yellow);
            _pickHint = sub2;
            UiKit.Place(sub2.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -156f), new Vector2(1200f, 44f));

            const float w = 480f, h = 420f, gap = 36f;
            for (int i = 0; i < Tuning.UpgradeChoices; i++)
            {
                float x = (i - 1) * (w + gap);
                var pos = new Vector2(x, -40f);

                var card = UiKit.Card(_picker.transform, "Pick" + i, UiKit.Cream, UiKit.Navy, 7f);
                UiKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            pos, new Vector2(w, h));

                var cap = UiKit.Card(card, "Key", UiKit.Yellow, UiKit.Navy, 4f, UiKit.Card9, false);
                UiKit.Place(cap, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -22f), new Vector2(84f, 58f));
                _pickKey.Add(Head(cap, "1", 34, TextAnchor.MiddleCenter));

                var name = Head(card, "", 36, TextAnchor.UpperCenter);
                name.rectTransform.offsetMin = new Vector2(24f, 0f);
                name.rectTransform.offsetMax = new Vector2(-24f, -96f);
                _pickName.Add(name);

                var body = Body(card, "", 25, TextAnchor.UpperLeft);
                body.rectTransform.offsetMin = new Vector2(34f, 30f);
                body.rectTransform.offsetMax = new Vector2(-34f, -160f);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                _pickBody.Add(body);

                _pickCards.Add(card);
            }

            _picker.SetActive(false);
        }

        Text _pickHint;

        void UpdatePicker()
        {
            var dir = _dir;
            if (_pickHint != null)
                _pickHint.text = GameInput.Active == GameInput.Scheme.Touch
                    ? "tap a card to take it"
                    : "a good day earns one piece of equipment";
            for (int i = 0; i < _pickCards.Count; i++)
            {
                bool used = i < dir.Offered.Count;
                SetActive(_pickCards[i].gameObject, used);
                if (!used) continue;

                var info = Upgrades.Describe(dir.Offered[i]);
                _pickName[i].text = info.Name;
                _pickBody[i].text = info.Effect + "\n\n" + info.Tradeoff;
                _pickKey[i].text = PickGlyph(i);
            }
        }

        static string PickGlyph(int i)
        {
            if (GameInput.Active == GameInput.Scheme.Gamepad)
                return i == 0 ? "X" : i == 1 ? "Y" : "B";
            if (GameInput.Active == GameInput.Scheme.Touch) return "TAP";
            return (i + 1).ToString();
        }

        /// <summary>
        /// Which offered card a tap landed on, or -1. Touch has no EventSystem, so the
        /// hit test is done by hand - but against the card's own RectTransform.
        ///
        /// It used to rebuild each card's rectangle arithmetically around 1920/2. That
        /// is only the centre of the canvas on a 16:9 screen: a CanvasScaler on match
        /// 0.5 blends the two axes, so a 19.5:9 phone gets a canvas roughly 2120 units
        /// wide whose centre is nowhere near 960. Every hit box sat about a hundred
        /// units to the left of the card it was supposed to belong to, which is why
        /// tapping an upgrade on an iPhone did nothing whatsoever.
        /// </summary>
        public int UpgradeCardAt(Vector2 screenPoint)
        {
            // Screen Space - Overlay takes a null camera; passing one gives wrong hits.
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;

            for (int i = 0; i < _pickCards.Count && i < _dir.Offered.Count; i++)
            {
                var rt = _pickCards[i];
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, cam))
                    return i;
            }
            return -1;
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
            // The picker is a full-screen decision, so the room HUD steps out of the way.
            // The briefing keeps it: knowing what day it is while reading the card helps.
            bool playing = ph == Phase.Playing || ph == Phase.DaySummary || ph == Phase.Briefing;
            bool front = ph == Phase.Intro || ph == Phase.Title || ph == Phase.Help;

            SetActive(_gameplay, playing);
            SetActive(_front, front);
            SetActive(_credits, ph == Phase.Credits);
            SetActive(_briefing, ph == Phase.Briefing);
            if (ph == Phase.Briefing) UpdateBriefing();
            SetActive(_picker, ph == Phase.UpgradePick);
            if (ph == Phase.UpgradePick) UpdatePicker();
            SetActive(_summary, ph == Phase.DaySummary || ph == Phase.RunOver);

            if (ph == Phase.Credits) UpdateCredits();
            if (front) UpdateFrontEnd(ph);

            // Above the early return, because RunOver is not a playing phase. Below it,
            // the game-over card was shown but never written: THE LAUNDRY WON was dead
            // code, and a lost run sat under whatever the previous day's card had said.
            if (ph == Phase.DaySummary || ph == Phase.RunOver) UpdateSummary();
            if (!playing) return;

            UpdateClock();
            UpdateDelivered();
            UpdateMonster();
            UpdateObjective();
            UpdateBadges();
            UpdateSnatchWarning();
            UpdateDeliveryPop();
            UpdateActionBar();
            UpdateFlash();
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

            // The finishing period is a promise: nothing more is coming, so everything
            // still out there can in principle be finished.
            // Only the one thing the clock itself cannot say. "215s left" next to
            // "03:35" was the same fact twice in two units.
            _secondsText.text = _dir.ArrivalsDone && _dir.CurrentPhase == Phase.Playing
                ? "LAST LOAD - no more arrivals" : "";
        }

        void UpdateDelivered()
        {
            _deliveredText.text = _dir.Delivered.ToString();

            // A short punch on the counter, so a delivery registers even if you were
            // looking at the closet rather than the corner.
            _deliveredPop = Mathf.Max(0f, _deliveredPop - Time.deltaTime * 3.2f);
            float punch = 1f + _deliveredPop * 0.35f;
            _deliveredText.rectTransform.localScale = Vector3.one * punch;

            _scoreText.text = "score  " + _dir.Score.ToString("0.#")
                            + " / " + _dir.Target.ToString("0.#") + " to pass";
            bool met = _dir.Score >= _dir.Target;
            _scoreText.color = met ? UiKit.Green
                : new Color(UiKit.Navy.r, UiKit.Navy.g, UiKit.Navy.b, 0.75f);
            _deliveredText.color = met ? UiKit.Green : UiKit.Navy;

            // The streak only appears once it is actually paying, so it reads as a reward
            // rather than another meter to manage.
            if (_dir.CleanStreak >= Tuning.StreakMin)
                _streakText.text = "CLEAN STREAK  x" + _dir.CleanStreak
                                 + "   +" + _dir.BonusScore.ToString("0.#");
            else if (_dir.BonusScore > 0f)
                _streakText.text = "streak bonus  +" + _dir.BonusScore.ToString("0.#");
            else
                _streakText.text = "";
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

            // "ANGER" was a bar with no owner and no stated consequence. Both now
            // appear, and the warning only replaces the caption once it is earned.
            _monsterLabel.text = "MONSTER";
            _monsterLabel.fontSize = 30;

            _monsterCaption.text = frac > 0.75f ? "IT HAS EYES NOW"
                                 : frac > 0.4f  ? "full bar = it eats you"
                                                : "fills while laundry waits";
            _monsterCaption.color = frac > 0.75f
                ? Color.Lerp(UiKit.Red, UiKit.Yellow, UiKit.Pulse(7f))
                : new Color(UiKit.Navy.r, UiKit.Navy.g, UiKit.Navy.b, 0.6f);

            int backlog = _dir.Backlog;
            _backlogText.text = backlog + " waiting";

            UpdateKitStrip();
            _backlogText.color = backlog >= Tuning.MonsterFullPile
                ? UiKit.Red : new Color(UiKit.Navy.r, UiKit.Navy.g, UiKit.Navy.b, 0.7f);
        }

        /// <summary>The owned-upgrade line, and the spray charge when there is one.</summary>
        void UpdateKitStrip()
        {
            var kit = _dir.Kit;
            if (kit == null || _ownedStrip == null) return;

            if (kit.Count == 0)
            {
                _ownedStrip.text = "";
            }
            else
            {
                var sb = new System.Text.StringBuilder("KIT:  ");
                bool first = true;
                foreach (var id in kit.Owned)
                {
                    if (!first) sb.Append("   ");
                    sb.Append(Upgrades.Describe(id).Name);
                    first = false;
                }
                _ownedStrip.text = sb.ToString();
            }

            bool spray = kit.Has(UpgradeId.WrinkleSpray);
            SetActive(_sprayChip.gameObject, spray);
            if (!spray) return;

            bool ready = kit.CanSpray;
            _sprayText.text = ready
                ? GameInput.SprayGlyph + "   WRINKLE SPRAY  x" + kit.SprayCharges
                : "SPRAY USED - refills tomorrow";
            UiKit.SetCardColors(_sprayChip, ready ? UiKit.Mint() : UiKit.Grey, UiKit.Navy);
        }

        /// <summary>
        /// The day's twist, kept on screen for as long as it is true.
        ///
        /// Hidden entirely on an ordinary day rather than showing "NORMAL", because a
        /// chip that is always there is furniture and stops being read - the point is
        /// that its presence is itself the signal.
        /// </summary>
        void UpdateModifierChip()
        {
            if (_modChip == null) return;

            string label = DayModifiers.ShortName(_dir.Today.Mod);
            bool show = !string.IsNullOrEmpty(label);
            SetActive(_modChip.gameObject, show);
            if (show) _modText.text = label;
        }

        void UpdateObjective()
        {
            UpdateModifierChip();

            bool teaching = _tutorial != null && _tutorial.Running;
            // Show the nudge on any day that is still teaching something, not only day one.
            bool dayOneHint = !teaching && _dir.FlashTimer <= 0f
                              && _dir.Day <= Tuning.DaySocks
                              && !string.IsNullOrEmpty(Briefings.Hint(_dir.Day, _player));

            bool objectiveUp = teaching || dayOneHint;
            SetActive(_objective.gameObject, objectiveUp);

            // The modifier chip sits under the objective card while there is one, and
            // moves up into its place when there is not. Left at the lower spot it hung
            // in mid-air across the tops of the machines with nothing above it.
            if (_modChip != null)
                _modChip.anchoredPosition = new Vector2(0f, objectiveUp ? -138f : -16f);
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
                _objectiveTitle.text = _dir.Day == 1 ? "GET STARTED" : "TODAY";
                _objectiveBody.text = Briefings.Hint(_dir.Day, _player);
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

                // In FRONT of the machine, not above it.
                //
                // Above was fine at 16:9 and a collision on a phone: the machines sit
                // near the top of the picture, so a badge above one lands in the strip
                // the day, objective and score cards already own. On an iPhone in
                // landscape all four badges were drawn across the tutorial card.
                // The floor in front of a machine is empty at every aspect, and a label
                // at a machine's feet still unmistakably belongs to that machine.
                var world = b.Machine.transform.position + new Vector3(0f, 0.28f, -1.18f);
                var sp = cam.WorldToScreenPoint(world);
                bool visible = sp.z > 0f;
                SetActive(b.Root.gameObject, visible);
                if (!visible) continue;
                b.Root.position = sp;

                var m = b.Machine;
                bool washer = m.MachineMode == LaundryMachine.Mode.Washer;
                var accent = washer ? UiKit.Blue : UiKit.Orange;

                if (m.OutOfOrder)
                {
                    // Dead for the day, as a fact about today rather than a consequence
                    // of anything the player did. Grey, not red: red is the colour of
                    // something that needs doing, and nothing can be done about this.
                    // Four letters. The pill is sized for EMPTY and ON FIRE, and a longer
                    // word runs off the end of it: OUT OF ORDER showed as a tail of DER.
                    b.Status.text = "DEAD";
                    // Grey, not navy: the badge text is navy and never changes, so on a
                    // navy pill the word simply disappeared.
                    b.Pill.color = UiKit.Grey;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 0f;
                    UiKit.SetCardColors(b.Root, UiKit.CreamDim, UiKit.NavyDeep);
                }
                else if (m.Offline)
                {
                    b.Status.text = "ON FIRE";
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
                else if (m.HasFinishedLoad && m.HoldsSpoiled)
                {
                    // Already spoiled: still unloadable, and still recoverable, but it is
                    // no longer a countdown - it is a job.
                    b.Status.text = "SPOILED";
                    b.Pill.color = UiKit.Red;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.fillAmount = 1f;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, UiKit.Red);
                }
                else if (m.HasFinishedLoad)
                {
                    // The useful number here is not "done", it is HOW LONG YOU HAVE. A
                    // finished load is only good news until its clock runs out.
                    float spoil = m.SecondsToSpoil;
                    bool urgent = spoil >= 0f
                                  && spoil <= Tuning.WrinkleGrace * Tuning.UrgentFraction;

                    if (spoil >= 0f)
                    {
                        b.Status.text = Mathf.CeilToInt(spoil) + "s LEFT";
                        b.RingFill.fillAmount = Mathf.Clamp01(spoil / Mathf.Max(1f, Tuning.WrinkleGrace));
                    }
                    else
                    {
                        b.Status.text = "READY";
                        b.RingFill.fillAmount = 1f;
                    }

                    var tone = urgent ? Color.Lerp(UiKit.Red, UiKit.Yellow, UiKit.Pulse(10f))
                                      : UiKit.Green;
                    b.Pill.color = tone;
                    SetActive(b.Pill.gameObject, true);
                    b.RingFill.color = tone;
                    UiKit.SetCardColors(b.Root, UiKit.Cream, tone);
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
            string action = "";     // something the key will actually do
            string advice = "";     // why there is nothing to press
            bool hold = false;

            if (near != null)
            {
                string tap = near.ActionPrompt(_player);
                string held = near.HoldPrompt(_player);
                if (!string.IsNullOrEmpty(held) && near.HoldSeconds(_player) > 0f)
                {
                    action = "Hold to " + char.ToLower(held[0]) + held.Substring(1);
                    hold = true;
                }
                else if (!string.IsNullOrEmpty(tap))
                {
                    action = tap;
                }
                else
                {
                    advice = near.BlockedReason(_player);
                }

                // The station's own counter, once, and only next to a real action. It
                // used to run on after advice too, which read as part of the sentence.
                if (!string.IsNullOrEmpty(action))
                {
                    string status = near.Status;
                    if (!string.IsNullOrEmpty(status)) action += "   (" + status + ")";
                }
            }

            bool show = !string.IsNullOrEmpty(action) || !string.IsNullOrEmpty(advice)
                        || _player.Carried.Count > 0;
            SetActive(_actionBar.gameObject, show);
            if (!show) return;

            if (!string.IsNullOrEmpty(action))
            {
                _actionText.text = action;
                _actionText.color = UiKit.Cream;
            }
            else if (!string.IsNullOrEmpty(advice))
            {
                _actionText.text = advice;
                // Dimmed, and with no key cap: this is a reason, not a button.
                _actionText.color = new Color(1f, 0.86f, 0.55f, 0.95f);
            }
            else
            {
                _actionText.text = _player.CarryPenalty > 0
                    ? "Carrying  (" + _player.CarryCapacity + " slot, AirPods lost)"
                    : "Carrying";
                _actionText.color = new Color(1f, 1f, 1f, 0.7f);
            }

            _actionKeyText.text = GameInput.InteractGlyph;
            SetActive(_actionKey.gameObject, !string.IsNullOrEmpty(action));

            bool holding = _player.HoldProgress > 0f;
            SetActive(_holdBar.gameObject, hold || holding);
            SetActive(_holdFill.gameObject, hold || holding);
            _holdFill.fillAmount = _player.HoldProgress;

            // One chip per carried garment: what it is, what state it is in, and which
            // bench it is for. The icon is tinted with the garment's own colour so a chip
            // and the thing in your arms read as the same object.
            for (int i = 0; i < _carryChips.Count; i++)
            {
                bool used = i < _player.Carried.Count;
                SetActive(_carryChips[i].gameObject, used);
                if (!used) continue;

                // Reverse the mapping so the first thing you picked up sits leftmost:
                // the chips are laid out from the right edge, but they are read from the left.
                var g = _player.Carried[_player.Carried.Count - 1 - i];
                if (g == null) { SetActive(_carryChips[i].gameObject, false); continue; }

                _carryIcons[i].sprite = UiKit.GarmentIcon(g.Kind);
                _carryIcons[i].color = Readable(g.DisplayColor);

                var stop = g.NextStopColor;
                _carryDest[i].text = g.NextStop;
                _carryDestBar[i].color = stop;

                // A garment running out of time gets a flashing outline, so the chip says
                // which one to deal with as well as where it goes.
                bool urgent = g.IsDecaying && g.DecayFraction > 0.6f;
                UiKit.SetCardColors(_carryChips[i], UiKit.Cream,
                                    urgent ? Color.Lerp(stop, UiKit.Red, UiKit.Pulse(9f)) : stop);
            }
        }

        /// <summary>
        /// Keep a garment's own colour but force it dark enough to read as a silhouette on
        /// cream. Whites and pale creams are real laundry colours and would vanish.
        /// </summary>
        static Color Readable(Color c)
        {
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return lum > 0.55f ? Color.Lerp(c, UiKit.Navy, (lum - 0.55f) / 0.45f * 0.8f) : c;
        }

        void UpdateFlash()
        {
            if (_dir.FlashTimer <= 0f) { _flashText.text = ""; return; }
            _flashText.text = _dir.FlashMessage;
            var c = _dir.FlashColor;
            c.a = Mathf.Clamp01(_dir.FlashTimer / 0.8f);
            _flashText.color = c;
        }

        /// <summary>One line explaining what the closing charge was for, or nothing.</summary>
        string ClosingLine()
        {
            if (_dir.UnfinishedAtClose <= 0) return "";
            return "\n\n" + _dir.UnfinishedAtClose + " left unfinished"
                 + "  ->  Monster +" + _dir.ClosingPenalty.ToString("0.#");
        }

        /// <summary>Tells the player whether this day earned a pick, and why.</summary>
        string UpgradeLine()
        {
            if (_dir.Stars <= 0)
                return "\n\nno equipment this time - a day needs at least one star";
            if (!_dir.Kit.AnyAvailable())
                return "\n\nevery piece of equipment already acquired";
            return "\n\nONE STAR EARNED - pick a piece of equipment next";
        }

        /// <summary>
        /// One line naming the day's modifier, or nothing on an ordinary day.
        ///
        /// The briefing card explained the rule before the day; the chip kept it on
        /// screen during it. Without this the summary was the one screen that forgot,
        /// and "why was that so hard" went unanswered at exactly the moment it was asked.
        /// </summary>
        string ModifierLine(string label, bool trailing = false)
        {
            string name = DayModifiers.ShortName(_dir.Today.Mod);
            if (string.IsNullOrEmpty(name)) return "";
            string line = label + name;
            return trailing ? line + "\n" : "\n" + line;
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
                    ModifierLine("lost on        ") +
                    (_dir.NewRecord ? "\n\nA NEW PERSONAL BEST" : "");
                _summaryAction.text = GameInput.ConfirmGlyph + "   back to title";
            }
            else
            {
                _summaryStats.text =
                    ModifierLine("today       ", trailing: true) +
                    "put away    " + _dir.Delivered + "\n" +
                    "score       " + _dir.Score.ToString("0.#") + " / " + _dir.Target.ToString("0.#")
                        + "   (stars are scored on this)\n" +
                    (_dir.BonusScore > 0f
                        ? "streak      +" + _dir.BonusScore.ToString("0.#")
                          + "   best x" + _dir.BestStreakToday + "\n" : "") +
                    "wrinkled    " + _dir.WrinkledCount + "\n" +
                    "mildewed    " + _dir.MildewedCount + "\n" +
                    "socks lost  " + _dir.VoidedSocks + ClosingLine() + UpgradeLine();
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
            // A phone player has no key cap to look at, so say where the controls are.
            string controls = GameInput.Active == GameInput.Scheme.Touch
                ? "Drag anywhere on the LEFT to walk.   The round button on the RIGHT does "
                  + "whatever you are standing next to - it says so on its face.\n"
                  + "   Some jobs need it held down. The small button on the left pauses.\n\n"
                : GameInput.MoveGlyph + " to move.   " + act + " to interact.   Some things need it HELD.\n\n";

            return
                controls +
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
