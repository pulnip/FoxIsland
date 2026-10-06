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
    /// detecting the player touching the water, gating player input, recording clears, and moving
    /// between stages.
    /// Runs after <see cref="PlayerController"/>, so the key that starts the game from the title
    /// never reaches the player in the same frame.
    /// </summary>
    [DefaultExecutionOrder(5)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelLoader _levelLoader;
        [SerializeField] private PlayerController _player;
        [SerializeField] private ItemSpawner _itemSpawner;
        [SerializeField] private Tide _tide;
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

        public Tide Tide => _tide;

        /// <summary>The lake surface the fox must stay above; it moves with the tide.</summary>
        public float WaterLevel => _tide != null ? _tide.Level : MapMetrics.WaterLevel;

        public int CollectedCount { get; private set; }

        public int TotalCount { get; private set; }

        /// <summary>The clear just recorded beat the stage's previous best (or was the first).</summary>
        public bool IsNewBest { get; private set; }

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
            if (_tide != null)
            {
                _tide.IsRunning = State != GameState.Title;
            }

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
            var waterLevel = WaterLevel;
            if (State == GameState.Playing && position.y < waterLevel)
            {
                if (_splashEffect != null)
                {
                    Instantiate(_splashEffect, new Vector3(position.x, waterLevel, position.z), Quaternion.identity);
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

        /// <summary>Reloads the current stage straight into play.</summary>
        public void Restart() => Reload(openAtTitle: false);

        public void NextStage()
        {
            if (_levelLoader.TryGetNextStage(out var next))
            {
                LevelSession.Stage = next;
                Reload(openAtTitle: false);
            }
        }

        /// <summary>Plays <paramref name="stage"/> from the stage list, if its world is unlocked.</summary>
        public void StartStage(StageRef stage)
        {
            if (!_levelLoader.Progress.IsUnlocked(_levelLoader.Worlds, stage))
            {
                return;
            }

            if (State == GameState.Title && stage == _levelLoader.Stage)
            {
                StartGame();
                return;
            }

            LevelSession.Stage = stage;
            Reload(openAtTitle: false);
        }

        public void QuitToTitle() => Reload(openAtTitle: true);

        /// <summary>Back to the title, opened on the stage list.</summary>
        public void OpenStages() => Reload(openAtTitle: true, stageSelect: true);

        /// <summary>Space on the clear card: the next stage, a retry in test play, or the stage list.</summary>
        public void ContinueAfterClear()
        {
            if (_levelLoader.TryGetNextStage(out _))
            {
                NextStage();
            }
            else if (_levelLoader.IsTestPlay)
            {
                Restart();
            }
            else
            {
                OpenStages();
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

        private static void Reload(bool openAtTitle, bool stageSelect = false)
        {
            LevelSession.OpenAtTitle = openAtTitle;
            LevelSession.OpenStageSelect = stageSelect;
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

            if (state == GameState.Cleared && !_levelLoader.IsTestPlay)
            {
                IsNewBest = _levelLoader.Progress.RecordClear(_levelLoader.Layout, _endTime - _startTime);
                _levelLoader.Progress.Save();
            }

            Time.timeScale = state == GameState.Paused ? 0f : 1f;
            _player.InputEnabled = state == GameState.Playing;
            if (_tide != null)
            {
                _tide.IsRunning = state != GameState.Title;
            }

            StateChanged?.Invoke(state);
        }
    }
}
