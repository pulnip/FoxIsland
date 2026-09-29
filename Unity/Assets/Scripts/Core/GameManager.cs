using System;
using MyGame.Items;
using MyGame.Map;
using MyGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MyGame.Core
{
    /// <summary>
    /// Owns the game state (plan.md §8): spawns and counts items, detects the player touching the
    /// water, gates player input, and restarts the scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private PlayerController _player;
        [SerializeField] private ItemSpawner _itemSpawner;
        [SerializeField] private ParticleSystem _splashEffect;

        private InputAction _restartAction;
        private float _startTime;
        private float _endTime;

        public event Action<GameState> StateChanged;

        /// <summary>
        /// Raised once items are spawned and again after every pickup.
        /// </summary>
        public event Action ItemCountChanged;

        public GameState State { get; private set; } = GameState.Playing;

        public int CollectedCount { get; private set; }

        public int TotalCount { get; private set; }

        public float ElapsedTime => (State == GameState.Playing ? Time.time : _endTime) - _startTime;

        private void OnEnable()
        {
            _restartAction = InputSystem.actions.FindAction("Player/Restart", throwIfNotFound: true);
            _restartAction.Enable();
        }

        private void Start()
        {
            _startTime = Time.time;
            var items = _itemSpawner.Spawn(_config.ItemCount);
            foreach (var item in items)
            {
                item.Collected += OnItemCollected;
            }

            TotalCount = items.Count;
            ItemCountChanged?.Invoke();
        }

        private void Update()
        {
            if (_restartAction.WasPressedThisFrame())
            {
                Restart();
                return;
            }

            var position = _player.transform.position;
            if (State == GameState.Playing && position.y < MapMetrics.WaterLevel)
            {
                if (_splashEffect != null)
                {
                    Instantiate(_splashEffect, new Vector3(position.x, MapMetrics.WaterLevel, position.z), Quaternion.identity);
                }

                SetState(GameState.GameOver);
            }
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void OnItemCollected(Item item)
        {
            item.Collected -= OnItemCollected;
            if (State != GameState.Playing)
            {
                return;
            }

            CollectedCount++;
            ItemCountChanged?.Invoke();
            if (CollectedCount == TotalCount)
            {
                SetState(GameState.Cleared);
            }
        }

        private void SetState(GameState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            _endTime = Time.time;
            _player.InputEnabled = state == GameState.Playing;
            StateChanged?.Invoke(state);
        }
    }
}
