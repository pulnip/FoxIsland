using System;
using MyGame.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MyGame.Core
{
    /// <summary>
    /// Owns the game state (plan.md §8): detects falling, gates player input, and restarts the scene.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private float _killPlaneY = -5f;

        private InputAction _restartAction;

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Playing;

        private void OnEnable()
        {
            _restartAction = InputSystem.actions.FindAction("Player/Restart", throwIfNotFound: true);
            _restartAction.Enable();
        }

        private void Update()
        {
            if (_restartAction.WasPressedThisFrame())
            {
                Restart();
                return;
            }

            if (State == GameState.Playing && _player.transform.position.y < _killPlaneY)
            {
                SetState(GameState.GameOver);
            }
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void SetState(GameState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            _player.InputEnabled = state == GameState.Playing;
            StateChanged?.Invoke(state);
        }
    }
}
