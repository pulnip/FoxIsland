using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.Map
{
    /// <summary>
    /// Scatters voxel grass tufts on meadow and forest blocks and feeds the <c>MyGame/Grass</c> shader, which bends
    /// every blade about its own base in the wind and away from the trampler's recent trail
    /// (plan.md §4.6). Placement is seeded by tile coordinates, so the field is the same every run.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        private const int Seed = 1234;
        private const int MeshVariants = 4;
        private const float BladeWidth = 0.045f;
        private const float EdgeMargin = 0.2f;
        private const float FullEdgeMargin = 0.12f;
        private const int PlacementAttempts = 20;
        private const float TrampleHeightTolerance = 0.3f;
        private const float BendBoundsPadding = 0.2f;

        /// <summary>Must match <c>GRASS_TRAIL_LENGTH</c> in Grass.shader.</summary>
        private const int TrailLength = 16;

        private static readonly int WindId = Shader.PropertyToID("_GrassWind");
        private static readonly int BendId = Shader.PropertyToID("_GrassBend");
        private static readonly int TrampleId = Shader.PropertyToID("_GrassTrample");

        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private Material _material;

        [Tooltip("Darker, shorter grass between the shrubs of forest blocks.")]
        [SerializeField] private Material _forestMaterial;
        [SerializeField] private float _forestScale = 0.7f;
        [SerializeField] private Vector2 _bladeHeight = new(0.12f, 0.2f);

        [Header("Coverage")]
        [Tooltip("Share of meadow blocks left bare; the rest are split between full and sparse.")]
        [SerializeField, Range(0f, 1f)] private float _bareShare = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _fullShare = 0.35f;
        [SerializeField] private Vector2Int _sparseTufts = new(3, 5);
        [SerializeField] private float _sparseSpacing = 0.3f;
        [SerializeField] private int _fullGrid = 4;
        [SerializeField, Range(0f, 1f)] private float _fullSkipChance = 0.1f;

        [Header("Wind")]
        [SerializeField] private Vector3 _windDirection = new(1f, 0f, 0.4f);
        [SerializeField] private float _swayAngle = 7f;
        [SerializeField] private float _swayFrequency = 1.1f;
        [SerializeField] private float _waveLength = 3f;

        [Header("Trampling")]
        [SerializeField] private Transform _trampler;
        [SerializeField] private float _trampleRadius = 0.4f;
        [SerializeField] private float _trampleAngle = 65f;
        [SerializeField] private float _recoverTime = 0.6f;

        private readonly Vector3[] _trail = new Vector3[TrailLength];
        private readonly float[] _trailTimes = CreateEmptyTrailTimes();
        private readonly Vector4[] _shaderTrail = new Vector4[TrailLength];
        private int _trailHead;
        private float _lastTrailTime = float.NegativeInfinity;
        private Mesh[] _meshes;

        private void Start() => Build();

        private void Update()
        {
            RecordTrail(Time.time);
            PushShaderGlobals(Time.time);
        }

        private void OnDisable()
        {
            System.Array.Clear(_shaderTrail, 0, _shaderTrail.Length);
            Shader.SetGlobalVectorArray(TrampleId, _shaderTrail);
        }

        public void Build()
        {
            _meshes ??= BuildMeshes();

            var tiles = new List<Tile>();
            foreach (var tile in _mapBuilder.Tiles)
            {
                if (tile.Spec.IsCrumbling)
                {
                    continue;
                }

                switch (tile.Spec.Skin)
                {
                    case Skin.Meadow:
                        tiles.Add(tile);
                        break;
                    case Skin.Forest:
                        PlaceForest(tile);
                        break;
                }
            }

            var coverage = AssignCoverage(tiles.Count);
            for (var i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                var random = new System.Random(Seed + tile.Coordinates.x * 73856093 ^ tile.Coordinates.y * 19349663);
                switch (coverage[i])
                {
                    case Coverage.Sparse:
                        PlaceSparse(tile, random);
                        break;
                    case Coverage.Full:
                        PlaceFull(tile, random);
                        break;
                }
            }

            PushShaderGlobals(Time.time);
        }

        /// <summary>
        /// Keeps a ring of the trampler's recent positions, one sample per <c>recoverTime / TrailLength</c>,
        /// so the stateless shader can let grass stand back up gradually behind it.
        /// </summary>
        private void RecordTrail(float now)
        {
            if (_trampler == null || now - _lastTrailTime < _recoverTime / TrailLength)
            {
                return;
            }

            _trailHead = (_trailHead + 1) % TrailLength;
            _trail[_trailHead] = _trampler.position;
            _trailTimes[_trailHead] = now;
            _lastTrailTime = now;
        }

        private void PushShaderGlobals(float now)
        {
            var wind = new Vector2(_windDirection.x, _windDirection.z).normalized;
            Shader.SetGlobalVector(WindId, new Vector4(wind.x, wind.y, _swayFrequency, _waveLength));
            Shader.SetGlobalVector(BendId, new Vector4(
                _swayAngle * Mathf.Deg2Rad, _trampleAngle * Mathf.Deg2Rad, _trampleRadius, TrampleHeightTolerance));

            for (var i = 0; i < TrailLength; i++)
            {
                var strength = Mathf.Clamp01(1f - (now - _trailTimes[i]) / _recoverTime);
                _shaderTrail[i] = new Vector4(_trail[i].x, _trail[i].y, _trail[i].z, strength);
            }

            if (_trampler != null)
            {
                var current = _trampler.position;
                _shaderTrail[_trailHead] = new Vector4(current.x, current.y, current.z, 1f);
            }

            Shader.SetGlobalVectorArray(TrampleId, _shaderTrail);
        }

        private static float[] CreateEmptyTrailTimes()
        {
            var times = new float[TrailLength];
            System.Array.Fill(times, float.NegativeInfinity);
            return times;
        }

        /// <summary>
        /// Splits the tiles by share rather than rolling each one, so every build mixes all three
        /// kinds; a seeded shuffle decides which tile gets which.
        /// </summary>
        private List<Coverage> AssignCoverage(int count)
        {
            var bare = Mathf.RoundToInt(count * _bareShare);
            var full = Mathf.Min(count - bare, Mathf.RoundToInt(count * _fullShare));
            var coverage = new List<Coverage>(count);
            for (var i = 0; i < count; i++)
            {
                coverage.Add(i < bare ? Coverage.Bare : i < bare + full ? Coverage.Full : Coverage.Sparse);
            }

            var random = new System.Random(Seed);
            for (var i = count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (coverage[i], coverage[j]) = (coverage[j], coverage[i]);
            }

            return coverage;
        }

        private void PlaceSparse(Tile tile, System.Random random)
        {
            var count = random.Next(_sparseTufts.x, _sparseTufts.y + 1);
            var placed = new List<Vector3>();
            for (var attempt = 0; attempt < count * PlacementAttempts && placed.Count < count; attempt++)
            {
                var position = RandomOnTop(random, EdgeMargin);
                if (placed.TrueForAll(other => Vector3.Distance(other, position) >= _sparseSpacing))
                {
                    placed.Add(position);
                    AddTuft(tile, position, random);
                }
            }
        }

        /// <summary>A few short, dark tufts between the forest-floor shrubs.</summary>
        private void PlaceForest(Tile tile)
        {
            var random = new System.Random(Seed * 7 + tile.Coordinates.x * 73856093 ^ tile.Coordinates.y * 19349663);
            var count = random.Next(1, 4);
            for (var i = 0; i < count; i++)
            {
                AddTuft(tile, RandomOnTop(random, EdgeMargin), random, _forestMaterial, _forestScale);
            }
        }

        private void PlaceFull(Tile tile, System.Random random)
        {
            var usable = MapMetrics.TileSize - FullEdgeMargin * 2f;
            var step = usable / _fullGrid;
            for (var x = 0; x < _fullGrid; x++)
            {
                for (var z = 0; z < _fullGrid; z++)
                {
                    if (random.NextDouble() < _fullSkipChance)
                    {
                        continue;
                    }

                    var jitter = step * 0.3f;
                    var position = new Vector3(
                        -usable * 0.5f + step * (x + 0.5f) + Range(random, -jitter, jitter),
                        0f,
                        -usable * 0.5f + step * (z + 0.5f) + Range(random, -jitter, jitter));
                    AddTuft(tile, position, random);
                }
            }
        }

        private void AddTuft(Tile tile, Vector3 localPosition, System.Random random) =>
            AddTuft(tile, localPosition, random, _material, 1f);

        private void AddTuft(Tile tile, Vector3 localPosition, System.Random random, Material material, float scale)
        {
            var tuft = new GameObject("GrassTuft");
            tuft.transform.SetParent(tile.Visual, false);
            tuft.transform.localPosition = localPosition + Vector3.up * tile.TopY;
            tuft.transform.localRotation = Quaternion.Euler(0f, Range(random, 0f, 360f), 0f);
            tuft.transform.localScale = new Vector3(1f, scale, 1f);
            tuft.AddComponent<MeshFilter>().sharedMesh = _meshes[random.Next(_meshes.Length)];
            var renderer = tuft.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private Mesh[] BuildMeshes()
        {
            var meshes = new Mesh[MeshVariants];
            var random = new System.Random(Seed);
            for (var v = 0; v < MeshVariants; v++)
            {
                var builder = new BladeMeshBuilder();
                var blades = random.Next(7, 12);
                for (var b = 0; b < blades; b++)
                {
                    var height = Range(random, _bladeHeight.x, _bladeHeight.y);
                    var basePosition = new Vector3(Range(random, -0.07f, 0.07f), 0f, Range(random, -0.07f, 0.07f));
                    builder.AddBlade(basePosition, new Vector3(BladeWidth, height, BladeWidth), (float)random.NextDouble());
                }

                meshes[v] = builder.ToMesh($"GrassTuft{v}", BendBoundsPadding);
            }

            return meshes;
        }

        private static Vector3 RandomOnTop(System.Random random, float margin)
        {
            var half = MapMetrics.TileSize * 0.5f - margin;
            return new Vector3(Range(random, -half, half), 0f, Range(random, -half, half));
        }

        private static float Range(System.Random random, float min, float max) =>
            min + (float)random.NextDouble() * (max - min);

        private enum Coverage : byte
        {
            Bare,
            Sparse,
            Full,
        }

        /// <summary>
        /// Axis-aligned blade boxes with flat per-face normals, merged into one mesh. Every vertex
        /// carries its blade's random phase (uv0.x) and base position (uv1) for the shader.
        /// </summary>
        private sealed class BladeMeshBuilder
        {
            private static readonly Vector3[] FaceNormals =
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };

            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _normals = new();
            private readonly List<Vector2> _phases = new();
            private readonly List<Vector2> _bases = new();
            private readonly List<int> _triangles = new();

            public void AddBlade(Vector3 basePosition, Vector3 size, float phase)
            {
                var center = basePosition + Vector3.up * (size.y * 0.5f);
                var half = size * 0.5f;
                foreach (var normal in FaceNormals)
                {
                    var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up);
                    var bitangent = Vector3.Cross(normal, tangent);
                    var start = _vertices.Count;
                    foreach (var (u, w) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        var corner = normal + tangent * u + bitangent * w;
                        _vertices.Add(center + Vector3.Scale(corner, half));
                        _normals.Add(normal);
                        _phases.Add(new Vector2(phase, 0f));
                        _bases.Add(new Vector2(basePosition.x, basePosition.z));
                    }

                    _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }

            public Mesh ToMesh(string name, float boundsPadding)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _phases);
                mesh.SetUVs(1, _bases);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                var bounds = mesh.bounds;
                bounds.Expand(boundsPadding * 2f);
                mesh.bounds = bounds;
                return mesh;
            }
        }
    }
}
