using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LaundryMonster;

/// <summary>
/// Builds the laundry room from scratch. Re-runnable: it deletes anything it made
/// last time first, so iterating on the layout never leaves duplicates behind.
///
/// Everything is Unity primitives. The detail comes from composing several of them
/// per prop, from textures generated at runtime by SurfaceStyle, and from the
/// lighting rig - not from imported art.
/// </summary>
public static class RoomBuilder
{
    const string RootName = "__ROOM__";

    /// <summary>Yaw applied to generated models so they face into the room.</summary>
    const float ModelYaw = 180f;

    /// <summary>
    /// The hero needs no yaw correction, unlike every other generated model.
    ///
    /// The props still come from the original image-to-3D pipeline, which produced
    /// meshes facing away from the room. The hero's mesh was re-exported from Blender
    /// when the rig was rebuilt, and came out facing the other way. Leaving ModelYaw on
    /// him pointed the model 180 degrees from the direction PlayerController steers, so
    /// he walked backwards - the mocap was fine, the model was turned around.
    /// </summary>
    const float HeroYaw = 0f;

    [MenuItem("Laundry Monster/Build Room")]
    public static string BuildRoom()
    {
        var scene = SceneManager.GetActiveScene();

        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
        foreach (var stale in new[] { "Player", "GameDirector", "HUD", "Lighting",
                                      "TouchControls", "Tutorial", "Music" })
        {
            var go = GameObject.Find(stale);
            if (go != null) Object.DestroyImmediate(go);
        }

        var root = new GameObject(RootName);

        // ---- palette ----
        // Warm sand rather than cold purple-grey: the floor is most of the screen and
        // it was setting the temperature of the whole room against the cream walls.
        var floorMat   = Mat(ChunkyArt.FloorIvory, 0.12f);
        var wallMat    = Mat(ChunkyArt.WallCream, 0.06f, 0f, "wall_paint", new Vector2(5f, 1.2f));
        var trimMat    = Mat(new Color(0.42f, 0.41f, 0.40f), 0.25f);
        var washerMat  = Mat(new Color(0.86f, 0.89f, 0.95f), 0.55f, 0.25f, "metal_brushed", new Vector2(1.2f, 1f));
        var dryerMat   = Mat(new Color(0.86f, 0.68f, 0.44f), 0.55f, 0.25f, "metal_brushed", new Vector2(1.2f, 1f));
        var glassMat   = Mat(new Color(0.10f, 0.13f, 0.18f), 0.85f, 0.10f);
        var panelMat   = Mat(new Color(0.22f, 0.24f, 0.28f), 0.50f);
        var hamperMat  = Mat(new Color(0.80f, 0.66f, 0.46f), 0.12f, 0f, "wicker", new Vector2(2f, 1.2f));
        var woodMat    = Mat(new Color(0.86f, 0.70f, 0.50f), 0.25f, 0f, "wood_top", new Vector2(2.2f, 1.2f));
        var closetMat  = Mat(new Color(0.46f, 0.66f, 0.49f), 0.18f, 0f, "cabinet_paint", new Vector2(1.4f, 2f));
        var chairMat   = Mat(new Color(0.80f, 0.40f, 0.42f), 0.20f, 0f, "fabric", new Vector2(1.6f, 1.6f));
        var monsterMat = Mat(new Color(0.30f, 0.28f, 0.26f), 0.10f);
        var sockMat    = Mat(new Color(0.52f, 0.45f, 0.72f), 0.20f, 0f, "cabinet_paint", new Vector2(1.6f, 1.2f));
        var chromeMat  = Mat(new Color(0.78f, 0.80f, 0.84f), 0.85f, 0.90f);
        var skinMat    = Mat(new Color(0.95f, 0.78f, 0.62f), 0.10f);
        var shirtMat   = Mat(new Color(0.25f, 0.55f, 0.85f), 0.20f);

        // ---- shell ----
        var floor = Box(root.transform, "Floor", new Vector3(0f, -0.05f, 1f),
                        new Vector3(17f, 0.1f, 13f), floorMat);
        // Bigger tiles, so there are ten across the room instead of sixteen.
        Style(floor, SurfaceStyle.Style.Floor, new Vector2(5f, 4f), 0.14f);
        floor.isStatic = true;

        // 4.4m rather than 2.8m. CameraFraming widens the field of view on a narrow
        // window, and the extra height is what it looks at instead of the void above.
        Box(root.transform, "Wall_Back",  new Vector3(0f, 2.2f, 7.6f),  new Vector3(17f, 4.4f, 0.4f), wallMat);
        Box(root.transform, "Wall_Left",  new Vector3(-8.3f, 2.2f, 1f), new Vector3(0.4f, 4.4f, 13f), wallMat);
        Box(root.transform, "Wall_Right", new Vector3(8.3f, 2.2f, 1f),  new Vector3(0.4f, 4.4f, 13f), wallMat);

        // A painted lower band, the way a laundry does it so scuffs do not show. It also
        // separates the wall from the floor, which were two creams meeting with nothing
        // between them and reading as one flat surface.
        var bandMat = Mat(ChunkyArt.WallTeal, 0.10f);
        Box(root.transform, "Band_Back",  new Vector3(0f, 0.62f, 7.36f),  new Vector3(17f, 1.24f, 0.08f), bandMat);
        Box(root.transform, "Band_Left",  new Vector3(-8.06f, 0.62f, 1f), new Vector3(0.08f, 1.24f, 13f), bandMat);
        Box(root.transform, "Band_Right", new Vector3(8.06f, 0.62f, 1f),  new Vector3(0.08f, 1.24f, 13f), bandMat);

        // Skirting, so the wall/floor join reads as a room rather than a box.
        Box(root.transform, "Skirt_Back",  new Vector3(0f, 0.09f, 7.36f),  new Vector3(17f, 0.18f, 0.12f), trimMat);
        Box(root.transform, "Skirt_Left",  new Vector3(-8.06f, 0.09f, 1f), new Vector3(0.12f, 0.18f, 13f), trimMat);
        Box(root.transform, "Skirt_Right", new Vector3(8.06f, 0.09f, 1f),  new Vector3(0.12f, 0.18f, 13f), trimMat);

        // ---- stations ----

        MakeMachine(root.transform, "Washer_A", new Vector3(-3.0f, 0f, 5.5f), washerMat, glassMat, panelMat, chromeMat, LaundryMachine.Mode.Washer);
        MakeMachine(root.transform, "Washer_B", new Vector3(-0.5f, 0f, 5.5f), washerMat, glassMat, panelMat, chromeMat, LaundryMachine.Mode.Washer);
        MakeMachine(root.transform, "Dryer_A",  new Vector3(2.5f, 0f, 5.5f),  dryerMat,  glassMat, panelMat, chromeMat, LaundryMachine.Mode.Dryer);
        MakeMachine(root.transform, "Dryer_B",  new Vector3(5.0f, 0f, 5.5f),  dryerMat,  glassMat, panelMat, chromeMat, LaundryMachine.Mode.Dryer);

        var shade = RoomArt.Mat(ChunkyArt.FloorIvory * 0.42f, 0.02f);
        var table = RoomArtProps.FoldTable(root.transform, new Vector3(4.5f, 0f, -2.5f), shade);
        table.AddComponent<FoldTable>().InteractRadius = 2.3f;
        table.AddComponent<LaundryMonster.Highlighter>();

        var closet = RoomArtProps.Closet(root.transform, new Vector3(7.1f, 0f, 0.5f), shade);
        closet.AddComponent<Closet>().InteractRadius = 2.3f;
        closet.AddComponent<LaundryMonster.Highlighter>();

        // A sorting basket, not a chair. Mechanically this is where laundry gets dumped,
        // and a kitchen chair in the middle of a laundry room read as a thing to sit on.
        var chair = RoomArtProps.SortingBasket(root.transform, new Vector3(0f, 0f, 0.5f), shade);
        chair.AddComponent<Chair>().InteractRadius = 2.1f;
        chair.AddComponent<LaundryMonster.Highlighter>();

        var drawer = RoomArtProps.SockDrawer(root.transform, new Vector3(-6.0f, 0f, -2.2f), shade);
        drawer.AddComponent<SockStation>().InteractRadius = 2.2f;
        drawer.AddComponent<LaundryMonster.Highlighter>();

        // ---- the Monster ----
        var monsterRoot = new GameObject("MonsterPile");
        monsterRoot.transform.SetParent(root.transform, false);
        monsterRoot.transform.position = new Vector3(-6.6f, 0.3f, 1.6f);
        if (AddModel(monsterRoot.transform, "Model", "monster", Vector3.zero,
                     Quaternion.Euler(0f, ModelYaw, 0f), Color.white, 0.1f) == null)
        {
            Child(monsterRoot.transform, "Body", PrimitiveType.Sphere, Vector3.zero, Vector3.one, monsterMat);
            Child(monsterRoot.transform, "Lump", PrimitiveType.Sphere,
                  new Vector3(0.45f, -0.15f, 0.20f), Vector3.one * 0.75f, monsterMat);
            Child(monsterRoot.transform, "Lump", PrimitiveType.Sphere,
                  new Vector3(-0.40f, -0.20f, -0.25f), Vector3.one * 0.70f, monsterMat);
        }
        // The Monster is where dirty laundry comes from: you pull clothes off it.
        monsterRoot.transform.position = new Vector3(-5.9f, 0f, 4.6f);
        monsterRoot.transform.localScale = Vector3.one * Tuning.MonsterMinScale;
        monsterRoot.AddComponent<MonsterAnimator>();
        monsterRoot.AddComponent<MonsterAttack>();
        // Eyes that open as it gets angry. The results screen has been promising these
        // for a while.
        monsterRoot.AddComponent<MonsterFace>();

        var hamperComp = monsterRoot.AddComponent<Hamper>();
        hamperComp.InteractRadius = 2.9f;          // it is big, so reach it from further out
        monsterRoot.AddComponent<LaundryMonster.Highlighter>();

        // ---- camera ----
        PlaceCamera();

        // ---- player ----
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -2f);
        BuildPlayer(player.transform, shirtMat, skinMat);
        var pc = player.AddComponent<PlayerController>();
        // Set here rather than left on the component's defaults, because they are a
        // property of the camera framing above as much as of the room: the old -4.2
        // southern limit is now a metre below the bottom edge of the picture.
        // The two southern corners are the tight ones: down there the player is closest
        // to the camera, so the head projects further out than the feet and (-7, -3.9)
        // put it past the left edge of the screen entirely. Nothing stands in those
        // corners, so the bounds give them up rather than the camera giving up its framing.
        pc.RoomMin = new Vector2(-6.3f, -3.4f);
        pc.RoomMax = new Vector2(6.3f, 6.2f);
        player.AddComponent<HeroAnimator>();
        // Lights the bench each thing in your arms is asking for.
        player.AddComponent<NextStopGuide>();

