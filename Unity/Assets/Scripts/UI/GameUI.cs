using MyGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.UI
{
    /// <summary>
    /// Shows and hides the panels in <c>GameUI.uxml</c> according to <see cref="GameManager.State"/>.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameUI : MonoBehaviour
    {
        private const string HiddenClass = "hidden";

        [SerializeField] private GameManager _gameManager;

        private VisualElement _gameOverPanel;
        private Button _gameOverRestartButton;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _gameOverPanel = root.Q<VisualElement>("game-over-panel");
            _gameOverRestartButton = root.Q<Button>("game-over-restart");

            _gameOverRestartButton.clicked += _gameManager.Restart;
            _gameManager.StateChanged += OnStateChanged;
            OnStateChanged(_gameManager.State);
        }

        private void OnDisable()
        {
            _gameOverRestartButton.clicked -= _gameManager.Restart;
            _gameManager.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(GameState state) =>
            _gameOverPanel.EnableInClassList(HiddenClass, state != GameState.GameOver);
    }
}
