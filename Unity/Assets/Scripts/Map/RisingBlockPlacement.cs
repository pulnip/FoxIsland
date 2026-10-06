using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Makes a cell's block move: it rests at the cell's height and moves to
    /// <see cref="RaisedHeight"/> while its group is on. A raised height below the cell's sinks it.
    /// </summary>
    [Serializable]
    public struct RisingBlockPlacement : IEquatable<RisingBlockPlacement>
    {
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private PuzzleGroup _group;
        [SerializeField, Range(0, CellSpec.MaxHeight)] private int _raisedHeight;

        public RisingBlockPlacement(Vector2Int cell, PuzzleGroup group, int raisedHeight)
        {
            _cell = cell;
            _group = group;
            _raisedHeight = raisedHeight;
        }

        public Vector2Int Cell => _cell;

        public PuzzleGroup Group => _group;

        public int RaisedHeight => _raisedHeight;

        public RisingBlockPlacement WithCell(Vector2Int cell) => new(cell, _group, _raisedHeight);

        public bool Equals(RisingBlockPlacement other) =>
            _cell == other._cell && _group == other._group && _raisedHeight == other._raisedHeight;

        public override bool Equals(object obj) => obj is RisingBlockPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_cell, _group, _raisedHeight);
    }
}
