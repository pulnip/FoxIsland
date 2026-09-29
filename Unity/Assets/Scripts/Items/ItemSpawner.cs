using System.Collections.Generic;
using System.Linq;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Items
{
    /// <summary>
    /// Places items on random solid tiles (plan.md §5). Start, crumbling, and hole cells are skipped.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private Item _itemPrefab;
        [SerializeField] private float _groundHeight = 0.4f;
        [SerializeField] private float _airHeight = 1.0f;

        public List<Item> Spawn(int count)
        {
            var candidates = _mapBuilder.Tiles.Where(tile => tile.Spec.Type == TileType.Solid).ToList();
            if (count > candidates.Count)
            {
                Debug.LogWarning($"[ItemSpawner] {count} items requested, only {candidates.Count} solid tiles.", this);
                count = candidates.Count;
            }

            Shuffle(candidates);
            var items = new List<Item>(count);
            for (var i = 0; i < count; i++)
            {
                var tile = candidates[i];
                var height = Random.value < 0.5f ? _groundHeight : _airHeight;
                var position = MapMetrics.TopCenter(tile.Coordinates, tile.Spec.Level) + Vector3.up * height;
                items.Add(Instantiate(_itemPrefab, position, Quaternion.identity, transform));
            }

            return items;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
