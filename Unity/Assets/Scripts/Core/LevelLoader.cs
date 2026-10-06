using MyGame.CameraRig;
using MyGame.Map;
using MyGame.Player;
using UnityEngine;

namespace MyGame.Core
{
    /// <summary>
    /// Picks the stage for this scene load (plan.md §8) and fits the scene to it: builds the map,
    /// puts the player on the start cell, frames the camera, and centers the lake. Runs before
    /// everything that reads the tiles. At the title it loads the Continue target, so Continue
    /// simply starts playing what is already on screen.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LevelLoader : MonoBehaviour
    {
        private const float SpawnClearance = 0.05f;
        private const float SpawnYaw = 45f;

        [SerializeField] private WorldList _worlds;
        [SerializeField] private MapBuilder _mapBuilder;
        [SerializeField] private PlayerController _player;
        [SerializeField] private QuarterViewCamera _camera;
        [SerializeField] private WaterSurface _water;

        public MapLayout Layout { get; private set; }

        public WorldList Worlds => _worlds;

        public PlayerProgress Progress { get; private set; }

        public StageRef Stage { get; private set; }

        public bool IsTestPlay { get; private set; }

        /// <summary>The world name and stage label, e.g. <c>Beach 1-3</c>.</summary>
        public string StageTitle => IsTestPlay ? "Test Play" : $"{_worlds[Stage.World].Name} {Stage.Label}";

        /// <summary>No stage follows this one.</summary>
        public bool IsFinalStage => !IsTestPlay && !_worlds.TryGetNext(Stage, out _);

        /// <summary>This is its world's last stage.</summary>
        public bool IsLastStageOfWorld => !IsTestPlay && Stage.Index == _worlds[Stage.World].Stages.Count - 1;

        private void Awake()
        {
            Progress = PlayerProgress.Load();
            var testLayout = LevelSession.TestLayout;
            IsTestPlay = testLayout != null;
            if (IsTestPlay)
            {
                Layout = testLayout;
            }
            else
            {
                var stage = LevelSession.OpenAtTitle ? Progress.ContinueTarget(_worlds) : LevelSession.Stage;
                if (!_worlds.TryGetStage(stage, out var layout))
                {
                    stage = Progress.ContinueTarget(_worlds);
                    _worlds.TryGetStage(stage, out layout);
                }

                Stage = stage;
                LevelSession.Stage = stage;
                Layout = layout;
            }

            if (Layout == null)
            {
                Debug.LogError("[LevelLoader] The world list has no stages.", this);
                return;
            }

            _mapBuilder.Build(Layout);
            PlacePlayer();

            var bounds = MapMetrics.FramedBounds(Layout.Width, Layout.Depth, Layout.MaxHeight, _player.JumpHeight);
            _camera.FramedBounds = bounds;
            _camera.Apply();
            _water.transform.position = new Vector3(bounds.center.x, MapMetrics.WaterLevel, bounds.center.z);
        }

        /// <summary>The next stage, if there is one and its world is unlocked (ask after recording a clear).</summary>
        public bool TryGetNextStage(out StageRef next) =>
            _worlds.TryGetNext(Stage, out next) && !IsTestPlay && Progress.IsUnlocked(_worlds, next);

        private void PlacePlayer()
        {
            var height = 0;
            if (Layout.TryGetStart(out var start))
            {
                height = Layout.GetCell(start).Height;
            }
            else
            {
                Debug.LogError($"[LevelLoader] {Layout.name}: the start {start} is not on a block.", Layout);
            }

            var position = MapMetrics.TopCenter(start, height) + Vector3.up * SpawnClearance;
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
