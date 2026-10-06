using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// A floor switch: on while the fox or a crate stands on its cell, and for
    /// <see cref="HoldTime"/> seconds after (a timer switch; 0 = only while pressed).
    /// </summary>
    [Serializable]
    public struct SwitchPlacement : IEquatable<SwitchPlacement>
    {
        public const float MaxHoldTime = 10f;

        [SerializeField] private Vector2Int _cell;
        [SerializeField] private PuzzleGroup _group;
        [SerializeField, Range(0f, MaxHoldTime)] private float _holdTime;

        public SwitchPlacement(Vector2Int cell, PuzzleGroup group, float holdTime = 0f)
        {
            _cell = cell;
            _group = group;
            _holdTime = holdTime;
        }

        public Vector2Int Cell => _cell;

        public PuzzleGroup Group => _group;

        public float HoldTime => Mathf.Clamp(_holdTime, 0f, MaxHoldTime);

        public SwitchPlacement WithCell(Vector2Int cell) => new(cell, _group, _holdTime);

        public bool Equals(SwitchPlacement other) => _cell == other._cell && _group == other._group && _holdTime.Equals(other._holdTime);

        public override bool Equals(object obj) => obj is SwitchPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_cell, _group, _holdTime);
    }
}
