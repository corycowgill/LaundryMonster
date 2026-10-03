using UnityEngine;
using UnityEngine.UI;

namespace LaundryMonster
{
    /// <summary>
    /// The playful-arcade look: cream cards, heavy navy outlines, yellow buttons.
    ///
    /// Everything is built from two stacked rounded rectangles - an outline behind a fill -
    /// rather than from a single sprite with the border baked in. That costs one extra
    /// Image per card and buys the thing the design needs most: the outline colour is free
    /// to change per widget, which is what makes a washer badge blue and a dryer badge
    /// orange without a second texture.
    ///
    /// Every sprite here is generated at runtime, so the HUD has no art dependencies and
    /// cannot break from a missing file.
    /// </summary>
    public static class UiKit
    {
        // ---------- palette ----------

        public static readonly Color Cream = new Color(0.992f, 0.969f, 0.925f, 1f);
        public static readonly Color CreamDim = new Color(0.937f, 0.906f, 0.855f, 1f);

        /// <summary>Outlines and nearly all text. The design has exactly one dark.</summary>
        public static readonly Color Navy = new Color(0.078f, 0.180f, 0.361f, 1f);
        public static readonly Color NavyDeep = new Color(0.047f, 0.125f, 0.278f, 1f);
        public static readonly Color NavyBar = new Color(0.102f, 0.227f, 0.439f, 1f);

        /// <summary>The primary action. Only ever one on screen.</summary>
        public static readonly Color Yellow = new Color(1.000f, 0.776f, 0.235f, 1f);
        public static readonly Color YellowDeep = new Color(0.949f, 0.639f, 0.118f, 1f);

        public static readonly Color Blue = new Color(0.176f, 0.549f, 0.941f, 1f);
        public static readonly Color BlueDeep = new Color(0.106f, 0.396f, 0.776f, 1f);

        public static readonly Color Orange = new Color(0.976f, 0.545f, 0.129f, 1f);
        public static readonly Color Green = new Color(0.208f, 0.780f, 0.349f, 1f);
        public static readonly Color Red = new Color(0.910f, 0.271f, 0.235f, 1f);
        public static readonly Color Grey = new Color(0.788f, 0.800f, 0.824f, 1f);

        /// <summary>A soft green for "ready to use", distinct from the alarm palette.</summary>
        public static Color Mint() => new Color(0.639f, 0.918f, 0.792f, 1f);

        // ---------- generated sprites ----------

        static Sprite _card, _pill, _white, _disc, _ring, _bar, _star;

        /// <summary>
        /// Hand back the cached sprite, or make it again if it is gone.
        ///
        /// NOT `_x ??= Make()`. The null-coalescing operator tests for real C# null, and
        /// a sprite Unity has destroyed - which is what happens to every runtime-created
        /// one when play mode exits - is a live managed object wrapping a dead native
        /// one. `??=` is perfectly happy with it and hands it straight to an Image, which
        /// then draws a plain white quad instead. That is why the machines' progress
        /// rings rendered as solid blocks: not a bad ring, a dead one.
        ///
        /// `== null` goes through UnityEngine.Object's operator, which knows the
        /// difference. Same trap as the GetComponent note further down this file.
        /// </summary>
        static Sprite Cached(ref Sprite slot, System.Func<Sprite> make)
        {
            if (slot == null) slot = make();
            return slot;
        }

        public static Sprite White => Cached(ref _white, Solid);
        public static Sprite Card9 => Cached(ref _card, () => RoundedRect(64, 18));
        public static Sprite Pill => Cached(ref _pill, () => RoundedRect(64, 31));

        /// <summary>For thin bars, where Pill's corners would be taller than the bar.</summary>
        public static Sprite Bar => Cached(ref _bar, () => RoundedRect(32, 10));
        public static Sprite Disc => Cached(ref _disc, () => DiscSprite(96));
        public static Sprite Ring => Cached(ref _ring, () => RingSprite(192, 0.26f));
        public static Sprite Star => Cached(ref _star, () => StarSprite(128));

