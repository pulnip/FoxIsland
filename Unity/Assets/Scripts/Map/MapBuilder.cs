using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Instantiates a tile for every block of a layout and dresses it in its skin's materials.
    /// <see cref="Core.LevelLoader"/> calls <see cref="Build"/> at scene load; the map tool's preview
    /// calls it in edit mode.
    /// </summary>
    public class MapBuilder : MonoBehaviour
    {
        [SerializeField] private Tile _solidPrefab;
        [SerializeField] private Tile _crumblingPrefab;
        [SerializeField] private SkinMaterials[] _skins = Array.Empty<SkinMaterials>();

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
                    var spec = layout.GetCell(coordinates);
                    if (!spec.IsBlock)
                    {
                        continue;
                    }

                    var tile = Instantiate(spec.IsCrumbling ? _crumblingPrefab : _solidPrefab, transform);
                    tile.Initialize(coordinates, spec);
                    ApplySkin(tile);
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

        private void ApplySkin(Tile tile)
        {
            foreach (var skin in _skins)
            {
                if (skin.Skin == tile.Spec.Skin)
                {
                    tile.SetMaterials(skin.Top, skin.Body);
                    return;
                }
            }

            Debug.LogWarning($"[MapBuilder] No materials for skin {tile.Spec.Skin}.", this);
        }
    }
}
