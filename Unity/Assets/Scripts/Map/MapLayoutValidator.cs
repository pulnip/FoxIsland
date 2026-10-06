using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Checks a layout for mistakes and approximates where the fox can get (plan.md §4.8).
    /// Movement rules follow the §4.5 tuning: one level up is a comfortable jump, two levels barely
    /// clears, any drop is fine, and a running jump crosses one hole onto a tile no higher than
    /// the takeoff.
    /// </summary>
    public static class MapLayoutValidator
    {
        private const int ComfortableClimb = 1;
        private const int HardClimb = 2;

        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static LayoutReport Validate(MapLayout layout)
        {
            if (!layout.TryGetTiles(out var tiles, out var error))
            {
                var failed = new LayoutReport(new Reachability[0, 0]);
                failed.Add(IssueSeverity.Error, $"Rows do not parse: {error}.");
                return failed;
            }

            var width = tiles.GetLength(0);
            var depth = tiles.GetLength(1);
            var easy = Flood(tiles, allowHardClimb: false, out var startCount);
            var any = Flood(tiles, allowHardClimb: true, out _);
            var cells = new Reachability[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    cells[column, row] = tiles[column, row].Type == TileType.Hole ? Reachability.NotStandable
                        : easy[column, row] ? Reachability.Reachable
                        : any[column, row] ? Reachability.HardJump
                        : Reachability.Unreachable;
                }
            }

            var report = new LayoutReport(cells);
            if (width < MapLayout.MinSize || width > MapLayout.MaxSize || depth < MapLayout.MinSize || depth > MapLayout.MaxSize)
            {
                report.Add(IssueSeverity.Error, $"Size {width} × {depth} is outside {MapLayout.MinSize}–{MapLayout.MaxSize} per side.");
            }

            if (startCount != 1)
            {
                report.Add(IssueSeverity.Error, startCount == 0 ? "No start tile (S)." : $"{startCount} start tiles; there must be exactly one.");
            }

            CheckItems(layout, tiles, cells, report);
            if (startCount == 1)
            {
                AddReachabilityWarnings(cells, report);
            }

            return report;
        }

        private static void CheckItems(MapLayout layout, TileSpec[,] tiles, Reachability[,] cells, LayoutReport report)
        {
            var solidTiles = 0;
            var unreachableSolid = 0;
            for (var column = 0; column < tiles.GetLength(0); column++)
            {
                for (var row = 0; row < tiles.GetLength(1); row++)
                {
                    if (tiles[column, row].Type == TileType.Solid)
                    {
                        solidTiles++;
                        if (cells[column, row] == Reachability.Unreachable)
                        {
                            unreachableSolid++;
                        }
                    }
                }
            }

            if (layout.ItemMode == ItemMode.Random)
            {
                if (layout.RandomItemCount == 0)
                {
                    report.Add(IssueSeverity.Error, "No items: the level would clear instantly.");
                }
                else if (layout.RandomItemCount > solidTiles)
                {
                    report.Add(IssueSeverity.Warning, $"{layout.RandomItemCount} random items but only {solidTiles} solid tiles; {solidTiles} will spawn.");
                }

                if (unreachableSolid > 0 && layout.RandomItemCount > 0)
                {
                    report.Add(IssueSeverity.Warning, $"Random items can land on {unreachableSolid} unreachable solid tile(s).");
                }

                return;
            }

            if (layout.Items.Count == 0)
            {
                report.Add(IssueSeverity.Error, "No items placed: the level would clear instantly.");
            }

            var seen = new HashSet<Vector2Int>();
            foreach (var item in layout.Items)
            {
                var cell = item.Cell;
                var inside = cell.x >= 0 && cell.y >= 0 && cell.x < tiles.GetLength(0) && cell.y < tiles.GetLength(1);
                if (!inside || tiles[cell.x, cell.y].Type != TileType.Solid)
                {
                    report.Add(IssueSeverity.Error, $"Item at {Format(cell)} is not on a solid tile.", inside ? cell : null);
                    continue;
                }

                if (!seen.Add(cell))
                {
                    report.Add(IssueSeverity.Error, $"Two items at {Format(cell)}.", cell);
                }

                if (cells[cell.x, cell.y] == Reachability.Unreachable)
                {
                    report.Add(IssueSeverity.Warning, $"Item at {Format(cell)} looks unreachable; the level may not clear.", cell);
                }
            }
        }

        private static void AddReachabilityWarnings(Reachability[,] cells, LayoutReport report)
        {
            for (var row = 0; row < cells.GetLength(1); row++)
            {
                for (var column = 0; column < cells.GetLength(0); column++)
                {
                    var cell = new Vector2Int(column, row);
                    switch (cells[column, row])
                    {
                        case Reachability.Unreachable:
                            report.Add(IssueSeverity.Warning, $"Tile {Format(cell)} looks unreachable.", cell);
                            break;
                        case Reachability.HardJump:
                            report.Add(IssueSeverity.Warning, $"Tile {Format(cell)} needs a two-level jump, which barely clears.", cell);
                            break;
                    }
                }
            }
        }

        /// <summary>Breadth-first search from the start tile; reports how many start tiles exist.</summary>
        private static bool[,] Flood(TileSpec[,] tiles, bool allowHardClimb, out int startCount)
        {
            var width = tiles.GetLength(0);
            var depth = tiles.GetLength(1);
            var reached = new bool[width, depth];
            var queue = new Queue<Vector2Int>();
            startCount = 0;
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    if (tiles[column, row].Type == TileType.Start)
                    {
                        startCount++;
                        if (startCount == 1)
                        {
                            reached[column, row] = true;
                            queue.Enqueue(new Vector2Int(column, row));
                        }
                    }
                }
            }

            var maxClimb = allowHardClimb ? HardClimb : ComfortableClimb;
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                var level = tiles[from.x, from.y].Level;
                foreach (var direction in Directions)
                {
                    var next = from + direction;
                    if (!IsInside(next, width, depth))
                    {
                        continue;
                    }

                    var target = next;
                    var nextSpec = tiles[next.x, next.y];
                    if (nextSpec.Type == TileType.Hole)
                    {
                        target = next + direction;
                        if (!IsInside(target, width, depth) || tiles[target.x, target.y].Type == TileType.Hole
                            || tiles[target.x, target.y].Level > level)
                        {
                            continue;
                        }
                    }
                    else if (nextSpec.Level - level > maxClimb)
                    {
                        continue;
                    }

                    if (!reached[target.x, target.y])
                    {
                        reached[target.x, target.y] = true;
                        queue.Enqueue(target);
                    }
                }
            }

            return reached;
        }

        private static bool IsInside(Vector2Int cell, int width, int depth) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;

        /// <summary>Column and row as the tool shows them: column from the left, row from the camera side.</summary>
        private static string Format(Vector2Int cell) => $"({cell.x}, {cell.y})";
    }
}
