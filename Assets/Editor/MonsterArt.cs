using UnityEngine;
using LaundryMonster;

/// <summary>
/// The Monster: a mountain of dirty laundry that has developed a personality.
///
/// It was the last photoscanned object left in the room, and the worst one to leave -
/// it is the biggest thing on screen, the thing the whole game is named after, and it
/// was a smooth grey lump standing in a hand-built cartoon laundry. Everything here is
/// the same rounded box as the machines and the baskets, at a much larger size.
///
/// Two jobs beyond looking right:
///
/// 1. It has to read as LAUNDRY rather than as a generic monster, so the silhouette is
///    broken by things you can name - a trouser leg, a sleeve, a towel over one
///    shoulder, a lone boot half swallowed. Its colours are the room's laundry palette
///    pushed grubby, so a garment on the Monster is visibly dirtier than the same
///    garment in the player's arms.
///
/// 2. It has to telegraph the one thing it does on purpose. Past 45% anger it reaches
///    over and takes washing back off the sorting basket, and until now that was
///    communicated by a line of text and a four-degree lean. There is an arm now, built
///    pointing at the basket, which MonsterAnimator stretches out over the three second
///    warning. The threat is visible from across the room without reading anything.
///
/// Local space matters here: MonsterAnimator scales the root every frame, so the mesh
/// runs y 0..1 with its feet at y 0 and is sized by the root. MonsterFace hangs eyes off
/// the root at fixed local offsets, which is why the face is a documented position
/// rather than wherever a lump happened to land.
/// </summary>
public static class MonsterArt
{
    static Material M(Color c, float s = 0.15f) => RoomArt.Mat(c, s);

    /// <summary>
    /// Laundry that has been in the pile too long: the room palette, darkened and
    /// drained. The player's clean washing uses the bright version of the same colours,
    /// so "dirty" and "clean" are the same hue at two different lives.
    /// </summary>
    static Color Grubby(int i)
    {
        var c = ChunkyArt.Laundry(i);
        float grey = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        return Color.Lerp(c, new Color(grey, grey, grey) * 0.82f, 0.42f) * 0.86f;
    }

    /// <summary>The face, in local units. MonsterFace reads these.</summary>
    public const float FaceY = 0.82f;
    public const float FaceZ = -0.30f;
    public const float EyeX = 0.19f;

    public static GameObject Build(Transform parent)
    {
        var root = new GameObject("Body");
        root.transform.SetParent(parent, false);
        var t = root.transform;

        // ---- the heap ----
        //
        // Three tiers rather than one blob, because the thing that makes a pile read as a
        // pile at this distance is a lumpy silhouette against the wall. Wide and low at
        // the bottom, narrowing to a peak that leans toward the camera so it looms.

        for (int i = 0; i < 7; i++)
        {
            float a = i * 0.897f;
            RoomArt.Lump(t, "Washing",
                         new Vector3(0.46f, 0.30f, 0.42f),
                         new Vector3(Mathf.Cos(a) * 0.34f, 0.16f + (i % 3) * 0.045f,
                                     Mathf.Sin(a) * 0.26f),
                         M(Grubby(i), 0.12f),
                         new Vector3(0f, a * 52f, i % 2 == 0 ? 11f : -13f));
        }

        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.257f + 0.6f;
            RoomArt.Lump(t, "Washing",
                         new Vector3(0.38f, 0.26f, 0.34f),
                         new Vector3(Mathf.Cos(a) * 0.23f, 0.44f + (i % 2) * 0.06f,
                                     Mathf.Sin(a) * 0.17f),
                         M(Grubby(i + 3), 0.12f),
                         new Vector3(0f, a * 44f, i % 2 == 0 ? -14f : 9f));
        }

