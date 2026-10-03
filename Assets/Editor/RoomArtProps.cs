using UnityEngine;

/// <summary>
/// The rest of the room, in the same chunky style as the machines.
///
/// Everything in here replaces something that used to be a plain cube or a photoscanned
/// model. The test each one has to pass is the same: from the gameplay camera, can you
/// tell what it is without a label?
/// </summary>
public static class RoomArtProps
{
    static Material M(Color c, float s = 0.15f, float m = 0f) => RoomArt.Mat(c, s, m);

    /// <summary>A rug, so a prop sits on something rather than on bare tiles.</summary>
    public static void Rug(Transform parent, Vector3 at, Vector2 size, Color c, float yaw)
    {
        RoomArt.Box(parent, "Rug", new Vector3(size.x, 0.045f, size.y), 0.16f,
                    new Vector3(at.x, 0.022f, at.z), M(c, 0.05f), new Vector3(0f, yaw, 0f));
        RoomArt.Box(parent, "RugBorder", new Vector3(size.x * 0.82f, 0.05f, size.y * 0.78f), 0.14f,
                    new Vector3(at.x, 0.03f, at.z), M(Color.Lerp(c, Color.white, 0.22f), 0.05f),
                    new Vector3(0f, yaw, 0f));
    }

    // ---------------------------------------------------------------- fold table

    /// <summary>
    /// Thick legs, a slightly crooked top, and the state a folding table is always in:
    /// one tidy stack, one leaning stack, and a shirt somebody gave up on.
    /// </summary>
    public static GameObject FoldTable(Transform parent, Vector3 pos, Material shade)
    {
        var go = new GameObject("FoldTable");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.transform;

        RoomArt.Box(t, "Top", new Vector3(2.8f, 0.18f, 1.6f), 0.08f,
                    new Vector3(0f, 0.88f, 0f), M(ChunkyArt.Wood, 0.18f), new Vector3(0f, 0f, 0.8f));
        RoomArt.Box(t, "Apron", new Vector3(2.6f, 0.14f, 1.4f), 0.06f,
                    new Vector3(0f, 0.76f, 0f), M(ChunkyArt.WoodDark, 0.15f));

        foreach (var lx in new[] { -1.18f, 1.18f })
        foreach (var lz in new[] { -0.6f, 0.6f })
            RoomArt.Box(t, "Leg", new Vector3(0.17f, 0.76f, 0.17f), 0.06f,
                        new Vector3(lx, 0.38f, lz), M(ChunkyArt.WoodDark, 0.15f),
                        new Vector3(lz > 0f ? 1.5f : -1.5f, 0f, lx > 0f ? -1.5f : 1.5f));

        // A tidy stack.
        for (int i = 0; i < 4; i++)
            RoomArt.Box(t, "Folded", new Vector3(0.62f, 0.11f, 0.48f), 0.05f,
                        new Vector3(-0.85f, 1.02f + i * 0.11f, 0.22f),
                        M(ChunkyArt.Laundry(i + 1), 0.2f), new Vector3(0f, i * 3f - 4f, 0f));

        // And a stack that is going to lose.
        for (int i = 0; i < 4; i++)
            RoomArt.Box(t, "Leaning", new Vector3(0.6f, 0.11f, 0.46f), 0.05f,
                        new Vector3(0.72f + i * 0.055f, 1.02f + i * 0.105f, -0.18f),
                        M(ChunkyArt.Laundry(i + 3), 0.2f), new Vector3(0f, 8f + i * 5f, 6f + i * 2.5f));

        // The shirt over the edge is the silhouette that says "table with laundry on it"
        // rather than "table with boxes on it".
        RoomArt.Lump(t, "DrapedShirt", new Vector3(0.75f, 0.2f, 0.52f),
                     new Vector3(-0.1f, 0.98f, -0.68f), M(ChunkyArt.Laundry(0), 0.2f));
        RoomArt.Box(t, "Sleeve", new Vector3(0.21f, 0.68f, 0.21f), 0.09f,
                    new Vector3(-0.32f, 0.72f, -0.86f), M(ChunkyArt.Laundry(0), 0.2f),
                    new Vector3(22f, 0f, 14f));

        // An abandoned mug.
        RoomArt.Box(t, "Mug", new Vector3(0.24f, 0.26f, 0.24f), 0.09f,
                    new Vector3(1.08f, 1.1f, 0.48f), M(ChunkyArt.Mint, 0.35f));
        RoomArt.Box(t, "MugHandle", new Vector3(0.09f, 0.16f, 0.09f), 0.04f,
                    new Vector3(1.24f, 1.1f, 0.48f), M(ChunkyArt.Mint, 0.35f));

        RoomArt.Shadow(parent, pos, 1.5f, 0.95f, shade);
        return go;
    }

