using MyGame.Map;
using MyGame.Player;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// Launches the fox <see cref="_launchHeight"/> high, enough for four levels, when it steps or
    /// lands on the pad (plan.md §4.10). Like a switch it has no collider: the fox stands on the
    /// block and the pad springs.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class Springboard : MonoBehaviour
    {
        private const float SenseHeight = 0.4f;

        [SerializeField] private Transform _pad;
        [SerializeField] private Transform _spring;
        [SerializeField] private float _launchHeight = 2.4f;
        [SerializeField] private float _cooldown = 0.3f;
        [SerializeField] private float _wobbleAmplitude = 0.08f;

        private Vector3 _padRest;
        private Vector3 _springScale;
        private float _lastLaunch = float.NegativeInfinity;

        public float LaunchHeight => _launchHeight;

        public void Initialize(Tile tile)
        {
            transform.position = tile.transform.position + Vector3.up * tile.TopY;
            _padRest = _pad.localPosition;
            _springScale = _spring.localScale;
        }

        private void Update()
        {
            if (Time.time - _lastLaunch > _cooldown && TryFindPlayer(out var player) && player.IsGrounded)
            {
                player.Launch(_launchHeight);
                _lastLaunch = Time.time;
            }

            var t = Time.time - _lastLaunch;
            var wobble = t < 1f ? _wobbleAmplitude * Mathf.Exp(-t * 6f) * Mathf.Sin(t * 28f) : 0f;
            _pad.localPosition = _padRest + Vector3.up * wobble;
            var stretch = Mathf.Max(0.2f, 1f + wobble / Mathf.Max(0.01f, _padRest.y));
            _spring.localScale = new Vector3(_springScale.x, _springScale.y * stretch, _springScale.z);
        }

        private bool TryFindPlayer(out PlayerController player)
        {
            var center = transform.position + Vector3.up * (SenseHeight * 0.5f);
            var halfExtents = new Vector3(MapMetrics.TileSize * 0.3f, SenseHeight * 0.5f, MapMetrics.TileSize * 0.3f);
            foreach (var hit in Physics.OverlapBox(center, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit is CharacterController && hit.TryGetComponent(out player))
                {
                    return true;
                }
            }

            player = null;
            return false;
        }
    }
}