        // ---- set dressing ----
        BuildDecor(root.transform);
        BuildWasherCorner(root.transform);
        BuildClusters(root.transform);

        // ---- lighting ----
        var lighting = new GameObject("Lighting");
        BuildLighting(lighting.transform);

        // ---- director + HUD ----
        var touchGo = new GameObject("TouchControls");
        touchGo.AddComponent<TouchControls>();

        var tutorialGo = new GameObject("Tutorial");
        tutorialGo.AddComponent<Tutorial>();

        BuildMusic();

        var dirGo = new GameObject("GameDirector");
        var dir = dirGo.AddComponent<GameDirector>();
        dir.Hamper = hamperComp;
        dir.MonsterPile = monsterRoot.transform;

        dir.GarmentMesh = LoadModelMesh("Assets/Models/folded/folded.fbx");

        // Enum order: Shirt, Pants, Towel, Sock, Delicate.
        var garmentFiles = new[] { "g_shirt", "g_jeans", "g_towel", "g_socks", "g_undies" };
        dir.GarmentMeshes = new Mesh[garmentFiles.Length];
        for (int i = 0; i < garmentFiles.Length; i++)
        {
            dir.GarmentMeshes[i] = LoadModelMesh($"Assets/Models/{garmentFiles[i]}/{garmentFiles[i]}.fbx");
            if (dir.GarmentMeshes[i] == null)
                Debug.LogWarning("RoomBuilder: missing garment mesh " + garmentFiles[i]);
        }

        var spawnParent = new GameObject("Garments");
        spawnParent.transform.SetParent(root.transform, false);
        dir.GarmentSpawnParent = spawnParent.transform;