        static Texture2D NewTex(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        /// <summary>
        /// Sprite.Create leaves the new sprite as an ordinary runtime object, so play
        /// mode takes it away on exit while the texture it wraps - which NewTex does
        /// flag - survives. Flagging both keeps a cached sprite alive as long as the
        /// static field that remembers it.
        /// </summary>
        static Sprite Keep(Sprite s)
        {
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite Solid()
        {
            var t = NewTex(1, 1);
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            return Keep(Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f));
        }

        static Sprite RoundedRect(int size, int radius)
        {
            var t = NewTex(size, size);
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(r - x - 0.5f, (x + 0.5f) - (size - r), 0f);
                    float dy = Mathf.Max(r - y - 0.5f, (y + 0.5f) - (size - r), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
                }
            }
            t.Apply();
            int b = Mathf.Clamp(radius, 1, size / 2 - 1);

            // 100, not 1. Image scales a sliced sprite's border by
            // canvas.referencePixelsPerUnit / sprite.pixelsPerUnit, and that default
            // reference is 100 - so a sprite made at 1 had its 18px corners drawn at
            // 1800 units, which turned every card into a lozenge.
            return Keep(Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b)));
        }

        static Sprite DiscSprite(int size)
        {
            var t = NewTex(size, size);
            float r = size * 0.5f;
            var c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1f, 1f, 1f,
                        Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c))));
            t.Apply();
            return Keep(Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f));
        }

        static Sprite RingSprite(int size, float thickness)
        {
            var t = NewTex(size, size);
            float outer = size * 0.5f;
            float inner = outer * (1f - thickness);
            var c = new Vector2(outer, outer);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f,
                        Mathf.Clamp01(Mathf.Min(outer - d, d - inner))));
                }
            t.Apply();
            return Keep(Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f));
        }

        /// <summary>
        /// A five-pointed star, drawn by testing each pixel against the polygon.
        ///
        /// The results screen used the font's asterisk, which is a punctuation mark: small,
        /// sitting on the text baseline, and nothing like the award the screen is trying
        /// to hand out.
        /// </summary>
        static Sprite StarSprite(int size)
        {
            var t = NewTex(size, size);
            var c = new Vector2(size * 0.5f, size * 0.5f);
            float outer = size * 0.48f, inner = outer * 0.42f;

            // Ten alternating points, starting at the top.
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = c + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 2x2 supersample, so the points are not jagged.
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (InPolygon(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), pts))
                                hits++;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 4f));
                }
            }
            t.Apply();
            return Keep(Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f));
        }

        // ---------- garment icons ----------

        static readonly System.Collections.Generic.Dictionary<GarmentKind, Sprite> _icons =
            new System.Collections.Generic.Dictionary<GarmentKind, Sprite>();

        /// <summary>
        /// A silhouette for each kind of laundry.
        ///
        /// Colour alone could not answer the question the player actually has - a red sock
        /// and a red shirt were the same red square, and they go to completely different
        /// places. A shape can be read at a glance and survives being small, which a word
        /// does not.
        /// </summary>
        public static Sprite GarmentIcon(GarmentKind kind)
        {
            // `cached != null` as well as TryGetValue: a dictionary happily keeps
            // handing back a sprite play mode destroyed on the way out, and an Image
            // given one of those silently draws a white square instead of a shirt.
            if (_icons.TryGetValue(kind, out var cached) && cached != null) return cached;
            var made = PolygonSprite(Outline(kind), 96);
            _icons[kind] = made;
            return made;
        }

        /// <summary>Points are 0..1 of the icon box, y up, traced in order.</summary>
        static Vector2[] Outline(GarmentKind kind)
        {
            switch (kind)
            {
                // A t-shirt: shoulders, sleeves, straight body, neck notch.
                case GarmentKind.Shirt:
                    return Pts(0.28f,0.92f, 0.12f,0.78f, 0.02f,0.64f, 0.20f,0.50f,
                               0.20f,0.06f, 0.80f,0.06f, 0.80f,0.50f, 0.98f,0.64f,
                               0.88f,0.78f, 0.72f,0.92f, 0.62f,0.82f, 0.38f,0.82f);

                // Waistband across the top, two legs below.
                case GarmentKind.Pants:
                    return Pts(0.20f,0.94f, 0.80f,0.94f, 0.80f,0.06f, 0.58f,0.06f,
                               0.52f,0.58f, 0.48f,0.58f, 0.42f,0.06f, 0.20f,0.06f);

                // A rectangle with a fringed hem, which is what makes it not a blanket.
                case GarmentKind.Towel:
                    return Pts(0.14f,0.92f, 0.86f,0.92f, 0.86f,0.20f, 0.78f,0.08f,
                               0.70f,0.20f, 0.62f,0.08f, 0.54f,0.20f, 0.46f,0.08f,
                               0.38f,0.20f, 0.30f,0.08f, 0.22f,0.20f, 0.14f,0.08f);

                // Cuff and leg, then the foot turning off to the right.
                case GarmentKind.Sock:
                    return Pts(0.26f,0.94f, 0.56f,0.94f, 0.56f,0.36f, 0.92f,0.36f,
                               0.92f,0.06f, 0.26f,0.06f);

                // A dress: narrow shoulders flaring to a wide hem.
                default:
                    return Pts(0.38f,0.94f, 0.62f,0.94f, 0.74f,0.78f, 0.60f,0.66f,
                               0.86f,0.06f, 0.14f,0.06f, 0.40f,0.66f, 0.26f,0.78f);
            }
        }

        static Vector2[] Pts(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return pts;
        }

        /// <summary>Fill a normalised polygon into a sprite, supersampled so it is not jagged.</summary>
        static Sprite PolygonSprite(Vector2[] norm, int size)
        {
            var pts = new Vector2[norm.Length];
            for (int i = 0; i < norm.Length; i++) pts[i] = norm[i] * size;

            var t = NewTex(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (InPolygon(new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f), pts))
                                hits++;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, hits / 4f));
                }
            }
            t.Apply();
            return Keep(Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f));
        }

        static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) /
                          (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        // ---------- widgets ----------

        static RectTransform Stretch(GameObject go, float inset)
        {
            // Not `??`. The null-coalescing operator uses real C# null, while a missing
            // Unity component comes back as a fake-null object that is NOT null to `??` -
            // so the missing RectTransform was kept and every access to it threw.
            // Unity's own == overload is the one that understands the difference.
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        static Image Layer(Transform parent, string name, Color color, Sprite shape, float inset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Stretch(go, inset);
            var img = go.AddComponent<Image>();
            img.sprite = shape;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// A card: outline, fill, and a drop shadow. Children added to the returned
        /// transform sit on top of the fill.
        /// </summary>
        public static RectTransform Card(Transform parent, string name, Color fill, Color outline,
                                         float border = 5f, Sprite shape = null, bool shadow = true)
        {
            shape ??= Card9;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();

            if (shadow)
            {
                var sh = Layer(root.transform, "Shadow", new Color(0.047f, 0.090f, 0.180f, 0.28f),
                               shape, 0f);
                sh.rectTransform.offsetMin = new Vector2(0f, -7f);
                sh.rectTransform.offsetMax = new Vector2(0f, -7f);
            }

            Layer(root.transform, "Outline", outline, shape, 0f);
            Layer(root.transform, "Fill", fill, shape, border);
            return rt;
        }

        /// <summary>Recolour a card built above, without rebuilding it.</summary>
        public static void SetCardColors(RectTransform card, Color fill, Color outline)
        {
            var o = card.Find("Outline");
            var f = card.Find("Fill");
            if (o != null) o.GetComponent<Image>().color = outline;
            if (f != null) f.GetComponent<Image>().color = fill;
        }

        /// <summary>A plain rounded block with no outline, for bars and chips.</summary>
        public static Image Block(Transform parent, string name, Color color, Sprite shape = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.sprite = shape ?? Pill;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                 Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Colour for a countdown: calm, then warning, then alarm.</summary>
        public static Color Remaining(float fraction)
        {
            if (fraction > 0.45f) return Blue;
            if (fraction > 0.20f) return Yellow;
            return Red;
        }

        public static float Pulse(float speed = 6f) =>
            0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
    }
}
