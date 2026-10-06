using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Raises and lowers the lake (plan.md §4.10) on a smooth cycle that starts at low tide. Sits on
    /// the lake's <see cref="WaterSurface"/> and moves it; the water check, icebergs, and the HUD
    /// gauge read <see cref="Level"/>. The clock only runs while <see cref="IsRunning"/>, so the
    /// tide waits at low water behind the title.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class Tide : MonoBehaviour
    {
        private TideSettings _settings;
        private float _time;

        public bool IsEnabled => _settings.Enabled;

        public bool IsRunning { get; set; }

        /// <summary>From 0 at low tide to 1 at high tide.</summary>
        public float Amount { get; private set; }

        public bool IsRising => IsEnabled && Mathf.Repeat(_time, _settings.Period) < _settings.Period * 0.5f;

        /// <summary>The lake surface now.</summary>
        public float Level => MapMetrics.WaterLevel + Rise;

        /// <summary>How far the lake stands above its usual level now.</summary>
        public float Rise => IsEnabled ? Amount * _settings.Rise : 0f;

        public void Configure(TideSettings settings)
        {
            _settings = settings;
            _time = 0f;
            Apply();
        }

        private void Update()
        {
            if (IsEnabled && IsRunning)
            {
                _time += Time.deltaTime;
                Apply();
            }
        }

        private void Apply()
        {
            Amount = IsEnabled ? (1f - Mathf.Cos(_time / _settings.Period * Mathf.PI * 2f)) * 0.5f : 0f;
            var position = transform.position;
            transform.position = new Vector3(position.x, Level, position.z);
        }
    }
}
