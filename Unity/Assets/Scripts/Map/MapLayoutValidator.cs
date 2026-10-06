using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Checks a layout for mistakes and approximates where the fox can get (plan.md §4.8).
    /// Movement rules follow the §4.5 tuning: one level up is a comfortable jump, two levels barely
    /// clears, any drop is fine, and a running jump crosses one hole onto a block no higher than
    /// the takeoff. Puzzle elements (§4.10) widen what a cell offers: a rising block stands at
    /// either height, a crate adds a step on its cell, a springboard climbs four levels, and a
    /// platform joins the cells along its path. The tide is judged at low water.
    /// </summary>
    public static class MapLayoutValidator
    {
        private const int ComfortableClimb = 1;
        private const int HardClimb = 2;
        private const int SpringClimb = 4;

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
            var floors = FloorHeights(layout, cells);
            var springs = new bool[width, depth];
            foreach (var cell in layout.Springboards.Where(cell => IsInside(cell, width, depth)))
            {
                springs[cell.x, cell.y] = true;
            }

            var paths = layout.Platforms.Where(platform => platform.IsStraight).Select(platform => platform.PathCells())
                .Where(path => path.All(cell => IsInside(cell, width, depth))).ToList();
            var easy = Flood(floors, springs, paths, start, hasStart, ComfortableClimb);
            var any = Flood(floors, springs, paths, start, hasStart, HardClimb);
            var reach = new Reachability[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    reach[column, row] = floors[column, row] == 0 ? Reachability.NotStandable
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
            CheckPuzzles(layout, cells, report);
            CheckTide(layout, cells, report);
            CheckItems(layout, cells, reach, report);
            if (hasStart)
            {
                AddReachabilityWarnings(reach, report);
            }

            return report;
        }

        /// <summary>
        /// Items float over firm, still blocks; never over the start cell, a rising block, or a
        /// crate's starting cell.
        /// </summary>
        public static bool CanHoldItem(MapLayout layout, CellSpec[,] cells, Vector2Int cell) =>
            cells[cell.x, cell.y].CanHoldItem && cell != layout.Start && !layout.KeepsItemsOff(cell);

        /// <summary>A block a puzzle element can sit on: not crumbling, not an iceberg.</summary>
        private static bool IsFirmBlock(CellSpec[,] cells, Vector2Int cell) =>
            IsInside(cell, cells.GetLength(0), cells.GetLength(1)) && cells[cell.x, cell.y].IsBlock
            && !cells[cell.x, cell.y].IsCrumbling && cells[cell.x, cell.y].Skin != Skin.Iceberg;

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

        private static void CheckPuzzles(MapLayout layout, CellSpec[,] cells, LayoutReport report)
        {
            var width = cells.GetLength(0);
            var depth = cells.GetLength(1);
            var rising = new HashSet<Vector2Int>(layout.RisingBlocks.Select(block => block.Cell));
            var springboards = new HashSet<Vector2Int>(layout.Springboards);
            var occupied = new HashSet<Vector2Int>();

            void CheckOccupant(Vector2Int cell, string what)
            {
                var inside = IsInside(cell, width, depth);
                if (!IsFirmBlock(cells, cell))
                {
                    report.Add(IssueSeverity.Error, $"{what} at {Format(cell)} needs a firm block (no hole, crumbling, or iceberg).", inside ? cell : null);
                }
                else if (!occupied.Add(cell))
                {
                    report.Add(IssueSeverity.Error, $"{what} at {Format(cell)} shares its cell with another switch, crate, or springboard.", cell);
                }
            }

            foreach (var floorSwitch in layout.Switches)
            {
                CheckOccupant(floorSwitch.Cell, $"Switch {floorSwitch.Group}");
                if (rising.Contains(floorSwitch.Cell))
                {
                    report.Add(IssueSeverity.Error, $"Switch at {Format(floorSwitch.Cell)} sits on a rising block.", floorSwitch.Cell);
                }
            }

            foreach (var cell in layout.Crates)
            {
                CheckOccupant(cell, "Crate");
                if (cell == layout.Start)
                {
                    report.Add(IssueSeverity.Error, $"Crate at {Format(cell)} sits on the start.", cell);
                }
            }

            foreach (var cell in springboards)
            {
                CheckOccupant(cell, "Springboard");
                if (cell == layout.Start)
                {
                    report.Add(IssueSeverity.Error, $"Springboard at {Format(cell)} sits on the start.", cell);
                }

                if (rising.Contains(cell))
                {
                    report.Add(IssueSeverity.Error, $"Springboard at {Format(cell)} sits on a rising block.", cell);
                }
            }

            var risingSeen = new HashSet<Vector2Int>();
            foreach (var block in layout.RisingBlocks)
            {
                var cell = block.Cell;
                if (!IsFirmBlock(cells, cell))
                {
                    report.Add(IssueSeverity.Error, $"Rising block at {Format(cell)} needs a firm block (no hole, crumbling, or iceberg).",
                        IsInside(cell, width, depth) ? cell : null);
                    continue;
                }

                if (!risingSeen.Add(cell))
                {
                    report.Add(IssueSeverity.Error, $"Two rising blocks at {Format(cell)}.", cell);
                }

                if (block.RaisedHeight == cells[cell.x, cell.y].Height)
                {
                    report.Add(IssueSeverity.Warning, $"Rising block at {Format(cell)} moves to the height it already has.", cell);
                }

                if (layout.Switches.All(floorSwitch => floorSwitch.Group != block.Group))
                {
                    report.Add(IssueSeverity.Warning, $"Rising block at {Format(cell)} never moves: group {block.Group} has no switch.", cell);
                }
            }

            foreach (var floorSwitch in layout.Switches)
            {
                if (layout.RisingBlocks.All(block => block.Group != floorSwitch.Group))
                {
                    report.Add(IssueSeverity.Warning, $"Switch at {Format(floorSwitch.Cell)} does nothing: group {floorSwitch.Group} has no rising block.",
                        IsInside(floorSwitch.Cell, width, depth) ? floorSwitch.Cell : null);
                }
            }

            foreach (var platform in layout.Platforms)
            {
                if (!IsInside(platform.From, width, depth) || !IsInside(platform.To, width, depth))
                {
                    report.Add(IssueSeverity.Error, $"Platform {Format(platform.From)}–{Format(platform.To)} leaves the grid.");
                    continue;
                }

                if (!platform.IsStraight)
                {
                    report.Add(IssueSeverity.Error, $"Platform {Format(platform.From)}–{Format(platform.To)} must run along one row or column.", platform.From);
                    continue;
                }

                foreach (var cell in platform.PathCells())
                {
                    var spec = cells[cell.x, cell.y];
                    if (spec.IsBlock && spec.Height >= platform.Height)
                    {
                        report.Add(IssueSeverity.Warning, $"Platform at height {platform.Height} passes through the block at {Format(cell)}.", cell);
                    }
                }
            }
        }

        private static void CheckTide(MapLayout layout, CellSpec[,] cells, LayoutReport report)
        {
            var tide = layout.Tide;
            if (!tide.Enabled)
            {
                return;
            }

            var highest = new Dictionary<Vector2Int, int>();
            foreach (var block in layout.RisingBlocks)
            {
                highest[block.Cell] = block.RaisedHeight;
            }

            var flooded = 0;
            for (var column = 0; column < cells.GetLength(0); column++)
            {
                for (var row = 0; row < cells.GetLength(1); row++)
                {
                    var cell = new Vector2Int(column, row);
                    var spec = cells[column, row];
                    if (!spec.IsBlock || spec.Skin == Skin.Iceberg)
                    {
                        continue;
                    }

                    var height = highest.TryGetValue(cell, out var raised) ? Mathf.Max(raised, spec.Height) : spec.Height;
                    if (MapMetrics.TopY(height) < tide.HighWaterLevel)
                    {
                        report.Flooded[column, row] = true;
                        flooded++;
                    }
                }
            }

            if (flooded > 0)
            {
                report.Add(IssueSeverity.Warning, $"{flooded} block(s) go under at high tide (≈); the fox must leave them in time.");
            }

            var start = layout.Start;
            if (IsInside(start, cells.GetLength(0), cells.GetLength(1)) && report.Flooded[start.x, start.y])
            {
                report.Add(IssueSeverity.Warning, $"The start {Format(start)} goes under at high tide.", start);
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
                    report.Add(IssueSeverity.Error,
                        $"Item at {Format(cell)} needs a firm block that is not the start (no hole, crumbling, iceberg, rising block, or crate).",
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

        /// <summary>
        /// Per cell, a bit mask of the heights the fox may stand at: the block, a rising block's
        /// raised height, the top of a crate on its starting cell, and a platform passing over.
        /// 0 means nothing to stand on.
        /// </summary>
        private static int[,] FloorHeights(MapLayout layout, CellSpec[,] cells)
        {
            var width = cells.GetLength(0);
            var depth = cells.GetLength(1);
            var floors = new int[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    if (cells[column, row].IsBlock)
                    {
                        floors[column, row] = 1 << cells[column, row].Height;
                    }
                }
            }

            foreach (var block in layout.RisingBlocks.Where(block => IsFirmBlock(cells, block.Cell)))
            {
                floors[block.Cell.x, block.Cell.y] |= 1 << block.RaisedHeight;
            }

            foreach (var cell in layout.Crates.Where(cell => IsFirmBlock(cells, cell)))
            {
                floors[cell.x, cell.y] |= 1 << (cells[cell.x, cell.y].Height + 1);
            }

            foreach (var platform in layout.Platforms.Where(platform => platform.IsStraight))
            {
                foreach (var cell in platform.PathCells().Where(cell => IsInside(cell, width, depth)))
                {
                    floors[cell.x, cell.y] |= 1 << platform.Height;
                }
            }

            return floors;
        }

        /// <summary>
        /// Breadth-first search from the start cell. A step works when some floor height of the next
        /// cell is at most <paramref name="maxClimb"/> above some floor height of this one.
        /// </summary>
        private static bool[,] Flood(int[,] floors, bool[,] springs, List<Vector2Int[]> paths, Vector2Int start, bool hasStart, int maxClimb)
        {
            var width = floors.GetLength(0);
            var depth = floors.GetLength(1);
            var reached = new bool[width, depth];
            if (!hasStart)
            {
                return reached;
            }

            var queue = new Queue<Vector2Int>();

            void Reach(Vector2Int cell)
            {
                if (!reached[cell.x, cell.y])
                {
                    reached[cell.x, cell.y] = true;
                    queue.Enqueue(cell);
                }
            }

            Reach(start);
            while (queue.Count > 0)
            {
                var from = queue.Dequeue();
                var here = floors[from.x, from.y];
                var spring = springs[from.x, from.y];
                foreach (var direction in Directions)
                {
                    var next = from + direction;
                    if (!IsInside(next, width, depth))
                    {
                        continue;
                    }

                    if (floors[next.x, next.y] != 0)
                    {
                        if (CanStep(here, floors[next.x, next.y], spring ? SpringClimb : maxClimb))
                        {
                            Reach(next);
                        }

                        continue;
                    }

                    var across = next + direction;
                    if (IsInside(across, width, depth) && floors[across.x, across.y] != 0
                        && CanStep(here, floors[across.x, across.y], spring ? SpringClimb : 0))
                    {
                        Reach(across);
                    }
                }

                foreach (var path in paths.Where(path => path.Contains(from)))
                {
                    foreach (var cell in path)
                    {
                        Reach(cell);
                    }
                }
            }

            return reached;
        }

        private static bool CanStep(int from, int to, int maxClimb) => LowestBit(to) - HighestBit(from) <= maxClimb;

        private static int LowestBit(int mask)
        {
            var bit = 0;
            while ((mask & (1 << bit)) == 0)
            {
                bit++;
            }

            return bit;
        }

        private static int HighestBit(int mask)
        {
            var bit = 31;
            while ((mask & (1 << bit)) == 0)
            {
                bit--;
            }

            return bit;
        }

        private static bool IsInside(Vector2Int cell, int width, int depth) =>
            cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;

        /// <summary>Column and row as the tool shows them: column from the left, row from the camera side.</summary>
        private static string Format(Vector2Int cell) => $"({cell.x}, {cell.y})";
    }
}
