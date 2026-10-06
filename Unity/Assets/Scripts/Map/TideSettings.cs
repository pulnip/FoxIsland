using System;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// A layout's tide (plan.md §4.10): the lake rises from its usual level by up to
    /// <see cref="Rise"/> and falls back once every <see cref="Period"/> seconds, starting at low tide.
    /// </summary>
    [Serializable]
    public struct TideSettings : IEquatable<TideSettings>
    {
        public const float MinRise = 0.3f;
        public const float MaxRise = 1f;
        public const float MinPeriod = 6f;
        public const float MaxPeriod = 12f;

        [SerializeField] private bool _enabled;
        [SerializeField, Range(MinRise, MaxRise)] private float _rise;
        [SerializeField, Range(MinPeriod, MaxPeriod)] private float _period;

        public TideSettings(bool enabled, float rise, float period)
        {
            _enabled = enabled;
            _rise = rise;
            _period = period;
        }

        public static TideSettings Default => new(false, 0.6f, 9f);

        public bool Enabled => _enabled;

        public float Rise => Mathf.Clamp(_rise, MinRise, MaxRise);

        public float Period => Mathf.Clamp(_period, MinPeriod, MaxPeriod);

        /// <summary>The lake surface at high tide (the usual level when the tide is off).</summary>
        public float HighWaterLevel => MapMetrics.WaterLevel + (_enabled ? Rise : 0f);

        public bool Equals(TideSettings other) => _enabled == other._enabled && _rise.Equals(other._rise) && _period.Equals(other._period);

        public override bool Equals(object obj) => obj is TideSettings other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_enabled, _rise, _period);
    }
}
