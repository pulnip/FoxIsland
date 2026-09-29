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

        public static float TopY(int level) => level * LevelHeight;

        public static Vector3 TopCenter(Vector2Int coordinates, int level) =>
            new(coordinates.x * TileSize, TopY(level), coordinates.y * TileSize);

        /// <summary>
        /// Space a <paramref name="width"/> × <paramref name="depth"/> grid can occupy, from the tile
        /// bottoms up to the top of the highest possible level.
        /// </summary>
        public static Bounds GridBounds(int width, int depth)
        {
            var half = TileSize * 0.5f;
            var bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(-half, TileBottom, -half),
                new Vector3((width - 1) * TileSize + half, TopY(TileSpec.MaxLevel), (depth - 1) * TileSize + half));
            return bounds;
        }
    }
}
