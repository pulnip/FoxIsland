using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// An iceberg (plan.md §4.9): bobs and rocks gently on the lake, and sinks a little while
    /// something stands on it, rising again when it is free. It rides the tide when there is one.
    /// The whole tile moves, collider included, so the fox rides it.
    /// </summary>
    [RequireComponent(typeof(Tile), typeof(BoxCollider))]
    public class FloatingBlock : MonoBehaviour
    {
        private const float OccupancyHeight = 0.6f;

        [SerializeField] private float _bobAmplitude = 0.03f;
        [SerializeField] private float _bobPeriod = 3f;
        [SerializeField] private float _tiltDegrees = 1.5f;
        [SerializeField] private float _sinkDepth = 0.12f;
        [SerializeField] private float _sinkSpeed = 0.35f;

        private Vector3 _restPosition;
        private float _phase;
        private float _sink;
        private Tile _tile;

        /// <summary>How far the block currently sits below its rest height (0 to the sink depth).</summary>
        public float Sink => _sink;

        public Tide Tide { get; set; }

        private void Start()
        {
            _tile = GetComponent<Tile>();
            _restPosition = transform.localPosition;
            _phase = (_tile.Coordinates.x * 0.37f + _tile.Coordinates.y * 0.71f) * Mathf.PI * 2f;
        }

        private void Update()
        {
            var target = IsOccupied() ? _sinkDepth : 0f;
            _sink = Mathf.MoveTowards(_sink, target, _sinkSpeed * Time.deltaTime);

            var wave = Time.time * Mathf.PI * 2f / _bobPeriod + _phase;
            var bob = Mathf.Sin(wave) * _bobAmplitude * (1f - _sink / _sinkDepth * 0.5f);
            var tide = Tide != null ? Tide.Rise : 0f;
            transform.localPosition = _restPosition + Vector3.up * (tide + bob - _sink);
            transform.localRotation = Quaternion.Euler(Mathf.Sin(wave * 0.7f) * _tiltDegrees, 0f, Mathf.Cos(wave * 0.9f) * _tiltDegrees);
        }

        private bool IsOccupied()
        {
            var size = new Vector3(MapMetrics.TileSize * 0.9f, OccupancyHeight, MapMetrics.TileSize * 0.9f);
            var center = transform.position + Vector3.up * (_tile.TopY + OccupancyHeight * 0.5f);
            foreach (var hit in Physics.OverlapBox(center, size * 0.5f, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit is CharacterController)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
