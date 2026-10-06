using System;

namespace MyGame.Map
{
    public readonly struct TileSpec : IEquatable<TileSpec>
    {
        public const int MaxLevel = 6;
        public const int SubalpineMinLevel = 3;
        public const int AlpineMinLevel = 5;

        private const char IceSuffix = 'i';

        public TileSpec(TileType type, int level, bool isIce = false)
        {
            Type = type;
            Level = level;
            IsIce = isIce;
        }

        public TileType Type { get; }

        public int Level { get; }

        /// <summary>An alpine solid tile covered in ice instead of snow; slippery.</summary>
        public bool IsIce { get; }

        public ElevationZone Zone => Level >= AlpineMinLevel ? ElevationZone.Alpine
            : Level >= SubalpineMinLevel ? ElevationZone.Subalpine
            : ElevationZone.Lowland;

        /// <summary>An alpine solid tile under deformable snow.</summary>
        public bool IsSnow => Type == TileType.Solid && Zone == ElevationZone.Alpine && !IsIce;

        /// <summary>
        /// Parses a layout token: <c>.</c> hole, <c>0</c>–<c>6</c> solid at that level, <c>5i</c> /
        /// <c>6i</c> ice at that level, <c>C</c> crumbling, <c>S</c> start. Crumbling and start
        /// tiles are level 0.
        /// </summary>
        public static bool TryParse(string token, out TileSpec spec)
        {
            spec = default;
            if (token is not { Length: 1 or 2 })
            {
                return false;
            }

            var symbol = token[0];
            if (token.Length == 2)
            {
                var level = symbol - '0';
                if (token[1] != IceSuffix || level < AlpineMinLevel || level > MaxLevel)
                {
                    return false;
                }

                spec = new TileSpec(TileType.Solid, level, isIce: true);
                return true;
            }

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
            _ => IsIce ? $"{Level}{IceSuffix}" : ((char)('0' + Level)).ToString(),
        };

        public bool Equals(TileSpec other) => Type == other.Type && Level == other.Level && IsIce == other.IsIce;

        public override bool Equals(object obj) => obj is TileSpec other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Type, Level, IsIce);

        public override string ToString() => IsIce ? $"{Type}({Level}, ice)" : $"{Type}({Level})";
    }
}
