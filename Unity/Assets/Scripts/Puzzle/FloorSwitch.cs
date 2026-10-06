using MyGame.Map;
using UnityEngine;

namespace MyGame.Puzzle
{
    /// <summary>
    /// A pressure plate (plan.md §4.10): pressed while the fox stands on it or a crate rests on its
    /// cell, which turns its group on. It has no collider; the fox stands on the block and the
    /// button sinks flush under it. <see cref="PuzzleBoard"/> calls <see cref="Refresh"/> each frame.
    /// </summary>
    public class FloorSwitch : MonoBehaviour
    {
        private const float SenseHeight = 0.6f;

        [SerializeField] private Transform _button;
        [SerializeField] private float _pressDepth = 0.05f;
        [SerializeField] private float _pressSpeed = 0.5f;

        private PuzzleBoard _board;
        private Vector3 _buttonRest;
        private float _press;

        public Vector2Int Cell { get; private set; }

        public PuzzleGroup Group { get; private set; }

        public bool IsPressed { get; private set; }

        public void Initialize(PuzzleBoard board, SwitchPlacement placement, Tile tile, Material material)
        {
            _board = board;
            Cell = placement.Cell;
            Group = placement.Group;
            transform.position = tile.transform.position + Vector3.up * tile.TopY;
            _buttonRest = _button.localPosition;
            if (material != null)
            {
                _button.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        public bool Refresh()
        {
            var center = transform.position + Vector3.up * (SenseHeight * 0.5f);
            var halfExtents = new Vector3(MapMetrics.TileSize * 0.33f, SenseHeight * 0.5f, MapMetrics.TileSize * 0.33f);
            IsPressed = _board.HasSettledCrate(Cell) || PuzzleBoard.OverlapsCharacter(center, halfExtents);
            _press = Mathf.MoveTowards(_press, IsPressed ? 1f : 0f, _pressSpeed / _pressDepth * Time.deltaTime);
            _button.localPosition = _buttonRest + Vector3.down * (_press * _pressDepth);
            return IsPressed;
        }
    }
}
