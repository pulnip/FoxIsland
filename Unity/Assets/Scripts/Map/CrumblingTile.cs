using System.Collections;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Shakes when first stood on, then sinks into the lake and respawns (plan.md §4.1).
    /// Only the visual moves; the collider stays put and is disabled while the tile is gone.
    /// </summary>
    [RequireComponent(typeof(Tile), typeof(BoxCollider))]
    public class CrumblingTile : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private ParticleSystem _dustEffect;
        [SerializeField] private ParticleSystem _splashEffect;
        [SerializeField] private float _shakeDuration = 0.8f;
        [SerializeField] private float _shakeAmplitude = 0.05f;
        [SerializeField] private float _fallDuration = 0.6f;
        [SerializeField] private float _fallDistance = 3f;
        [SerializeField] private float _respawnDelay = 3f;
        [SerializeField] private float _respawnPopDuration = 0.2f;

        private BoxCollider _collider;
        private bool _isCrumbling;

        private void Awake() => _collider = GetComponent<BoxCollider>();

        public void NotifyStoodOn()
        {
            if (!_isCrumbling)
            {
                StartCoroutine(Crumble());
            }
        }

        private IEnumerator Crumble()
        {
            _isCrumbling = true;

            for (var elapsed = 0f; elapsed < _shakeDuration; elapsed += Time.deltaTime)
            {
                var ramp = Mathf.Lerp(0.3f, 1f, elapsed / _shakeDuration);
                var jitter = Random.insideUnitCircle * (_shakeAmplitude * ramp);
                _visual.localPosition = new Vector3(jitter.x, 0f, jitter.y);
                yield return null;
            }

            _collider.enabled = false;
            var top = transform.TransformPoint(_collider.center + Vector3.up * (_collider.size.y * 0.5f));
            if (_dustEffect != null)
            {
                Instantiate(_dustEffect, top, Quaternion.identity);
            }

            var splashed = false;
            for (var elapsed = 0f; elapsed < _fallDuration; elapsed += Time.deltaTime)
            {
                var progress = elapsed / _fallDuration;
                var drop = _fallDistance * progress * progress;
                _visual.localPosition = Vector3.down * drop;
                if (!splashed && top.y - drop < MapMetrics.WaterLevel)
                {
                    splashed = true;
                    SpawnSplash(top);
                }

                yield return null;
            }

            _visual.gameObject.SetActive(false);
            yield return new WaitForSeconds(Mathf.Max(0f, _respawnDelay - _fallDuration));

            while (IsOccupied())
            {
                yield return null;
            }

            _visual.localPosition = Vector3.zero;
            _visual.gameObject.SetActive(true);
            _collider.enabled = true;
            for (var elapsed = 0f; elapsed < _respawnPopDuration; elapsed += Time.deltaTime)
            {
                _visual.localScale = Vector3.one * Mathf.SmoothStep(0.6f, 1f, elapsed / _respawnPopDuration);
                yield return null;
            }

            _visual.localScale = Vector3.one;
            _isCrumbling = false;
        }

        private void SpawnSplash(Vector3 top)
        {
            if (_splashEffect != null)
            {
                Instantiate(_splashEffect, new Vector3(top.x, MapMetrics.WaterLevel, top.z), Quaternion.identity);
            }
        }

        private bool IsOccupied()
        {
            var center = transform.TransformPoint(_collider.center);
            var hits = Physics.OverlapBox(center, _collider.size * 0.5f, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
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
