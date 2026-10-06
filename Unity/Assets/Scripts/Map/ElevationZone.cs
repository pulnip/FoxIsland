namespace MyGame.Map
{
    /// <summary>Vegetation belt of a tile by height (plan.md §4.9).</summary>
    public enum ElevationZone : byte
    {
        /// <summary>Levels 0–2: grass and dirt.</summary>
        Lowland,

        /// <summary>Levels 3–4: conifer forest floor, dwarf shrubs, krummholz near the treeline.</summary>
        Subalpine,

        /// <summary>Levels 5–6: snow or ice over rock, moss and cushion plants poking through.</summary>
        Alpine,
    }
}
