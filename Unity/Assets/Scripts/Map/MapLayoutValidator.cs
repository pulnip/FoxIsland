using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Checks a layout for mistakes and approximates where the fox can get (plan.md §4.8).
    /// Movement rules follow the §4.5 tuning: one level up is a comfortable jump, two levels barely
    /// clears, any drop is fine, and a running jump crosses one hole onto a block no higher than
    /// the takeoff.
    /// </summary>
    public static class MapLayoutValidator
    {
        private const int ComfortableClimb = 1;
        private const int HardClimb = 2;

        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public static LayoutReport Validate(MapLayout layout)
        {
            if (!layout.TryGetCells(out var cells, out var error))
            {
                var failed = new LayoutReport(new Reachability[0, 0]);
                failed.Add(IssueSeverity.Error, $"Rows do not parse: {error}.");
                return failed;
            }

            var width = cells.GetLength(0);
            var depth = cells.GetLength(1);
            var start = layout.Start;
            var hasStart = IsInside(start, width, depth) && cells[start.x, start.y].IsBlock;
            var easy = Flood(cells, start, hasStart, allowHardClimb: false);
            var any = Flood(cells, start, hasStart, allowHardClimb: true);
            var reach = new Reachability[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    reach[column, row] = !cells[column, row].IsBlock ? Reachability.NotStandable
                        : easy[column, row] ? Reachability.Reachable
                        : any[column, row] ? Reachability.HardJump
                        : Reachability.Unreachable;
                }
            }

            var report = new LayoutReport(reach);
            if (width < MapLayout.MinSize || width > MapLayout.MaxSize || depth < MapLayout.MinSize || depth > MapLayout.MaxSize)
            {
                report.Add(IssueSeverity.Error, $"Size {width} × {depth} is outside {MapLayout.MinSize}–{MapLayout.MaxSize} per side.");
            }

            if (!hasStart)
            {
                report.Add(IssueSeverity.Error, $"The start {Format(start)} is not on a block.", IsInside(start, width, depth) ? start : null);
            }

            CheckSkins(cells, report);
            CheckItems(layout, cells, reach, report);
            if (hasStart)
            {
                AddReachabilityWarnings(reach, report);
            }

            return report;
        }

        private static void CheckSkins(CellSpec[,] cells, LayoutReport report)
        {
            for (var row = 0; row < cells.GetLength(1); row++)
            {
                for (var column = 0; column < cells.GetLength(0); column++)
                {
                    var cell = cells[column, row];
                    if (cell.IsBlock && cell.Skin == Skin.Iceberg && cell.Height != 0)
                    {
                        report.Add(IssueSeverity.Error, $"Iceberg {Format(new Vector2Int(column, row))} floats in the lake, so it must be height 0.",
                            new Vector2Int(column, row));
                    }
                }
            }
        }

        private static void CheckItems(MapLayout layout, CellSpec[,] cells, Reachability[,] reach, LayoutReport report)
        {
            var holders = 0;
            var unreachableHolders = 0;
            for (var column = 0; column < cells.GetLength(0); column++)
            {
                for (var row = 0; row < cells.GetLength(1); row++)
                {
                    if (CanHoldItem(layout, cells, new Vector2Int(column, row)))
                    {
                        holders++;
                        if (reach[column, row] == Reachability.Unreachable)
                        {
                            unreachableHolders++;
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
                else if (layout.RandomItemCount > holders)
                {
                    report.Add(IssueSeverity.Warning, $"{layout.RandomItemCount} random items but only {holders} cells can hold one; {holders} will spawn.");
                }

                if (unreachableHolders > 0 && layout.RandomItemCount > 0)
                {
                    report.Add(IssueSeverity.Warning, $"Random items can land on {unreachableHolders} unreachable cell(s).");
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
                var inside = IsInside(cell, cells.GetLength(0), cells.GetLength(1));
                if (!inside || !CanHoldItem(layout, cells, cell))
                {
                    report.Add(IssueSeverity.Error, $"Item at {Format(cell)} needs a firm block that is not the start (no hole, crumbling, or iceberg).",
                        inside ? cell : null);
                    continue;
                }

                if (!seen.Add(cell))
                {
                    report.Add(IssueSeverity.Error, $"Two items at {Format(cell)}.", cell);
                }

                if (reach[cell.x, cell.y] == Reachability.Unreachable)
                {
                    report.Add(IssueSeverity.Warning, $"Item at {Format(cell)} looks unreachable; the level may not clear.", cell);
                }
            }
        }

        /// <summary>Items float over firm, still blocks; never over the start cell.</summary>
        public static bool CanHoldItem(MapLayout layout, CellSpec[,] cells, Vector2Int cell) =>
            cells[cell.x, cell.y].CanHoldItem && cell != layout.Start;

        private static void AddReachabilityWarnings(Reachability[,] reach, LayoutReport report)
        {
            for (var row = 0; row < reach.GetLength(1); row++)
            {
                for (var column = 0; column < reach.GetLength(0); column++)
                {
                    var cell = new Vector2Int(column, row);
                    switch (reach[column, row])
                    {
                        case Reachability.Unreachable:
                            report.Add(IssueSeverity.Warning, $"Block {Format(cell)} looks unreachable.", cell);
                            break;
                        case Reachability.HardJump:
                            report.Add(IssueSeverity.Warning, $"Block {Format(cell)} needs a two-level jump, which barely clears.", cell);
                            break;
                    }
                }
            }
        }

        /// <summary>Breadth-first search from the start cell over blocks.</summary>
        private static bool[,] Flood(CellSpec[,] cells, Vector2Int start, bool hasStart, bool allowHardClimb)
        {
            var width = cells.GetLength(0);
            var depth = cells.GetLength(1);
            var reached = new bool[width, depth];
            if (!hasStart)
            {
                return reached;
            }

            var queue = new Queue<Vector2Int>();
            reached[start.x, start.y] = true;
            queue.Enqueue(start);
            var maxClimb = allowHardClimb ? HardClimb : ComfortableClimb;
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                var height = cells[from.x, from.y].Height;
                foreach (var direction in Directions)
                {
                    var next = from + direction;
                    if (!IsInside(next, width, depth))
                    {
                        continue;
                    }

                    var target = next;
                    if (!cells[next.x, next.y].IsBlock)
                    {
                        target = next + direction;
                        if (!IsInside(target, width, depth) || !cells[target.x, target.y].IsBlock
                            || cells[target.x, target.y].Height > height)
                        {
                            continue;
                        }
                    }
                    else if (cells[next.x, next.y].Height - height > maxClimb)
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
