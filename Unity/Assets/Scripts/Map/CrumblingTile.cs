using System.Collections;
using UnityEngine;

namespace MyGame.Map
{
    /// <summary>
    /// Shakes when first stood on, then drops away and respawns (plan.md §4.1).
    /// Only the visual moves; the collider stays put and is disabled while the tile is gone.
    /// </summary>
    [RequireComponent(typeof(Tile), typeof(BoxCollider))]
    public class CrumblingTile : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private float _shakeDuration = 0.8f;
        [SerializeField] private float _shakeAmplitude = 0.04f;
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
                var jitter = Random.insideUnitCircle * _shakeAmplitude;
                _visual.localPosition = new Vector3(jitter.x, 0f, jitter.y);
                yield return null;
            }

            _collider.enabled = false;
            for (var elapsed = 0f; elapsed < _fallDuration; elapsed += Time.deltaTime)
            {
                var progress = elapsed / _fallDuration;
                _visual.localPosition = Vector3.down * (_fallDistance * progress * progress);
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
