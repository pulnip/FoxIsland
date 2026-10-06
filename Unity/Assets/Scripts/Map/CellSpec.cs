using System;

namespace MyGame.Map
{
    /// <summary>
    /// One grid cell of a layout (plan.md §4.3): either a hole (lake) or a block with a height, a
    /// skin, and whether it crumbles. Token form: <c>.</c> for a hole, otherwise
    /// <c>&lt;height&gt;&lt;skin&gt;[c]</c>, e.g. <c>0s</c>, <c>2m</c>, <c>5i</c>, <c>0mc</c>.
    /// </summary>
    public readonly struct CellSpec : IEquatable<CellSpec>
    {
        public const int MaxHeight = 6;

        private const string SkinCodes = "smfrniwb";
        private const char HoleToken = '.';
        private const char CrumblingFlag = 'c';

        public CellSpec(int height, Skin skin, bool isCrumbling = false)
        {
            IsBlock = true;
            Height = height;
            Skin = skin;
            IsCrumbling = isCrumbling;
        }

        public static CellSpec Hole => default;

        /// <summary>False for a hole: lake water, nothing to stand on.</summary>
        public bool IsBlock { get; }

        public int Height { get; }

        public Skin Skin { get; }

        public bool IsCrumbling { get; }

        /// <summary>Keeps its momentum underfoot (§4.5).</summary>
        public bool IsSlippery => IsBlock && Skin is Skin.Ice or Skin.Iceberg;

        /// <summary>Prints press into it (§4.9).</summary>
        public bool IsSnow => IsBlock && Skin == Skin.Snow;

        /// <summary>A fixed or random item may float over it: a firm block that does not move.</summary>
        public bool CanHoldItem => IsBlock && !IsCrumbling && Skin != Skin.Iceberg;

        public static char CodeOf(Skin skin) => SkinCodes[(int)skin];

        public static bool TryParse(string token, out CellSpec spec)
        {
            spec = Hole;
            if (token == null || token.Length == 0)
            {
                return false;
            }

            if (token.Length == 1)
            {
                return token[0] == HoleToken;
            }

            var height = token[0] - '0';
            var skin = SkinCodes.IndexOf(token[1]);
            var isCrumbling = token.Length == 3 && token[2] == CrumblingFlag;
            if (height < 0 || height > MaxHeight || skin < 0 || token.Length > 3 || (token.Length == 3 && !isCrumbling))
            {
                return false;
            }

            spec = new CellSpec(height, (Skin)skin, isCrumbling);
            return true;
        }

        public string ToToken() => IsBlock
            ? $"{(char)('0' + Height)}{CodeOf(Skin)}{(IsCrumbling ? CrumblingFlag.ToString() : string.Empty)}"
            : HoleToken.ToString();

        public CellSpec WithHeight(int height) => new(height, Skin, IsCrumbling);

        public CellSpec WithSkin(Skin skin) => new(Height, skin, IsCrumbling);

        public CellSpec WithCrumbling(bool isCrumbling) => new(Height, Skin, isCrumbling);

        public bool Equals(CellSpec other) =>
            IsBlock == other.IsBlock && Height == other.Height && Skin == other.Skin && IsCrumbling == other.IsCrumbling;

        public override bool Equals(object obj) => obj is CellSpec other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(IsBlock, Height, Skin, IsCrumbling);

        public override string ToString() => ToToken();
    }
}
