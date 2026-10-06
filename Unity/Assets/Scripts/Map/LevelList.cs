using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>The levels in play order (plan.md §4.3). Edited with Tools/MyGame/Map Tool.</summary>
    [CreateAssetMenu(menuName = "MyGame/Level List", fileName = "LevelList")]
    public class LevelList : ScriptableObject
    {
        [SerializeField] private MapLayout[] _levels = System.Array.Empty<MapLayout>();

        public IReadOnlyList<MapLayout> Levels => _levels;

        public int Count => _levels.Length;

        public MapLayout this[int index] => _levels[index];

        public void SetLevels(MapLayout[] levels) => _levels = levels;
    }
}
