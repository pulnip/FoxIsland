namespace MyGame.Map
{
    public enum ItemMode : byte
    {
        /// <summary>A set number of items on random solid tiles, reshuffled on every load.</summary>
        Random,

        /// <summary>Items on the cells listed in the layout.</summary>
        Fixed,
    }
}
