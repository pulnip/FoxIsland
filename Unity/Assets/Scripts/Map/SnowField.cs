using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.Map
{
    /// <summary>
    /// Snow on the alpine tiles (plan.md §4.9): a <see cref="MapMetrics.SnowDepth"/> slab on every
    /// snow tile, and one track texture over the whole grid that <see cref="Stamp"/> presses prints
    /// into. The <c>MyGame/Snow</c> shader sinks the slab by the texture. Prints stay for the level.
    /// </summary>
    public class SnowField : MonoBehaviour
    {
        private const int Resolution = 512;
        private const int Segments = 16;
        private const float VisualInset = 0.04f;
        private const float RectPadding = 0.5f;

        private static readonly int TracksId = Shader.PropertyToID("_SnowTracks");
        private static readonly int RectId = Shader.PropertyToID("_SnowTrackRect");
        private static readonly int TexelId = Shader.PropertyToID("_SnowTrackTexel");

        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private Material _material;

        private Texture2D _tracks;
        private byte[] _pixels;
        private Rect _rect;
        private Mesh _slab;
        private bool _isDirty;

        private void Start() => Build();

        private void LateUpdate() => UploadIfDirty();

        private void OnDestroy()
        {
            DestroyAsset(_tracks);
            DestroyAsset(_slab);
        }

        public void Build()
        {
            var layout = _mapBuilder.Layout;
            if (layout == null)
            {
                return;
            }

            var bounds = MapMetrics.GridBounds(layout.Width, layout.Depth, layout.MaxHeight);
            _rect = Rect.MinMaxRect(bounds.min.x - RectPadding, bounds.min.z - RectPadding, bounds.max.x + RectPadding, bounds.max.z + RectPadding);
            if (_tracks == null)
            {
                _tracks = new Texture2D(Resolution, Resolution, TextureFormat.R8, false, true)
                {
                    name = "SnowTracks",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                _pixels = new byte[Resolution * Resolution];
            }

            System.Array.Clear(_pixels, 0, _pixels.Length);
            _isDirty = true;
            UploadIfDirty();
            Shader.SetGlobalTexture(TracksId, _tracks);
            Shader.SetGlobalVector(RectId, new Vector4(_rect.xMin, _rect.yMin, 1f / _rect.width, 1f / _rect.height));
            Shader.SetGlobalVector(TexelId, new Vector4(1f / Resolution, 1f / Resolution, _rect.width / Resolution, _rect.height / Resolution));

            _slab ??= BuildSlab();
            foreach (var tile in _mapBuilder.Tiles)
            {
                if (tile.Spec.IsSnow)
                {
                    AddSlab(tile);
                }
            }
        }

        /// <summary>
        /// Presses an ellipse into the snow: <paramref name="size"/> is its width (x) and length (z)
        /// in world units, turned by <paramref name="yawDegrees"/>; <paramref name="depth"/> is 0–1 of
        /// the shader's maximum press. Overlapping prints keep the deeper value.
        /// </summary>
        public void Stamp(Vector3 position, Vector2 size, float yawDegrees, float depth)
        {
            if (_pixels == null)
            {
                return;
            }

            var texelX = _rect.width / Resolution;
            var texelZ = _rect.height / Resolution;
            var radius = Mathf.Max(size.x, size.y) * 0.5f;
            var minX = Mathf.Max(0, Mathf.FloorToInt((position.x - radius - _rect.xMin) / texelX));
            var maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt((position.x + radius - _rect.xMin) / texelX));
            var minZ = Mathf.Max(0, Mathf.FloorToInt((position.z - radius - _rect.yMin) / texelZ));
            var maxZ = Mathf.Min(Resolution - 1, Mathf.CeilToInt((position.z + radius - _rect.yMin) / texelZ));
            var yaw = yawDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(yaw);
            var sin = Mathf.Sin(yaw);
            var halfWidth = size.x * 0.5f;
            var halfLength = size.y * 0.5f;

            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = _rect.xMin + (x + 0.5f) * texelX - position.x;
                    var dz = _rect.yMin + (z + 0.5f) * texelZ - position.z;
                    var across = (dx * cos - dz * sin) / halfWidth;
                    var along = (dx * sin + dz * cos) / halfLength;
                    var distance = Mathf.Sqrt(across * across + along * along);
                    if (distance >= 1f)
                    {
                        continue;
                    }

                    var value = (byte)Mathf.RoundToInt(255f * depth * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 1f, distance)));
                    var index = z * Resolution + x;
                    if (value > _pixels[index])
                    {
                        _pixels[index] = value;
                        _isDirty = true;
                    }
                }
            }
        }

        private void UploadIfDirty()
        {
            if (!_isDirty || _tracks == null)
            {
                return;
            }

            _tracks.SetPixelData(_pixels, 0);
            _tracks.Apply(false);
            _isDirty = false;
        }

        private void AddSlab(Tile tile)
        {
            var snow = new GameObject("Snow");
            snow.transform.SetParent(tile.Visual, false);
            snow.transform.localPosition = Vector3.up * tile.TopY;
            snow.AddComponent<MeshFilter>().sharedMesh = _slab;
            var renderer = snow.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// A <see cref="MapMetrics.SnowDepth"/>-thick slab with a subdivided top. Vertex color R marks
        /// vertices that sink (top and the upper edge of the sides), G marks the top face.
        /// </summary>
        private static Mesh BuildSlab()
        {
            var size = MapMetrics.TileSize - VisualInset;
            var half = size * 0.5f;
            var step = size / Segments;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            var side = Segments + 1;
            for (var z = 0; z < side; z++)
            {
                for (var x = 0; x < side; x++)
                {
                    vertices.Add(new Vector3(x * step - half, MapMetrics.SnowDepth, z * step - half));
                    normals.Add(Vector3.up);
                    colors.Add(new Color(1f, 1f, 0f));
                }
            }

            for (var z = 0; z < Segments; z++)
            {
                for (var x = 0; x < Segments; x++)
                {
                    var corner = z * side + x;
                    triangles.AddRange(new[] { corner, corner + side, corner + 1, corner + 1, corner + side, corner + side + 1 });
                }
            }

            AddSide(vertices, normals, colors, triangles, Vector3.back, half, step);
            AddSide(vertices, normals, colors, triangles, Vector3.forward, half, step);
            AddSide(vertices, normals, colors, triangles, Vector3.left, half, step);
            AddSide(vertices, normals, colors, triangles, Vector3.right, half, step);

            var mesh = new Mesh { name = "SnowSlab", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>One side wall facing <paramref name="normal"/>, its top edge matching the top grid.</summary>
        private static void AddSide(
            List<Vector3> vertices, List<Vector3> normals,
            List<Color> colors, List<int> triangles,
            Vector3 normal, float half, float step)
        {
            var tangent = Vector3.Cross(Vector3.up, normal);
            var start = vertices.Count;
            for (var i = 0; i <= Segments; i++)
            {
                var along = -half + i * step;
                var basePoint = normal * half + tangent * along;
                vertices.Add(basePoint);
                vertices.Add(basePoint + Vector3.up * MapMetrics.SnowDepth);
                normals.Add(normal);
                normals.Add(normal);
                colors.Add(new Color(0f, 0f, 0f));
                colors.Add(new Color(1f, 0f, 0f));
            }

            for (var i = 0; i < Segments; i++)
            {
                var bottom = start + i * 2;
                triangles.AddRange(new[] { bottom, bottom + 2, bottom + 1, bottom + 2, bottom + 3, bottom + 1 });
            }
        }

        private static void DestroyAsset(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(asset);
            }
            else
            {
                DestroyImmediate(asset);
            }
        }
    }
}
