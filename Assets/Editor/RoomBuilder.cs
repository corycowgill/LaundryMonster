using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using LaundryMonster;

/// <summary>
/// Builds the laundry room from scratch. Re-runnable: it deletes anything it made
/// last time first, so iterating on the layout never leaves duplicates behind.
/// </summary>
public static class RoomBuilder
{
    const string RootName = "__ROOM__";

    [MenuItem("Laundry Monster/Build Room")]
    public static string BuildRoom()
    {
        var scene = SceneManager.GetActiveScene();

        // Wipe the previous build.
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
        foreach (var stale in new[] { "Player", "GameDirector", "HUD" })
        {
            var go = GameObject.Find(stale);
            if (go != null) Object.DestroyImmediate(go);
        }

        var root = new GameObject(RootName);

        // ---- materials ----
        var floorMat = Mat(new Color(0.38f, 0.37f, 0.40f));
        var wallMat = Mat(new Color(0.52f, 0.50f, 0.47f));
        var washerMat = Mat(new Color(0.86f, 0.88f, 0.91f));
        var dryerMat = Mat(new Color(0.88f, 0.74f, 0.55f));
        var hamperMat = Mat(new Color(0.55f, 0.43f, 0.30f));
        var tableMat = Mat(new Color(0.72f, 0.58f, 0.38f));
        var closetMat = Mat(new Color(0.40f, 0.52f, 0.42f));
        var chairMat = Mat(new Color(0.62f, 0.34f, 0.36f));
        var monsterMat = Mat(new Color(0.30f, 0.28f, 0.26f));
        var playerMat = Mat(new Color(0.25f, 0.55f, 0.85f));

        // ---- room shell ----
        var floor = Box(root.transform, "Floor", new Vector3(0f, -0.05f, 1f),
                        new Vector3(17f, 0.1f, 13f), floorMat);
        floor.isStatic = true;

        Box(root.transform, "Wall_Back", new Vector3(0f, 1.2f, 7.6f), new Vector3(17f, 2.4f, 0.4f), wallMat);
        Box(root.transform, "Wall_Left", new Vector3(-8.3f, 1.2f, 1f), new Vector3(0.4f, 2.4f, 13f), wallMat);
        Box(root.transform, "Wall_Right", new Vector3(8.3f, 1.2f, 1f), new Vector3(0.4f, 2.4f, 13f), wallMat);

        // ---- stations ----
        var hamper = Box(root.transform, "Hamper", new Vector3(-6.5f, 0.45f, 5.5f),
                         new Vector3(1.3f, 0.9f, 1.3f), hamperMat);
        var hamperComp = hamper.AddComponent<Hamper>();
        hamperComp.InteractRadius = 2.0f;

        var washerA = MakeMachine(root.transform, "Washer_A", new Vector3(-3.0f, 0.6f, 5.5f),
                                  washerMat, LaundryMachine.Mode.Washer);
        var washerB = MakeMachine(root.transform, "Washer_B", new Vector3(-0.5f, 0.6f, 5.5f),
                                  washerMat, LaundryMachine.Mode.Washer);
        var dryerA = MakeMachine(root.transform, "Dryer_A", new Vector3(2.5f, 0.6f, 5.5f),
                                 dryerMat, LaundryMachine.Mode.Dryer);
        var dryerB = MakeMachine(root.transform, "Dryer_B", new Vector3(5.0f, 0.6f, 5.5f),
                                 dryerMat, LaundryMachine.Mode.Dryer);

        var table = Box(root.transform, "FoldTable", new Vector3(4.5f, 0.45f, -2.5f),
                        new Vector3(2.6f, 0.9f, 1.4f), tableMat);
        table.AddComponent<FoldTable>().InteractRadius = 2.2f;

        var closet = Box(root.transform, "Closet", new Vector3(7.0f, 1.0f, 0.5f),
                         new Vector3(1.2f, 2.0f, 2.6f), closetMat);
        closet.AddComponent<Closet>().InteractRadius = 2.2f;

        var sockMat = Mat(new Color(0.45f, 0.40f, 0.58f));
        var sockDrawer = Box(root.transform, "SockDrawer", new Vector3(-6.0f, 0.4f, -2.2f),
                             new Vector3(1.8f, 0.8f, 1.2f), sockMat);
        sockDrawer.AddComponent<SockStation>().InteractRadius = 2.1f;

        var chair = Box(root.transform, "TheChair", new Vector3(0f, 0.45f, 0.5f),
                        new Vector3(1.3f, 0.9f, 1.3f), chairMat);
        chair.AddComponent<Chair>().InteractRadius = 2.0f;

        // ---- the Monster: a pile that grows out of your neglect ----
        var monsterRoot = new GameObject("MonsterPile");
        monsterRoot.transform.SetParent(root.transform, false);
        monsterRoot.transform.position = new Vector3(-6.5f, 0.3f, 1.0f);
        var monsterBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        monsterBody.name = "Body";
        monsterBody.transform.SetParent(monsterRoot.transform, false);
        monsterBody.GetComponent<Renderer>().sharedMaterial = monsterMat;
        Object.DestroyImmediate(monsterBody.GetComponent<Collider>());
        monsterRoot.transform.localScale = new Vector3(0.35f, 0.26f, 0.35f);

        // ---- player ----
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0f, -2f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // A nose, so you can tell which way you are facing.
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Facing";
        nose.transform.SetParent(player.transform, false);
        nose.transform.localPosition = new Vector3(0f, 1.0f, 0.45f);
        nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.3f);
        nose.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.95f, 0.85f, 0.4f));
        Object.DestroyImmediate(nose.GetComponent<Collider>());

        player.AddComponent<PlayerController>();

        // ---- director + HUD ----
        var dirGo = new GameObject("GameDirector");
        var dir = dirGo.AddComponent<GameDirector>();
        dir.Hamper = hamperComp;
        dir.MonsterPile = monsterRoot.transform;

        var spawnParent = new GameObject("Garments");
        spawnParent.transform.SetParent(root.transform, false);
        dir.GarmentSpawnParent = spawnParent.transform;

        var hudGo = new GameObject("HUD");
        hudGo.AddComponent<HUD>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        return "Room built: 2 washers, 2 dryers, hamper, fold table, sock drawer, closet, chair, monster, player, HUD.";
    }

    static GameObject MakeMachine(Transform parent, string name, Vector3 pos,
                                  Material mat, LaundryMachine.Mode mode)
    {
        var go = Box(parent, name, pos, new Vector3(1.6f, 1.2f, 1.4f), mat);
        var m = go.AddComponent<LaundryMachine>();
        m.MachineMode = mode;
        m.InteractRadius = 1.8f;   // tight, so adjacent machines stay distinguishable

        // A dark porthole, so washers and dryers read at a glance.
        var port = GameObject.CreatePrimitive(PrimitiveType.Cube);
        port.name = "Port";
        port.transform.SetParent(go.transform, false);
        port.transform.localPosition = new Vector3(0f, 0.05f, -0.72f);
        port.transform.localScale = new Vector3(0.8f, 0.8f, 0.12f);
        port.GetComponent<Renderer>().sharedMaterial =
            Mat(mode == LaundryMachine.Mode.Washer
                ? new Color(0.15f, 0.22f, 0.32f)
                : new Color(0.32f, 0.20f, 0.15f));
        Object.DestroyImmediate(port.GetComponent<Collider>());

        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strip.name = "Strip";
        strip.transform.SetParent(go.transform, false);
        strip.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        strip.transform.localScale = new Vector3(1.5f, 0.06f, 1.2f);
        strip.GetComponent<Renderer>().sharedMaterial =
            Mat(mode == LaundryMachine.Mode.Washer
                ? new Color(0.20f, 0.45f, 0.75f)
                : new Color(0.85f, 0.45f, 0.15f));
        Object.DestroyImmediate(strip.GetComponent<Collider>());
        return go;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh.name = "Mesh";
        mesh.transform.SetParent(go.transform, false);
        mesh.transform.localScale = scale;
        mesh.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(mesh.GetComponent<Collider>());

        return go;
    }

    static Material Mat(Color c)
    {
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        Shader shader = rp != null && rp.defaultMaterial != null
            ? rp.defaultMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new System.Exception("No URP Lit shader found.");

        var m = new Material(shader);
        m.color = c;
        return m;
    }
}
