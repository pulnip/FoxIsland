using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// A voxel pillar whose top surface sits at its level height. The visual reaches down to
    /// <see cref="MapMetrics.LakeBedY"/>; the collider covers the full cell down to
    /// <see cref="MapMetrics.TileBottom"/>. Visuals are inset slightly so the grid reads clearly.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class Tile : MonoBehaviour
    {
        private const float TopThickness = 0.15f;
        private const float VisualInset = 0.04f;

        [SerializeField] private Transform _top;
        [SerializeField] private Transform _body;

        public Vector2Int Coordinates { get; private set; }

        public TileSpec Spec { get; private set; }

        public float TopY => MapMetrics.TopY(Spec.Level);

        public void Initialize(Vector2Int coordinates, TileSpec spec)
        {
            Coordinates = coordinates;
            Spec = spec;
            name = $"Tile_{coordinates.x}_{coordinates.y}_{spec.Type}";
            transform.localPosition = MapMetrics.TopCenter(coordinates, 0);
            ApplyLevel(spec.Level);
        }

        /// <summary>Swaps the slab and pillar materials; the map builder picks them by elevation zone.</summary>
        public void SetMaterials(Material top, Material body)
        {
            _top.GetComponent<MeshRenderer>().sharedMaterial = top;
            _body.GetComponent<MeshRenderer>().sharedMaterial = body;
        }

        public void ApplyLevel(int level)
        {
            var top = MapMetrics.TopY(level);
            var height = top - MapMetrics.TileBottom;
            var visualWidth = MapMetrics.TileSize - VisualInset;

            _top.localPosition = new Vector3(0f, top - TopThickness * 0.5f, 0f);
            _top.localScale = new Vector3(visualWidth, TopThickness, visualWidth);

            var bodyHeight = top - TopThickness - MapMetrics.LakeBedY;
            _body.localPosition = new Vector3(0f, MapMetrics.LakeBedY + bodyHeight * 0.5f, 0f);
            _body.localScale = new Vector3(visualWidth, bodyHeight, visualWidth);

            var collider = GetComponent<BoxCollider>();
            collider.center = new Vector3(0f, MapMetrics.TileBottom + height * 0.5f, 0f);
            collider.size = new Vector3(MapMetrics.TileSize, height, MapMetrics.TileSize);
        }
    }
}
