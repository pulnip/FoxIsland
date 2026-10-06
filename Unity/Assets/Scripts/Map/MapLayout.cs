using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// One level (plan.md §4.3): cell rows in the <see cref="CellSpec"/> token notation, the start
    /// cell, the item setup, and the puzzle elements (§4.10). Edited with Tools/MyGame/Map Tool;
    /// <see cref="MapLayoutValidator"/> checks it.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/Map Layout", fileName = "MapLayout")]
    public class MapLayout : ScriptableObject, ISerializationCallbackReceiver
    {
        public const int MinSize = 2;
        public const int MaxSize = 8;

        [Tooltip("First row is the farthest from the camera. Space-separated tokens: '.' hole, " +
                 "or <height 0-6><skin s m f r n i w b>[c for crumbling], e.g. 0s 2m 5i 0mc.")]
        [SerializeField] private string[] _rows =
        {
            "1m . 2m 2m",
            "0sc 1m . 0sc",
            "0m . 0sc 1m",
            "0m 0m 0m .",
        };

        [Tooltip("Column and row of the start cell; row 0 is nearest the camera.")]
        [SerializeField] private Vector2Int _start;

        [SerializeField] private ItemMode _itemMode = ItemMode.Random;
        [SerializeField, Min(0)] private int _randomItemCount = 4;
        [SerializeField] private ItemPlacement[] _items = Array.Empty<ItemPlacement>();

        [Header("Puzzle elements")]
        [SerializeField] private TideSettings _tide = TideSettings.Default;
        [SerializeField] private SwitchPlacement[] _switches = Array.Empty<SwitchPlacement>();
        [SerializeField] private RisingBlockPlacement[] _risingBlocks = Array.Empty<RisingBlockPlacement>();
        [SerializeField] private Vector2Int[] _crates = Array.Empty<Vector2Int>();
        [SerializeField] private Vector2Int[] _springboards = Array.Empty<Vector2Int>();
        [SerializeField] private PlatformPlacement[] _platforms = Array.Empty<PlatformPlacement>();

        private CellSpec[,] _cells;

        public int Width => Cells.GetLength(0);

        public int Depth => Cells.GetLength(1);

        public IReadOnlyList<string> Rows => _rows;

        public Vector2Int Start
        {
            get => _start;
            set => _start = value;
        }

        /// <summary>The highest block, raised block, or platform; frames the camera.</summary>
        public int MaxHeight
        {
            get
            {
                var max = 0;
                foreach (var cell in Cells)
                {
                    max = Mathf.Max(max, cell.Height);
                }

                foreach (var block in _risingBlocks)
                {
                    max = Mathf.Max(max, block.RaisedHeight);
                }

                foreach (var platform in _platforms)
                {
                    max = Mathf.Max(max, platform.Height);
                }

                return Mathf.Min(max, CellSpec.MaxHeight);
            }
        }

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

        public TideSettings Tide
        {
            get => _tide;
            set => _tide = value;
        }

        public IReadOnlyList<SwitchPlacement> Switches => _switches;

        public IReadOnlyList<RisingBlockPlacement> RisingBlocks => _risingBlocks;

        /// <summary>The cells the crates start on.</summary>
        public IReadOnlyList<Vector2Int> Crates => _crates;

        public IReadOnlyList<Vector2Int> Springboards => _springboards;

        public IReadOnlyList<PlatformPlacement> Platforms => _platforms;

        private CellSpec[,] Cells
        {
            get
            {
                if (_cells == null)
                {
                    if (!TryParse(_rows, out _cells, out var error))
                    {
                        Debug.LogError($"[MapLayout] {name}: {error}", this);
                        _cells = new CellSpec[0, 0];
                    }
                }

                return _cells;
            }
        }

        /// <summary>
        /// Row 0 is the row nearest the camera.
        /// </summary>
        public CellSpec GetCell(Vector2Int coordinates) => Cells[coordinates.x, coordinates.y];

        public bool IsInside(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Depth;

        /// <summary>The start cell, if it lies on a block.</summary>
        public bool TryGetStart(out Vector2Int coordinates)
        {
            coordinates = _start;
            return IsInside(_start) && Cells[_start.x, _start.y].IsBlock;
        }

        /// <summary>A fresh copy of the cells, indexed [column, row], for editing.</summary>
        public bool TryGetCells(out CellSpec[,] cells, out string error) => TryParse(_rows, out cells, out error);

        public void SetRows(string[] rows)
        {
            _rows = rows;
            _cells = null;
        }

        /// <summary>Writes <paramref name="cells"/> ([column, row]) back as text rows.</summary>
        public void SetCells(CellSpec[,] cells)
        {
            var width = cells.GetLength(0);
            var depth = cells.GetLength(1);
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

                    builder.Append(cells[column, row].ToToken());
                }

                rows[i] = builder.ToString();
            }

            SetRows(rows);
        }

        public void SetItems(ItemPlacement[] items) => _items = items;

        public void SetSwitches(SwitchPlacement[] switches) => _switches = switches;

        public void SetRisingBlocks(RisingBlockPlacement[] risingBlocks) => _risingBlocks = risingBlocks;

        public void SetCrates(Vector2Int[] crates) => _crates = crates;

        public void SetSpringboards(Vector2Int[] springboards) => _springboards = springboards;

        public void SetPlatforms(PlatformPlacement[] platforms) => _platforms = platforms;

        /// <summary>Items never float over a rising block or a crate's starting cell.</summary>
        public bool KeepsItemsOff(Vector2Int cell) =>
            Array.Exists(_risingBlocks, block => block.Cell == cell) || Array.IndexOf(_crates, cell) >= 0;

        /// <summary>Copies everything but the name from <paramref name="other"/>.</summary>
        public void CopyFrom(MapLayout other)
        {
            SetRows((string[])other._rows.Clone());
            _start = other._start;
            _itemMode = other._itemMode;
            _randomItemCount = other._randomItemCount;
            _items = (ItemPlacement[])other._items.Clone();
            _tide = other._tide;
            _switches = (SwitchPlacement[])other._switches.Clone();
            _risingBlocks = (RisingBlockPlacement[])other._risingBlocks.Clone();
            _crates = (Vector2Int[])other._crates.Clone();
            _springboards = (Vector2Int[])other._springboards.Clone();
            _platforms = (PlatformPlacement[])other._platforms.Clone();
        }

        /// <summary>
        /// Checks the text shape only (tokens, rectangular rows). Content rules such as the start
        /// cell belong to <see cref="MapLayoutValidator"/>, so a layout mid-edit still loads.
        /// </summary>
        private static bool TryParse(string[] rows, out CellSpec[,] cells, out string error)
        {
            cells = null;
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
                    cells = new CellSpec[width, depth];
                }
                else if (tokens.Length != width)
                {
                    error = $"row {i} has {tokens.Length} cells, expected {width}";
                    return false;
                }

                var row = depth - 1 - i;
                for (var column = 0; column < width; column++)
                {
                    if (!CellSpec.TryParse(tokens[column], out var spec))
                    {
                        error = $"row {i}, column {column}: unknown token '{tokens[column]}'";
                        return false;
                    }

                    cells[column, row] = spec;
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

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        /// <summary>Undo, JSON overwrite, and reloads replace the rows behind the parse cache's back.</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize() => _cells = null;

        private void OnEnable() => _cells = null;

        private void OnValidate()
        {
            _cells = null;
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