    // ------------------------------------------------------------------- closet

    /// <summary>Rounded cupboard in faded mint, with one door that will not quite shut.</summary>
    public static GameObject Closet(Transform parent, Vector3 pos, Material shade)
    {
        var go = new GameObject("Closet");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.transform;

        var body = M(ChunkyArt.Mint, 0.18f);
        var door = M(Color.Lerp(ChunkyArt.Mint, Color.white, 0.25f), 0.22f);

        RoomArt.Box(t, "Carcass", new Vector3(1.15f, 2.2f, 2.5f), 0.16f,
                    new Vector3(0f, 1.1f, 0f), body);
        RoomArt.Box(t, "Plinth", new Vector3(1.2f, 0.18f, 2.55f), 0.07f,
                    new Vector3(0f, 0.09f, 0f), M(ChunkyArt.WoodDark, 0.15f));

        // The left door shuts. The right one does not, and there is a sleeve explaining why.
        RoomArt.Box(t, "DoorShut", new Vector3(0.12f, 1.9f, 1.15f), 0.06f,
                    new Vector3(-0.58f, 1.18f, 0.62f), door);
        RoomArt.Box(t, "DoorAjar", new Vector3(0.12f, 1.9f, 1.15f), 0.06f,
                    new Vector3(-0.62f, 1.18f, -0.68f), door, new Vector3(0f, -11f, 0f));
        RoomArt.Lump(t, "TrappedSleeve", new Vector3(0.18f, 0.5f, 0.16f),
                     new Vector3(-0.70f, 0.8f, -0.9f), M(ChunkyArt.Laundry(4), 0.2f),
                     new Vector3(0f, 0f, 18f));

        foreach (var dz in new[] { 0.2f, -1.1f })
            RoomArt.Box(t, "Handle", new Vector3(0.09f, 0.3f, 0.09f), 0.04f,
                        new Vector3(-0.68f, 1.18f, dz), M(ChunkyArt.Chrome, 0.7f, 0.5f));

        RoomArt.Shadow(parent, pos, 0.75f, 1.4f, shade);
        return go;
    }

    // ----------------------------------------------------------- sorting basket