        var hudGo = new GameObject("HUD");
        var hud = hudGo.AddComponent<HUD>();
        hud.TitleArt = LoadTitleArt();
        hud.HeadingFont = LoadFont("LilitaOne-Regular");
        hud.BodyFont = LoadFont("NunitoSans-Bold");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        return "Room rebuilt: machines, chair, closet, fold table, sock drawer, monster-as-hamper, "
             + "player, 4-light rig, textured surfaces, highlight pads, and set dressing.";
    }


    /// <summary>
    /// Set dressing. None of it is interactive - it exists so the room reads as a
    /// laundry room rather than a grid of stations on a floor.
    /// </summary>
    static void BuildDecor(Transform root)
    {
        var plastic = Mat(new Color(0.90f, 0.92f, 0.95f), 0.55f);
        var dark    = Mat(new Color(0.20f, 0.21f, 0.24f), 0.35f);
        var steel   = Mat(new Color(0.72f, 0.74f, 0.78f), 0.8f, 0.85f);
        var white   = Mat(new Color(0.94f, 0.94f, 0.92f), 0.3f);

        var decor = new GameObject("Decor");
        decor.transform.SetParent(root, false);
        var d = decor.transform;

        // --- detergent bottles and boxes on top of the machines ---
        var bottleCols = new[]
        {
            new Color(0.25f, 0.55f, 0.85f), new Color(0.90f, 0.45f, 0.20f),
            new Color(0.35f, 0.72f, 0.45f), new Color(0.85f, 0.30f, 0.45f),
            new Color(0.95f, 0.80f, 0.25f),
        };
        // The machines now have control panels of their own at this height, so the old
        // cylinders-on-the-lid went through them. Supplies live on the shelf instead.

        // --- wall shelf above the machines, with more supplies ---
        Child(d, "Shelf", PrimitiveType.Cube, new Vector3(0.5f, 2.15f, 7.25f),
              new Vector3(8.5f, 0.09f, 0.55f), Mat(new Color(0.80f, 0.74f, 0.62f), 0.2f));
        foreach (var bx in new[] { -2.6f, -1.4f, 0.4f, 1.9f, 3.1f })
            Child(d, "ShelfBottle", PrimitiveType.Cylinder,
                  new Vector3(bx, 2.34f, 7.25f), new Vector3(0.14f, 0.14f, 0.14f),
                  Mat(bottleCols[Mathf.Abs(bx.GetHashCode()) % bottleCols.Length], 0.45f));

        // --- wall clock: you are always watching the time in here ---
        var clock = Child(d, "Clock", PrimitiveType.Cylinder,
                          new Vector3(6.6f, 2.25f, 7.33f), new Vector3(0.52f, 0.05f, 0.52f), white);
        clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var rim = Child(d, "ClockRim", PrimitiveType.Cylinder,
                        new Vector3(6.6f, 2.25f, 7.37f), new Vector3(0.60f, 0.04f, 0.60f), dark);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Child(d, "ClockHand", PrimitiveType.Cube,
              new Vector3(6.6f, 2.38f, 7.29f), new Vector3(0.04f, 0.22f, 0.02f), dark);
        Child(d, "ClockHand2", PrimitiveType.Cube,
              new Vector3(6.72f, 2.25f, 7.29f), new Vector3(0.24f, 0.035f, 0.02f), dark);

        // --- plumbing along the back wall ---
        Child(d, "Pipe", PrimitiveType.Cylinder, new Vector3(0f, 2.55f, 7.3f),
              new Vector3(0.11f, 8.2f, 0.11f), steel).transform.localRotation =
              Quaternion.Euler(0f, 0f, 90f);
        foreach (var px in new[] { -6.5f, -2.0f, 3.0f, 7.0f })
            Child(d, "PipeBracket", PrimitiveType.Cube, new Vector3(px, 2.55f, 7.42f),
                  new Vector3(0.16f, 0.2f, 0.16f), dark);
        Child(d, "Downpipe", PrimitiveType.Cylinder, new Vector3(-7.9f, 1.4f, 7.3f),
              new Vector3(0.11f, 1.4f, 0.11f), steel);

        // --- strip light housings, mounted high over the machine row ---
        //
        // They used to hang mid-room at y 3.22, z 3.2. The camera looks down the room at
        // 48 degrees from (0, 11.5, -9.5), and from there a housing out in the middle of
        // the room projects within a fraction of a degree of the tops of the machines
        // behind it - so the fixtures sat squarely over the washers and the Monster and
        // hid what the player needed to read.
        //
        // Back against the wall and lower, they clear the machine tops by about five
        // degrees, and they end up where a laundromat would actually put them.
        foreach (var lx in new[] { -3.2f, 3.2f })
        {
            Child(d, "LightHousing", PrimitiveType.Cube, new Vector3(lx, 2.62f, 6.6f),
                  new Vector3(2.6f, 0.12f, 0.45f), dark);
            Child(d, "LightTube", PrimitiveType.Cylinder, new Vector3(lx, 2.53f, 6.6f),
                  new Vector3(0.11f, 1.2f, 0.11f), Mat(new Color(1f, 1f, 0.95f), 0.1f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // --- floor drain, because every laundry room has one ---
        var drain = Child(d, "Drain", PrimitiveType.Cylinder, new Vector3(2.0f, 0.012f, 1.2f),
                          new Vector3(0.55f, 0.012f, 0.55f), dark);
        Child(d, "DrainRim", PrimitiveType.Cylinder, new Vector3(2.0f, 0.008f, 1.2f),
              new Vector3(0.68f, 0.008f, 0.68f), steel);

        // The photoreal wicker baskets are gone: they were the loudest clash in the
        // room, a scanned object standing next to hand-built ones. Chunky stand-ins live
        // in BuildClusters instead.

        // --- an ironing board nobody ever puts away ---
        var board = RoomArt.Box(d, "IroningBoard", new Vector3(0.68f, 0.12f, 2.3f), 0.06f,
                                new Vector3(-7.3f, 0.86f, -0.4f), RoomArt.Mat(ChunkyArt.Cream, 0.2f),
                                new Vector3(0f, 0f, 6f));
        foreach (var bz in new[] { 0.45f, -1.2f })
            RoomArt.Box(d, "BoardLeg", new Vector3(0.11f, 0.8f, 0.11f), 0.04f,
                        new Vector3(-7.3f, 0.42f, bz), RoomArt.Mat(ChunkyArt.Chrome, 0.7f, 0.5f));

        // Stray socks are now RoomArtProps.SockTrail: five oversized ones in a line
        // leading to the Monster, rather than four real-sized ones that were specks.

        // --- a sign on the back wall ---
        // Taller than it was: it now has a caption and a very large zero to hold.
        Child(d, "SignBoard", PrimitiveType.Cube, new Vector3(-5.0f, 2.26f, 7.33f),
              new Vector3(2.4f, 0.82f, 0.07f), Mat(new Color(0.22f, 0.38f, 0.55f), 0.3f));
        Child(d, "SignStripe", PrimitiveType.Cube, new Vector3(-5.0f, 1.88f, 7.30f),
              new Vector3(2.0f, 0.05f, 0.03f), white);
        Child(d, "SignStripe2", PrimitiveType.Cube, new Vector3(-5.0f, 2.63f, 7.30f),
              new Vector3(2.0f, 0.05f, 0.03f), white);
    }



    /// <summary>
    /// One washer corner, dressed.
    ///
    /// Built first and on its own, because a style has to pass the recognition test at
    /// the real gameplay zoom before it is worth applying to the whole room: can you
    /// tell what each of these is without a label, from where the camera actually sits?
    ///
    /// The cluster sits in the pocket between the first washer and the back-left wall,
    /// which is outside every highlight pad - the nearest, the washer's own, reaches
    /// z 6.14 at this x.
    /// </summary>
    static void BuildWasherCorner(Transform root)
    {
        var corner = new GameObject("WasherCorner");
        corner.transform.SetParent(root, false);
        var c = corner.transform;

        // Contact shadows are the difference between a prop on the floor and a prop
        // hovering over it. Floor ivory, knocked well down.
        var shade = RoomArt.Mat(ChunkyArt.FloorIvory * 0.42f, 0.02f);

        // On the LEFT wall, not the back one. The camera sees the back wall only up to
        // about y 1.85, and the machines stand in front of most of that - a shelf behind
        // them was a shelf nobody would ever see. The side walls are seen obliquely and
        // have far more visible area.
        var shelf = RoomArt.DetergentShelf(c, new Vector3(-7.75f, 1.35f, 5.4f), shade);
        shelf.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        RoomArt.Spill(c, new Vector3(-4.4f, 0f, 6.5f), 0.5f, new Color(0.74f, 0.86f, 0.92f));
        RoomArt.Basket(c, new Vector3(-5.3f, 0f, 6.6f), -22f, ChunkyArt.Lavender, 1, true, shade);
        RoomArt.Pile(c, new Vector3(-6.9f, 0f, 6.5f), 1.0f, 3, shade);

        foreach (var t in corner.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = true;
    }


    /// <summary>
    /// The rest of the clutter, in clusters rather than scattered.
    ///
    /// The previous attempt was a hundred small objects spread evenly over the floor,
    /// which from the gameplay camera is visual noise - none of it was large enough to
    /// identify and all of it competed with the laundry the player is tracking. These
    /// are five or six readable scenes instead, each one pushed against a wall or tucked
    /// beside a station, with the middle of the floor left clear to walk through.
    /// </summary>
    static void BuildClusters(Transform root)
    {
        var cl = new GameObject("Clusters");
        cl.transform.SetParent(root, false);
        var c = cl.transform;
        var shade = RoomArt.Mat(ChunkyArt.FloorIvory * 0.42f, 0.02f);

        // The dryer end: fuzz, and a bin that has given up. Clear of the dryer pads,
        // which reach x 6.45 at this z.
        RoomArtProps.LintCorner(c, new Vector3(6.9f, 0f, 6.3f), shade);

        // Lost and found, down the right-hand wall between the dryers and the closet.
        RoomArtProps.LostAndFound(c, new Vector3(7.3f, 0f, 3.2f), shade);

        // A trail of oversized socks, leading out of the room toward the Monster. The
        // socks are deliberately much bigger than real ones - at this zoom a real-sized
        // sock is a speck, and the trail has to read as a trail.
        RoomArtProps.SockTrail(c, new Vector3(-1.6f, 0f, 1.2f), new Vector3(-5.2f, 0f, 3.9f), 5, 2);

        // Another laundry heap against the left wall, past the drawer.
        RoomArt.Pile(c, new Vector3(-7.1f, 0f, 0.9f), 1.1f, 5, shade);
        RoomArt.Basket(c, new Vector3(-7.2f, 0f, 2.6f), 28f, ChunkyArt.Mint, 4, true, shade);

        // And one lint ball, in the corner, watching.
        RoomArtProps.LintBall(c, new Vector3(-7.5f, 0f, -3.1f), 0.5f, true);

        // The joke everyone will see: enormous underwear on the line down the left wall.
        RoomArtProps.GiantUnderwear(c, new Vector3(-7.88f, 2.18f, 4.6f), 0f);

        foreach (var t in cl.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = true;
    }


    /// <summary>
    /// The mess.
    ///
    /// BuildDecor makes the room read as a laundry room; this makes it read as one
    /// somebody has been losing a fight with. Dust under everything, change on the floor,
    /// a puddle nobody mopped, a plant nobody watered, and a sign counting days since the
    /// last incident that has clearly never got past zero.
    ///
    /// Two rules, both of which the earlier readability pass earned and this must not
    /// spend:
    ///
    ///   Nothing within 1.6m of a station. That is the radius of the highlight pad, and
    ///   the pads are how the player knows where to take what they are carrying. Clutter
    ///   sitting in one is clutter sitting on the game's clearest signal.
    ///
    ///   Nothing saturated. The stations own the strong blues, oranges and greens; the
    ///   mess is beiges, greys and dusty pastels. A funny room that competes with its own
    ///   interface is not funny for long.
    ///
    /// Everything here is flat on the floor or pushed against a wall, so the player never
    /// appears to walk through a solid object.
    /// </summary>
    static void BuildMess(Transform root)
    {
        var mess = new GameObject("Mess");
        mess.transform.SetParent(root, false);
        var d = mess.transform;

        // The floor is base 0.78 multiplied by a ~0.76 tile texture, so it renders
        // around 0.59. Anything untextured sitting on it renders at its full colour, so
        // these are picked against 0.59 rather than against the number in the material.
        var lint    = Mat(new Color(0.63f, 0.60f, 0.55f), 0.05f);
        var lintDim = Mat(new Color(0.56f, 0.53f, 0.49f), 0.05f);
        var grime   = Mat(new Color(0.49f, 0.46f, 0.42f), 0.10f);
        var suds    = Mat(new Color(0.60f, 0.64f, 0.66f), 0.35f);
        var copper  = Mat(new Color(0.78f, 0.56f, 0.32f), 0.75f, 0.85f);
        var silver  = Mat(new Color(0.80f, 0.82f, 0.85f), 0.80f, 0.85f);
        var card    = Mat(new Color(0.72f, 0.60f, 0.44f), 0.10f);
        var paper   = Mat(new Color(0.95f, 0.93f, 0.86f), 0.05f);
        var steel   = Mat(new Color(0.72f, 0.74f, 0.78f), 0.80f, 0.85f);
        var dark    = Mat(new Color(0.20f, 0.21f, 0.24f), 0.35f);
        var hazard  = Mat(new Color(0.93f, 0.76f, 0.18f), 0.30f);
        var deadLeaf = Mat(new Color(0.55f, 0.48f, 0.26f), 0.10f);
        var liveLeaf = Mat(new Color(0.36f, 0.52f, 0.33f), 0.15f);

        // A flat mark on the floor. Opaque and barely off the floor's own colour, so it
        // reads as grime rather than as a thing you could pick up.
        GameObject Stain(string name, Vector3 at, float rx, float rz, float yaw, Material m)
        {
            var go = Child(d, name, PrimitiveType.Cylinder,
                           new Vector3(at.x, 0.011f, at.z), new Vector3(rx, 0.006f, rz), m);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        // A clump of fluff: three squashed spheres, which is enough to stop it reading
        // as a ball and start it reading as something swept into a corner.
        void Fluff(Vector3 at, float size, Material m)
        {
            Child(d, "Lint", PrimitiveType.Sphere, new Vector3(at.x, size * 0.32f, at.z),
                  new Vector3(size, size * 0.6f, size * 0.85f), m);
            Child(d, "Lint", PrimitiveType.Sphere,
                  new Vector3(at.x + size * 0.42f, size * 0.24f, at.z - size * 0.22f),
                  new Vector3(size * 0.7f, size * 0.44f, size * 0.62f), m);
            Child(d, "Lint", PrimitiveType.Sphere,
                  new Vector3(at.x - size * 0.34f, size * 0.22f, at.z + size * 0.28f),
                  new Vector3(size * 0.6f, size * 0.4f, size * 0.55f), m);
        }

        void Coin(Vector3 at, Material m)
        {
            Child(d, "Coin", PrimitiveType.Cylinder, new Vector3(at.x, 0.015f, at.z),
                  new Vector3(0.11f, 0.008f, 0.11f), m);
        }

        void Peg(Vector3 at, float yaw, Color c)
        {
            var p = Child(d, "Peg", PrimitiveType.Cube, new Vector3(at.x, 0.03f, at.z),
                          new Vector3(0.07f, 0.05f, 0.21f), Mat(c, 0.25f));
            p.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- the floor is not clean ----------

        // Somebody's detergent went over by the washers and was left to dry.
        Stain("SoapPuddle", new Vector3(-1.75f, 0f, 4.2f), 0.85f, 0.6f, 18f, suds);
        Stain("SoapEdge", new Vector3(-1.2f, 0f, 3.85f), 0.42f, 0.3f, -30f, suds);
        var tipped = Child(d, "TippedBottle", PrimitiveType.Cylinder,
                           new Vector3(-2.35f, 0.17f, 4.5f), new Vector3(0.18f, 0.22f, 0.18f),
                           Mat(new Color(0.42f, 0.60f, 0.80f), 0.5f));
        tipped.transform.localRotation = Quaternion.Euler(90f, 24f, 0f);
        Child(d, "TippedCap", PrimitiveType.Cylinder, new Vector3(-2.0f, 0.05f, 4.78f),
              new Vector3(0.11f, 0.04f, 0.11f), Mat(new Color(0.95f, 0.95f, 0.92f), 0.4f));

        // Traffic. Where everyone walks, the floor is darker.
        Stain("Scuff", new Vector3(0.9f, 0f, 2.6f), 1.1f, 0.75f, 12f, grime);
        Stain("Scuff", new Vector3(3.3f, 0f, 0.4f), 0.9f, 0.62f, 40f, grime);
        Stain("DrainRing", new Vector3(2.0f, 0f, 1.2f), 0.78f, 0.78f, 0f, grime);

        // ---------- dust, in every place a broom does not reach ----------
        // Against the wall behind the machines, where a broom genuinely cannot reach,
        // and in the corners. Not scattered over the floor the player works on.
        Fluff(new Vector3(-4.3f, 0f, 6.3f), 0.26f, lint);
        Fluff(new Vector3(1.05f, 0f, 6.35f), 0.23f, lintDim);
        Fluff(new Vector3(3.8f, 0f, 6.3f), 0.20f, lintDim);
        Fluff(new Vector3(-7.5f, 0f, 6.9f), 0.34f, lint);
        Fluff(new Vector3(7.5f, 0f, 6.8f), 0.30f, lintDim);
        Fluff(new Vector3(-7.4f, 0f, -2.9f), 0.28f, lint);
        Fluff(new Vector3(6.9f, 0f, 5.2f), 0.24f, lintDim);

        // ---------- the money that lives under the machines ----------
        Coin(new Vector3(-2.55f, 0f, 5.95f), copper);
        Coin(new Vector3(0.2f, 0f, 6.1f), silver);
        Coin(new Vector3(2.9f, 0f, 5.85f), copper);
        Coin(new Vector3(4.15f, 0f, 3.4f), silver);
        Coin(new Vector3(-3.4f, 0f, 2.9f), copper);
        Coin(new Vector3(1.6f, 0f, -1.5f), silver);
        Coin(new Vector3(-5.2f, 0f, 0.6f), copper);

        // ---------- pegs, which escape ----------
        Peg(new Vector3(-0.8f, 0f, 0.9f), 22f, new Color(0.85f, 0.55f, 0.35f));
        Peg(new Vector3(2.4f, 0f, 2.1f), -48f, new Color(0.55f, 0.70f, 0.85f));
        Peg(new Vector3(-3.1f, 0f, -1.2f), 71f, new Color(0.80f, 0.78f, 0.45f));
        Peg(new Vector3(5.3f, 0f, 1.6f), -15f, new Color(0.70f, 0.60f, 0.80f));
        Peg(new Vector3(-6.6f, 0f, 3.3f), 40f, new Color(0.85f, 0.55f, 0.35f));

        // ---------- a mop and bucket, retired mid-job ----------
        Child(d, "Bucket", PrimitiveType.Cylinder, new Vector3(-7.35f, 0.26f, -2.95f),
              new Vector3(0.62f, 0.26f, 0.62f), Mat(new Color(0.55f, 0.62f, 0.58f), 0.3f));
        Child(d, "BucketWater", PrimitiveType.Cylinder, new Vector3(-7.35f, 0.47f, -2.95f),
              new Vector3(0.54f, 0.02f, 0.54f), Mat(new Color(0.58f, 0.60f, 0.52f), 0.6f));
        var mop = Child(d, "MopHandle", PrimitiveType.Cylinder, new Vector3(-7.78f, 0.95f, -2.2f),
                        new Vector3(0.05f, 0.95f, 0.05f), Mat(new Color(0.70f, 0.60f, 0.44f), 0.2f));
        mop.transform.localRotation = Quaternion.Euler(9f, 0f, 5f);
        Child(d, "MopHead", PrimitiveType.Sphere, new Vector3(-7.9f, 0.16f, -2.5f),
              new Vector3(0.30f, 0.20f, 0.34f), Mat(new Color(0.66f, 0.64f, 0.58f), 0.1f));

        // ---------- a wet floor sign, placed hours after the wetness ----------
        Child(d, "ConeBase", PrimitiveType.Cylinder, new Vector3(3.0f, 0.06f, 1.95f),
              new Vector3(0.52f, 0.06f, 0.52f), hazard);
        Child(d, "ConeMid", PrimitiveType.Cylinder, new Vector3(3.0f, 0.26f, 1.95f),
              new Vector3(0.34f, 0.20f, 0.34f), hazard);
        Child(d, "ConeTop", PrimitiveType.Cylinder, new Vector3(3.0f, 0.52f, 1.95f),
              new Vector3(0.17f, 0.14f, 0.17f), hazard);
        Child(d, "ConeBand", PrimitiveType.Cylinder, new Vector3(3.0f, 0.34f, 1.95f),
              new Vector3(0.36f, 0.05f, 0.36f), Mat(new Color(0.95f, 0.95f, 0.93f), 0.3f));

        // ---------- a plant that is mostly a memory ----------
        Child(d, "PotRim", PrimitiveType.Cylinder, new Vector3(-7.4f, 0.34f, 5.9f),
              new Vector3(0.58f, 0.34f, 0.58f), Mat(new Color(0.74f, 0.52f, 0.42f), 0.15f));
        Child(d, "PotSoil", PrimitiveType.Cylinder, new Vector3(-7.4f, 0.66f, 5.9f),
              new Vector3(0.50f, 0.03f, 0.50f), Mat(new Color(0.30f, 0.25f, 0.21f), 0.05f));
        var stem = Child(d, "Stem", PrimitiveType.Cylinder, new Vector3(-7.4f, 0.98f, 5.9f),
                         new Vector3(0.05f, 0.34f, 0.05f), deadLeaf);
        stem.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
        // One leaf still going, three that gave up.
        Child(d, "Leaf", PrimitiveType.Sphere, new Vector3(-7.18f, 1.22f, 5.98f),
              new Vector3(0.30f, 0.05f, 0.16f), liveLeaf);
        Child(d, "Leaf", PrimitiveType.Sphere, new Vector3(-7.6f, 1.02f, 5.78f),
              new Vector3(0.26f, 0.04f, 0.14f), deadLeaf)
            .transform.localRotation = Quaternion.Euler(0f, 40f, -34f);
        Child(d, "Leaf", PrimitiveType.Sphere, new Vector3(-7.26f, 0.88f, 5.72f),
              new Vector3(0.24f, 0.04f, 0.13f), deadLeaf)
            .transform.localRotation = Quaternion.Euler(0f, -25f, -48f);
        Child(d, "FallenLeaf", PrimitiveType.Sphere, new Vector3(-6.9f, 0.02f, 5.5f),
              new Vector3(0.22f, 0.02f, 0.12f), deadLeaf);

        // ---------- lost and found, which is mostly lost ----------
        Child(d, "LostBox", PrimitiveType.Cube, new Vector3(7.25f, 0.33f, 4.2f),
              new Vector3(1.2f, 0.66f, 0.95f), card);
        Child(d, "LostBoxLip", PrimitiveType.Cube, new Vector3(7.25f, 0.68f, 4.2f),
              new Vector3(1.27f, 0.06f, 1.02f), Mat(new Color(0.64f, 0.52f, 0.38f), 0.1f));

        var sockMesh = LoadModelMesh("Assets/Models/g_socks/g_socks.fbx");
        var spill = new[]
        {
            (new Vector3(7.15f, 0.74f, 4.0f), new Color(0.80f, 0.42f, 0.42f), 14f),
            (new Vector3(7.4f, 0.78f, 4.4f), new Color(0.45f, 0.55f, 0.78f), -38f),
            (new Vector3(7.0f, 0.72f, 4.45f), new Color(0.86f, 0.80f, 0.45f), 62f),
            (new Vector3(6.7f, 0.02f, 3.6f), new Color(0.50f, 0.68f, 0.52f), -12f),
        };
        foreach (var (at, col, yaw) in spill)
        {
            GameObject so;
            if (sockMesh != null)
            {
                so = new GameObject("LostSock");
                so.transform.SetParent(d, false);
                so.AddComponent<MeshFilter>().sharedMesh = sockMesh;
                so.AddComponent<MeshRenderer>().sharedMaterial = Mat(col, 0.2f);
            }
            else
            {
                so = Child(d, "LostSock", PrimitiveType.Cube, Vector3.zero,
                           new Vector3(0.22f, 0.08f, 0.3f), Mat(col, 0.2f));
            }
            so.transform.position = at;
            so.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- a clothesline down the left wall ----------
        var line = Child(d, "Clothesline", PrimitiveType.Cylinder,
                         new Vector3(-7.95f, 2.25f, 2.4f), new Vector3(0.025f, 2.4f, 0.025f), steel);
        line.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (var (z, col, drop) in new[]
                 {
                     (0.6f, new Color(0.82f, 0.56f, 0.52f), 0.55f),
                     (2.3f, new Color(0.56f, 0.66f, 0.80f), 0.70f),
                     (3.9f, new Color(0.86f, 0.84f, 0.74f), 0.48f),
                 })
        {
            Child(d, "HungItem", PrimitiveType.Cube,
                  new Vector3(-7.92f, 2.25f - drop * 0.5f, z),
                  new Vector3(0.05f, drop, 0.52f), Mat(col, 0.15f));
            Child(d, "LinePeg", PrimitiveType.Cube, new Vector3(-7.92f, 2.27f, z),
                  new Vector3(0.06f, 0.14f, 0.07f), Mat(new Color(0.84f, 0.78f, 0.62f), 0.2f));
        }

        // ---------- a cobweb nobody is tall enough to care about ----------
        var web = Mat(new Color(0.86f, 0.86f, 0.84f), 0.05f);
        for (int i = 0; i < 3; i++)
        {
            var strand = Child(d, "Cobweb", PrimitiveType.Cylinder,
                               new Vector3(-7.75f, 3.45f, 7.05f),
                               new Vector3(0.012f, 0.62f, 0.012f), web);
            strand.transform.localRotation = Quaternion.Euler(52f, 35f + i * 36f, 28f);
        }

        // ---------- a noticeboard of things nobody reads ----------
        // x 5.55: right of the shelf's end at 4.75 and left of the clock at 6.6. At 3.4
        // it was behind the shelf with the detergent bottles standing in front of it.
        Child(d, "Corkboard", PrimitiveType.Cube, new Vector3(5.55f, 1.78f, 7.33f),
              new Vector3(1.5f, 1.0f, 0.06f), Mat(new Color(0.72f, 0.58f, 0.38f), 0.08f));
        Child(d, "CorkFrame", PrimitiveType.Cube, new Vector3(5.55f, 1.78f, 7.36f),
              new Vector3(1.62f, 1.12f, 0.03f), Mat(new Color(0.48f, 0.38f, 0.26f), 0.1f));
        var notes = new[]
        {
            (new Vector3(5.15f, 2.0f, 7.29f), 5f, 0.36f, 0.3f),
            (new Vector3(5.72f, 2.03f, 7.29f), -8f, 0.3f, 0.26f),
            (new Vector3(5.95f, 1.62f, 7.29f), 11f, 0.34f, 0.3f),
            (new Vector3(5.25f, 1.55f, 7.29f), -4f, 0.3f, 0.24f),
        };
        foreach (var (at, roll, w, h) in notes)
            Child(d, "Note", PrimitiveType.Cube, at, new Vector3(w, h, 0.02f), paper)
                .transform.localRotation = Quaternion.Euler(0f, 0f, roll);

        // ---------- a sock that has been on that wall for months ----------
        if (sockMesh != null)
        {
            var stuck = new GameObject("StaticClingSock");
            stuck.transform.SetParent(d, false);
            stuck.AddComponent<MeshFilter>().sharedMesh = sockMesh;
            stuck.AddComponent<MeshRenderer>().sharedMaterial =
                Mat(new Color(0.86f, 0.50f, 0.58f), 0.2f);
            stuck.transform.position = new Vector3(0.9f, 1.75f, 7.26f);
            stuck.transform.localRotation = Quaternion.Euler(84f, 0f, 22f);
        }

        // ---------- a radio, permanently on one station ----------
        Child(d, "Radio", PrimitiveType.Cube, new Vector3(4.7f, 2.33f, 7.25f),
              new Vector3(0.62f, 0.3f, 0.3f), Mat(new Color(0.52f, 0.47f, 0.42f), 0.25f));
        Child(d, "RadioGrille", PrimitiveType.Cube, new Vector3(4.56f, 2.33f, 7.10f),
              new Vector3(0.26f, 0.2f, 0.02f), dark);
        Child(d, "RadioDial", PrimitiveType.Cylinder, new Vector3(4.9f, 2.36f, 7.10f),
              new Vector3(0.09f, 0.02f, 0.09f), silver)
            .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var ant = Child(d, "RadioAntenna", PrimitiveType.Cylinder,
                        new Vector3(4.95f, 2.72f, 7.3f), new Vector3(0.016f, 0.38f, 0.016f), silver);
        ant.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);

        // ---------- the sign, which has never got past zero ----------
        var signFont = LoadFont("LilitaOne-Regular");
        WallText(d, "DAYS SINCE INCIDENT", new Vector3(-5.0f, 2.47f, 7.26f), 0.045f,
                 new Color(0.94f, 0.96f, 0.99f), signFont);
        WallText(d, "0", new Vector3(-5.0f, 2.12f, 7.26f), 0.13f,
                 new Color(0.98f, 0.82f, 0.25f), signFont);

        // Static, so a hundred small props batch into a handful of draw calls instead of
        // a hundred. None of it ever moves.
        foreach (var t in mess.GetComponentsInChildren<Transform>(true))
            t.gameObject.isStatic = true;
    }

    /// <summary>
    /// Readable text on the back wall.
    ///
    /// Left unrotated: a TextMesh reads toward its own -Z, which is the direction the
    /// back wall faces, so it already points at the player. Turning it to face them turns
    /// it away, and the first version of this sign came out a mirror image.
    /// </summary>
    static GameObject WallText(Transform parent, string text, Vector3 pos, float size,
                               Color color, Font font)
    {
        if (font == null) return null;

        var go = new GameObject("Sign_" + text);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        // No rotation: a TextMesh already reads toward -Z, which is the way the back
        // wall faces. Turning it round was what made the first sign a mirror image.
        go.transform.localRotation = Quaternion.identity;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 80;
        tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;

        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        return go;
    }


    /// <summary>
    /// Instantiate a rigged model, keeping its armature. A skinned mesh cannot be dropped
    /// in via MeshFilter the way a static one can - it needs its bone hierarchy, so the
    /// whole imported object gets instantiated.
    /// </summary>
    static GameObject AddRiggedModel(Transform parent, string name, string folder,
                                     Vector3 localPos, Quaternion localRot, Color tint, float smoothness)
    {
        var path = $"Assets/Models/{folder}/{folder}.fbx";

        // Generic rig with an Avatar. The model file carries no clips of its own - those
        // live in the person@<clip>.fbx files and bind through this Avatar.
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp != null && (imp.animationType != ModelImporterAnimationType.Generic ||
                            imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                            imp.importAnimation))
        {
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false;
            imp.SaveAndReimport();
        }

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) return null;

        var go = (GameObject)Object.Instantiate(src, parent);
        go.name = name;
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one;

        var skin = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skin == null) { Object.DestroyImmediate(go); return null; }

        var mat = Mat(tint, smoothness);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Models/{folder}/material_0.png");
        if (tex != null) mat.mainTexture = tex;
        skin.sharedMaterial = mat;
        skin.updateWhenOffscreen = true;
        return go;
    }

    // ---------- props ----------

    /// <summary>First Mesh inside an imported model file, or null if it is not there.</summary>
    static Mesh LoadModelMesh(string assetPath)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (o is Mesh m) return m;
        return null;
    }

    /// <summary>Drop a generated model in as a child, with a URP material built from its baked texture.</summary>
    static GameObject AddModel(Transform parent, string name, string folder,
                               Vector3 localPos, Quaternion localRot, Color tint, float smoothness)
    {
        var mesh = LoadModelMesh($"Assets/Models/{folder}/{folder}.fbx");
        if (mesh == null) return null;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var rend = go.AddComponent<MeshRenderer>();

        var mat = Mat(tint, smoothness);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Models/{folder}/material_0.png");
        if (tex != null) mat.mainTexture = tex;
        rend.sharedMaterial = mat;
        return go;
    }



    /// <summary>
    /// Pull the camera in.
    ///
    /// It sat at y 11.5 / z -9.5 looking down at 48 degrees, which put about a metre of
    /// wall and empty floor around every edge of the room - the props were small and the
    /// floor was most of the picture. These numbers are the closest that still keep the
    /// back wall, the fold table and the player's southern movement limit in frame; the
    /// bottom edge of the view lands at z -4.3 against a player limit of -3.9, and the
    /// top edge passes under the lamp rig at z 6.6 rather than slicing through it.
    ///
    /// Lives here rather than in the scene so it survives a rebuild and is reviewable.
    /// </summary>
    static void PlaceCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.transform.position = new Vector3(0f, 9.6f, -7.3f);
        cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        cam.fieldOfView = 45f;

        // The field of view is no longer a constant. These are the things that have to
        // stay on screen; CameraFraming solves for the narrowest view containing them
        // at whatever shape the window turns out to be.
        var fr = cam.GetComponent<CameraFraming>();
        if (fr == null) fr = cam.gameObject.AddComponent<CameraFraming>();

        // The room may use 87% of the half-height and 97% of the half-width; the rest is
        // left for the HUD bands. Without the vertical figure the solver is happy to put
        // the tops of the machines at 90% of the half-height, which is underneath the
        // day, objective and score cards - and it did, on every screen wider than 16:10.
        fr.VerticalSafe = 0.87f;
        fr.HorizontalSafe = 0.97f;

        fr.MustSee = new[]
        {
            // The four corners the player can reach, at head height - the southern two
            // are the tight ones, being nearest the lens.
            new Vector3(-6.3f, 1.8f, -3.4f), new Vector3(6.3f, 1.8f, -3.4f),
            new Vector3(-6.3f, 1.8f,  6.2f), new Vector3(6.3f, 1.8f,  6.2f),

            // The machines, high enough to include the status badge above each one.
            new Vector3(-3.0f, 2.3f, 5.5f), new Vector3(5.0f, 2.3f, 5.5f),

            // The three stations furthest off the centre line.
            new Vector3(7.1f, 2.0f, 0.5f),    // closet
            new Vector3(-6.0f, 1.2f, -2.2f),  // sock drawer
            new Vector3(-5.9f, 2.6f, 4.6f),   // the Monster, at full size
        };
        fr.Refresh();
    }

    static GameObject MakeMachine(Transform parent, string name, Vector3 pos, Material shell,
                                  Material glass, Material panel, Material chrome,
                                  LaundryMachine.Mode mode)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // Built, not scanned.
        //
        // The generated washer and dryer meshes were photoreal objects sitting next to
        // hand-made cubes, and the mismatch was most of why the room read as unrelated
        // props on a floor rather than as a place. RoomArt builds both machines out of
        // the same rounded box every other prop is made of, with the door and the knob
        // exaggerated enough to be identified at the zoom the game is actually played at.
        bool washer = mode == LaundryMachine.Mode.Washer;
        int index = name.EndsWith("_B") ? 1 : 0;
        var built = RoomArt.Machine(go.transform, "Chunky", Vector3.zero, mode, index);
        built.transform.localPosition = Vector3.zero;
        GameObject model = built;
        if (model == null)
        {
            Child(go.transform, "Mesh", PrimitiveType.Cube,
                  new Vector3(0f, 0.62f, 0f), new Vector3(1.55f, 1.15f, 1.35f), shell);
        }

        // Feet, so it sits on the floor instead of in it.
        if (model == null)
        foreach (var fx in new[] { -0.6f, 0.6f })
            foreach (var fz in new[] { -0.5f, 0.5f })
                Child(go.transform, "Foot", PrimitiveType.Cube,
                      new Vector3(fx, 0.03f, fz), new Vector3(0.16f, 0.06f, 0.16f), panel);

        // Door: a disc on the front face, with a chrome rim behind it.
        if (model == null) {
        var rim = Child(go.transform, "DoorRim", PrimitiveType.Cylinder,
                        new Vector3(0f, 0.58f, -0.69f), new Vector3(0.92f, 0.035f, 0.92f), chrome);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var door = Child(go.transform, "Door", PrimitiveType.Cylinder,
                         new Vector3(0f, 0.58f, -0.72f), new Vector3(0.78f, 0.035f, 0.78f), glass);
        door.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Control panel and a dial.
        Child(go.transform, "Panel", PrimitiveType.Cube,
              new Vector3(0f, 1.14f, -0.52f), new Vector3(1.4f, 0.18f, 0.3f), panel);
        var dial = Child(go.transform, "Dial", PrimitiveType.Cylinder,
                         new Vector3(-0.48f, 1.16f, -0.68f), new Vector3(0.17f, 0.03f, 0.17f), chrome);
        dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        }

        // No colour-coded lid any more: the whole machine is the colour now. Turquoise
        // and sky blue wash, coral and orange dry, which reads from anywhere in the room
        // rather than only from directly overhead.

        var m = go.AddComponent<LaundryMachine>();
        m.MachineMode = mode;
        m.InteractRadius = 1.85f;
        // Judders while it runs, nods while it waits to be emptied, shakes when on fire.
        go.AddComponent<MachineMotion>();
        go.AddComponent<LaundryMonster.Highlighter>();
        return go;
    }

    static GameObject MakeHamper(Transform parent, Vector3 pos, Material mat)
    {
        var go = new GameObject("Hamper");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        if (AddModel(go.transform, "Model", "basket", Vector3.zero,
                     Quaternion.Euler(0f, ModelYaw, 0f), Color.white, 0.12f) == null)
        {
            Child(go.transform, "Mesh", PrimitiveType.Cube,
                  new Vector3(0f, 0.42f, 0f), new Vector3(1.35f, 0.84f, 1.35f), mat);
            Child(go.transform, "Rim", PrimitiveType.Cube,
                  new Vector3(0f, 0.86f, 0f), new Vector3(1.48f, 0.10f, 1.48f),
                  Mat(new Color(0.46f, 0.34f, 0.23f), 0.2f));
        }
        return go;
    }

    static GameObject MakeFoldTable(Transform parent, Vector3 pos, Material wood, Material chrome)
    {
        var go = new GameObject("FoldTable");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        Child(go.transform, "Mesh", PrimitiveType.Cube,
              new Vector3(0f, 0.86f, 0f), new Vector3(2.7f, 0.12f, 1.5f), wood);
        foreach (var lx in new[] { -1.2f, 1.2f })
            foreach (var lz in new[] { -0.58f, 0.58f })
                Child(go.transform, "Leg", PrimitiveType.Cylinder,
                      new Vector3(lx, 0.43f, lz), new Vector3(0.1f, 0.43f, 0.1f), chrome);

        // A small stack of folded laundry on the far end, as set dressing.
        for (int i = 0; i < 3; i++)
            Child(go.transform, "Dressing", PrimitiveType.Cube,
                  new Vector3(0.95f, 0.96f + i * 0.1f, 0.2f),
                  new Vector3(0.5f, 0.09f, 0.4f),
                  Mat(new Color(0.85f - i * 0.08f, 0.88f, 0.93f), 0.15f));
        return go;
    }

    static GameObject MakeCloset(Transform parent, Vector3 pos, Material body, Material chrome)
    {
        var go = new GameObject("Closet");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // The generated closet is deliberately NOT used. Both TRELLIS attempts came back
        // with a hollow open face that reads as a shelf from every angle, and no rotation
        // fixes a hole. The composed primitive version is simply better here.

        Child(go.transform, "Mesh", PrimitiveType.Cube,
              new Vector3(0f, 1.1f, 0f), new Vector3(1.1f, 2.2f, 2.6f), body);

        foreach (var dz in new[] { -0.64f, 0.64f })
        {
            Child(go.transform, "Door", PrimitiveType.Cube,
                  new Vector3(-0.57f, 1.1f, dz), new Vector3(0.06f, 2.0f, 1.2f),
                  Mat(new Color(0.36f, 0.48f, 0.39f), 0.25f));
            Child(go.transform, "Handle", PrimitiveType.Cylinder,
                  new Vector3(-0.63f, 1.1f, dz + (dz < 0f ? 0.42f : -0.42f)),
                  new Vector3(0.05f, 0.16f, 0.05f), chrome);
        }
        return go;
    }

    static GameObject MakeChair(Transform parent, Vector3 pos, Material fabric, Material wood)
    {
        var go = new GameObject("TheChair");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        if (AddModel(go.transform, "Model", "chair", Vector3.zero,
                     Quaternion.Euler(0f, ModelYaw, 0f), Color.white, 0.2f) == null)
        {
            Child(go.transform, "Mesh", PrimitiveType.Cube,
                  new Vector3(0f, 0.48f, 0f), new Vector3(1.25f, 0.18f, 1.2f), fabric);
            Child(go.transform, "Back", PrimitiveType.Cube,
                  new Vector3(0f, 0.95f, 0.52f), new Vector3(1.25f, 0.8f, 0.14f), fabric);
            foreach (var lx in new[] { -0.5f, 0.5f })
                foreach (var lz in new[] { -0.47f, 0.47f })
                    Child(go.transform, "Leg", PrimitiveType.Cylinder,
                          new Vector3(lx, 0.24f, lz), new Vector3(0.09f, 0.24f, 0.09f), wood);
        }
        return go;
    }

    static GameObject MakeSockDrawer(Transform parent, Vector3 pos, Material body, Material chrome)
    {
        var go = new GameObject("SockDrawer");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        Child(go.transform, "Mesh", PrimitiveType.Cube,
              new Vector3(0f, 0.45f, 0f), new Vector3(1.9f, 0.9f, 1.25f), body);
        for (int i = 0; i < 2; i++)
        {
            Child(go.transform, "DrawerFront", PrimitiveType.Cube,
                  new Vector3(0f, 0.28f + i * 0.36f, -0.64f), new Vector3(1.75f, 0.30f, 0.07f),
                  Mat(new Color(0.42f, 0.37f, 0.54f), 0.25f));
            Child(go.transform, "Handle", PrimitiveType.Cube,
                  new Vector3(0f, 0.28f + i * 0.36f, -0.70f), new Vector3(0.6f, 0.05f, 0.05f), chrome);
        }
        return go;
    }

    /// <summary>
    /// Put the Animator on the model child, matching the clip paths.
    ///
    /// Generic animation binds by transform path relative to the Animator's own object,
    /// and the clips were exported with the armature directly under the FBX root - which
    /// is this object. An Animator on the player root instead would look for the bones one
    /// level too high and silently animate nothing.
    /// </summary>
    static void AttachHeroAnimator(GameObject model)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                       "Assets/Animation/Hero.controller");
        if (ctrl == null)
        {
            Debug.LogWarning("[RoomBuilder] Hero.controller missing - run "
                           + "Laundry Monster/Rebuild Hero Animation. Hero will not animate.");
            return;
        }

        var avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/person/person.fbx")
                                  .OfType<Avatar>().FirstOrDefault();

        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        if (avatar != null) anim.avatar = avatar;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    /// <summary>
    /// The music rig. Clips are generated art like everything else here - three beds from
    /// Stable Audio, looped with ffmpeg - and are looked up by name so a missing one
    /// leaves the game silent rather than broken.
    /// </summary>
    /// <summary>
    /// The title screen's key art. Imported as a sprite rather than a texture, because
    /// uGUI will not draw it otherwise, and left uncompressed-ish at a sane size so the
    /// lettering in the artwork stays crisp.
    /// </summary>
    static Sprite LoadTitleArt()
    {
        const string path = "Assets/Art/title_bg.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite
                            || imp.maxTextureSize != 2048
                            || imp.mipmapEnabled))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.maxTextureSize = 2048;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = false;
            imp.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning("[RoomBuilder] no title art at " + path);
        return sprite;
    }

    /// <summary>
    /// A font from Assets/Fonts. Returns null if it is missing, and the HUD falls back to
    /// the built-in legacy font rather than rendering nothing.
    /// </summary>
    static Font LoadFont(string name)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>($"Assets/Fonts/{name}.ttf");
        if (font == null) Debug.LogWarning($"[RoomBuilder] missing font {name}");
        return font;
    }

    static void BuildMusic()
    {
        var go = new GameObject("Music");
        var mp = go.AddComponent<MusicPlayer>();
        mp.TitleTrack = LoadMusic("title");
        mp.DayTrack = LoadMusic("day");
        mp.RushTrack = LoadMusic("rush");

        if (mp.DayTrack == null)
            Debug.LogWarning("[RoomBuilder] no music found in Assets/Audio/Music.");
    }

    static AudioClip LoadMusic(string name)
    {
        var path = $"Assets/Audio/Music/{name}.ogg";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) return null;

        // Compressed in memory, not decompressed on load: three 30s stereo beds
        // decompressed up front is tens of megabytes of WebGL heap for no benefit.
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp != null)
        {
            var settings = imp.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.CompressedInMemory
                || settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                imp.defaultSampleSettings = settings;
                imp.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
        return clip;
    }

    static void BuildPlayer(Transform root, Material shirt, Material skin)
    {
        // Rigged first: the hero needs limbs that move independently.
        var rigged = AddRiggedModel(root, "Model", "person", Vector3.zero,
                                    Quaternion.Euler(0f, HeroYaw, 0f), Color.white, 0.15f);
        if (rigged != null)
        {
            AttachHeroAnimator(rigged);
            return;
        }
        if (AddModel(root, "Model", "person", Vector3.zero,
                     Quaternion.Euler(0f, HeroYaw, 0f), Color.white, 0.15f) != null)
            return;

        Child(root, "Body", PrimitiveType.Capsule,
              new Vector3(0f, 0.62f, 0f), new Vector3(0.72f, 0.46f, 0.72f), shirt);
        Child(root, "Head", PrimitiveType.Sphere,
              new Vector3(0f, 1.26f, 0f), new Vector3(0.52f, 0.52f, 0.52f), skin);

        // Hair and a nose: between them you can always read which way you are facing.
        Child(root, "Hair", PrimitiveType.Sphere,
              new Vector3(0f, 1.34f, -0.04f), new Vector3(0.54f, 0.40f, 0.54f),
              Mat(new Color(0.26f, 0.19f, 0.15f), 0.2f));
        Child(root, "Facing", PrimitiveType.Cube,
              new Vector3(0f, 1.24f, 0.25f), new Vector3(0.1f, 0.1f, 0.12f), skin);

        foreach (var ax in new[] { -0.42f, 0.42f })
            Child(root, "Arm", PrimitiveType.Capsule,
                  new Vector3(ax, 0.72f, 0.05f), new Vector3(0.2f, 0.26f, 0.2f), shirt);
    }

    static void BuildLighting(Transform root)
    {
        // Key: warm, angled, and the only shadow caster.
        var key = new GameObject("Key Light");
        key.transform.SetParent(root, false);
        var kl = key.AddComponent<Light>();
        kl.type = LightType.Directional;
        kl.color = new Color(1f, 0.96f, 0.90f);
        kl.intensity = 1.05f;
        kl.shadows = LightShadows.Soft;
        kl.shadowStrength = 0.72f;
        key.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

        // Fill: cool, opposite side, no shadows - stops the dark sides going black.
        var fill = new GameObject("Fill Light");
        fill.transform.SetParent(root, false);
        var fl = fill.AddComponent<Light>();
        fl.type = LightType.Directional;
        fl.color = new Color(0.70f, 0.80f, 1f);
        fl.intensity = 0.32f;
        fl.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(28f, 150f, 0f);

        // Two strip lights overhead, for pools of fluorescent light on the floor.
        foreach (var px in new[] { -3.2f, 3.2f })
        {
            var lamp = new GameObject("Strip Light");
            lamp.transform.SetParent(root, false);
            lamp.transform.position = new Vector3(px, 3.1f, 3.2f);
            var pl = lamp.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(0.95f, 0.97f, 1f);
            pl.intensity = 2.2f;
            pl.range = 13f;
            pl.shadows = LightShadows.None;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.44f, 0.50f);
        RenderSettings.ambientEquatorColor = new Color(0.33f, 0.33f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.19f, 0.20f);

        // Remove the template light so it does not fight the rig.
        var old = GameObject.Find("Directional Light");
        if (old != null) Object.DestroyImmediate(old);
    }

    // ---------- helpers ----------

    static GameObject Child(Transform parent, string name, PrimitiveType type,
                            Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        Child(go.transform, "Mesh", PrimitiveType.Cube, Vector3.zero, scale, mat);
        return go;
    }

    static void Style(GameObject go, SurfaceStyle.Style kind, Vector2 tiling,
                      float smoothness, float metallic = 0f)
    {
        var s = go.AddComponent<SurfaceStyle>();
        s.Kind = kind;
        s.Tiling = tiling;
        s.Smoothness = smoothness;
        s.Metallic = metallic;
    }

    /// <summary>
    /// Load a generated texture from Assets/Textures and make sure it is imported to
    /// repeat with mipmaps - a clamped texture shows one stretched copy per face.
    /// Returns null when the file is absent, and callers fall back to flat colour.
    /// </summary>
    static Texture2D Tex(string name)
    {
        var path = "Assets/Textures/" + name + ".png";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return null;

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null && (imp.wrapMode != TextureWrapMode.Repeat || !imp.mipmapEnabled))
        {
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return tex;
    }

    static Material Mat(Color c, float smoothness, float metallic, string texName, Vector2 tiling)
    {
        var m = Mat(c, smoothness, metallic);
        var t = Tex(texName);
        if (t != null)
        {
            m.mainTexture = t;            // _BaseMap on URP/Lit
            m.mainTextureScale = tiling;
        }
        return m;
    }

    static Material Mat(Color c, float smoothness = 0.2f, float metallic = 0f)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader shader = rp != null && rp.defaultMaterial != null
            ? rp.defaultMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("No URP Lit shader found.");

        var m = new Material(shader);
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        return m;
    }
}
