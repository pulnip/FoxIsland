using System.Collections.Generic;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Merges colored boxes (each turned about Y) into one flat-shaded mesh for the
    /// <c>MyGame/VoxelLit</c> shader.
    /// </summary>
    internal sealed class VoxelMeshBuilder
    {
        private static readonly Vector3[] FaceNormals =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _triangles = new();

        /// <summary>
        /// Adds a box by its bottom center, so <paramref name="bottom"/>.y = 0 sits on the ground.
        /// <paramref name="color"/> is sRGB, like material colors; vertex colors are not converted for us.
        /// </summary>
        public void AddBox(Vector3 bottom, Vector3 size, float yawDegrees, Color color)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                color = color.linear;
            }

            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var center = bottom + Vector3.up * (size.y * 0.5f);
            var half = size * 0.5f;
            foreach (var normal in FaceNormals)
            {
                var tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up);
                var bitangent = Vector3.Cross(normal, tangent);
                var start = _vertices.Count;
                foreach (var (u, w) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    var corner = Vector3.Scale(normal + tangent * u + bitangent * w, half);
                    _vertices.Add(center + rotation * corner);
                    _normals.Add(rotation * normal);
                    _colors.Add(color);
                }

                _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
