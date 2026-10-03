using UnityEngine;
using UnityEditor;

/// <summary>
/// Things somebody stuck on the laundry wall.
///
/// The walls were the last big empty surfaces in the room - two long stretches of flat
/// cream that the eye slides straight off. Posters fix that, and they are the one place
/// the room gets to make its joke in words instead of in shapes.
///
/// These are the only painted artwork in the room; everything else is built out of
/// rounded boxes. That is deliberate. A poster IS a flat printed picture, so a painted
/// one does not fight the chunky style the way a photoscanned prop does - it reads as a
/// piece of paper stuck to a wall, which is exactly what it is.
///
/// Every one hangs slightly crooked. A poster taped up straight reads as a texture on a
/// wall; a poster two degrees off reads as something a person put there.
/// </summary>
public static class RoomArtPosters
{
    const string Dir = "Assets/Art/Posters";

    /// <summary>Which wall, and therefore which way everything has to face.</summary>
    public enum Wall { Left, Right, Back }

    /// <summary>
    /// The rotation that turns a poster's face into the room.
    ///
    /// Unlit quads here, but the convention is the same one the back-wall signs use: the
    /// art faces the quad's own -Z, so the back wall - whose visible side also points -Z
    /// - needs no rotation at all.
    /// </summary>
    static Quaternion Facing(Wall w) => w switch
    {
        Wall.Left => Quaternion.Euler(0f, -90f, 0f),
        Wall.Right => Quaternion.Euler(0f, 90f, 0f),
        _ => Quaternion.identity,
    };

    /// <summary>
    /// A framed poster: a wooden surround, and the printed sheet inside it.
    ///
    /// <paramref name="height"/> is in metres; the width follows the image's own aspect,
    /// so a square poster stays square and nothing gets stretched to fit a guess.
    /// </summary>
    public static GameObject Poster(Transform parent, string name, Wall wall, Vector3 at,
                                    float height, float tilt)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Dir}/{name}.png");
        if (tex == null)
        {
            Debug.LogWarning($"[Posters] no art at {Dir}/{name}.png - skipping it. "
                             + "The room still builds; the wall is just emptier.");
            return null;
        }

        float width = height * (tex.width / (float)Mathf.Max(1, tex.height));

        var go = new GameObject("Poster_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = at;
        go.transform.localRotation = Facing(wall) * Quaternion.Euler(0f, 0f, tilt);
        var t = go.transform;

        // The frame sits behind the sheet and overhangs it a little on every side.
        RoomArt.Box(t, "Frame", new Vector3(width + 0.10f, height + 0.10f, 0.05f), 0.022f,
                    Vector3.zero, RoomArt.Mat(ChunkyArt.WoodDark, 0.15f));

        var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        sheet.name = "Sheet";
        var col = sheet.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);
        sheet.transform.SetParent(t, false);
        // Proud of the frame's front face, which reaches z -0.025.
        sheet.transform.localPosition = new Vector3(0f, 0f, -0.031f);
        sheet.transform.localScale = new Vector3(width, height, 1f);

        // Unlit, so a poster is the colour it was drawn in.
        //
        // The room's four warm lamps are placed for the machines and the floor, and a lit
        // quad flat against a side wall catches almost nothing from them - every poster
        // came out as a grey rectangle with a hint of a joke on it. These are printed
        // ink: they do not need to take part in the lighting.
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = shader != null
            ? new Material(shader)
            : new Material(sheet.GetComponent<Renderer>().sharedMaterial);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        // Knocked back very slightly, so the brightest paper is not the brightest thing
        // in a room lit to about 0.6.
        var tint = new Color(0.88f, 0.87f, 0.85f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        mat.color = tint;

        var rend = sheet.GetComponent<MeshRenderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        foreach (var child in go.GetComponentsInChildren<Transform>(true))
            child.gameObject.isStatic = true;

        return go;
    }
}
