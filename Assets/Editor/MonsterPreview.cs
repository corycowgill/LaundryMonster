using UnityEngine;
using UnityEditor;

/// <summary>
/// Look at the Monster the way the player will, without having to play badly for five
/// minutes first.
///
/// The Monster is only itself at full size with its face out, and that state is reached
/// by letting a day go wrong. Checking the art by playing to it is slow enough that it
/// does not get checked, which is how it stayed photoscanned for so long.
/// </summary>
public static class MonsterPreview
{
    [MenuItem("Laundry Monster/Preview Monster (angry)")]
    public static string Angry() => Pose(2.9f, true);

    [MenuItem("Laundry Monster/Preview Monster (calm)")]
    public static string Calm() => Pose(1.3f, false);

    static string Pose(float scale, bool face)
    {
        var m = GameObject.Find("MonsterPile");
        if (m == null) return "no MonsterPile - build the room first";

        m.transform.localScale = Vector3.one * scale;

        Transform faceT = null, arm = null;
        foreach (var t in m.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Face") faceT = t;
            else if (t.name == "ArmPivot") arm = t;
        }
        if (faceT != null) faceT.gameObject.SetActive(face);

        // Mid-reach, which is the pose the arm exists for.
        if (arm != null)
        {
            arm.localRotation = face
                ? Quaternion.Euler(LaundryMonster.MonsterArm.ReachEuler)
                : Quaternion.Euler(LaundryMonster.MonsterArm.RestEuler);
            arm.localScale = face
                ? LaundryMonster.MonsterArm.ReachScale
                : LaundryMonster.MonsterArm.RestScale;
        }

        return $"Monster posed at {scale}x, face={face}, arm={(arm != null ? "found" : "MISSING")}";
    }
}
