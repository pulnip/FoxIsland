using System;
using System.Collections.Generic;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// Builds a layout's puzzle elements (plan.md §4.10) on top of the map and answers the grid
    /// questions they share: which switch groups are on, where the crates are, and how high a
    /// cell's floor stands right now. Call <see cref="Build"/> after <see cref="MapBuilder.Build"/>.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class PuzzleBoard : MonoBehaviour
    {
        /// <summary>Top of a crate that fell into a hole: flush with a height-0 block.</summary>
        public const float FilledHoleTop = 0f;

        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private FloorSwitch _switchPrefab;
        [SerializeField] private GameObject _risingMarkerPrefab;
        [SerializeField] private Crate _cratePrefab;
        [SerializeField] private Springboard _springboardPrefab;
        [SerializeField] private MovingPlatform _platformPrefab;

        [Tooltip("One per PuzzleGroup, in order.")]
        [SerializeField] private Material[] _groupMaterials = Array.Empty<Material>();

        [SerializeField] private ParticleSystem _splashEffect;

        private readonly List<FloorSwitch> _switches = new();
        private readonly Dictionary<Vector2Int, Crate> _crates = new();
        private readonly HashSet<Vector2Int> _filledHoles = new();
        private readonly HashSet<Vector2Int> _springboardCells = new();
        private readonly bool[] _groupsOn = new bool[PuzzleGroups.Count];
        private MapLayout _layout;

        public MapLayout Layout => _layout;

        public IReadOnlyCollection<Crate> Crates => _crates.Values;

        public ParticleSystem SplashEffect => _splashEffect;

        /// <summary>True when the fox's capsule overlaps the box.</summary>
        public static bool OverlapsCharacter(Vector3 center, Vector3 halfExtents)
        {
            foreach (var hit in Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit is CharacterController)
                {
                    return true;
                }
            }

            return false;
        }

        public void Build(MapLayout layout)
        {
            Clear();
            _layout = layout;
            if (layout == null)
            {
                return;
            }

            foreach (var placement in layout.Switches)
            {
                if (TryGetTile(placement.Cell, out var tile))
                {
                    var floorSwitch = Instantiate(_switchPrefab, transform);
                    floorSwitch.Initialize(this, placement, tile, MaterialOf(placement.Group));
                    _switches.Add(floorSwitch);
                }
            }

            foreach (var placement in layout.RisingBlocks)
            {
                if (TryGetTile(placement.Cell, out var tile) && !tile.TryGetComponent<RisingBlock>(out _))
                {
                    var marker = Instantiate(_risingMarkerPrefab, tile.Visual);
                    tile.gameObject.AddComponent<RisingBlock>().Initialize(this, placement, marker, MaterialOf(placement.Group));
                }
            }

            foreach (var cell in layout.Springboards)
            {
                if (TryGetTile(cell, out var tile))
                {
                    Instantiate(_springboardPrefab, transform).Initialize(tile);
                    _springboardCells.Add(cell);
                }
            }

            foreach (var cell in layout.Crates)
            {
                if (TryGetTile(cell, out var tile) && !_crates.ContainsKey(cell))
                {
                    var crate = Instantiate(_cratePrefab, transform);
                    crate.Initialize(this, cell, tile);
                    _crates.Add(cell, crate);
                }
            }

            foreach (var placement in layout.Platforms)
            {
                Instantiate(_platformPrefab, transform).Initialize(placement);
            }
        }

        public void Clear()
        {
            _layout = null;
            _switches.Clear();
            _crates.Clear();
            _filledHoles.Clear();
            _springboardCells.Clear();
            Array.Clear(_groupsOn, 0, _groupsOn.Length);
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

        public bool IsGroupOn(PuzzleGroup group) => _groupsOn[(int)group];

        /// <summary>A crate has come to rest on <paramref name="cell"/>.</summary>
        public bool HasSettledCrate(Vector2Int cell) => _crates.TryGetValue(cell, out var crate) && crate.IsSettled;

        /// <summary>
        /// Whether a crate resting with its bottom at <paramref name="bottom"/> can slide into
        /// <paramref name="target"/>: the floor there must be no higher, free of crates,
        /// springboards, and the fox, and solid (crumbling blocks and icebergs refuse it).
        /// A hole takes the crate in, which then fills it.
        /// </summary>
        public bool CanEnter(Vector2Int target, float bottom, out float landing, out Tile floor, out bool fillsHole)
        {
            landing = bottom;
            floor = null;
            fillsHole = false;
            if (_layout == null || !_layout.IsInside(target) || _crates.ContainsKey(target) || _springboardCells.Contains(target))
            {
                return false;
            }

            if (_mapBuilder.TryGetTile(target, out var tile))
            {
                if (tile.Spec.IsCrumbling || tile.Spec.Skin == Skin.Iceberg)
                {
                    return false;
                }

                landing = tile.transform.position.y + tile.TopY;
                floor = tile;
            }
            else if (_filledHoles.Contains(target))
            {
                landing = FilledHoleTop;
            }
            else
            {
                landing = FilledHoleTop - Crate.Height;
                fillsHole = true;
            }

            if (landing > bottom + 0.05f)
            {
                return false;
            }

            var center = MapMetrics.TopCenter(target, 0);
            var low = Mathf.Min(landing, bottom);
            var high = bottom + Crate.Height;
            center.y = (low + high) * 0.5f;
            var halfExtents = new Vector3(MapMetrics.TileSize * 0.48f, (high - low) * 0.5f, MapMetrics.TileSize * 0.48f);
            return !OverlapsCharacter(center, halfExtents);
        }

        /// <summary>Moves a crate's claim on the grid before it slides.</summary>
        public void MoveCrate(Crate crate, Vector2Int from, Vector2Int to)
        {
            _crates.Remove(from);
            _crates[to] = crate;
        }

        /// <summary>A crate sank into a hole: the cell becomes a height-0 floor.</summary>
        public void FillHole(Vector2Int cell)
        {
            _crates.Remove(cell);
            _filledHoles.Add(cell);
        }

        private void Update()
        {
            Array.Clear(_groupsOn, 0, _groupsOn.Length);
            foreach (var floorSwitch in _switches)
            {
                if (floorSwitch.Refresh())
                {
                    _groupsOn[(int)floorSwitch.Group] = true;
                }
            }
        }

        private bool TryGetTile(Vector2Int cell, out Tile tile)
        {
            if (_mapBuilder.TryGetTile(cell, out tile))
            {
                return true;
            }

            Debug.LogWarning($"[PuzzleBoard] {_layout.name}: no block at {cell} for a puzzle element; skipped.", this);
            return false;
        }

        private Material MaterialOf(PuzzleGroup group) => (int)group < _groupMaterials.Length ? _groupMaterials[(int)group] : null;
    }
}
