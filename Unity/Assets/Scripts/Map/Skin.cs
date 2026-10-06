namespace MyGame.Map
{
    /// <summary>
    /// What a block is made of (plan.md §4.9): its materials, decorations, and footing. Each skin
    /// has a one-letter code in the layout tokens.
    /// </summary>
    public enum Skin : byte
    {
        /// <summary><c>s</c>: beach sand over sandstone; shells, pebbles, driftwood.</summary>
        Sand,

        /// <summary><c>m</c>: grass over dirt; lowland grass.</summary>
        Meadow,

        /// <summary><c>f</c>: conifer forest floor over dark earth; shrubs, needle litter, krummholz.</summary>
        Forest,

        /// <summary><c>r</c>: bare rock; stones and moss.</summary>
        Rock,

        /// <summary><c>n</c>: deep snow over rock; prints press into it.</summary>
        Snow,

        /// <summary><c>i</c>: ice over rock; slippery.</summary>
        Ice,

        /// <summary><c>w</c>: shallow water over a sandy bottom; the fox can stand in it.</summary>
        Water,

        /// <summary><c>b</c>: an iceberg floating in the lake (height 0 only); slippery.</summary>
        Iceberg,
    }
}