    /// <summary>
    /// What used to be a kitchen chair in the middle of the floor.
    ///
    /// Mechanically this is the dumping ground, so it should look like one: a big
    /// sorting basket on a worn rug, already losing. A chair read as a chair - a thing
    /// to sit on, in a room where nobody sits.
    /// </summary>
    public static GameObject SortingBasket(Transform parent, Vector3 pos, Material shade)
    {
        var go = new GameObject("TheChair");    // name kept: Chair.cs and the scene expect it
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.transform;

        Rug(t, Vector3.zero, new Vector2(2.6f, 2.2f), new Color(0.80f, 0.60f, 0.52f), 9f);

        var shell = M(ChunkyArt.Lavender, 0.25f);
        var rim = M(Color.Lerp(ChunkyArt.Lavender, Color.white, 0.3f), 0.3f);
        var hole = M(Color.Lerp(ChunkyArt.Lavender, ChunkyArt.Charcoal, 0.5f), 0.1f);

        RoomArt.Box(t, "Tub", new Vector3(1.35f, 0.78f, 1.12f), 0.2f,
                    new Vector3(0f, 0.44f, 0f), shell, new Vector3(0f, 7f, 0f));
        RoomArt.Box(t, "Rim", new Vector3(1.48f, 0.15f, 1.25f), 0.07f,
                    new Vector3(0f, 0.82f, 0f), rim, new Vector3(0f, 7f, 0f));

        foreach (var hx in new[] { -0.38f, 0f, 0.38f })
            RoomArt.Box(t, "Vent", new Vector3(0.2f, 0.26f, 0.06f), 0.08f,
                        new Vector3(hx, 0.44f, -0.58f), hole, new Vector3(0f, 7f, 0f));

        // Already overflowing. An empty tub in the middle of the floor is just as
        // confusing as the chair was - it has to look like the place things get dumped.
        for (int i = 0; i < 7; i++)
        {
            float a = i * 0.95f;
            RoomArt.Lump(t, "Dumped",
                         new Vector3(0.56f + (i % 3) * 0.08f, 0.3f, 0.46f),
                         new Vector3(Mathf.Cos(a) * 0.34f, 0.86f + (i % 3) * 0.12f,
                                     Mathf.Sin(a) * 0.28f),
                         M(ChunkyArt.Laundry(i), 0.2f),
                         new Vector3(0f, a * 36f, (i % 2 == 0 ? 11f : -8f)));
        }

        // Spilling over the side, so it reads as too full rather than merely full.
        RoomArt.Lump(t, "Spilling", new Vector3(0.5f, 0.26f, 0.4f),
                     new Vector3(-0.78f, 0.44f, 0.28f), M(ChunkyArt.Laundry(3), 0.2f),
                     new Vector3(0f, 24f, 38f));
        RoomArt.Box(t, "Sleeve", new Vector3(0.2f, 0.74f, 0.2f), 0.09f,
                    new Vector3(0.72f, 0.72f, -0.2f), M(ChunkyArt.Laundry(1), 0.2f),
                    new Vector3(14f, 0f, 42f));

        RoomArt.Shadow(parent, pos, 0.95f, 0.82f, shade);
        return go;
    }

    // -------------------------------------------------------------- sock drawer

    public static GameObject SockDrawer(Transform parent, Vector3 pos, Material shade)
    {
        var go = new GameObject("SockDrawer");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.transform;

        var body = M(ChunkyArt.Lavender, 0.2f);
        var front = M(Color.Lerp(ChunkyArt.Lavender, ChunkyArt.Charcoal, 0.22f), 0.25f);

        RoomArt.Box(t, "Carcass", new Vector3(1.95f, 0.95f, 1.25f), 0.14f,
                    new Vector3(0f, 0.5f, 0f), body);
        RoomArt.Box(t, "Top", new Vector3(2.05f, 0.12f, 1.35f), 0.06f,
                    new Vector3(0f, 1.02f, 0f), M(ChunkyArt.Wood, 0.2f));

        // The lower drawer is open, with socks escaping. That is the whole station.
        RoomArt.Box(t, "DrawerShut", new Vector3(1.75f, 0.32f, 0.1f), 0.05f,
                    new Vector3(0f, 0.72f, -0.64f), front);
        RoomArt.Box(t, "DrawerOpen", new Vector3(1.75f, 0.32f, 0.5f), 0.07f,
                    new Vector3(0f, 0.3f, -0.84f), front);
        foreach (var y in new[] { 0.72f, 0.3f })
            RoomArt.Box(t, "Handle", new Vector3(0.7f, 0.09f, 0.09f), 0.04f,
                        new Vector3(0f, y, y > 0.5f ? -0.71f : -1.1f),
                        M(ChunkyArt.Chrome, 0.7f, 0.5f));

        for (int i = 0; i < 4; i++)
            RoomArt.Lump(t, "Sock", new Vector3(0.34f, 0.17f, 0.24f),
                         new Vector3(-0.5f + i * 0.36f, 0.46f, -0.86f),
                         M(ChunkyArt.Laundry(i), 0.2f), new Vector3(0f, i * 24f - 30f, 14f));

        RoomArt.Shadow(parent, pos, 1.1f, 0.78f, shade);
        return go;
    }

    // ------------------------------------------------------------- the jokes

