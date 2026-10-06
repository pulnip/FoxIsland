using System;

namespace MyGame.Map
{
    /// <summary>A stage by position: world index and stage index within it, both zero-based.</summary>
    public readonly struct StageRef : IEquatable<StageRef>
    {
        public StageRef(int world, int index)
        {
            World = world;
            Index = index;
        }

        public int World { get; }

        public int Index { get; }

        /// <summary>One-based, as players see it: <c>2-3</c> is world 2, stage 3.</summary>
        public string Label => $"{World + 1}-{Index + 1}";

        public bool Equals(StageRef other) => World == other.World && Index == other.Index;

        public override bool Equals(object obj) => obj is StageRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(World, Index);

        public static bool operator ==(StageRef left, StageRef right) => left.Equals(right);

        public static bool operator !=(StageRef left, StageRef right) => !left.Equals(right);

        public override string ToString() => Label;
    }
}
