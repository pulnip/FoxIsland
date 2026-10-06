using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// One level (plan.md §4.3): tile rows in the §4.2 text notation plus its item setup.
    /// Edited with Tools/MyGame/Map Tool; <see cref="MapLayoutValidator"/> checks it.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/Map Layout", fileName = "MapLayout")]
    public class MapLayout : ScriptableObject
    {
        public const int MinSize = 2;
        public const int MaxSize = 8;

        [Tooltip("First row is the farthest from the camera. Space-separated tokens: " +
                 ". hole, 0-2 solid level, C crumbling, S start.")]
        [SerializeField] private string[] _rows =
        {
            "1 . 2 2",
            "C 1 . C",
            "0 . C 1",
            "S 0 0 .",
        };

        [SerializeField] private ItemMode _itemMode = ItemMode.Random;
        [SerializeField, Min(0)] private int _randomItemCount = 4;
        [SerializeField] private ItemPlacement[] _items = Array.Empty<ItemPlacement>();

        private TileSpec[,] _tiles;

        public int Width => Tiles.GetLength(0);

        public int Depth => Tiles.GetLength(1);

        public IReadOnlyList<string> Rows => _rows;

        public ItemMode ItemMode
        {
            get => _itemMode;
            set => _itemMode = value;
        }

        public int RandomItemCount
        {
            get => _randomItemCount;
            set => _randomItemCount = Mathf.Max(0, value);
        }

        /// <summary>Used when <see cref="ItemMode"/> is <see cref="Map.ItemMode.Fixed"/>.</summary>
        public IReadOnlyList<ItemPlacement> Items => _items;

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

        /// <summary>A fresh copy of the tiles, indexed [column, row], for editing.</summary>
        public bool TryGetTiles(out TileSpec[,] tiles, out string error) => TryParse(_rows, out tiles, out error);

        public void SetRows(string[] rows)
        {
            _rows = rows;
            _tiles = null;
        }

        /// <summary>Writes <paramref name="tiles"/> ([column, row]) back as text rows.</summary>
        public void SetTiles(TileSpec[,] tiles)
        {
            var width = tiles.GetLength(0);
            var depth = tiles.GetLength(1);
            var rows = new string[depth];
            var builder = new StringBuilder();
            for (var i = 0; i < depth; i++)
            {
                builder.Clear();
                var row = depth - 1 - i;
                for (var column = 0; column < width; column++)
                {
                    if (column > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(tiles[column, row].ToToken());
                }

                rows[i] = builder.ToString();
            }

            SetRows(rows);
        }

        public void SetItems(ItemPlacement[] items) => _items = items;

        /// <summary>
        /// Checks the text shape only (tokens, rectangular rows). Content rules such as the single
        /// start tile belong to <see cref="MapLayoutValidator"/>, so a layout mid-edit still loads.
        /// </summary>
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
            for (var i = 0; i < depth; i++)
            {
                var tokens = (rows[i] ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
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

                    tiles[column, row] = spec;
                }
            }

            if (width == 0)
            {
                error = "layout rows are empty";
                return false;
            }

            error = null;
            return true;
        }

        private void OnEnable() => _tiles = null;

        private void OnValidate()
        {
            _tiles = null;
            foreach (var issue in MapLayoutValidator.Validate(this).Issues)
            {
                if (issue.Severity == IssueSeverity.Error)
                {
                    Debug.LogWarning($"[MapLayout] {name}: {issue.Message}", this);
                }
            }
        }
    }
}
