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
    /// Owns the game state (plan.md §8): the title screen, pausing, spawning and counting items,
    /// detecting the player touching the water, gating player input, and moving between levels.
    /// Runs after <see cref="PlayerController"/>, so the key that starts the game from the title
    /// never reaches the player in the same frame.
    /// </summary>
    [DefaultExecutionOrder(5)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelLoader _levelLoader;
        [SerializeField] private PlayerController _player;
        [SerializeField] private ItemSpawner _itemSpawner;
        [SerializeField] private ParticleSystem _splashEffect;

        private InputAction _restartAction;
        private InputAction _pauseAction;
        private InputAction _startAction;
        private float _startTime;
        private float _endTime;

        public event Action<GameState> StateChanged;

        /// <summary>
        /// Raised once items are spawned and again after every pickup.
        /// </summary>
        public event Action ItemCountChanged;

        public GameState State { get; private set; }

        public LevelLoader Level => _levelLoader;

        public int CollectedCount { get; private set; }

        public int TotalCount { get; private set; }

        public float ElapsedTime => State switch
        {
            GameState.Title => 0f,
            GameState.Playing or GameState.Paused => Time.time - _startTime,
            _ => _endTime - _startTime,
        };

        /// <summary>Test play from the map tool always skips the title.</summary>
        private void Awake() =>
            State = LevelSession.OpenAtTitle && !_levelLoader.IsTestPlay ? GameState.Title : GameState.Playing;

        private void OnEnable()
        {
            var actions = InputSystem.actions;
            _restartAction = actions.FindAction("Player/Restart", throwIfNotFound: true);
            _pauseAction = actions.FindAction("Player/Pause", throwIfNotFound: true);
            _startAction = actions.FindAction("Player/Jump", throwIfNotFound: true);
            _restartAction.Enable();
            _pauseAction.Enable();
            _startAction.Enable();
        }

        private void OnDisable() => Time.timeScale = 1f;

        private void Start()
        {
            _startTime = Time.time;
            _player.InputEnabled = State == GameState.Playing;

            var items = _itemSpawner.Spawn(_levelLoader.Layout);
            foreach (var item in items)
            {
                item.Collected += OnItemCollected;
            }

            TotalCount = items.Count;
            ItemCountChanged?.Invoke();
        }

        private void Update()
        {
            if (State == GameState.Title)
            {
                if (_startAction.WasPressedThisFrame())
                {
                    StartGame();
                }

                return;
            }

            if (_restartAction.WasPressedThisFrame())
            {
                Restart();
                return;
            }

            if (State == GameState.Cleared && _startAction.WasPressedThisFrame())
            {
                ContinueAfterClear();
                return;
            }

            if (_pauseAction.WasPressedThisFrame())
            {
                TogglePause();
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

        public void StartGame()
        {
            if (State != GameState.Title)
            {
                return;
            }

            _startTime = Time.time;
            SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
            {
                SetState(GameState.Paused);
            }
            else if (State == GameState.Paused)
            {
                SetState(GameState.Playing);
            }
        }

        /// <summary>Reloads the current level straight into play.</summary>
        public void Restart() => Reload(openAtTitle: false);

        public void NextLevel()
        {
            if (!_levelLoader.HasNextLevel)
            {
                return;
            }

            LevelSession.LevelIndex++;
            Reload(openAtTitle: false);
        }

        public void QuitToTitle()
        {
            LevelSession.LevelIndex = 0;
            Reload(openAtTitle: true);
        }

        /// <summary>Space on the clear card: the next level, a retry in test play, or the title after the last level.</summary>
        public void ContinueAfterClear()
        {
            if (_levelLoader.HasNextLevel)
            {
                NextLevel();
            }
            else if (_levelLoader.IsTestPlay)
            {
                Restart();
            }
            else
            {
                QuitToTitle();
            }
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Reload(bool openAtTitle)
        {
            LevelSession.OpenAtTitle = openAtTitle;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

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
            if (state == GameState.GameOver || state == GameState.Cleared)
            {
                _endTime = Time.time;
            }

            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            _player.InputEnabled = state == GameState.Playing;
            StateChanged?.Invoke(state);
        }
    }
}