    /// <summary>One enormous pair of underwear, pegged up where everyone can see it.</summary>
    public static void GiantUnderwear(Transform parent, Vector3 at, float yaw)
    {
        var go = new GameObject("GiantUnderwear");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = go.transform;

        var cloth = M(new Color(0.98f, 0.95f, 0.98f), 0.1f);
        RoomArt.Box(t, "Waist", new Vector3(0.1f, 0.22f, 1.5f), 0.1f, new Vector3(0f, 0f, 0f),
                    M(new Color(0.55f, 0.62f, 0.82f), 0.15f));
        RoomArt.Box(t, "Body", new Vector3(0.1f, 0.95f, 1.42f), 0.26f,
                    new Vector3(0f, -0.55f, 0f), cloth);
        RoomArt.Box(t, "LegL", new Vector3(0.1f, 0.3f, 0.5f), 0.14f,
                    new Vector3(0f, -1.02f, -0.42f), cloth);
        RoomArt.Box(t, "LegR", new Vector3(0.1f, 0.3f, 0.5f), 0.14f,
                    new Vector3(0f, -1.02f, 0.42f), cloth);
        RoomArt.Box(t, "Spot", new Vector3(0.04f, 0.2f, 0.2f), 0.09f,
                    new Vector3(-0.06f, -0.5f, 0.3f), M(ChunkyArt.Laundry(0), 0.15f));
    }

    /// <summary>A lint ball that has been here long enough to develop opinions.</summary>
    public static void LintBall(Transform parent, Vector3 at, float size, bool withEyes)
    {
        var go = new GameObject(withEyes ? "LintBallAlive" : "LintBall");
        go.transform.SetParent(parent, false);
        // localPosition, not position. LintCorner passes offsets relative to its own
        // root, and setting world position threw the dryer's lint into the middle of
        // the floor, where it sat on the sorting basket's rug looking like spilt porridge.
        go.transform.localPosition = at;
        var t = go.transform;

        var fuzz = M(new Color(0.82f, 0.78f, 0.70f), 0.04f);
        RoomArt.Lump(t, "Fuzz", new Vector3(size, size * 0.78f, size * 0.9f),
                     new Vector3(0f, size * 0.4f, 0f), fuzz);
        RoomArt.Lump(t, "Fuzz", new Vector3(size * 0.7f, size * 0.55f, size * 0.62f),
                     new Vector3(size * 0.42f, size * 0.26f, -size * 0.2f), fuzz);
        RoomArt.Lump(t, "Fuzz", new Vector3(size * 0.6f, size * 0.5f, size * 0.55f),
                     new Vector3(-size * 0.38f, size * 0.24f, size * 0.26f), fuzz);

        if (withEyes)
        {
            var white = M(new Color(0.98f, 0.98f, 0.96f), 0.3f);
            var pupil = M(new Color(0.08f, 0.07f, 0.1f), 0.3f);
            foreach (var ex in new[] { -size * 0.17f, size * 0.17f })
            {
                RoomArt.Lump(t, "Eye", Vector3.one * size * 0.22f,
                             new Vector3(ex, size * 0.5f, -size * 0.42f), white);
                RoomArt.Lump(t, "Pupil", Vector3.one * size * 0.1f,
                             new Vector3(ex, size * 0.5f, -size * 0.52f), pupil);
            }
        }
    }

    /// <summary>Oversized socks dropped in a line, leading the eye to the Monster.</summary>
    public static void SockTrail(Transform parent, Vector3 from, Vector3 to, int count, int seed)
    {
        var go = new GameObject("SockTrail");
        go.transform.SetParent(parent, false);
        var t = go.transform;

        for (int i = 0; i < count; i++)
        {
            float f = (i + 0.5f) / count;
            var p = Vector3.Lerp(from, to, f);
            float wobble = Mathf.Sin(f * 7f + seed) * 0.42f;
            var side = Vector3.Cross(Vector3.up, (to - from).normalized);
            p += side * wobble;

            RoomArt.Lump(t, "BigSock", new Vector3(0.52f, 0.2f, 0.34f),
                         new Vector3(p.x, 0.1f, p.z), M(ChunkyArt.Laundry(seed + i), 0.2f),
                         new Vector3(0f, f * 160f + seed * 40f, 9f));
            RoomArt.Box(t, "SockCuff", new Vector3(0.2f, 0.16f, 0.3f), 0.07f,
                        new Vector3(p.x + 0.22f, 0.12f, p.z - 0.08f),
                        M(ChunkyArt.Cream, 0.2f), new Vector3(0f, f * 160f + seed * 40f, 9f));
        }
    }

