using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// A moving platform: its top sits at <see cref="Height"/>, and it travels from
    /// <see cref="From"/> to <see cref="To"/> (same row or column) and back once every
    /// <see cref="Period"/> seconds.
    /// </summary>
    [Serializable]
    public struct PlatformPlacement : IEquatable<PlatformPlacement>
    {
        public const float MinPeriod = 3f;
        public const float MaxPeriod = 12f;

        [SerializeField] private Vector2Int _from;
        [SerializeField] private Vector2Int _to;
        [SerializeField, Range(0, CellSpec.MaxHeight)] private int _height;
        [SerializeField, Range(MinPeriod, MaxPeriod)] private float _period;

        public PlatformPlacement(Vector2Int from, Vector2Int to, int height, float period)
        {
            _from = from;
            _to = to;
            _height = height;
            _period = period;
        }

        public Vector2Int From => _from;

        public Vector2Int To => _to;

        public int Height => _height;

        public float Period => Mathf.Clamp(_period, MinPeriod, MaxPeriod);

        /// <summary>The ends share a row or a column and differ.</summary>
        public bool IsStraight => _from != _to && (_from.x == _to.x || _from.y == _to.y);

        /// <summary>The cells under the path, from one end to the other (only for a straight path).</summary>
        public Vector2Int[] PathCells()
        {
            if (!IsStraight)
            {
                return new[] { _from, _to };
            }

            var step = new Vector2Int(Math.Sign(_to.x - _from.x), Math.Sign(_to.y - _from.y));
            var count = Mathf.Abs(_to.x - _from.x) + Mathf.Abs(_to.y - _from.y) + 1;
            var cells = new Vector2Int[count];
            for (var i = 0; i < count; i++)
            {
                cells[i] = _from + step * i;
            }

            return cells;
        }

        public PlatformPlacement Moved(Func<Vector2Int, Vector2Int> move) => new(move(_from), move(_to), _height, _period);

        public bool Equals(PlatformPlacement other) =>
            _from == other._from && _to == other._to && _height == other._height && _period.Equals(other._period);

        public override bool Equals(object obj) => obj is PlatformPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_from, _to, _height, _period);
    }
}
