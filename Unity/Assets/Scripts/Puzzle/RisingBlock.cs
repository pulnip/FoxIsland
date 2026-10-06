using MyGame.Map;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// Moves its tile between the cell's height and the raised height while its group is on
    /// (plan.md §4.10). The pillar and collider stretch so the block always reaches the lake bed,
    /// and a frame in the group's color marks the top.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(Tile))]
    public class RisingBlock : MonoBehaviour
    {
        private const float TravelTime = 0.4f;

        private PuzzleBoard _board;
        private Tile _tile;
        private Vector3 _restPosition;
        private float _raisedOffset;
        private float _offset;

        public PuzzleGroup Group { get; private set; }

        /// <summary>How far the block stands above (or below) its resting height now.</summary>
        public float Offset => _offset;

        public void Initialize(PuzzleBoard board, RisingBlockPlacement placement, GameObject marker, Material material)
        {
            _board = board;
            _tile = GetComponent<Tile>();
            Group = placement.Group;
            _restPosition = transform.localPosition;
            _raisedOffset = MapMetrics.TopY(placement.RaisedHeight) - _tile.TopY;
            marker.transform.localPosition = Vector3.up * _tile.TopY;
            if (material != null)
            {
                foreach (var renderer in marker.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        private void Update()
        {
            var target = _board.IsGroupOn(Group) ? _raisedOffset : 0f;
            if (Mathf.Approximately(_offset, target))
            {
                return;
            }

            var speed = Mathf.Abs(_raisedOffset) / TravelTime;
            _offset = Mathf.MoveTowards(_offset, target, speed * Time.deltaTime);
            transform.localPosition = _restPosition + Vector3.up * _offset;
            _tile.ApplyLevel(_tile.Spec.Height, MapMetrics.LakeBedY - _offset, MapMetrics.TileBottom - _offset);
        }
    }
}
