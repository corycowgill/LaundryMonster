using UnityEngine;
using UnityEngine.UI;

namespace LaundryMonster
{
    /// <summary>
    /// Builds and drives the whole HUD in code. Uses legacy uGUI Text with the
    /// built-in LegacyRuntime font so it needs no imported font assets and no
    /// scene wiring - nothing here can break from a missing resource.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        PlayerController _player;
        GameDirector _dir;

        Text _dayText, _timeText, _scoreText, _carryText, _promptText, _monsterText, _flashText;
        Image _timeFill, _monsterFill, _holdFill;
        GameObject _holdGroup, _summaryPanel;
        Text _summaryText;

        Font _font;
        Sprite _white;

        void Start()
        {
            _player = Object.FindAnyObjectByType<PlayerController>();
            _dir = GameDirector.Instance;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _white = MakeWhiteSprite();
            Build();
        }

        /// <summary>
        /// Image.Type.Filled ignores fillAmount when sprite is null - Unity falls back to
        /// drawing a plain full rect - so every bar needs a real sprite to animate.
        /// </summary>
        static Sprite MakeWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        }

        // ---------- construction ----------

        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGo.transform;

            // --- top left: day + time ---
            _dayText = MakeText(root, "Day", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(40f, -40f), new Vector2(420f, 70f), 54, TextAnchor.UpperLeft);

            MakeImage(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                      new Vector2(44f, -118f), new Vector2(360f, 18f),
                      new Color(0f, 0f, 0f, 0.45f), TextAnchor.UpperLeft);

            _timeFill = MakeImage(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                  new Vector2(44f, -118f), new Vector2(360f, 18f),
                                  new Color(0.35f, 0.75f, 0.95f), TextAnchor.UpperLeft);
            _timeFill.type = Image.Type.Filled;
            _timeFill.fillMethod = Image.FillMethod.Horizontal;
            _timeFill.fillOrigin = 0;

            _timeText = MakeText(root, "", new Vector2(0f, 1f), new Vector2(0f, 1f),
                                 new Vector2(44f, -140f), new Vector2(360f, 34f), 24, TextAnchor.UpperLeft);

            // --- top right: score ---
            _scoreText = MakeText(root, "", new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(-40f, -40f), new Vector2(460f, 110f), 40, TextAnchor.UpperRight);

            // --- bottom left: monster ---
            _monsterText = MakeText(root, "MONSTER", new Vector2(0f, 0f), new Vector2(0f, 0f),
                                    new Vector2(44f, 86f), new Vector2(360f, 30f), 24, TextAnchor.LowerLeft);

            MakeImage(root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                      new Vector2(44f, 50f), new Vector2(360f, 20f),
                      new Color(0f, 0f, 0f, 0.45f), TextAnchor.LowerLeft);

            _monsterFill = MakeImage(root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                                     new Vector2(44f, 50f), new Vector2(360f, 20f),
                                     new Color(0.75f, 0.25f, 0.25f), TextAnchor.LowerLeft);
            _monsterFill.type = Image.Type.Filled;
            _monsterFill.fillMethod = Image.FillMethod.Horizontal;
            _monsterFill.fillOrigin = 0;

            // --- bottom centre: carrying + prompt + hold bar ---
            _carryText = MakeText(root, "", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                  new Vector2(0f, 150f), new Vector2(900f, 36f), 26, TextAnchor.LowerCenter);

            _promptText = MakeText(root, "", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(0f, 96f), new Vector2(900f, 48f), 32, TextAnchor.LowerCenter);

            _holdGroup = new GameObject("HoldBar");
            _holdGroup.transform.SetParent(root, false);
            var hg = _holdGroup.AddComponent<RectTransform>();
            Anchor(hg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f),
                   new Vector2(320f, 16f), TextAnchor.LowerCenter);

            MakeImage(_holdGroup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      Vector2.zero, new Vector2(320f, 16f), new Color(0f, 0f, 0f, 0.5f), TextAnchor.MiddleCenter);

            _holdFill = MakeImage(_holdGroup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  Vector2.zero, new Vector2(320f, 16f),
                                  new Color(0.95f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
            _holdFill.type = Image.Type.Filled;
            _holdFill.fillMethod = Image.FillMethod.Horizontal;
            _holdFill.fillOrigin = 0;
            _holdGroup.SetActive(false);

            // --- centre: pocket-disaster flash ---
            _flashText = MakeText(root, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(0f, 180f), new Vector2(1100f, 70f), 40, TextAnchor.MiddleCenter);

            // --- centre: day summary ---
            _summaryPanel = new GameObject("Summary");
            _summaryPanel.transform.SetParent(root, false);
            var sp = _summaryPanel.AddComponent<RectTransform>();
            Anchor(sp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                   new Vector2(760f, 520f), TextAnchor.MiddleCenter);

            MakeImage(_summaryPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      Vector2.zero, new Vector2(760f, 520f), new Color(0.05f, 0.05f, 0.07f, 0.92f),
                      TextAnchor.MiddleCenter);

            _summaryText = MakeText(_summaryPanel.transform, "", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    Vector2.zero, new Vector2(700f, 470f), 32, TextAnchor.MiddleCenter);
            _summaryPanel.SetActive(false);
        }

        void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, TextAnchor pivotFrom)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = PivotFor(pivotFrom);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Vector2 PivotFor(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return new Vector2(0f, 1f);
                case TextAnchor.UpperRight: return new Vector2(1f, 1f);
                case TextAnchor.LowerLeft: return new Vector2(0f, 0f);
                case TextAnchor.LowerRight: return new Vector2(1f, 0f);
                case TextAnchor.LowerCenter: return new Vector2(0.5f, 0f);
                case TextAnchor.UpperCenter: return new Vector2(0.5f, 1f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        Text MakeText(Transform parent, string content, Vector2 aMin, Vector2 aMax,
                      Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            Anchor(rt, aMin, aMax, pos, size, align);

            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = fontSize;
            t.text = content;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        Image MakeImage(Transform parent, Vector2 aMin, Vector2 aMax,
                        Vector2 pos, Vector2 size, Color color, TextAnchor align)
        {
            var go = new GameObject("Image");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            Anchor(rt, aMin, aMax, pos, size, align);

            var img = go.AddComponent<Image>();
            img.sprite = _white;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ---------- per-frame ----------

        void Update()
        {
            if (_dir == null) _dir = GameDirector.Instance;
            if (_dir == null || _dayText == null) return;

            _dayText.text = "DAY " + _dir.Day;

            float timeFrac = _dir.DayLength <= 0f ? 0f : _dir.TimeLeft / _dir.DayLength;
            _timeFill.fillAmount = timeFrac;
            _timeFill.color = timeFrac < 0.2f
                ? new Color(0.95f, 0.4f, 0.25f)
                : new Color(0.35f, 0.75f, 0.95f);
            _timeText.text = Mathf.CeilToInt(_dir.TimeLeft) + "s left";

            _scoreText.text = _dir.Score.ToString("0.#") + " / " + _dir.Target.ToString("0.#")
                              + "\n" + _dir.Delivered + " put away";

            float mFrac = _dir.Monster / Tuning.MonsterMax;
            _monsterFill.fillAmount = mFrac;
            _monsterText.text = mFrac > 0.75f ? "MONSTER - it has eyes now" : "MONSTER";

            UpdateFlash();
            UpdateCarryAndPrompt();
            UpdateSummary();
        }

        void UpdateFlash()
        {
            if (_flashText == null) return;

            if (_dir.FlashTimer <= 0f)
            {
                if (_flashText.text.Length > 0) _flashText.text = "";
                return;
            }

            _flashText.text = _dir.FlashMessage;
            // Fade out over the last second so it does not just vanish.
            var c = _dir.FlashColor;
            c.a = Mathf.Clamp01(_dir.FlashTimer);
            _flashText.color = c;
        }

        void UpdateCarryAndPrompt()
        {
            if (_player == null)
            {
                _player = Object.FindAnyObjectByType<PlayerController>();
                if (_player == null) return;
            }

            if (_player.Carried.Count == 0)
            {
                _carryText.text = _player.CarryPenalty > 0
                    ? "carrying nothing  (" + _player.CarryCapacity + " slot, AirPods lost)"
                    : "carrying nothing";
                _carryText.color = new Color(1f, 1f, 1f, 0.5f);
            }
            else
            {
                string s = "carrying: ";
                for (int i = 0; i < _player.Carried.Count; i++)
                {
                    var g = _player.Carried[i];
                    if (i > 0) s += " + ";
                    s += Garment.StateLabel(g.State);
                    if (g.IsDecaying)
                        s += " (" + Mathf.CeilToInt(g.DecayLimit - g.StateTimer) + "s)";
                }
                _carryText.text = s;
                _carryText.color = Color.white;
            }

            var near = _player.Nearest;
            if (near == null)
            {
                _promptText.text = "";
            }
            else
            {
                // A station can offer both, and the player must see both to choose.
                string tap = near.ActionPrompt(_player);
                string hold = near.HoldPrompt(_player);
                string status = near.Status;

                string line = near.Label;
                if (!string.IsNullOrEmpty(status)) line += "  [" + status + "]";
                if (!string.IsNullOrEmpty(tap)) line += "\npress E to " + tap;
                if (!string.IsNullOrEmpty(hold) && near.HoldSeconds(_player) > 0f)
                    line += "\nhold E to " + hold;
                _promptText.text = line;
            }

            bool holding = _player.HoldProgress > 0f;
            if (_holdGroup.activeSelf != holding) _holdGroup.SetActive(holding);
            if (holding) _holdFill.fillAmount = _player.HoldProgress;
        }

        void UpdateSummary()
        {
            bool show = _dir.CurrentPhase != Phase.Playing;
            if (_summaryPanel.activeSelf != show) _summaryPanel.SetActive(show);
            if (!show) return;

            if (_dir.CurrentPhase == Phase.RunOver)
            {
                _summaryText.text =
                    "THE LAUNDRY WON\n\n" +
                    "It was never going to stop.\n" +
                    "You made it to day " + _dir.Day + ".\n\n" +
                    "press SPACE to start over";
                return;
            }

            string stars = "";
            for (int i = 0; i < 3; i++) stars += i < _dir.Stars ? "* " : "- ";

            _summaryText.text =
                "DAY " + _dir.Day + " COMPLETE\n\n" +
                "put away      " + _dir.Delivered + "\n" +
                "score         " + _dir.Score.ToString("0.#") + " / " + _dir.Target.ToString("0.#") + "\n" +
                "wrinkled      " + _dir.WrinkledCount + "\n" +
                "mildewed      " + _dir.MildewedCount + "\n" +
                "socks lost    " + _dir.VoidedSocks + "\n\n" +
                stars + "\n\n" +
                "press SPACE for day " + (_dir.Day + 1);
        }
    }
}
