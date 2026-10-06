using MyGame.CameraRig;
using MyGame.Map;
using MyGame.Player;
using UnityEngine;

namespace MyGame.Core
{
    /// <summary>
    /// Picks the level for this scene load (plan.md §8) and fits the scene to it: builds the map,
    /// puts the player on the start tile, frames the camera, and centers the lake. Runs before
    /// everything that reads the tiles.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LevelLoader : MonoBehaviour
    {
        private const float SpawnClearance = 0.05f;
        private const float SpawnYaw = 45f;

        [SerializeField] private LevelList _levels;
        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private PlayerController _player;
        [SerializeField] private QuarterViewCamera _camera;
        [SerializeField] private WaterSurface _water;

        private int _levelIndex;

        public MapLayout Layout { get; private set; }

        public bool IsTestPlay { get; private set; }

        /// <summary>One-based, for display.</summary>
        public int LevelNumber => _levelIndex + 1;

        public int LevelCount => _levels.Count;

        public bool HasNextLevel => !IsTestPlay && _levelIndex + 1 < LevelCount;

        private void Awake()
        {
            var testLayout = LevelSession.TestLayout;
            IsTestPlay = testLayout != null;
            if (IsTestPlay)
            {
                Layout = testLayout;
            }
            else
            {
                _levelIndex = Mathf.Clamp(LevelSession.LevelIndex, 0, LevelCount - 1);
                LevelSession.LevelIndex = _levelIndex;
                Layout = _levels[_levelIndex];
            }

            _mapBuilder.Build(Layout);
            PlacePlayer();

            var bounds = MapMetrics.FramedBounds(Layout.Width, Layout.Depth, _player.JumpHeight);
            _camera.FramedBounds = bounds;
            _camera.Apply();
            _water.transform.position = new Vector3(bounds.center.x, MapMetrics.WaterLevel, bounds.center.z);
        }

        private void PlacePlayer()
        {
            if (!Layout.TryGetStart(out var start))
            {
                Debug.LogError($"[LevelLoader] {Layout.name} has no start tile; spawning at (0, 0).", Layout);
            }

            var position = MapMetrics.TopCenter(start, 0) + Vector3.up * SpawnClearance;
            var hasController = _player.TryGetComponent<CharacterController>(out var controller);
            if (hasController)
            {
                controller.enabled = false;
            }

            _player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, SpawnYaw, 0f));
            if (hasController)
            {
                controller.enabled = true;
            }
        }
    }
}
