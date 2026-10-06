using System;

namespace MyGame.Map
{
    public readonly struct TileSpec : IEquatable<TileSpec>
    {
        public const int MaxLevel = 2;

        public TileSpec(TileType type, int level)
        {
            Type = type;
            Level = level;
        }

        public TileType Type { get; }

        public int Level { get; }

        /// <summary>
        /// Parses a layout token: <c>.</c> hole, <c>0</c>–<c>2</c> solid at that level,
        /// <c>C</c> crumbling, <c>S</c> start. Crumbling and start tiles are level 0.
        /// </summary>
        public static bool TryParse(string token, out TileSpec spec)
        {
            spec = default;
            if (token is not { Length: 1 })
            {
                return false;
            }

            var symbol = token[0];
            switch (symbol)
            {
                case '.':
                    spec = new TileSpec(TileType.Hole, 0);
                    return true;
                case 'C':
                    spec = new TileSpec(TileType.Crumbling, 0);
                    return true;
                case 'S':
                    spec = new TileSpec(TileType.Start, 0);
                    return true;
                case >= '0' and <= '9' when symbol - '0' <= MaxLevel:
                    spec = new TileSpec(TileType.Solid, symbol - '0');
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The layout token that <see cref="TryParse"/> reads back as this spec.</summary>
        public string ToToken() => Type switch
        {
            TileType.Hole => ".",
            TileType.Crumbling => "C",
            TileType.Start => "S",
            _ => ((char)('0' + Level)).ToString(),
        };

        public bool Equals(TileSpec other) => Type == other.Type && Level == other.Level;

        public override bool Equals(object obj) => obj is TileSpec other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Type, Level);

        public override string ToString() => $"{Type}({Level})";
    }
}
