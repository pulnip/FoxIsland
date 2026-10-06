using System.Linq;
using MyGame.Core;
using MyGame.Map;
using MyGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    /// <summary>
    /// Drives the HUD and the panels in <c>GameUI.uxml</c> from <see cref="GameManager"/> (plan.md §8.1),
    /// including the title's stage list, which it builds from the world list and saved progress.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const string HintsHiddenClass = "hints--hidden";
        private const string BumpClass = "hud--bump";
        private const string NextStageClass = "stage-card--next";
        private const string EmptyStageClass = "stage-card--empty";
        private const string BestClass = "card__note--best";
        private const long BumpDurationMs = 120;

        [SerializeField] private GameManager _gameManager;
        [SerializeField] private PlayerController _player;

        private VisualElement _root;
        private VisualElement _hud;
        private VisualElement _hudItems;
        private Label _hudItemsLabel;
        private Label _hudTime;
        private Label _hudLevel;
        private VisualElement _hints;
        private GameIcon _modeIcon;
        private Label _modeLabel;
        private VisualElement _titlePanel;
        private Label _titleStartLabel;
        private VisualElement _stagesPanel;
        private VisualElement _stagesList;
        private VisualElement _pausePanel;
        private VisualElement _gameOverPanel;
        private VisualElement _clearPanel;
        private Label _clearTitle;
        private Label _clearTime;
        private Label _clearItems;
        private Label _clearBest;
        private VisualElement _clearNext;
        private VisualElement _clearStages;
        private Label _clearStagesLabel;
        private InputAction _backAction;
        private bool _stagesOpen;
        private int _shownSeconds = -1;

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _hud = _root.Q<VisualElement>("hud");
            _hudItems = _root.Q<VisualElement>("hud-items");
            _hudItemsLabel = _root.Q<Label>("hud-items-label");
            _hudTime = _root.Q<Label>("hud-time");
            _hudLevel = _root.Q<Label>("hud-level");
            _hints = _root.Q<VisualElement>("hud-hints");
            _modeIcon = _root.Q<GameIcon>("hud-mode-icon");
            _modeLabel = _root.Q<Label>("hud-mode");
            _titlePanel = _root.Q<VisualElement>("title-panel");
            _titleStartLabel = _root.Q<Label>("title-start-label");
            _stagesPanel = _root.Q<VisualElement>("stages-panel");
            _stagesList = _root.Q<VisualElement>("stages-list");
            _pausePanel = _root.Q<VisualElement>("pause-panel");
            _gameOverPanel = _root.Q<VisualElement>("game-over-panel");
            _clearPanel = _root.Q<VisualElement>("clear-panel");
            _clearTitle = _root.Q<Label>("clear-title");
            _clearTime = _root.Q<Label>("clear-time");
            _clearItems = _root.Q<Label>("clear-items");
            _clearBest = _root.Q<Label>("clear-best");
            _clearNext = _root.Q<VisualElement>("clear-next");
            _clearStages = _root.Q<VisualElement>("clear-stages");
            _clearStagesLabel = _root.Q<Label>("clear-stages-label");
            _backAction = InputSystem.actions.FindAction("Player/Pause", throwIfNotFound: true);

            Bind("title-start", GameState.Title, _gameManager.StartGame);
            Bind("title-stages", GameState.Title, () => ShowStages(true));
            Bind("title-quit", GameState.Title, _gameManager.QuitApplication);
            Bind("stages-back", GameState.Title, () => ShowStages(false));
            Bind("pause-resume", GameState.Paused, _gameManager.TogglePause);
            Bind("pause-restart", GameState.Paused, _gameManager.Restart);
            Bind("pause-quit", GameState.Paused, _gameManager.QuitToTitle);
            Bind("game-over-restart", GameState.GameOver, _gameManager.Restart);
            Bind("clear-next", GameState.Cleared, _gameManager.NextStage);
            Bind("clear-restart", GameState.Cleared, _gameManager.Restart);
            Bind("clear-stages", GameState.Cleared, _gameManager.OpenStages);

            _gameManager.StateChanged += OnStateChanged;
            _gameManager.ItemCountChanged += OnItemCountChanged;
            _player.RunningChanged += OnRunningChanged;
        }

        /// <summary>After every Awake, so the game manager has picked its first state and level.</summary>
        private void Start()
        {
            var level = _gameManager.Level;
            _hudLevel.text = level.StageTitle.ToUpperInvariant();
            var anyCleared = level.Worlds.AllStages().Any(stage => level.Worlds.TryGetStage(stage, out var layout) && level.Progress.IsCleared(layout));
            _titleStartLabel.text = $"{(anyCleared ? "Continue" : "Start")}  {level.Stage.Label}";
            _stagesOpen = LevelSession.OpenStageSelect && _gameManager.State == GameState.Title;
            if (_stagesOpen)
            {
                BuildStageList();
            }

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
            if (_stagesOpen && _backAction.WasPressedThisFrame())
            {
                ShowStages(false);
            }

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
            _titlePanel.EnableInClassList(HiddenClass, state != GameState.Title || _stagesOpen);
            _stagesPanel.EnableInClassList(HiddenClass, state != GameState.Title || !_stagesOpen);
            _pausePanel.EnableInClassList(HiddenClass, state != GameState.Paused);
            _gameOverPanel.EnableInClassList(HiddenClass, state != GameState.GameOver);
            _clearPanel.EnableInClassList(HiddenClass, state != GameState.Cleared);
            if (state == GameState.Cleared)
            {
                ShowClear();
            }
        }

        private void ShowClear()
        {
            var level = _gameManager.Level;
            _clearTime.text = FormatTime(_gameManager.ElapsedTime);
            _clearItems.text = $"{_gameManager.CollectedCount} / {_gameManager.TotalCount}";

            var hasNext = level.TryGetNextStage(out _);
            _clearTitle.text = level.IsFinalStage ? "All Clear!" : level.IsLastStageOfWorld ? "World Clear!" : "Clear!";
            _clearNext.EnableInClassList(HiddenClass, !hasNext);
            _clearStages.EnableInClassList(HiddenClass, level.IsTestPlay);
            _clearStagesLabel.text = hasNext ? "Stages" : "Stages (Space)";

            _clearBest.EnableInClassList(HiddenClass, level.IsTestPlay);
            _clearBest.EnableInClassList(BestClass, _gameManager.IsNewBest);
            _clearBest.text = _gameManager.IsNewBest || !level.Progress.TryGetBestTime(level.Layout, out var best)
                ? "New best!"
                : $"Best {FormatTime(best)}";
        }

        private void ShowStages(bool open)
        {
            _stagesOpen = open;
            if (open)
            {
                BuildStageList();
            }

            OnStateChanged(_gameManager.State);
        }

        /// <summary>
        /// One row per world: its name (accent bar, lock when locked) and a card per stage with a
        /// check and best time once cleared. The Continue target is outlined.
        /// </summary>
        private void BuildStageList()
        {
            var level = _gameManager.Level;
            var worlds = level.Worlds;
            var progress = level.Progress;
            var next = progress.ContinueTarget(worlds);
            _stagesList.Clear();
            for (var w = 0; w < worlds.Count; w++)
            {
                var world = worlds[w];
                var unlocked = progress.IsWorldUnlocked(worlds, w);
                var row = new VisualElement();
                row.AddToClassList("stages__row");
                var header = new VisualElement();
                header.AddToClassList("stages__world");
                header.style.borderLeftColor = world.Color;
                var name = new Label(world.Name.ToUpperInvariant());
                name.AddToClassList("stages__world-name");
                header.Add(name);
                if (!unlocked)
                {
                    header.Add(Icon(GameIconKind.Lock, "stages__lock"));
                }

                row.Add(header);
                for (var i = 0; i < world.Stages.Count; i++)
                {
                    row.Add(StageCard(new StageRef(w, i), world.Stages[i], unlocked, next));
                }

                if (world.Stages.Count == 0)
                {
                    var empty = new Label("Coming soon");
                    empty.AddToClassList("stage-card__time");
                    row.Add(empty);
                }

                _stagesList.Add(row);
            }
        }

        private VisualElement StageCard(StageRef stage, MapLayout layout, bool unlocked, StageRef next)
        {
            var progress = _gameManager.Level.Progress;
            var card = new Button(() =>
            {
                if (_gameManager.State == GameState.Title)
                {
                    _gameManager.StartStage(stage);
                }
            });
            card.AddToClassList("stage-card");
            card.SetEnabled(unlocked && layout != null);
            card.EnableInClassList(NextStageClass, unlocked && stage == next);

            var top = new VisualElement();
            top.AddToClassList("stage-card__top");
            var label = new Label(stage.Label);
            label.AddToClassList("stage-card__label");
            top.Add(label);
            if (progress.IsCleared(layout))
            {
                top.Add(Icon(GameIconKind.Check, "stage-card__check"));
            }

            card.Add(top);
            var time = new Label(progress.TryGetBestTime(layout, out var best) ? FormatTime(best) : "—");
            time.AddToClassList("stage-card__time");
            card.Add(time);
            return card;
        }

        private static GameIcon Icon(GameIconKind kind, string className)
        {
            var icon = new GameIcon { Kind = kind, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("icon");
            icon.AddToClassList(className);
            return icon;
        }

        private static string FormatTime(float time)
        {
            var minutes = Mathf.FloorToInt(time / 60f);
            return $"{minutes}:{time - minutes * 60f:00.0}";
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
