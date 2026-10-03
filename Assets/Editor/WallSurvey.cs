using UnityEngine;
using UnityEditor;
using System.Text;

/// <summary>
/// Which parts of the walls the player can actually see.
///
/// Posters were being hung by eye and then checked in a screenshot, which is slow and
/// gets the near-vertical side walls wrong - they are seen so obliquely that intuition
/// about "head height" is worthless. This walks a grid over each wall, projects every
/// point through the real gameplay camera, and reports the band that is both inside the
/// frame and not behind a machine.
///
/// Run it, read the bands, hang the posters in them.
/// </summary>
public static class WallSurvey
{
    [MenuItem("Laundry Monster/Survey Walls")]
    public static string Survey()
    {
        var cam = Camera.main;
        if (cam == null) return "no camera";

        var sb = new StringBuilder();
        sb.AppendLine($"camera {cam.transform.position} fov {cam.fieldOfView:0.0} aspect {cam.aspect:0.00}");

        // The HUD owns the top and bottom of the screen: a day card and an objective
        // card across the top, a Monster card and the action bar across the bottom.
        // Anything outside this band is on screen but underneath a panel.
        const float SafeLow = 0.18f, SafeHigh = 0.80f;

        Report(sb, cam, "LEFT  wall (x -8.07)", -8.07f, true, SafeLow, SafeHigh);
        Report(sb, cam, "RIGHT wall (x  8.07)", 8.07f, true, SafeLow, SafeHigh);
        Report(sb, cam, "BACK  wall (z  7.37)", 7.37f, false, SafeLow, SafeHigh);
        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    /// <summary>
    /// Score every poster actually in the scene: what fraction of each one the player can
    /// see, through the real camera, with everything else in the room in the way.
    ///
    /// Hanging a poster and eyeballing a screenshot misses the ones that are a third
    /// behind a cupboard, and misses entirely the ones whose top edge is under the
    /// objective card. This gives a number per poster, so "make sure they are visible"
    /// is something that can be checked rather than hoped.
    /// </summary>
    /// <summary>
    /// Score every candidate poster slot along both side walls, at the worst aspect.
    ///
    /// Nudging one poster and re-checking is a slow way to find out that the closet
    /// shadows half the right-hand wall. This prints the whole wall at once, so the
    /// placement can be read off instead of searched for.
    /// </summary>
    [MenuItem("Laundry Monster/Sweep Poster Slots")]
    public static string Sweep()
    {
        var cam = Camera.main;
        if (cam == null) return "no camera";

        var fr = cam.GetComponent<LaundryMonster.CameraFraming>();
        var apply = typeof(LaundryMonster.CameraFraming).GetMethod("Apply",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);

        var rends = new System.Collections.Generic.List<Renderer>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (n.StartsWith("Wall_") || n.StartsWith("Band_") || n.StartsWith("Skirt")
                || n.StartsWith("Floor") || n.StartsWith("Poster") || n == "Sheet" || n == "Frame")
                continue;
            // The hero walks; he is not a permanent obstruction.
            if (r.GetComponentInParent<LaundryMonster.PlayerController>() != null) continue;
            rends.Add(r);
        }

        float[] aspects = { 2.25f, 1.777f, 1.333f };
        var sb = new StringBuilder();
        sb.AppendLine("worst-of-three visibility for a 1.02m poster centred at y 1.62");
        var head = new StringBuilder("   z:   ");
        for (int i = 0; i < 14; i++) head.Append((0.2f + i * 0.4f).ToString("0.0").PadLeft(6));
        sb.AppendLine(head.ToString());

        foreach (float wallX in new[] { -8.07f, 8.07f })
        {
            var row = new StringBuilder((wallX < 0 ? " LEFT" : " RIGHT").PadRight(8));
            for (int i = 0; i < 14; i++)
            {
                float z = 0.2f + i * 0.4f;
                float worst = 999f;
                foreach (float asp in aspects)
                {
                    cam.aspect = asp;
                    if (fr != null && apply != null) apply.Invoke(fr, new object[] { asp });
                    int clear = 0, n = 0;
                    for (float u = -0.45f; u <= 0.45f; u += 0.225f)
                    for (float v = -0.45f; v <= 0.45f; v += 0.225f)
                    {
                        var w = new Vector3(wallX, 1.62f + v * 1.02f,
                                            z + u * 1.02f * (wallX < 0 ? -1f : 1f));
                        var vp = cam.WorldToViewportPoint(w);
                        n++;
                        if (vp.z <= 0f || vp.x < 0.03f || vp.x > 0.97f
                            || vp.y < 0.18f || vp.y > 0.80f) continue;
                        var from = cam.transform.position;
                        var dir = w - from;
                        float dist = dir.magnitude;
                        bool hit = false;
                        foreach (var r in rends)
                        {
                            var b = r.bounds;
                            if (b.IntersectRay(new Ray(from, dir.normalized), out float h)
                                && h < dist - 0.25f) { hit = true; break; }
                        }
                        if (!hit) clear++;
                    }
                    worst = Mathf.Min(worst, 100f * clear / n);
                }
                row.Append(worst.ToString("0").PadLeft(6));
            }
            sb.AppendLine(row.ToString());
        }
        cam.ResetAspect();
        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    [MenuItem("Laundry Monster/Check Poster Visibility")]
    public static string CheckPosters()
    {
        var cam = Camera.main;
        if (cam == null) return "no camera";

        // Checked at three shapes, not one. The side walls are the first thing a narrow
        // window loses: a poster that fills the frame on a phone held sideways can be
        // entirely past the edge at 4:3, and tuning against the editor's current aspect
        // is how four of them ended up invisible on every desktop.
        float[] aspects = { 2.25f, 1.777f, 1.333f };
        string[] labels = { "wide", "16:9", "4:3" };

        var fr = cam.GetComponent<LaundryMonster.CameraFraming>();
        var apply = typeof(LaundryMonster.CameraFraming).GetMethod("Apply",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);

        var sheets = new System.Collections.Generic.List<Transform>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Poster_")) continue;
            var sh = t.Find("Sheet");
            if (sh != null) sheets.Add(sh);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{sheets.Count} posters, scored at {string.Join(" / ", labels)}");

        int bad = 0;
        var worstOf = new float[sheets.Count];
        var reason = new string[sheets.Count];
        for (int i = 0; i < worstOf.Length; i++) worstOf[i] = 999f;

        for (int a = 0; a < aspects.Length; a++)
        {
            cam.aspect = aspects[a];
            if (fr != null && apply != null) apply.Invoke(fr, new object[] { aspects[a] });

            for (int s2 = 0; s2 < sheets.Count; s2++)
            {
                int clear = 0, off = 0, hud = 0, blocked = 0, n = 0;
                var culprits = new System.Collections.Generic.Dictionary<string, int>();
                for (float u = -0.45f; u <= 0.45f; u += 0.15f)
                for (float v = -0.45f; v <= 0.45f; v += 0.15f)
                {
                    var w = sheets[s2].TransformPoint(new Vector3(u, v, 0f));
                    var vp = cam.WorldToViewportPoint(w);
                    n++;
                    if (vp.z <= 0f || vp.x < 0.03f || vp.x > 0.97f) off++;
                    else if (vp.y < 0.18f || vp.y > 0.80f) hud++;
                    else
                    {
                        var who = Blocker(cam, w);
                        if (who != null)
                        {
                            blocked++;
                            culprits.TryGetValue(who, out int c);
                            culprits[who] = c + 1;
                        }
                        else clear++;
                    }
                }
                float pct = 100f * clear / n;
                if (pct < worstOf[s2])
                {
                    worstOf[s2] = pct;
                    string worstCulprit = "";
                    int most = 0;
                    foreach (var kv in culprits) if (kv.Value > most) { most = kv.Value; worstCulprit = kv.Key; }
                    reason[s2] = $"at {labels[a]}: off {100f * off / n:0}% hud {100f * hud / n:0}% "
                                 + $"blocked {100f * blocked / n:0}%"
                                 + (worstCulprit == "" ? "" : $" by {worstCulprit}");
                }
            }
        }

        cam.ResetAspect();
        if (fr != null && apply != null) apply.Invoke(fr, new object[] { cam.aspect });

        for (int i = 0; i < sheets.Count; i++)
        {
            string name = sheets[i].parent.name.Replace("Poster_poster_", "");
            string verdict = worstOf[i] >= 85f ? "good" : worstOf[i] >= 60f ? "MARGINAL" : "HIDDEN";
            if (worstOf[i] < 85f) bad++;
            sb.AppendLine($"  {name,-18} worst {worstOf[i],5:0}%  {reason[i],-42} {verdict}");
        }
        sb.AppendLine($"{bad} of {sheets.Count} below 85% at their worst aspect.");
        Debug.Log(sb.ToString());
        return sb.ToString();
    }

    static void Report(StringBuilder sb, Camera cam, string label, float fixedCoord,
                       bool sideWall, float lo, float hi)
    {
        sb.AppendLine();
        sb.AppendLine(label);

        // Along the wall, and up it.
        float aFrom = sideWall ? -3.4f : -7.5f;
        float aTo = sideWall ? 6.6f : 7.5f;

        for (float a = aFrom; a <= aTo + 0.01f; a += 1.0f)
        {
            string row = $"  {(sideWall ? "z" : "x")}={a,5:0.0} : ";
            for (float y = 0.9f; y <= 3.2f; y += 0.3f)
            {
                var p = sideWall ? new Vector3(fixedCoord, y, a) : new Vector3(a, y, fixedCoord);
                var v = cam.WorldToViewportPoint(p);

                char c;
                if (v.z <= 0f || v.x < 0.02f || v.x > 0.98f || v.y < 0.02f || v.y > 0.98f)
                    c = '.';                      // off screen entirely
                else if (v.y < lo || v.y > hi)
                    c = '~';                      // on screen, but under the HUD
                else if (Blocked(cam, p))
                    c = 'x';                      // something is in the way
                else
                    c = '#';                      // clear
                row += c;
            }
            sb.AppendLine(row + "   (y 0.9 -> 3.2 in 0.3 steps)");
        }
    }

    static bool Blocked(Camera cam, Vector3 target) => Blocker(cam, target) != null;

    /// <summary>
    /// What is between the camera and this patch of wall, if anything.
    ///
    /// Returns the name, not just a yes - "hidden" is not actionable, "hidden by the
    /// detergent shelf" is.
    /// </summary>
    static string Blocker(Camera cam, Vector3 target)
    {
        var from = cam.transform.position;
        var dir = target - from;
        float dist = dir.magnitude;

        // Props are built without colliders, so a physics ray would sail through all of
        // them. Walk the renderers instead and test their bounds against the segment.
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var n = r.gameObject.name;
            // The wall itself, its band and its skirting are the thing we are aiming at.
            if (n.StartsWith("Wall_") || n.StartsWith("Band_") || n.StartsWith("Skirt")
                || n.StartsWith("Floor") || n.StartsWith("Poster") || n == "Sheet"
                || n == "Frame") continue;

            // No "is the target inside this?" let-off. That exemption was meant to stop a
            // prop occluding itself, and instead it quietly excused the worst case there
            // is: a poster standing INSIDE the closet scored 100% visible, because every
            // sample point was within the closet's bounds and so skipped the test. The
            // distance margin below already handles self-occlusion.
            var b = r.bounds;
            if (b.IntersectRay(new Ray(from, dir.normalized), out float hit)
                && hit < dist - 0.25f)
                return n;
        }
        return null;
    }
}
