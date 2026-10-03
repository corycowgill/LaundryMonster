using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The house style: one shape, one palette.
///
/// The room had three art languages fighting each other - photoscanned machines, a
/// photoreal wicker basket, and bare Unity cubes - which is why it read as a utility
/// room with unrelated objects dropped into it rather than as a place. Everything here
/// is built from ONE primitive, a box with genuinely rounded corners, so a detergent
/// bottle and a washing machine and a laundry basket are visibly the same kind of
/// object at different sizes.
///
/// Rounded corners are the whole trick. A cube reads as a programmer placeholder at any
/// size; the same box with a 0.2m radius reads as a moulded plastic thing, and catches
/// a highlight along every edge, which is what separates a prop from the wall behind it.
/// </summary>
public static class ChunkyArt
{
    // ---------- palette ----------
    //
    // A warm, slightly faded room with saturated props on top of it. The room surfaces
    // are deliberately low-chroma so the laundry, which is what the player is actually
    // tracking, owns every strong colour in the frame.

    public static readonly Color WallCream  = new Color(0.96f, 0.92f, 0.79f);
    public static readonly Color WallMint   = new Color(0.84f, 0.92f, 0.86f);
    public static readonly Color WallTeal   = new Color(0.33f, 0.60f, 0.60f);
    public static readonly Color FloorIvory = new Color(0.93f, 0.88f, 0.78f);
    public static readonly Color Grout      = new Color(0.62f, 0.57f, 0.50f);

    public static readonly Color Turquoise  = new Color(0.24f, 0.73f, 0.76f);
    public static readonly Color SkyBlue    = new Color(0.46f, 0.73f, 0.93f);
    public static readonly Color Coral      = new Color(0.97f, 0.50f, 0.42f);
    public static readonly Color Sunny      = new Color(0.98f, 0.70f, 0.24f);

    public static readonly Color Lavender   = new Color(0.73f, 0.68f, 0.86f);
    public static readonly Color Mint       = new Color(0.62f, 0.86f, 0.74f);
    public static readonly Color Wood       = new Color(0.85f, 0.66f, 0.42f);
    public static readonly Color WoodDark   = new Color(0.66f, 0.48f, 0.30f);
    public static readonly Color Cream      = new Color(0.98f, 0.95f, 0.88f);
    public static readonly Color Charcoal   = new Color(0.22f, 0.24f, 0.28f);
    public static readonly Color Chrome     = new Color(0.82f, 0.85f, 0.88f);

    /// <summary>Laundry. Bold, and repeated across the room so it looks designed.</summary>
    public static readonly Color[] Wash =
    {
        new Color(0.96f, 0.42f, 0.62f),   // pink
        new Color(0.99f, 0.82f, 0.28f),   // yellow
        new Color(0.34f, 0.57f, 0.93f),   // blue
        new Color(0.44f, 0.80f, 0.50f),   // green
        new Color(0.66f, 0.47f, 0.89f),   // purple
        new Color(0.99f, 0.62f, 0.33f),   // apricot
    };

    public static Color Laundry(int i) => Wash[((i % Wash.Length) + Wash.Length) % Wash.Length];

    // ---------- the one shape everything is made of ----------

    const string ShapeDir = "Assets/Generated/Shapes";
    static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

    /// <summary>
    /// A box whose corners and edges are rounded by <paramref name="radius"/>.
    ///
    /// Built by taking the points of a subdivided cube and pushing each one out to the
    /// surface of a rounded box: clamp the point into the inner box, then step back out
    /// by the radius along the direction it was clamped from. Flat faces stay perfectly
    /// flat (the clamp moves nothing but the one axis), edges and corners round over,
    /// and the direction you stepped along IS the surface normal, so smooth shading
    /// comes out of the same arithmetic.
    ///
    /// Meshes are cached by shape and saved as assets, so a hundred props share a
    /// handful of meshes and the build keeps them.
    /// </summary>
    public static Mesh RoundedBox(Vector3 size, float radius, int seg = 8)
    {
        var b = size * 0.5f;
        radius = Mathf.Max(0.001f, Mathf.Min(radius, Mathf.Min(b.x, Mathf.Min(b.y, b.z)) * 0.98f));

        string key = $"rb_{size.x:0.###}_{size.y:0.###}_{size.z:0.###}_{radius:0.###}_{seg}";
        if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;

        var path = $"{ShapeDir}/{key}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { Cache[key] = existing; return existing; }

        var inner = b - Vector3.one * radius;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var tris = new List<int>();

        var axes = new[]
        {
            Vector3.right, Vector3.left, Vector3.up,
            Vector3.down, Vector3.forward, Vector3.back,
        };

        foreach (var n in axes)
        {
            // Any perpendicular pair will do; both are axis-aligned because n is.
            var u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up).normalized;
            var v = Vector3.Cross(n, u);

            float bn = Mathf.Abs(Vector3.Dot(n, b));
            float bu = Mathf.Abs(Vector3.Dot(u, b));
            float bv = Mathf.Abs(Vector3.Dot(v, b));

            int start = verts.Count;
            for (int i = 0; i <= seg; i++)
            for (int j = 0; j <= seg; j++)
            {
                // Samples bunched toward the edges, which is where the curvature is.
                float a = Curve(i / (float)seg);
                float c = Curve(j / (float)seg);

                var p = n * bn + u * (a * bu) + v * (c * bv);
                var cl = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                     Mathf.Clamp(p.y, -inner.y, inner.y),
                                     Mathf.Clamp(p.z, -inner.z, inner.z));
                var dir = (p - cl).normalized;

                verts.Add(cl + dir * radius);
                norms.Add(dir);
            }

            for (int i = 0; i < seg; i++)
            for (int j = 0; j < seg; j++)
            {
                int i0 = start + i * (seg + 1) + j;
                int i1 = i0 + (seg + 1);
                int i2 = i0 + 1;
                int i3 = i1 + 1;
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
                tris.Add(i2); tris.Add(i1); tris.Add(i3);
            }
        }

        var mesh = new Mesh { name = key };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(ShapeDir))
            AssetDatabase.CreateFolder("Assets/Generated", "Shapes");
        AssetDatabase.CreateAsset(mesh, path);

        Cache[key] = mesh;
        return mesh;
    }

    /// <summary>Push samples toward -1 and +1, so the rounded edges get the triangles.</summary>
    static float Curve(float t)
    {
        float s = t * 2f - 1f;
        return Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.62f);
    }

    /// <summary>A rounded box so round it is a blob - for laundry, lint and bubbles.</summary>
    public static Mesh Blob(Vector3 size) => RoundedBox(size, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.48f, 8);
}