        // ---- the head ----
        //
        // One big lump at the top front. The eyes sit on its forward slope, which with a
        // camera pitched fifty degrees down is the part of it actually facing the player -
        // a face on the vertical front would be looking at the skirting board.
        var headMat = M(Grubby(2), 0.12f);
        RoomArt.Lump(t, "Head", new Vector3(0.66f, 0.50f, 0.54f),
                     new Vector3(0f, 0.74f, -0.06f), headMat,
                     new Vector3(-6f, 4f, 3f));
        // Two more bundles overlapping the crown. One blob at this size is a beach ball;
        // the job is a head made of wadded-up washing, and that needs a dent in it.
        RoomArt.Lump(t, "HeadLump", new Vector3(0.34f, 0.26f, 0.30f),
                     new Vector3(-0.26f, 0.88f, 0.04f), M(Grubby(4), 0.12f),
                     new Vector3(0f, 28f, 16f));
        RoomArt.Lump(t, "HeadLump", new Vector3(0.28f, 0.22f, 0.26f),
                     new Vector3(0.27f, 0.84f, 0.08f), M(Grubby(1), 0.12f),
                     new Vector3(0f, -34f, -12f));

        // ---- things you can name ----
        //
        // Without these it is a heap of coloured lumps. With them it is laundry.

        // A trouser leg out of the left flank, knee bent.
        RoomArt.Box(t, "TrouserLeg", new Vector3(0.20f, 0.62f, 0.20f), 0.09f,
                    new Vector3(-0.46f, 0.30f, -0.14f), M(Grubby(2), 0.12f),
                    new Vector3(66f, -22f, 0f));
        RoomArt.Box(t, "TrouserCuff", new Vector3(0.23f, 0.10f, 0.23f), 0.05f,
                    new Vector3(-0.60f, 0.56f, -0.30f), M(Grubby(2) * 0.88f, 0.12f),
                    new Vector3(66f, -22f, 0f));

        // A sleeve flung out the back, so the silhouette is not symmetrical.
        RoomArt.Box(t, "Sleeve", new Vector3(0.18f, 0.50f, 0.18f), 0.08f,
                    new Vector3(0.44f, 0.26f, 0.30f), M(Grubby(4), 0.12f),
                    new Vector3(-58f, 30f, 0f));

        // A towel over one shoulder, like a cape it has given up adjusting.
        RoomArt.Box(t, "Towel", new Vector3(0.40f, 0.52f, 0.07f), 0.05f,
                    new Vector3(-0.30f, 0.60f, 0.20f), M(Grubby(1) * 1.06f, 0.08f),
                    new Vector3(16f, -34f, -12f));

        // A single boot, half swallowed. Every laundry pile has one.
        RoomArt.Lump(t, "Boot", new Vector3(0.26f, 0.20f, 0.38f),
                     new Vector3(0.36f, 0.12f, -0.34f),
                     M(new Color(0.34f, 0.26f, 0.22f), 0.3f), new Vector3(0f, 24f, 0f));
        RoomArt.Box(t, "BootSole", new Vector3(0.24f, 0.06f, 0.36f), 0.03f,
                    new Vector3(0.36f, 0.03f, -0.34f),
                    M(new Color(0.18f, 0.17f, 0.18f), 0.2f), new Vector3(0f, 24f, 0f));

        // Sunglasses pushed up on top of its head, which is the joke: it is not hiding
        // from you, it just cannot be bothered to find them.
        //
        // Lying FLAT on the crown, which is the part of the Monster a camera pitched
        // fifty degrees down sees best. Tipped up on edge they stood off the back of the
        // head like a roof rack and read as hardware rather than as sunglasses.
        var dark = M(new Color(0.16f, 0.16f, 0.21f), 0.55f);
        RoomArt.Box(t, "ShadesBridge", new Vector3(0.11f, 0.05f, 0.07f), 0.02f,
                    new Vector3(0.01f, 1.00f, -0.05f), dark, new Vector3(-8f, 9f, -4f));
        foreach (float lx in new[] { -0.13f, 0.13f })
            RoomArt.Box(t, "ShadesLens", new Vector3(0.17f, 0.045f, 0.13f), 0.035f,
                        new Vector3(0.01f + lx, 0.995f, -0.05f), dark,
                        new Vector3(-8f, 9f, -4f));

        // ---- the face ----
        //
        // Inactive until MonsterFace turns it on. A calm Monster is just a pile of
        // washing; the face arriving is the warning, and the warning should not be
        // showing on a day where nothing has gone wrong yet.
        var face = new GameObject("Face");
        face.transform.SetParent(t, false);
        var f = face.transform;

