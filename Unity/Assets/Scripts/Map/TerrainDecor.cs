using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.Map
{
    /// <summary>
    /// Low voxel decorations for the subalpine and alpine belts (plan.md §4.9): dwarf shrubs and
    /// needle litter on the conifer forest floor (level 3), wind-flattened krummholz mats and rocks
    /// at the treeline (level 4), moss, lichen, and cushion plants poking through the snow
    /// (level 5), and bare rock and wind ridges on the deepest snow (level 6). Ice stays clean.
    /// Seeded per tile; nothing is taller than 0.2, so the fox and gems stay visible.
    /// </summary>
    public class TerrainDecor : MonoBehaviour
    {
        private const int Seed = 4321;
        private const int Variants = 4;
        private const float EdgeMargin = 0.2f;
        private const float Spacing = 0.28f;
        private const int PlacementAttempts = 20;

        private static readonly Color[] LeafGreens = { new(0.17f, 0.36f, 0.22f), new(0.22f, 0.43f, 0.24f), new(0.27f, 0.47f, 0.27f) };
        private static readonly Color[] Berries = { new(0.32f, 0.24f, 0.55f), new(0.72f, 0.16f, 0.22f) };
        private static readonly Color[] Litter = { new(0.47f, 0.27f, 0.16f), new(0.56f, 0.35f, 0.19f), new(0.38f, 0.24f, 0.15f) };
        private static readonly Color[] Rocks = { new(0.50f, 0.50f, 0.53f), new(0.42f, 0.42f, 0.46f), new(0.58f, 0.57f, 0.58f) };
        private static readonly Color[] Mosses = { new(0.44f, 0.55f, 0.34f), new(0.55f, 0.60f, 0.42f), new(0.76f, 0.72f, 0.40f), new(0.70f, 0.75f, 0.66f) };
        private static readonly Color[] Flowers = { new(0.96f, 0.96f, 0.95f), new(0.95f, 0.62f, 0.76f) };
        private static readonly Color CushionGreen = new(0.33f, 0.53f, 0.30f);
        private static readonly Color KrummholzDark = new(0.13f, 0.29f, 0.20f);
        private static readonly Color KrummholzLight = new(0.20f, 0.38f, 0.25f);
        private static readonly Color SnowWhite = new(0.95f, 0.97f, 1f);

        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private Material _material;

        [Tooltip("Krummholz mats and snow ridges are shaped by this wind (XZ).")]
        [SerializeField] private Vector3 _windDirection = new(1f, 0f, 0.4f);

        private Dictionary<Kind, Mesh[]> _meshes;

        private enum Kind : byte
        {
            Shrub,
            NeedleLitter,
            Krummholz,
            Rock,
            Moss,
            Cushion,
            WindRidge,
        }

        private void Start() => Build();

        private void OnDestroy()
        {
            if (_meshes == null)
            {
                return;
            }

            foreach (var variants in _meshes.Values)
            {
                foreach (var mesh in variants)
                {
                    DestroyMesh(mesh);
                }
            }
        }

        public void Build()
        {
            _meshes ??= BuildMeshes();
            foreach (var tile in _mapBuilder.Tiles)
            {
                var spec = tile.Spec;
                if (spec.Type != TileType.Solid || spec.Zone == ElevationZone.Lowland || spec.IsIce)
                {
                    continue;
                }

                var random = new System.Random(Seed + tile.Coordinates.x * 73856093 ^ tile.Coordinates.y * 19349663);
                var placed = new List<Vector3>();
                var surface = spec.IsSnow ? tile.TopY + MapMetrics.SnowDepth - 0.01f : tile.TopY;
                switch (spec.Level)
                {
                    case 3:
                        Scatter(tile, Kind.Shrub, random.Next(2, 5), surface, placed, random);
                        Scatter(tile, Kind.NeedleLitter, random.Next(2, 5), surface, placed, random);
                        break;
                    case 4:
                        Scatter(tile, Kind.Krummholz, random.Next(1, 3), surface, placed, random);
                        Scatter(tile, Kind.Rock, random.Next(1, 3), surface, placed, random);
                        Scatter(tile, Kind.NeedleLitter, random.Next(0, 3), surface, placed, random);
                        break;
                    case 5:
                        Scatter(tile, Kind.Moss, random.Next(2, 4), surface, placed, random);
                        Scatter(tile, Kind.Cushion, random.Next(1, 3), surface, placed, random);
                        Scatter(tile, Kind.Rock, random.Next(0, 2), surface, placed, random);
                        break;
                    default:
                        Scatter(tile, Kind.Rock, random.Next(1, 3), surface, placed, random);
                        Scatter(tile, Kind.WindRidge, random.Next(1, 3), surface, placed, random);
                        break;
                }
            }
        }

        private void Scatter(Tile tile, Kind kind, int count, float surfaceY, List<Vector3> placed, System.Random random)
        {
            var added = 0;
            for (var attempt = 0; attempt < count * PlacementAttempts && added < count; attempt++)
            {
                var half = MapMetrics.TileSize * 0.5f - EdgeMargin;
                var position = new Vector3(Range(random, -half, half), 0f, Range(random, -half, half));
                if (!placed.TrueForAll(other => Vector3.Distance(other, position) >= Spacing))
                {
                    continue;
                }

                placed.Add(position);
                added++;
                var aligned = kind is Kind.Krummholz or Kind.WindRidge;
                var yaw = aligned ? WindYaw() + Range(random, -12f, 12f) : Range(random, 0f, 360f);
                var variants = _meshes[kind];
                AddDecor(tile, kind.ToString(), variants[random.Next(variants.Length)], position + Vector3.up * surfaceY, yaw);
            }
        }

        private void AddDecor(Tile tile, string name, Mesh mesh, Vector3 localPosition, float yaw)
        {
            var decor = new GameObject(name);
            decor.transform.SetParent(tile.transform, false);
            decor.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
            decor.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = decor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private float WindYaw() => Mathf.Atan2(_windDirection.x, _windDirection.z) * Mathf.Rad2Deg;

        private static Dictionary<Kind, Mesh[]> BuildMeshes()
        {
            var random = new System.Random(Seed);
            var meshes = new Dictionary<Kind, Mesh[]>();
            foreach (Kind kind in System.Enum.GetValues(typeof(Kind)))
            {
                var variants = new Mesh[Variants];
                for (var v = 0; v < Variants; v++)
                {
                    var builder = new VoxelMeshBuilder();
                    Shape(kind, builder, random);
                    variants[v] = builder.ToMesh($"{kind}{v}");
                }

                meshes.Add(kind, variants);
            }

            return meshes;
        }

        /// <summary>One variant of each decoration, in local space with y = 0 on the ground.</summary>
        private static void Shape(Kind kind, VoxelMeshBuilder builder, System.Random random)
        {
            switch (kind)
            {
                case Kind.Shrub:
                    // Dwarf rhododendron / bilberry clump with a few berries on top.
                    var twigs = random.Next(4, 8);
                    for (var i = 0; i < twigs; i++)
                    {
                        var size = Range(random, 0.06f, 0.1f);
                        var height = Range(random, 0.06f, 0.16f);
                        builder.AddBox(Around(random, 0.09f), new Vector3(size, height, size), Range(random, 0f, 90f), Pick(LeafGreens, random));
                    }

                    var berries = random.Next(2, 5);
                    for (var i = 0; i < berries; i++)
                    {
                        builder.AddBox(Around(random, 0.08f) + Vector3.up * Range(random, 0.08f, 0.15f), Vector3.one * 0.025f, 0f, Pick(Berries, random));
                    }

                    break;
                case Kind.NeedleLitter:
                    var needles = random.Next(4, 8);
                    for (var i = 0; i < needles; i++)
                    {
                        builder.AddBox(Around(random, 0.14f), new Vector3(0.07f, 0.012f, 0.025f), Range(random, 0f, 180f), Pick(Litter, random));
                    }

                    break;
                case Kind.Krummholz:
                    // A prostrate pine mat stretched downwind, lumpy on top.
                    var length = Range(random, 0.32f, 0.46f);
                    builder.AddBox(Vector3.zero, new Vector3(Range(random, 0.18f, 0.24f), 0.05f, length), 0f, KrummholzDark);
                    var lumps = random.Next(4, 7);
                    for (var i = 0; i < lumps; i++)
                    {
                        var along = Range(random, -length * 0.4f, length * 0.4f);
                        builder.AddBox(new Vector3(Range(random, -0.06f, 0.06f), 0.05f, along),
                            new Vector3(0.08f, Range(random, 0.02f, 0.05f), 0.09f), 0f, KrummholzLight);
                    }

                    break;
                case Kind.Rock:
                    var stones = random.Next(1, 4);
                    for (var i = 0; i < stones; i++)
                    {
                        var size = Range(random, 0.07f, 0.15f);
                        builder.AddBox(Around(random, 0.06f) + Vector3.down * 0.02f,
                            new Vector3(size, Range(random, 0.06f, 0.13f), size * Range(random, 0.7f, 1.2f)), Range(random, 0f, 90f), Pick(Rocks, random));
                    }

                    break;
                case Kind.Moss:
                    // Moss and lichen crusts barely above the snow.
                    var crusts = random.Next(3, 7);
                    for (var i = 0; i < crusts; i++)
                    {
                        var size = Range(random, 0.05f, 0.1f);
                        builder.AddBox(Around(random, 0.1f), new Vector3(size, 0.016f, size * Range(random, 0.6f, 1.2f)), Range(random, 0f, 90f), Pick(Mosses, random));
                    }

                    break;
                case Kind.Cushion:
                    // A cushion plant: a low green dome dotted with tiny flowers.
                    builder.AddBox(Vector3.zero, new Vector3(0.13f, 0.04f, 0.13f), 0f, CushionGreen);
                    builder.AddBox(Vector3.up * 0.04f, new Vector3(0.09f, 0.03f, 0.09f), 0f, CushionGreen);
                    var flowers = random.Next(3, 6);
                    for (var i = 0; i < flowers; i++)
                    {
                        builder.AddBox(Around(random, 0.05f) + Vector3.up * Range(random, 0.04f, 0.07f), Vector3.one * 0.022f, 0f, Pick(Flowers, random));
                    }

                    break;
                case Kind.WindRidge:
                    // Sastrugi: snow ridges carved along the wind.
                    var ridges = random.Next(2, 4);
                    for (var i = 0; i < ridges; i++)
                    {
                        builder.AddBox(new Vector3(Range(random, -0.08f, 0.08f), -0.005f, Range(random, -0.05f, 0.05f)),
                            new Vector3(0.05f, Range(random, 0.02f, 0.035f), Range(random, 0.18f, 0.3f)), 0f, SnowWhite);
                    }

                    break;
            }
        }

        private static Vector3 Around(System.Random random, float radius) =>
            new(Range(random, -radius, radius), 0f, Range(random, -radius, radius));

        private static Color Pick(Color[] colors, System.Random random) => colors[random.Next(colors.Length)];

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        private static void DestroyMesh(Mesh mesh)
        {
            if (Application.isPlaying)
            {
                Destroy(mesh);
            }
            else
            {
                DestroyImmediate(mesh);
            }
        }
    }
}
