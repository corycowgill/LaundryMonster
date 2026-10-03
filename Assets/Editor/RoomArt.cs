using UnityEngine;
using LaundryMonster;

/// <summary>
/// The props, all built from ChunkyArt's one rounded box.
///
/// Two rules carried over from the readability work, which this must not spend:
/// nothing sits within 1.6m of a station (that is the radius of the highlight pad, and
/// the pads are how the player knows where to take what they are carrying), and the
/// room surfaces stay low-chroma so the laundry owns the strong colours.
///
/// One rule of its own: every pile needs a silhouette. A heap of blobs reads as a heap
/// of blobs at gameplay zoom; the same heap with one sleeve sticking out of it reads as
/// clothes. Every cluster here has at least one thing poking out of it.
/// </summary>
public static class RoomArt
{
    // ---------- building blocks ----------

    public static Material Mat(Color c, float smoothness = 0.15f, float metallic = 0f)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var shader = rp != null && rp.defaultMaterial != null
            ? rp.defaultMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");

        var m = new Material(shader) { color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        return m;
    }

    /// <summary>A rounded-box prop.</summary>
    public static GameObject Box(Transform parent, string name, Vector3 size, float radius,
                                 Vector3 pos, Material mat, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = ChunkyArt.RoundedBox(size, radius);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>A soft lump: laundry, lint, bubbles, anything that is not manufactured.</summary>
    public static GameObject Lump(Transform parent, string name, Vector3 size,
                                  Vector3 pos, Material mat, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = ChunkyArt.Blob(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Disc(Transform parent, string name, Vector3 pos, float rx, float rz,
                           float yaw, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = new Vector3(rx, 0.005f, rz);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>
    /// A contact shadow, so a prop sits ON the floor instead of hovering a centimetre
    /// above it. Three overlapping discs rather than one, because a perfect ellipse
    /// under a lumpy pile is the thing that gives a fake shadow away.
    /// </summary>
    public static void Shadow(Transform parent, Vector3 at, float rx, float rz, Material shade)
    {
        Disc(parent, "Shadow", new Vector3(at.x, 0.008f, at.z), rx, rz, 0f, shade);
        Disc(parent, "Shadow", new Vector3(at.x + rx * 0.22f, 0.008f, at.z - rz * 0.18f),
             rx * 0.72f, rz * 0.68f, 24f, shade);
        Disc(parent, "Shadow", new Vector3(at.x - rx * 0.2f, 0.008f, at.z + rz * 0.2f),
             rx * 0.66f, rz * 0.7f, -31f, shade);
    }

    // ---------- the machines ----------

    /// <summary>
    /// A washing machine with an oversized door, a thick wonky knob and a bulging body.
    ///
    /// The two buttons and the big round door sit where eyes and a mouth would, which is
    /// all the face a background prop needs - the strong expressions belong to the
    /// Monster, and four machines pulling faces would compete with it.
    /// </summary>
    public static GameObject Machine(Transform parent, string name, Vector3 pos,
                                     LaundryMachine.Mode mode, int index)
    {
        bool washer = mode == LaundryMachine.Mode.Washer;

        var shell = Mat(washer
            ? (index % 2 == 0 ? ChunkyArt.Turquoise : ChunkyArt.SkyBlue)
            : (index % 2 == 0 ? ChunkyArt.Coral : ChunkyArt.Sunny), 0.22f);
        var shellDark = Mat(Color.Lerp(shell.color, ChunkyArt.Charcoal, 0.30f), 0.18f);
        var cream = Mat(ChunkyArt.Cream, 0.20f);
        var chrome = Mat(ChunkyArt.Chrome, 0.75f, 0.6f);
        var charcoal = Mat(ChunkyArt.Charcoal, 0.3f);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var t = go.transform;

        // Body. A big radius on a box this size is what makes it read as moulded.
        Box(t, "Body", new Vector3(1.52f, 1.34f, 1.26f), 0.26f, new Vector3(0f, 0.70f, 0f), shell);

        // Scuffed lower panel - wear concentrated at the base, where feet and baskets hit.
        Box(t, "Kick", new Vector3(1.54f, 0.30f, 1.28f), 0.12f, new Vector3(0f, 0.17f, 0f), shellDark);

        // Control panel, sitting proud of the top.
        Box(t, "Panel", new Vector3(1.46f, 0.30f, 1.14f), 0.12f, new Vector3(0f, 1.46f, 0f), cream);
        Box(t, "Display", new Vector3(0.52f, 0.15f, 0.06f), 0.05f,
            new Vector3(0.24f, 1.48f, -0.60f), charcoal);

        // The wonky knob. Nothing in this room is quite straight.
        var knob = Box(t, "Knob", new Vector3(0.26f, 0.26f, 0.14f), 0.11f,
                       new Vector3(-0.46f, 1.47f, -0.60f), Mat(ChunkyArt.Coral, 0.3f),
                       new Vector3(0f, 0f, 17f));
        Box(knob.transform, "KnobMark", new Vector3(0.05f, 0.11f, 0.04f), 0.02f,
            new Vector3(0f, 0.06f, -0.06f), cream);

        // Two buttons, which from the front are a pair of eyes.
        Box(t, "Button", new Vector3(0.12f, 0.12f, 0.08f), 0.05f,
            new Vector3(-0.08f, 1.49f, -0.59f), Mat(ChunkyArt.Mint, 0.4f));
        Box(t, "Button", new Vector3(0.12f, 0.12f, 0.08f), 0.05f,
            new Vector3(0.62f, 1.49f, -0.59f), Mat(ChunkyArt.Mint, 0.4f));

        // The oversized door. This is the machine's defining feature, so it is almost as
        // wide as the machine - at gameplay zoom a modest door just reads as a smudge.
        Box(t, "DoorRim", new Vector3(1.06f, 1.06f, 0.14f), 0.50f,
            new Vector3(0f, 0.70f, -0.60f), cream);
        var glass = Mat(washer
            ? new Color(0.13f, 0.26f, 0.34f)
            : new Color(0.55f, 0.32f, 0.14f), 0.85f);
        Box(t, "DoorGlass", new Vector3(0.80f, 0.80f, 0.10f), 0.38f,
            new Vector3(0f, 0.70f, -0.66f), glass);

        if (washer)
        {
            // A sock pressed flat against the inside of the glass, which is the single
            // most recognisable thing a washing machine can be doing.
            Lump(t, "SockOnGlass", new Vector3(0.34f, 0.20f, 0.06f),
                 new Vector3(0.14f, 0.82f, -0.70f), Mat(ChunkyArt.Laundry(index), 0.25f),
                 new Vector3(0f, 0f, 28f));
            // Detergent drawer, with a dried drip under it.
            Box(t, "Drawer", new Vector3(0.46f, 0.14f, 0.08f), 0.05f,
                new Vector3(0.44f, 1.27f, -0.62f), cream);
            Box(t, "Drip", new Vector3(0.10f, 0.26f, 0.03f), 0.04f,
                new Vector3(0.44f, 1.12f, -0.635f), Mat(new Color(0.76f, 0.82f, 0.80f), 0.5f));
        }
        else
        {
            // Warm light through the dryer door, and fuzz caught around the filter.
            Box(t, "Warmth", new Vector3(0.56f, 0.56f, 0.06f), 0.27f,
                new Vector3(0f, 0.70f, -0.70f), Mat(new Color(1f, 0.78f, 0.42f), 0.6f));
            var fuzz = Mat(new Color(0.88f, 0.84f, 0.76f), 0.05f);
            Lump(t, "Lint", new Vector3(0.26f, 0.14f, 0.12f), new Vector3(-0.42f, 1.26f, -0.60f), fuzz);
            Lump(t, "Lint", new Vector3(0.18f, 0.11f, 0.10f), new Vector3(-0.26f, 1.22f, -0.62f), fuzz);
        }

        // Feet, and one of them is a folded shim, because of course it is.
        foreach (var fx in new[] { -0.52f, 0.52f })
        foreach (var fz in new[] { -0.44f, 0.44f })
            Box(t, "Foot", new Vector3(0.18f, 0.1f, 0.18f), 0.04f,
                new Vector3(fx, 0.05f, fz), charcoal);

        return go;
    }

    // ---------- clusters ----------

    /// <summary>A basket with a thick rim, big vents, and laundry climbing out of it.</summary>
    public static void Basket(Transform parent, Vector3 at, float yaw, Color body, int seed,
                              bool overflowing, Material shade)
    {
        var root = new GameObject("Basket");
        root.transform.SetParent(parent, false);
        root.transform.position = at;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var t = root.transform;

        var shell = Mat(body, 0.25f);
        var rim = Mat(Color.Lerp(body, Color.white, 0.3f), 0.3f);
        var hole = Mat(Color.Lerp(body, ChunkyArt.Charcoal, 0.55f), 0.1f);

        Box(t, "Tub", new Vector3(1.05f, 0.62f, 0.86f), 0.17f, new Vector3(0f, 0.33f, 0f), shell);
        Box(t, "Rim", new Vector3(1.16f, 0.13f, 0.97f), 0.06f, new Vector3(0f, 0.63f, 0f), rim);

        // Big ventilation holes. Small ones vanish at this zoom.
        foreach (var hx in new[] { -0.28f, 0.0f, 0.28f })
            Box(t, "Vent", new Vector3(0.17f, 0.21f, 0.05f), 0.07f,
                new Vector3(hx, 0.34f, -0.44f), hole);

        if (overflowing)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.27f;
                Lump(t, "Washing",
                     new Vector3(0.44f + (i % 3) * 0.07f, 0.26f, 0.38f),
                     new Vector3(Mathf.Cos(a) * 0.26f, 0.68f + (i % 3) * 0.10f, Mathf.Sin(a) * 0.2f),
                     Mat(ChunkyArt.Laundry(seed + i), 0.2f),
                     new Vector3(0f, a * 30f, (i % 2 == 0 ? 12f : -9f)));
            }

            // The silhouette: a sleeve over the rim, so the heap reads as clothes.
            Box(t, "Sleeve", new Vector3(0.17f, 0.62f, 0.17f), 0.08f,
                new Vector3(0.56f, 0.50f, 0.1f), Mat(ChunkyArt.Laundry(seed + 2), 0.2f),
                new Vector3(8f, 0f, 34f));
            Box(t, "Cuff", new Vector3(0.20f, 0.14f, 0.20f), 0.07f,
                new Vector3(0.74f, 0.26f, 0.14f), Mat(ChunkyArt.Cream, 0.2f),
                new Vector3(8f, 0f, 34f));
        }

        Shadow(parent, at, 0.78f, 0.68f, shade);
    }

    /// <summary>A heap of washing on the floor, with something always sticking out.</summary>
    public static void Pile(Transform parent, Vector3 at, float size, int seed, Material shade)
    {
        var root = new GameObject("Pile");
        root.transform.SetParent(parent, false);
        root.transform.position = at;
        var t = root.transform;

        for (int i = 0; i < 6; i++)
        {
            float a = i * 1.05f + seed;
            Lump(t, "Washing",
                 new Vector3(0.52f * size, 0.24f * size, 0.44f * size),
                 new Vector3(Mathf.Cos(a) * 0.3f * size, 0.11f * size + (i % 3) * 0.1f * size,
                             Mathf.Sin(a) * 0.26f * size),
                 Mat(ChunkyArt.Laundry(seed + i), 0.2f),
                 new Vector3(0f, a * 40f, (i % 2 == 0 ? 10f : -12f)));
        }

        // A trouser leg out of the side of it.
        Box(t, "Leg", new Vector3(0.18f * size, 0.72f * size, 0.18f * size), 0.08f * size,
            new Vector3(0.42f * size, 0.22f * size, -0.22f * size),
            Mat(ChunkyArt.Laundry(seed + 3), 0.2f), new Vector3(72f, 18f, 0f));

        Shadow(parent, at, 0.82f * size, 0.7f * size, shade);
    }

    /// <summary>Broad handles, giant caps, bold colour blocks.</summary>
    public static GameObject DetergentShelf(Transform parent, Vector3 at, Material shade)
    {
        var root = new GameObject("DetergentShelf");
        root.transform.SetParent(parent, false);
        root.transform.position = at;
        var t = root.transform;

        Box(t, "Plank", new Vector3(2.3f, 0.13f, 0.46f), 0.06f, Vector3.zero,
            Mat(ChunkyArt.Wood, 0.2f));
        foreach (var bx in new[] { -0.85f, 0.85f })
            Box(t, "Bracket", new Vector3(0.12f, 0.26f, 0.34f), 0.05f,
                new Vector3(bx, -0.18f, 0.06f), Mat(ChunkyArt.WoodDark, 0.2f));

        // Three bottles, each one a body, a band and a cap you can see from across a room.
        var bottle = new (float x, Color body, Color cap, float h)[]
        {
            (-0.72f, ChunkyArt.Laundry(2), ChunkyArt.Sunny, 0.46f),
            (-0.18f, ChunkyArt.Laundry(0), ChunkyArt.Cream, 0.38f),
            (0.42f, ChunkyArt.Mint, ChunkyArt.Coral, 0.50f),
        };
        foreach (var (x, body, cap, h) in bottle)
        {
            Box(t, "Bottle", new Vector3(0.34f, h, 0.28f), 0.09f,
                new Vector3(x, 0.07f + h * 0.5f, 0f), Mat(body, 0.35f));
            Box(t, "Band", new Vector3(0.36f, 0.12f, 0.30f), 0.05f,
                new Vector3(x, 0.07f + h * 0.42f, 0f), Mat(ChunkyArt.Cream, 0.3f));
            Box(t, "Cap", new Vector3(0.22f, 0.15f, 0.22f), 0.07f,
                new Vector3(x, 0.09f + h + 0.05f, 0f), Mat(cap, 0.4f));
            // The broad handle, which is what makes a box read as a bottle.
            Box(t, "Handle", new Vector3(0.09f, 0.24f, 0.1f), 0.04f,
                new Vector3(x + 0.21f, 0.07f + h * 0.68f, 0f), Mat(body, 0.35f));
        }

        // A measuring cup nobody put back.
        Box(t, "MeasuringCup", new Vector3(0.26f, 0.2f, 0.26f), 0.09f,
            new Vector3(0.93f, 0.17f, -0.02f), Mat(ChunkyArt.Cream, 0.4f),
            new Vector3(0f, 22f, 13f));

        return root;
    }

    /// <summary>
    /// A spill: irregular, glossy, with bubbles. Deliberately unlike the interaction
    /// pads - those are flat discs of flat colour, and a spill that looked like one
    /// would be a lie about where the player should go.
    /// </summary>
    public static void Spill(Transform parent, Vector3 at, float size, Color tint)
    {
        var root = new GameObject("Spill");
        root.transform.SetParent(parent, false);
        root.transform.position = new Vector3(at.x, 0f, at.z);
        var t = root.transform;

        var wet = Mat(tint, 0.9f);
        Disc(t, "Pool", new Vector3(0f, 0.012f, 0f), 1.0f * size, 0.78f * size, 12f, wet);
        Disc(t, "Pool", new Vector3(0.34f * size, 0.012f, -0.2f * size),
             0.62f * size, 0.5f * size, -28f, wet);
        Disc(t, "Pool", new Vector3(-0.3f * size, 0.012f, 0.22f * size),
             0.5f * size, 0.42f * size, 40f, wet);
        Disc(t, "Pool", new Vector3(0.1f * size, 0.012f, 0.4f * size),
             0.3f * size, 0.26f * size, 5f, wet);

        var foam = Mat(Color.Lerp(tint, Color.white, 0.55f), 0.8f);
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.3f;
            Lump(t, "Bubble", Vector3.one * (0.07f + (i % 3) * 0.035f) * size,
                 new Vector3(Mathf.Cos(a) * 0.42f * size, 0.04f, Mathf.Sin(a) * 0.34f * size), foam);
        }
    }
}
