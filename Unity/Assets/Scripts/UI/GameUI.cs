using MyGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    /// <summary>
    /// Drives the HUD and the panels in <c>GameUI.uxml</c> from <see cref="GameManager"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private GameManager _gameManager;

        private Label _hudItems;
        private VisualElement _gameOverPanel;
        private Button _gameOverRestartButton;
        private VisualElement _clearPanel;
        private Label _clearTime;
        private Button _clearRestartButton;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _hudItems = root.Q<Label>("hud-items");
            _gameOverPanel = root.Q<VisualElement>("game-over-panel");
            _gameOverRestartButton = root.Q<Button>("game-over-restart");
            _clearPanel = root.Q<VisualElement>("clear-panel");
            _clearTime = root.Q<Label>("clear-time");
            _clearRestartButton = root.Q<Button>("clear-restart");

            _gameOverRestartButton.clicked += _gameManager.Restart;
            _clearRestartButton.clicked += _gameManager.Restart;
            _gameManager.StateChanged += OnStateChanged;
            _gameManager.ItemCountChanged += OnItemCountChanged;
            OnStateChanged(_gameManager.State);
            OnItemCountChanged();
        }

        private void OnDisable()
        {
            _gameOverRestartButton.clicked -= _gameManager.Restart;
            _clearRestartButton.clicked -= _gameManager.Restart;
            _gameManager.StateChanged -= OnStateChanged;
            _gameManager.ItemCountChanged -= OnItemCountChanged;
        }

        private void OnStateChanged(GameState state)
        {
            _gameOverPanel.EnableInClassList(HiddenClass, state != GameState.GameOver);
            _clearPanel.EnableInClassList(HiddenClass, state != GameState.Cleared);
            if (state == GameState.Cleared)
            {
                _clearTime.text = $"Time {_gameManager.ElapsedTime:0.0} s";
            }
        }

        private void OnItemCountChanged() =>
            _hudItems.text = $"{_gameManager.CollectedCount} / {_gameManager.TotalCount}";
    }
}
