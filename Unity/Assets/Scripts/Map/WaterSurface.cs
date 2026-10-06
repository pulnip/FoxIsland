using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.Map
{
    /// <summary>
    /// Builds the flat grid the <c>MyGame/Water</c> shader bobs (plan.md §4.7). The grid is centered on
    /// this transform, which sits at <see cref="MapMetrics.WaterLevel"/>.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterSurface : MonoBehaviour
    {
        private const float WaveBoundsPadding = 0.1f;

        [SerializeField] private float _size = 60f;
        [SerializeField] private float _cellSize = 0.5f;

        private Mesh _mesh;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        public void Build()
        {
            var cells = Mathf.Max(1, Mathf.RoundToInt(_size / _cellSize));
            var side = cells + 1;
            var half = _size * 0.5f;
            var step = _size / cells;

            var vertices = new Vector3[side * side];
            for (var z = 0; z < side; z++)
            {
                for (var x = 0; x < side; x++)
                {
                    vertices[z * side + x] = new Vector3(x * step - half, 0f, z * step - half);
                }
            }

            var triangles = new int[cells * cells * 6];
            var t = 0;
            for (var z = 0; z < cells; z++)
            {
                for (var x = 0; x < cells; x++)
                {
                    var corner = z * side + x;
                    triangles[t++] = corner;
                    triangles[t++] = corner + side;
                    triangles[t++] = corner + 1;
                    triangles[t++] = corner + 1;
                    triangles[t++] = corner + side;
                    triangles[t++] = corner + side + 1;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "WaterGrid", hideFlags = HideFlags.DontSave };
            }

            _mesh.Clear();
            _mesh.indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(_size, WaveBoundsPadding * 2f, _size));
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }
    }
}
