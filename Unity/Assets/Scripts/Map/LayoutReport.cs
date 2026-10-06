using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    public enum IssueSeverity : byte
    {
        Warning,
        Error,
    }

    /// <summary>How the fox can get to a cell, judged from the start tile.</summary>
    public enum Reachability : byte
    {
        /// <summary>A hole: nothing to stand on.</summary>
        NotStandable,
        Unreachable,

        /// <summary>Only with a two-level jump, which barely clears.</summary>
        HardJump,
        Reachable,
    }

    public readonly struct LayoutIssue
    {
        public LayoutIssue(IssueSeverity severity, string message, Vector2Int? cell = null)
        {
            Severity = severity;
            Message = message;
            Cell = cell;
        }

        public IssueSeverity Severity { get; }

        public string Message { get; }

        public Vector2Int? Cell { get; }
    }

    /// <summary>Result of <see cref="MapLayoutValidator.Validate"/>.</summary>
    public sealed class LayoutReport
    {
        private readonly List<LayoutIssue> _issues = new();

        public LayoutReport(Reachability[,] cells)
        {
            Cells = cells;
            Flooded = new bool[cells.GetLength(0), cells.GetLength(1)];
        }

        public IReadOnlyList<LayoutIssue> Issues => _issues;

        /// <summary>Per cell, indexed [column, row]; empty when the rows do not parse.</summary>
        public Reachability[,] Cells { get; }

        /// <summary>Per cell, indexed [column, row]: the block goes under the water at high tide.</summary>
        public bool[,] Flooded { get; }

        public bool HasErrors { get; private set; }

        public void Add(IssueSeverity severity, string message, Vector2Int? cell = null)
        {
            _issues.Add(new LayoutIssue(severity, message, cell));
            HasErrors |= severity == IssueSeverity.Error;
        }
    }
}
