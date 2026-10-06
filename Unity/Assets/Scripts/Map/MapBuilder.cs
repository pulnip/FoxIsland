using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Instantiates tile prefabs for a layout. <see cref="Core.LevelLoader"/> calls
    /// <see cref="Build"/> at scene load; the map tool's preview calls it in edit mode.
    /// </summary>
    public class MapBuilder : MonoBehaviour
    {
        [SerializeField] private Tile _solidPrefab;
        [SerializeField] private Tile _crumblingPrefab;
        [SerializeField] private Tile _startPrefab;

        [Header("Elevation zones (plan.md §4.9); lowland keeps the solid prefab's materials")]
        [SerializeField] private Material _subalpineTop;
        [SerializeField] private Material _subalpineBody;
        [SerializeField] private Material _snowTop;
        [SerializeField] private Material _iceTop;
        [SerializeField] private Material _alpineBody;

        private readonly List<Tile> _tiles = new();
        private readonly Dictionary<Vector2Int, Tile> _tilesByCell = new();

        /// <summary>The layout last passed to <see cref="Build"/>.</summary>
        public MapLayout Layout { get; private set; }

        public IReadOnlyList<Tile> Tiles => _tiles;

        public bool TryGetTile(Vector2Int coordinates, out Tile tile) => _tilesByCell.TryGetValue(coordinates, out tile);

        public void Build(MapLayout layout)
        {
            Clear();
            Layout = layout;
            if (layout == null)
            {
                Debug.LogError("[MapBuilder] No layout to build.", this);
                return;
            }

            for (var row = 0; row < layout.Depth; row++)
            {
                for (var column = 0; column < layout.Width; column++)
                {
                    var coordinates = new Vector2Int(column, row);
                    var spec = layout.GetTile(coordinates);
                    var prefab = GetPrefab(spec.Type);
                    if (prefab == null)
                    {
                        continue;
                    }

                    var tile = Instantiate(prefab, transform);
                    tile.Initialize(coordinates, spec);
                    ApplyZoneMaterials(tile);
                    _tiles.Add(tile);
                    _tilesByCell.Add(coordinates, tile);
                }
            }
        }

        public void Clear()
        {
            _tiles.Clear();
            _tilesByCell.Clear();
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

        private void ApplyZoneMaterials(Tile tile)
        {
            var spec = tile.Spec;
            if (spec.Type != TileType.Solid)
            {
                return;
            }

            switch (spec.Zone)
            {
                case ElevationZone.Subalpine:
                    tile.SetMaterials(_subalpineTop, _subalpineBody);
                    break;
                case ElevationZone.Alpine:
                    tile.SetMaterials(spec.IsIce ? _iceTop : _snowTop, _alpineBody);
                    break;
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
