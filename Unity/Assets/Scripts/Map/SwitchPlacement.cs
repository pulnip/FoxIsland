using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>A floor switch: pressed while the fox or a crate stands on its cell.</summary>
    [Serializable]
    public struct SwitchPlacement : IEquatable<SwitchPlacement>
    {
        [SerializeField] private Vector2Int _cell;
        [SerializeField] private PuzzleGroup _group;

        public SwitchPlacement(Vector2Int cell, PuzzleGroup group)
        {
            _cell = cell;
            _group = group;
        }

        public Vector2Int Cell => _cell;

        public PuzzleGroup Group => _group;

        public SwitchPlacement WithCell(Vector2Int cell) => new(cell, _group);

        public bool Equals(SwitchPlacement other) => _cell == other._cell && _group == other._group;

        public override bool Equals(object obj) => obj is SwitchPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_cell, _group);
    }
}
