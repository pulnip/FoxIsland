using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Links switches to the rising blocks they drive (plan.md §4.10): a group is on while any of
    /// its switches is pressed.
    /// </summary>
    public enum PuzzleGroup : byte
    {
        A,
        B,
        C,
    }

    public static class PuzzleGroups
    {
        public const int Count = 3;

        /// <summary>The group's color on switches, rising blocks, and in the map tool (sRGB).</summary>
        public static Color ColorOf(PuzzleGroup group) => group switch
        {
            PuzzleGroup.A => new Color(0.93f, 0.33f, 0.42f),
            PuzzleGroup.B => new Color(0.27f, 0.56f, 0.95f),
            _ => new Color(0.66f, 0.42f, 0.92f),
        };
    }
}
