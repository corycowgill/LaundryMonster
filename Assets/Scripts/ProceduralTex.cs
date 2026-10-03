using System.Collections.Generic;
using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Textures generated at runtime. Same rule as the meshes and the audio: the
    /// project imports no art, so everything here is built from code and cached.
    /// </summary>
    public static class ProceduralTex
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D Get(string key, int size, System.Func<int, int, Color> fill,
                             FilterMode filter = FilterMode.Bilinear)
        {
            Texture2D cached;
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.filterMode = filter;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.hideFlags = HideFlags.HideAndDontSave;

            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = fill(x, y);

            tex.SetPixels(px);
            tex.Apply(true);
            Cache[key] = tex;
            return tex;
        }

        /// <summary>Lino floor tiles with grout lines and a bit of grime per tile.</summary>
        public static Texture2D FloorTile()
        {
            return Get("floor", 128, (x, y) =>
            {
                const int size = 128;
                const int cells = 2;
                int cell = size / cells;

                int cx = x % cell, cy = y % cell;
                int tileX = x / cell, tileY = y / cell;

                // Grout, two pixels instead of three and close to the tile in value.
                // At 0.26 against a 0.59 tile it was the strongest edge on screen, and
                // the floor read as a grid the player had to look past.
                if (cx < 2 || cy < 2)
                    return new Color(0.58f, 0.53f, 0.47f);

                // Alternating tiles, each with its own faint tint so the floor is not flat.
                bool alt = ((tileX + tileY) & 1) == 0;
                float basis = alt ? 0.80f : 0.76f;

                float grain = Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.045f;
                float blotch = Mathf.PerlinNoise(x * 0.04f + tileX, y * 0.04f + tileY) * 0.04f;
                float v = basis + grain + blotch - 0.03f;

                // Warm, so the floor belongs to the same room as the cream walls.
                return new Color(v, v * 0.965f, v * 0.915f);
            });
        }

        /// <summary>Painted breeze-block wall, slightly mottled.</summary>
        public static Texture2D Wall()
        {
            return Get("wall", 128, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.07f;
                float streak = Mathf.PerlinNoise(x * 0.015f, y * 0.4f) * 0.04f;
                float v = 0.70f + n + streak - 0.05f;
                return new Color(v, v * 0.985f, v * 0.95f);
            });
        }

        /// <summary>Brushed metal for machine bodies.</summary>
        public static Texture2D Brushed()
        {
            return Get("brushed", 128, (x, y) =>
            {
                float brush = Mathf.PerlinNoise(x * 1.6f, y * 0.06f) * 0.05f;
                float v = 0.93f + brush - 0.025f;
                return new Color(v, v, v);
            });
        }

        /// <summary>Woven plastic, for the hamper and baskets.</summary>
        public static Texture2D Weave()
        {
            return Get("weave", 64, (x, y) =>
            {
                bool warp = ((x / 4) & 1) == 0;
                bool weft = ((y / 4) & 1) == 0;
                float v = warp ^ weft ? 0.95f : 0.78f;
                v += Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.05f;
                return new Color(v, v, v);
            }, FilterMode.Point);
        }

        /// <summary>Soft radial disc, used for the highlight ring under the active station.</summary>
        public static Texture2D Ring()
        {
            return Get("ring", 128, (x, y) =>
            {
                float dx = (x / 127f) - 0.5f;
                float dy = (y / 127f) - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;     // 0 centre, 1 edge

                // A band near the rim, fading both ways.
                float band = 1f - Mathf.Abs(d - 0.82f) / 0.18f;
                band = Mathf.Clamp01(band);
                if (d > 1f) band = 0f;

                return new Color(1f, 1f, 1f, band * band);
            });
        }
    }
}
