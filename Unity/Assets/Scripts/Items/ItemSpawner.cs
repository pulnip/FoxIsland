using System.Collections.Generic;
using System.Linq;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Items
{
    /// <summary>
    /// Places a layout's items (plan.md §5): on its fixed cells, or on random solid tiles. Start,
    /// crumbling, and hole cells never hold an item.
    /// </summary>
    public class ItemSpawner : MonoBehaviour
    {
        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private Item _itemPrefab;
        [SerializeField] private float _groundHeight = 0.4f;
        [SerializeField] private float _airHeight = 1.0f;

        public List<Item> Spawn(MapLayout layout) =>
            layout.ItemMode == ItemMode.Fixed ? SpawnFixed(layout.Items) : SpawnRandom(layout.RandomItemCount);

        private List<Item> SpawnFixed(IReadOnlyList<ItemPlacement> placements)
        {
            var items = new List<Item>(placements.Count);
            foreach (var placement in placements)
            {
                if (!_mapBuilder.TryGetTile(placement.Cell, out var tile) || tile.Spec.Type != TileType.Solid)
                {
                    Debug.LogWarning($"[ItemSpawner] No solid tile at {placement.Cell}; item skipped.", this);
                    continue;
                }

                items.Add(SpawnAt(tile, placement.InAir));
            }

            return items;
        }

        private List<Item> SpawnRandom(int count)
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
                items.Add(SpawnAt(candidates[i], inAir: Random.value < 0.5f));
            }

            return items;
        }

        private Item SpawnAt(Tile tile, bool inAir)
        {
            var height = inAir ? _airHeight : _groundHeight;
            var position = MapMetrics.TopCenter(tile.Coordinates, tile.Spec.Level) + Vector3.up * height;
            return Instantiate(_itemPrefab, position, Quaternion.identity, transform);
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
