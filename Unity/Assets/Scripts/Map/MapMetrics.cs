using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// World-space conventions for the tile grid (plan.md §4.4).
    /// </summary>
    public static class MapMetrics
    {
        public const float TileSize = 1.5f;
        public const float LevelHeight = 0.5f;
        public const float TileBottom = -0.5f;

        /// <summary>Surface of the lake around and under the grid; touching it ends the game.</summary>
        public const float WaterLevel = -0.3f;

        /// <summary>Floor of the lake; tile visuals reach down to it as pillars.</summary>
        public const float LakeBedY = -1.5f;

        /// <summary>Snow lying on alpine tiles; the fox stands on the tile top, sunk this deep.</summary>
        public const float SnowDepth = 0.08f;

        /// <summary>Water standing on a shallow-water block, above its top; the fox wades in it.</summary>
        public const float PoolDepth = 0.15f;

        /// <summary>Bottom of an iceberg's underwater body; it floats instead of reaching the lake bed.</summary>
        public const float IcebergBottom = -0.9f;

        public static float TopY(int level) => level * LevelHeight;

        public static Vector3 TopCenter(Vector2Int coordinates, int level) =>
            new(coordinates.x * TileSize, TopY(level), coordinates.y * TileSize);

        /// <summary>
        /// Space a <paramref name="width"/> × <paramref name="depth"/> grid occupies, from the tile
        /// bottoms up to the top of <paramref name="maxLevel"/>.
        /// </summary>
        public static Bounds GridBounds(int width, int depth, int maxLevel)
        {
            var half = TileSize * 0.5f;
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(-half, TileBottom, -half),
                new Vector3((width - 1) * TileSize + half, TopY(maxLevel), (depth - 1) * TileSize + half));
            return bounds;
        }

        /// <summary>
        /// What the camera keeps on screen: <see cref="GridBounds"/> raised by
        /// <paramref name="headroom"/> (the player's jump height) above the highest level.
        /// </summary>
        public static Bounds FramedBounds(int width, int depth, int maxLevel, float headroom)
        {
            var bounds = GridBounds(width, depth, maxLevel);
            bounds.SetMinMax(bounds.min, bounds.max + Vector3.up * headroom);
            return bounds;
        }
    }
}