        // The mouth hangs from a pivot at the top lip, so opening it drops the jaw
        // instead of inflating in both directions. It is scaled by MonsterFace, and the
        // pivot is what carries the name it looks for.
        //
        // This is not fussiness: grown symmetrically, the mouth swallowed its own teeth.
        // They are siblings rather than children for the matching reason - teeth that
        // stretch with the mouth look like chewing gum.
        var mouth = new GameObject("Mouth");
        mouth.transform.SetParent(f, false);
        mouth.transform.localPosition = new Vector3(0f, FaceY - 0.115f, FaceZ - 0.01f);
        mouth.transform.localRotation = Quaternion.Euler(-14f, 0f, -4f);
        RoomArt.Lump(mouth.transform, "MouthInner", new Vector3(0.36f, 0.17f, 0.14f),
                     new Vector3(0f, -0.075f, 0f),
                     M(new Color(0.16f, 0.09f, 0.12f), 0.05f));

        // Proud of the mouth's front face, or they sit inside it and never show.
        var tooth = M(ChunkyArt.Cream, 0.25f);
        foreach (float tx in new[] { -0.11f, 0.02f, 0.13f })
            RoomArt.Box(f, "Tooth", new Vector3(0.08f, 0.08f, 0.06f), 0.015f,
                        new Vector3(tx, FaceY - 0.115f, FaceZ - 0.085f), tooth,
                        new Vector3(-14f, 0f, tx * 70f));

        // Two grubby socks for eyebrows. Rotation is the whole expression: MonsterFace
        // tips the inner ends down as the anger rises, and nothing else on the Monster
        // says "furious" as cheaply or as clearly.
        //
        // Sat ON the head's forward slope. The head is very round, so its surface pulls
        // back sharply as you climb it: brows placed at the eyes' own depth hung off the
        // front of it like a pair of horns. These follow the curve - higher means
        // shallower - and are narrow enough not to reach the silhouette edge.
        var brow = M(Grubby(5) * 0.62f, 0.1f);
        RoomArt.Box(f, "BrowL", new Vector3(0.21f, 0.08f, 0.11f), 0.035f,
                    new Vector3(-0.155f, FaceY + 0.12f, -0.20f), brow,
                    new Vector3(-34f, 0f, 0f));
        RoomArt.Box(f, "BrowR", new Vector3(0.21f, 0.08f, 0.11f), 0.035f,
                    new Vector3(0.155f, FaceY + 0.12f, -0.20f), brow,
                    new Vector3(-34f, 0f, 0f));

        face.SetActive(false);

        // ---- the arm ----
        //
        // Built along +x, which from where the Monster stands points at the sorting
        // basket. MonsterAnimator stretches it along that axis over the snatch warning,
        // so the reach is the arm getting longer rather than the body leaning. Retracted
        // it is squashed to a stub and reads as one more bulge in the pile.
        var arm = new GameObject("ArmPivot");
        arm.transform.SetParent(t, false);
        arm.transform.localPosition = new Vector3(0.28f, 0.58f, -0.14f);
        var a2 = arm.transform;

        RoomArt.Box(a2, "UpperSleeve", new Vector3(0.36f, 0.21f, 0.21f), 0.10f,
                    new Vector3(0.18f, 0f, 0f), M(Grubby(0), 0.12f));
        RoomArt.Box(a2, "Cuff", new Vector3(0.09f, 0.24f, 0.24f), 0.04f,
                    new Vector3(0.38f, -0.01f, 0f), M(ChunkyArt.Cream * 0.88f, 0.15f));
        RoomArt.Box(a2, "Forearm", new Vector3(0.26f, 0.18f, 0.18f), 0.08f,
                    new Vector3(0.54f, -0.03f, 0f), M(Grubby(0) * 0.9f, 0.12f));
        // A sock for a hand. It has never had fingers and does not need them.
        RoomArt.Lump(a2, "Mitten", new Vector3(0.27f, 0.23f, 0.23f),
                     new Vector3(0.72f, -0.05f, 0f), M(Grubby(3), 0.15f));
        RoomArt.Lump(a2, "Thumb", new Vector3(0.11f, 0.10f, 0.15f),
                     new Vector3(0.70f, -0.02f, -0.12f), M(Grubby(3), 0.15f));

        arm.transform.localRotation = Quaternion.Euler(MonsterArm.RestEuler);
        arm.transform.localScale = MonsterArm.RestScale;

        return root;
    }
}
