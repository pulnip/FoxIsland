using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    public class MapBuilder : MonoBehaviour
    {
        [SerializeField] private MapLayout _layout;
        [SerializeField] private Tile _solidPrefab;
        [SerializeField] private Tile _crumblingPrefab;
        [SerializeField] private Tile _startPrefab;

        private readonly List<Tile> _tiles = new();

        public MapLayout Layout => _layout;

        public IReadOnlyList<Tile> Tiles => _tiles;

        private void Awake() => Build();

        public void Build()
        {
            Clear();
            if (_layout == null)
            {
                Debug.LogError("[MapBuilder] No layout assigned.", this);
                return;
            }

            for (var row = 0; row < _layout.Depth; row++)
            {
                for (var column = 0; column < _layout.Width; column++)
                {
                    var coordinates = new Vector2Int(column, row);
                    var spec = _layout.GetTile(coordinates);
                    var prefab = GetPrefab(spec.Type);
                    if (prefab == null)
                    {
                        continue;
                    }

                    var tile = Instantiate(prefab, transform);
                    tile.Initialize(coordinates, spec);
                    _tiles.Add(tile);
                }
            }
        }

        public void Clear()
        {
            _tiles.Clear();
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private Tile GetPrefab(TileType type) => type switch
        {
            TileType.Solid => _solidPrefab,
            TileType.Crumbling => _crumblingPrefab,
            TileType.Start => _startPrefab,
            _ => null,
        };
    }
}
