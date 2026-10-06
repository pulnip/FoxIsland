using System.Collections.Generic;
using System.Linq;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Player
{
    /// <summary>
    /// Stamps a footprint under each named paw when the Walk/Run clips fire their
    /// <c>Footstep</c> animation events: a print particle tinted for the ground (sand, rock, earth), a
    /// pressed print in snow (<see cref="SnowField"/>), a small splash in shallow water, nothing on
    /// ice. Landing in snow presses a body-sized dent. Lives on the animated model so it receives
    /// the events.
    /// </summary>
    public class FootprintEmitter : MonoBehaviour
    {
        private const float RayStartHeight = 0.2f;
        private const float RayLength = 0.4f;
        private const float SurfaceOffset = 0.006f;
        private const float LandingPrintDepth = 0.55f;

        private static readonly Vector2 PawPrintSize = new(0.07f, 0.09f);
        private static readonly Vector2 LandingPrintSize = new(0.24f, 0.42f);
        private static readonly Color SandPrint = new(0.64f, 0.52f, 0.34f);
        private static readonly Color RockPrint = new(0.33f, 0.33f, 0.35f);

        [SerializeField] private PlayerController _player;
        [SerializeField] private ParticleSystem _footprints;
        [SerializeField] private ParticleSystem _stepSplash;

        private Dictionary<string, Renderer> _paws;

        private void OnEnable() => _player.Landed += OnLanded;

        private void OnDisable() => _player.Landed -= OnLanded;

        /// <summary>
        /// Animation event. <paramref name="legs"/> is a comma-separated list of leg names,
        /// e.g. <c>LegFR,LegBL</c>; each maps to the <c>Mesh_</c> renderer of that leg.
        /// </summary>
        public void Footstep(string legs)
        {
            if (!_player.IsGrounded)
            {
                return;
            }

            _paws ??= GetComponentsInChildren<Renderer>().ToDictionary(renderer => renderer.name);
            var yaw = _player.transform.eulerAngles.y;
            foreach (var leg in legs.Split(','))
            {
                if (!_paws.TryGetValue($"Mesh_{leg.Trim()}", out var paw))
                {
                    continue;
                }

                var bounds = paw.bounds;
                var origin = new Vector3(bounds.center.x, bounds.min.y + RayStartHeight, bounds.center.z);
                if (!Physics.Raycast(origin, Vector3.down, out var hit, RayLength, ~0, QueryTriggerInteraction.Ignore)
                    || hit.collider is CharacterController)
                {
                    continue;
                }

                if (TryGetSurface(hit, out var tile))
                {
                    if (tile.Spec.IsSlippery)
                    {
                        continue;
                    }

                    if (tile.Spec.IsSnow && TryGetSnow(tile, out var snow))
                    {
                        snow.Stamp(hit.point, PawPrintSize, yaw, 1f);
                        continue;
                    }

                    if (tile.Spec.Skin == Skin.Water)
                    {
                        if (_stepSplash != null)
                        {
                            Instantiate(_stepSplash, hit.point + Vector3.up * MapMetrics.PoolDepth, Quaternion.identity);
                        }

                        continue;
                    }
                }

                var print = new ParticleSystem.EmitParams
                {
                    position = hit.point + hit.normal * SurfaceOffset,
                    rotation3D = new Vector3(0f, yaw, 0f),
                    applyShapeToPosition = false,
                };
                if (tile != null)
                {
                    switch (tile.Spec.Skin)
                    {
                        case Skin.Sand:
                            print.startColor = SandPrint;
                            break;
                        case Skin.Rock:
                            print.startColor = RockPrint;
                            break;
                    }
                }

                _footprints.Emit(print, 1);
            }
        }

        private void OnLanded()
        {
            var origin = _player.transform.position + Vector3.up * RayStartHeight;
            if (Physics.Raycast(origin, Vector3.down, out var hit, RayLength, ~0, QueryTriggerInteraction.Ignore)
                && TryGetSurface(hit, out var tile) && tile.Spec.IsSnow && TryGetSnow(tile, out var snow))
            {
                snow.Stamp(hit.point, LandingPrintSize, _player.transform.eulerAngles.y, LandingPrintDepth);
            }
        }

        private static bool TryGetSurface(RaycastHit hit, out Tile tile) => hit.collider.TryGetComponent(out tile);

        /// <summary>The snow field lives on the map object that owns the tiles.</summary>
        private static bool TryGetSnow(Tile tile, out SnowField snow)
        {
            snow = tile.GetComponentInParent<SnowField>();
            return snow != null;
        }
    }
}