    /// <summary>The dryer corner: fuzz, and a bin that lost.</summary>
    public static void LintCorner(Transform parent, Vector3 at, Material shade)
    {
        var go = new GameObject("LintCorner");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var t = go.transform;

        RoomArt.Box(t, "Bin", new Vector3(0.78f, 0.86f, 0.78f), 0.16f,
                    new Vector3(0f, 0.43f, 0f), M(new Color(0.58f, 0.64f, 0.62f), 0.2f),
                    new Vector3(0f, 14f, 3f));
        RoomArt.Box(t, "BinRim", new Vector3(0.88f, 0.12f, 0.88f), 0.06f,
                    new Vector3(0f, 0.88f, 0f), M(new Color(0.70f, 0.75f, 0.73f), 0.25f),
                    new Vector3(0f, 14f, 3f));

        LintBall(t, new Vector3(0.05f, 0.82f, 0f), 0.46f, false);
        LintBall(t, new Vector3(0.62f, 0f, -0.5f), 0.4f, false);
        LintBall(t, new Vector3(-0.72f, 0f, 0.3f), 0.34f, true);   // this one is watching

        RoomArt.Shadow(parent, at, 0.62f, 0.6f, shade);
    }

    /// <summary>Lost and found: one boot, somebody's pants, and a bear nobody claimed.</summary>
    public static void LostAndFound(Transform parent, Vector3 at, Material shade)
    {
        var go = new GameObject("LostAndFound");
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        var t = go.transform;

        var shell = M(new Color(0.92f, 0.72f, 0.34f), 0.22f);
        RoomArt.Box(t, "Tub", new Vector3(1.15f, 0.68f, 0.95f), 0.18f,
                    new Vector3(0f, 0.36f, 0f), shell, new Vector3(0f, -12f, 0f));
        RoomArt.Box(t, "Rim", new Vector3(1.26f, 0.13f, 1.06f), 0.06f,
                    new Vector3(0f, 0.7f, 0f), M(Color.Lerp(shell.color, Color.white, 0.3f), 0.3f),
                    new Vector3(0f, -12f, 0f));

        // One boot, sole up.
        RoomArt.Box(t, "Boot", new Vector3(0.28f, 0.52f, 0.3f), 0.1f,
                    new Vector3(-0.3f, 0.86f, 0.1f), M(new Color(0.36f, 0.27f, 0.22f), 0.3f),
                    new Vector3(0f, 20f, 26f));
        RoomArt.Box(t, "BootSole", new Vector3(0.3f, 0.1f, 0.34f), 0.04f,
                    new Vector3(-0.42f, 1.08f, 0.14f), M(new Color(0.2f, 0.18f, 0.16f), 0.2f),
                    new Vector3(0f, 20f, 26f));

        // A bear, resigned.
        var bear = M(new Color(0.78f, 0.58f, 0.38f), 0.15f);
        RoomArt.Lump(t, "BearBody", new Vector3(0.34f, 0.34f, 0.3f),
                     new Vector3(0.26f, 0.86f, -0.1f), bear);
        RoomArt.Lump(t, "BearHead", new Vector3(0.26f, 0.24f, 0.24f),
                     new Vector3(0.3f, 1.1f, -0.16f), bear);
        foreach (var ex in new[] { -0.08f, 0.08f })
            RoomArt.Lump(t, "BearEar", Vector3.one * 0.11f,
                         new Vector3(0.3f + ex, 1.22f, -0.14f), bear);

        RoomArt.Lump(t, "StraySmalls", new Vector3(0.4f, 0.16f, 0.28f),
                     new Vector3(0.0f, 0.78f, 0.34f), M(ChunkyArt.Laundry(2), 0.2f),
                     new Vector3(0f, 32f, 10f));

        RoomArt.Shadow(parent, at, 0.78f, 0.68f, shade);
    }
}
