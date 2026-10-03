using UnityEngine;

namespace LaundryMonster
{
    /// <summary>
    /// Where the Monster's arm rests and where it reaches to.
    ///
    /// Shared between MonsterArt, which builds the arm tucked in, and MonsterAnimator,
    /// which stretches it out during a snatch. Two copies of these numbers would drift
    /// apart and leave the arm starting its reach already half extended.
    ///
    /// It lives with the runtime code rather than beside the builder because the builder
    /// is in the editor assembly, which nothing at runtime is allowed to see.
    /// </summary>
    public static class MonsterArm
    {
        /// <summary>Folded up against the pile, squashed to a stub.</summary>
        public static readonly Vector3 RestEuler = new Vector3(0f, -12f, 54f);
        public static readonly Vector3 RestScale = new Vector3(0.20f, 1f, 1f);

        /// <summary>
        /// Out toward the sorting basket. The Monster stands at (-5.9, 4.6) and the
        /// basket is at (0, 0.5), so from the arm's own +x that is about 35 degrees of
        /// yaw, tipped down a little because the arm leaves at chest height and the
        /// basket is near the floor.
        /// </summary>
        public static readonly Vector3 ReachEuler = new Vector3(0f, 35f, -14f);
        public static readonly Vector3 ReachScale = new Vector3(1.15f, 1f, 1f);
    }
}
