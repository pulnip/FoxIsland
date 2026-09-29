using UnityEngine;

namespace MyGame.Map
{
    [CreateAssetMenu(menuName = "MyGame/Map Layout", fileName = "MapLayout")]
    public class MapLayout : ScriptableObject
    {
        [Tooltip("First row is the farthest from the camera. Space-separated tokens: " +
                 ". hole, 0-2 solid level, C crumbling, S start.")]
        [SerializeField] private string[] _rows =
        {
            "0 0 0 0",
            "0 0 0 0",
            "0 0 0 0",
            "S 0 0 0",
        };

        private TileSpec[,] _tiles;

        public int Width => Tiles.GetLength(0);

        public int Depth => Tiles.GetLength(1);

        private TileSpec[,] Tiles
        {
            get
            {
                if (_tiles == null)
                {
                    if (!TryParse(_rows, out _tiles, out var error))
                    {
                        Debug.LogError($"[MapLayout] {name}: {error}", this);
                        _tiles = new TileSpec[0, 0];
                    }
                }

                return _tiles;
            }
        }

        /// <summary>
        /// Row 0 is the row nearest the camera.
        /// </summary>
        public TileSpec GetTile(Vector2Int coordinates) => Tiles[coordinates.x, coordinates.y];

        public bool TryGetStart(out Vector2Int coordinates)
        {
            for (var row = 0; row < Depth; row++)
            {
                for (var column = 0; column < Width; column++)
                {
                    if (Tiles[column, row].Type == TileType.Start)
                    {
                        coordinates = new Vector2Int(column, row);
                        return true;
                    }
                }
            }

            coordinates = default;
            return false;
        }

        public void SetRows(string[] rows)
        {
            _rows = rows;
            _tiles = null;
        }

        private static bool TryParse(string[] rows, out TileSpec[,] tiles, out string error)
        {
            tiles = null;
            if (rows == null || rows.Length == 0)
            {
                error = "layout has no rows";
                return false;
            }

            var depth = rows.Length;
            var width = -1;
            var startCount = 0;
            for (var i = 0; i < depth; i++)
            {
                var tokens = (rows[i] ?? string.Empty).Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                if (width < 0)
                {
                    width = tokens.Length;
                    tiles = new TileSpec[width, depth];
                }
                else if (tokens.Length != width)
                {
                    error = $"row {i} has {tokens.Length} tiles, expected {width}";
                    return false;
                }

                var row = depth - 1 - i;
                for (var column = 0; column < width; column++)
                {
                    if (!TileSpec.TryParse(tokens[column], out var spec))
                    {
                        error = $"row {i}, column {column}: unknown token '{tokens[column]}'";
                        return false;
                    }

                    if (spec.Type == TileType.Start)
                    {
                        startCount++;
                    }

                    tiles[column, row] = spec;
                }
            }

            if (width == 0)
            {
                error = "layout rows are empty";
                return false;
            }

            if (startCount != 1)
            {
                error = $"layout needs exactly one start tile (S), found {startCount}";
                return false;
            }

            error = null;
            return true;
        }

        private void OnEnable() => _tiles = null;

        private void OnValidate()
        {
            _tiles = null;
            if (!TryParse(_rows, out _, out var error))
            {
                Debug.LogWarning($"[MapLayout] {name}: {error}", this);
            }
        }
    }
}
