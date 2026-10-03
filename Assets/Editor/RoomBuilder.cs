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
        var floorMat   = Mat(new Color(0.52f, 0.50f, 0.55f), 0.35f);
        var wallMat    = Mat(new Color(0.84f, 0.82f, 0.76f), 0.08f, 0f, "wall_paint", new Vector2(5f, 1.2f));
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
        Style(floor, SurfaceStyle.Style.Floor, new Vector2(8f, 6f), 0.35f);
        floor.isStatic = true;

        Box(root.transform, "Wall_Back",  new Vector3(0f, 1.4f, 7.6f),  new Vector3(17f, 2.8f, 0.4f), wallMat);
        Box(root.transform, "Wall_Left",  new Vector3(-8.3f, 1.4f, 1f), new Vector3(0.4f, 2.8f, 13f), wallMat);
        Box(root.transform, "Wall_Right", new Vector3(8.3f, 1.4f, 1f),  new Vector3(0.4f, 2.8f, 13f), wallMat);

        // Skirting, so the wall/floor join reads as a room rather than a box.
        Box(root.transform, "Skirt_Back",  new Vector3(0f, 0.09f, 7.36f),  new Vector3(17f, 0.18f, 0.12f), trimMat);
        Box(root.transform, "Skirt_Left",  new Vector3(-8.06f, 0.09f, 1f), new Vector3(0.12f, 0.18f, 13f), trimMat);
        Box(root.transform, "Skirt_Right", new Vector3(8.06f, 0.09f, 1f),  new Vector3(0.12f, 0.18f, 13f), trimMat);

        // ---- stations ----

        MakeMachine(root.transform, "Washer_A", new Vector3(-3.0f, 0f, 5.5f), washerMat, glassMat, panelMat, chromeMat, LaundryMachine.Mode.Washer);
        MakeMachine(root.transform, "Washer_B", new Vector3(-0.5f, 0f, 5.5f), washerMat, glassMat, panelMat, chromeMat, LaundryMachine.Mode.Washer);
        MakeMachine(root.transform, "Dryer_A",  new Vector3(2.5f, 0f, 5.5f),  dryerMat,  glassMat, panelMat, chromeMat, LaundryMachine.Mode.Dryer);
        MakeMachine(root.transform, "Dryer_B",  new Vector3(5.0f, 0f, 5.5f),  dryerMat,  glassMat, panelMat, chromeMat, LaundryMachine.Mode.Dryer);

        var table = MakeFoldTable(root.transform, new Vector3(4.5f, 0f, -2.5f), woodMat, chromeMat);
        table.AddComponent<FoldTable>().InteractRadius = 2.3f;
        table.AddComponent<LaundryMonster.Highlighter>();

        var closet = MakeCloset(root.transform, new Vector3(7.1f, 0f, 0.5f), closetMat, chromeMat);
        closet.AddComponent<Closet>().InteractRadius = 2.3f;
        closet.AddComponent<LaundryMonster.Highlighter>();

        var chair = MakeChair(root.transform, new Vector3(0f, 0f, 0.5f), chairMat, woodMat);
        chair.AddComponent<Chair>().InteractRadius = 2.1f;
        chair.AddComponent<LaundryMonster.Highlighter>();

        var drawer = MakeSockDrawer(root.transform, new Vector3(-6.0f, 0f, -2.2f), sockMat, chromeMat);
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

        var hamperComp = monsterRoot.AddComponent<Hamper>();
        hamperComp.InteractRadius = 2.9f;          // it is big, so reach it from further out
        monsterRoot.AddComponent<LaundryMonster.Highlighter>();

        // ---- player ----
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -2f);
        BuildPlayer(player.transform, shirtMat, skinMat);
        player.AddComponent<PlayerController>();
        player.AddComponent<HeroAnimator>();

        // ---- set dressing ----
        BuildDecor(root.transform);

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
        var tops = new[] { -3.0f, -0.5f, 2.5f, 5.0f };
        for (int i = 0; i < tops.Length; i++)
        {
            float x = tops[i] + (i % 2 == 0 ? 0.42f : -0.40f);
            var col = bottleCols[i % bottleCols.Length];
            Child(d, "Detergent", PrimitiveType.Cylinder,
                  new Vector3(x, 1.44f, 5.55f), new Vector3(0.17f, 0.16f, 0.17f), Mat(col, 0.5f));
            Child(d, "Cap", PrimitiveType.Cylinder,
                  new Vector3(x, 1.63f, 5.55f), new Vector3(0.10f, 0.04f, 0.10f), white);
            if (i % 2 == 0)
                Child(d, "SoapBox", PrimitiveType.Cube,
                      new Vector3(tops[i] - 0.45f, 1.42f, 5.5f), new Vector3(0.3f, 0.36f, 0.22f),
                      Mat(bottleCols[(i + 2) % bottleCols.Length], 0.2f));
        }

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

        // --- spare baskets stacked in the corner ---
        for (int i = 0; i < 2; i++)
        {
            var b = AddModel(d, "SpareBasket", "basket",
                             new Vector3(7.3f, i * 0.55f, -3.6f),
                             Quaternion.Euler(0f, ModelYaw + i * 40f, 0f), Color.white, 0.12f);
            if (b == null)
                Child(d, "SpareBasket", PrimitiveType.Cube,
                      new Vector3(7.3f, 0.4f + i * 0.55f, -3.6f),
                      new Vector3(1.0f, 0.7f, 1.0f), plastic);
        }

        // --- an ironing board nobody ever puts away ---
        var board = Child(d, "IroningBoard", PrimitiveType.Cube, new Vector3(-7.3f, 0.85f, -0.4f),
                          new Vector3(0.62f, 0.08f, 2.3f), white);
        board.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
        Child(d, "BoardLeg", PrimitiveType.Cylinder, new Vector3(-7.3f, 0.42f, 0.45f),
              new Vector3(0.07f, 0.42f, 0.07f), steel);
        Child(d, "BoardLeg", PrimitiveType.Cylinder, new Vector3(-7.3f, 0.42f, -1.2f),
              new Vector3(0.07f, 0.42f, 0.07f), steel);

        // --- a few stray socks on the floor, which is the whole premise ---
        var sockMesh = LoadModelMesh("Assets/Models/g_socks/g_socks.fbx");
        var strayCols = new[]
        {
            new Color(0.85f, 0.35f, 0.35f), new Color(0.35f, 0.5f, 0.8f),
            new Color(0.95f, 0.85f, 0.4f), new Color(0.4f, 0.7f, 0.5f),
        };
        var strayAt = new[]
        {
            new Vector3(-1.9f, 0.02f, -3.4f), new Vector3(5.6f, 0.02f, 2.1f),
            new Vector3(-4.6f, 0.02f, 0.2f),  new Vector3(1.2f, 0.02f, 6.4f),
        };
        for (int i = 0; i < strayAt.Length; i++)
        {
            GameObject so;
            if (sockMesh != null)
            {
                so = new GameObject("StraySock");
                so.transform.SetParent(d, false);
                so.AddComponent<MeshFilter>().sharedMesh = sockMesh;
                so.AddComponent<MeshRenderer>().sharedMaterial = Mat(strayCols[i], 0.2f);
            }
            else
            {
                so = Child(d, "StraySock", PrimitiveType.Cube, Vector3.zero,
                           new Vector3(0.22f, 0.08f, 0.3f), Mat(strayCols[i], 0.2f));
            }
            so.transform.position = strayAt[i];
            so.transform.localRotation = Quaternion.Euler(0f, i * 57f, 0f);
        }

        // --- a sign on the back wall ---
        Child(d, "SignBoard", PrimitiveType.Cube, new Vector3(-5.0f, 2.3f, 7.33f),
              new Vector3(2.4f, 0.62f, 0.07f), Mat(new Color(0.22f, 0.38f, 0.55f), 0.3f));
        Child(d, "SignStripe", PrimitiveType.Cube, new Vector3(-5.0f, 2.08f, 7.30f),
              new Vector3(2.0f, 0.07f, 0.03f), white);
        Child(d, "SignStripe2", PrimitiveType.Cube, new Vector3(-5.0f, 2.52f, 7.30f),
              new Vector3(2.0f, 0.07f, 0.03f), white);
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



    static GameObject MakeMachine(Transform parent, string name, Vector3 pos, Material shell,
                                  Material glass, Material panel, Material chrome,
                                  LaundryMachine.Mode mode)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // Generated model if we have one; the primitive build is the fallback.
        bool washer = mode == LaundryMachine.Mode.Washer;
        var model = AddModel(go.transform, "Model", washer ? "washer" : "dryer", Vector3.zero,
                             Quaternion.Euler(0f, ModelYaw, 0f),
                             Color.white, 0.5f);
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

        // Colour-coded lid, so washers and dryers read instantly from above. This stays
        // even with the model: it is the thing that tells the two machine types apart.
        Child(go.transform, "Strip", PrimitiveType.Cube,
              new Vector3(0f, model != null ? 1.33f : 1.21f, model != null ? 0f : 0.12f),
              new Vector3(model != null ? 0.95f : 1.5f, 0.07f, model != null ? 0.98f : 1.1f),
              Mat(mode == LaundryMachine.Mode.Washer
                  ? new Color(0.22f, 0.50f, 0.82f)
                  : new Color(0.90f, 0.50f, 0.18f), 0.4f));

        var m = go.AddComponent<LaundryMachine>();
        m.MachineMode = mode;
        m.InteractRadius = 1.85f;
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
