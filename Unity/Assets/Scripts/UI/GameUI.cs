using MyGame.Core;
using MyGame.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    /// <summary>
    /// Drives the HUD and the panels in <c>GameUI.uxml</c> from <see cref="GameManager"/> (plan.md §8).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const string HintsHiddenClass = "hints--hidden";
        private const string BumpClass = "hud--bump";
        private const long BumpDurationMs = 120;

        [SerializeField] private GameManager _gameManager;
        [SerializeField] private PlayerController _player;

        private VisualElement _root;
        private VisualElement _hud;
        private VisualElement _hudItems;
        private Label _hudItemsLabel;
        private Label _hudTime;
        private VisualElement _hints;
        private GameIcon _modeIcon;
        private Label _modeLabel;
        private VisualElement _titlePanel;
        private VisualElement _pausePanel;
        private VisualElement _gameOverPanel;
        private VisualElement _clearPanel;
        private Label _clearTime;
        private Label _clearItems;
        private int _shownSeconds = -1;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _hud = _root.Q<VisualElement>("hud");
            _hudItems = _root.Q<VisualElement>("hud-items");
            _hudItemsLabel = _root.Q<Label>("hud-items-label");
            _hudTime = _root.Q<Label>("hud-time");
            _hints = _root.Q<VisualElement>("hud-hints");
            _modeIcon = _root.Q<GameIcon>("hud-mode-icon");
            _modeLabel = _root.Q<Label>("hud-mode");
            _titlePanel = _root.Q<VisualElement>("title-panel");
            _pausePanel = _root.Q<VisualElement>("pause-panel");
            _gameOverPanel = _root.Q<VisualElement>("game-over-panel");
            _clearPanel = _root.Q<VisualElement>("clear-panel");
            _clearTime = _root.Q<Label>("clear-time");
            _clearItems = _root.Q<Label>("clear-items");

            Bind("title-start", GameState.Title, _gameManager.StartGame);
            Bind("title-quit", GameState.Title, _gameManager.QuitApplication);
            Bind("pause-resume", GameState.Paused, _gameManager.TogglePause);
            Bind("pause-restart", GameState.Paused, _gameManager.Restart);
            Bind("pause-quit", GameState.Paused, _gameManager.QuitToTitle);
            Bind("game-over-restart", GameState.GameOver, _gameManager.Restart);
            Bind("clear-restart", GameState.Cleared, _gameManager.Restart);

            _gameManager.StateChanged += OnStateChanged;
            _gameManager.ItemCountChanged += OnItemCountChanged;
            _player.RunningChanged += OnRunningChanged;
            OnStateChanged(_gameManager.State);
            OnItemCountChanged();
            OnRunningChanged(_player.IsRunning);
        }

        private void OnDisable()
        {
            _gameManager.StateChanged -= OnStateChanged;
            _gameManager.ItemCountChanged -= OnItemCountChanged;
            _player.RunningChanged -= OnRunningChanged;
        }

        private void Update()
        {
            var seconds = Mathf.FloorToInt(_gameManager.ElapsedTime);
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                _hudTime.text = $"{seconds / 60}:{seconds % 60:00}";
            }
        }

        /// <summary>
        /// Buttons act only in the state that shows them: a hidden button can keep keyboard focus,
        /// and Space (jump) would otherwise submit it.
        /// </summary>
        private void Bind(string buttonName, GameState state, System.Action action)
        {
            _root.Q<Button>(buttonName).clicked += () =>
            {
                if (_gameManager.State == state)
                {
                    action();
                }
            };
        }

        private void OnStateChanged(GameState state)
        {
            _root.focusController?.focusedElement?.Blur();
            _hud.EnableInClassList(HiddenClass, state == GameState.Title);
            _hints.EnableInClassList(HintsHiddenClass, state != GameState.Playing);
            _titlePanel.EnableInClassList(HiddenClass, state != GameState.Title);
            _pausePanel.EnableInClassList(HiddenClass, state != GameState.Paused);
            _gameOverPanel.EnableInClassList(HiddenClass, state != GameState.GameOver);
            _clearPanel.EnableInClassList(HiddenClass, state != GameState.Cleared);
            if (state == GameState.Cleared)
            {
                var time = _gameManager.ElapsedTime;
                var minutes = Mathf.FloorToInt(time / 60f);
                _clearTime.text = $"{minutes}:{time - minutes * 60f:00.0}";
                _clearItems.text = $"{_gameManager.CollectedCount} / {_gameManager.TotalCount}";
            }
        }

        private void OnItemCountChanged()
        {
            _hudItemsLabel.text = $"{_gameManager.CollectedCount} / {_gameManager.TotalCount}";
            if (_gameManager.CollectedCount > 0)
            {
                _hudItems.AddToClassList(BumpClass);
                _hudItems.schedule.Execute(() => _hudItems.RemoveFromClassList(BumpClass)).StartingIn(BumpDurationMs);
            }
        }

        private void OnRunningChanged(bool isRunning)
        {
            _modeIcon.Kind = isRunning ? GameIconKind.Run : GameIconKind.Walk;
            _modeLabel.text = isRunning ? "RUNNING" : "WALKING";
        }
    }
}
