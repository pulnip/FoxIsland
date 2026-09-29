using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// World-space conventions for the tile grid (plan.md §4.4).
    /// </summary>
    public static class MapMetrics
    {
        public const float TileSize = 1f;
        public const float LevelHeight = 0.5f;
        public const float TileBottom = -0.5f;

        public static float TopY(int level) => level * LevelHeight;

        public static Vector3 TopCenter(Vector2Int coordinates, int level) =>
            new(coordinates.x * TileSize, TopY(level), coordinates.y * TileSize);
    }
}
