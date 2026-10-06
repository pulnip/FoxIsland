using MyGame.Map;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// Shuttles between its two cells (plan.md §4.10), easing in and out and pausing briefly at each
    /// end. The fox rides it because <see cref="Player.PlayerController"/> follows whatever it
    /// stands on.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Share of each one-way trip spent waiting at the end it arrived at.")]
        [SerializeField, Range(0f, 0.4f)] private float _holdFraction = 0.2f;

        private Vector3 _from;
        private Vector3 _to;
        private float _period = PlatformPlacement.MinPeriod;
        private float _time;

        public void Initialize(PlatformPlacement placement)
        {
            _from = MapMetrics.TopCenter(placement.From, placement.Height);
            _to = MapMetrics.TopCenter(placement.To, placement.Height);
            _period = placement.Period;
            _time = 0f;
            transform.position = _from;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var phase = Mathf.Repeat(_time / _period, 1f);
            var travel = Mathf.InverseLerp(0f, 1f - _holdFraction, Mathf.Repeat(phase * 2f, 1f));
            var eased = Mathf.SmoothStep(0f, 1f, travel);
            transform.position = Vector3.Lerp(_from, _to, phase < 0.5f ? eased : 1f - eased);
        }
    }
}
