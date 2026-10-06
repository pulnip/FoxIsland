using System.Collections;
using MyGame.Map;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// A one-level crate (plan.md §4.10). Pushing against a side for <see cref="_pushDelay"/> slides
    /// it one cell, unless the floor there is higher or taken. It drops to a lower floor, keeps
    /// sliding over ice, and fills a hole it falls into, becoming a height-0 floor for good. It rests
    /// on whatever block is under it, rising blocks included, and holds switches down.
    /// The pivot is the bottom center.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    [RequireComponent(typeof(BoxCollider))]
    public class Crate : MonoBehaviour
    {
        public const float Height = MapMetrics.LevelHeight;

        [SerializeField] private float _pushDelay = 0.2f;
        [SerializeField] private float _slideTime = 0.25f;
        [SerializeField] private float _iceSlideTime = 0.18f;
        [SerializeField] private float _gravity = 20f;

        private PuzzleBoard _board;
        private Tile _floor;
        private Vector2Int _pushDirection;
        private float _pushTime;
        private int _lastPushFrame = -10;

        public Vector2Int Cell { get; private set; }

        /// <summary>Resting, not sliding or falling.</summary>
        public bool IsSettled { get; private set; } = true;

        /// <summary>Fell into a hole and now fills it.</summary>
        public bool IsSunk { get; private set; }

        public float Bottom => transform.position.y;

        public void Initialize(PuzzleBoard board, Vector2Int cell, Tile floor)
        {
            _board = board;
            Cell = cell;
            _floor = floor;
            transform.position = MapMetrics.TopCenter(cell, 0) + Vector3.up * FloorTop();
        }

        /// <summary>The fox pushes this frame toward <paramref name="direction"/> (one grid axis).</summary>
        public void Push(Vector2Int direction)
        {
            if (!IsSettled || IsSunk || _lastPushFrame == Time.frameCount)
            {
                return;
            }

            if (_lastPushFrame < Time.frameCount - 1 || direction != _pushDirection)
            {
                _pushTime = 0f;
                _pushDirection = direction;
            }

            _lastPushFrame = Time.frameCount;
            _pushTime += Time.deltaTime;
            if (_pushTime >= _pushDelay)
            {
                _pushTime = 0f;
                TrySlide(direction, _slideTime);
            }
        }

        private void Update()
        {
            if (IsSettled && !IsSunk && _floor != null)
            {
                var position = transform.position;
                transform.position = new Vector3(position.x, FloorTop(), position.z);
            }
        }

        private float FloorTop() => _floor != null ? _floor.transform.position.y + _floor.TopY : PuzzleBoard.FilledHoleTop;

        private bool TrySlide(Vector2Int direction, float duration)
        {
            var target = Cell + direction;
            if (!_board.CanEnter(target, Bottom, out var landing, out var floor, out var fillsHole))
            {
                return false;
            }

            _board.MoveCrate(this, Cell, target);
            StartCoroutine(Slide(direction, target, landing, floor, fillsHole, duration));
            return true;
        }

        private IEnumerator Slide(Vector2Int direction, Vector2Int target, float landing, Tile floor, bool fillsHole, float duration)
        {
            IsSettled = false;
            var start = transform.position;
            var end = MapMetrics.TopCenter(target, 0) + Vector3.up * start.y;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            transform.position = end;
            Cell = target;
            _floor = floor;

            var speed = 0f;
            var splashed = false;
            while (transform.position.y > landing)
            {
                speed += _gravity * Time.deltaTime;
                var y = Mathf.Max(landing, transform.position.y - speed * Time.deltaTime);
                transform.position = new Vector3(end.x, y, end.z);
                if (!splashed && fillsHole && y < MapMetrics.WaterLevel && _board.SplashEffect != null)
                {
                    splashed = true;
                    Instantiate(_board.SplashEffect, new Vector3(end.x, MapMetrics.WaterLevel, end.z), Quaternion.identity);
                }

                yield return null;
            }

            if (fillsHole)
            {
                IsSunk = true;
                _board.FillHole(target);
            }

            IsSettled = true;
            if (!fillsHole && floor != null && floor.Spec.Skin == Skin.Ice)
            {
                TrySlide(direction, _iceSlideTime);
            }
        }
    }
}
