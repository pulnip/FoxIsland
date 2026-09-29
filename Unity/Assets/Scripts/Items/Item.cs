using System;
using MyGame.Player;
using UnityEngine;

namespace MyGame.Items
{
    /// <summary>
    /// A collectible that spins and bobs. Only the visual moves; the trigger stays put.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class Item : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private ParticleSystem _pickupEffect;
        [SerializeField] private float _spinSpeed = 90f;
        [SerializeField] private float _bobAmplitude = 0.08f;
        [SerializeField] private float _bobPeriod = 1.5f;

        private float _bobPhase;
        private bool _isCollected;

        public event Action<Item> Collected;

        private void Awake() => _bobPhase = UnityEngine.Random.value * _bobPeriod;

        private void Update()
        {
            var t = (Time.time + _bobPhase) / _bobPeriod;
            _visual.localPosition = Vector3.up * (Mathf.Sin(t * 2f * Mathf.PI) * _bobAmplitude);
            _visual.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected || !other.TryGetComponent<PlayerController>(out _))
            {
                return;
            }

            _isCollected = true;
            if (_pickupEffect != null)
            {
                Instantiate(_pickupEffect, _visual.position, Quaternion.identity);
            }

            Collected?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
