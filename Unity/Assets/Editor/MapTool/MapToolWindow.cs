using System.Collections.Generic;
using System.Linq;
using MyGame.Map;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MyGame.EditorTools.MapTool
{
    /// <summary>
    /// Tools/MyGame/Map Tool (plan.md §4.8): manages the level list and paints one layout on a
    /// top-down grid, with live checks, a Scene view preview, and test play.
    /// </summary>
    internal sealed class MapToolWindow : EditorWindow
    {
        private const float CellSize = 52f;
        private const float CellGap = 2f;
        private const float AxisLabelSize = 18f;
        private const float ListWidth = 220f;
        private const string SelectionKey = "FoxIsland.MapTool.Selected";
        private const string PreviewKey = "FoxIsland.MapTool.Preview";
        private const int MaxRandomItems = 32;

        private static readonly Color HoleColor = new(0.16f, 0.36f, 0.58f);
        private static readonly Color[] LevelColors = { new(0.38f, 0.60f, 0.26f), new(0.49f, 0.74f, 0.33f), new(0.64f, 0.86f, 0.46f) };
        private static readonly Color CrumblingColor = new(0.86f, 0.70f, 0.45f);
        private static readonly Color StartColor = new(0.42f, 0.62f, 0.86f);
        private static readonly Color GemColor = new(1f, 0.82f, 0.22f);
        private static readonly Color UnreachableColor = new(0.95f, 0.25f, 0.2f);
        private static readonly Color HardJumpColor = new(1f, 0.6f, 0.1f);

        private static readonly BrushInfo[] Brushes =
        {
            new(Brush.Hole, "Hole", HoleColor),
            new(Brush.Level0, "Level 0", LevelColors[0]),
            new(Brush.Level1, "Level 1", LevelColors[1]),
            new(Brush.Level2, "Level 2", LevelColors[2]),
            new(Brush.Crumbling, "Crumbling", CrumblingColor),
            new(Brush.Start, "Start", StartColor),
            new(Brush.ItemGround, "Item", GemColor),
            new(Brush.ItemAir, "Item (air)", GemColor),
            new(Brush.EraseItem, "Erase item", Color.gray),
        };

        [SerializeField] private Brush _brush = Brush.Level0;

        private LevelList _levels;
        private SerializedObject _levelsObject;
        private ReorderableList _list;
        private MapLayout _layout;
        private TileSpec[,] _tiles;
        private string _parseError;
        private LayoutReport _report;
        private Vector2Int? _highlight;
        private Vector2Int? _lastPainted;
        private int _strokeGroup;
        private bool _preview;
        private string _status;
        private Vector2 _scroll;
        private GUIStyle _cellStyle;
        private GUIStyle _gemStyle;

        private enum Brush
        {
            Hole,
            Level0,
            Level1,
            Level2,
            Crumbling,
            Start,
            ItemGround,
            ItemAir,
            EraseItem,
        }

        [MenuItem("Tools/MyGame/Map Tool", priority = 10)]
        public static void Open() => GetWindow<MapToolWindow>("Map Tool");

        private void OnEnable()
        {
            minSize = new Vector2(760f, 520f);
            _preview = SessionState.GetBool(PreviewKey, false);
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadLevels();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SaveLayout();
            MapToolPreview.Clear();
        }

        private void OnUndoRedo()
        {
            _levelsObject?.Update();
            Reload();
            RefreshPreview(frame: false);
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                RefreshPreview(frame: false);
            }
        }

        private void OnGUI()
        {
            _cellStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 16, normal = { textColor = Color.white } };
            _gemStyle ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.LowerCenter, fontSize = 13, normal = { textColor = GemColor } };

            if (_levels == null)
            {
                EditorGUILayout.HelpBox($"No level list at {MapAssetBuilder.LevelListPath}. Run Tools/MyGame/Build All.", MessageType.Error);
                if (GUILayout.Button("Reload"))
                {
                    LoadLevels();
                }

                return;
            }

            HandleShortcuts();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    DrawLevelList();
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
                {
                    _scroll = scroll.scrollPosition;
                    DrawEditor();
                }
            }
        }

        // ---------- Level list ----------

        private void LoadLevels()
        {
            _levels = AssetDatabase.LoadAssetAtPath<LevelList>(MapAssetBuilder.LevelListPath);
            if (_levels == null)
            {
                return;
            }

            _levelsObject = new SerializedObject(_levels);
            _list = new ReorderableList(_levelsObject, _levelsObject.FindProperty("_levels"), true, true, false, false)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Levels (play order)"),
                drawElementCallback = DrawLevelElement,
                onSelectCallback = list => Select(list.index),
            };

            Select(Mathf.Clamp(SessionState.GetInt(SelectionKey, 0), 0, Mathf.Max(0, _levels.Count - 1)));
        }

        private void DrawLevelElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var level = _list.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue;
            EditorGUI.LabelField(rect, $"{index + 1}.  {(level != null ? level.name : "(missing)")}");
        }

        private void DrawLevelList()
        {
            _levelsObject.Update();
            _list.DoLayoutList();
            if (_levelsObject.ApplyModifiedProperties())
            {
                AssetDatabase.SaveAssetIfDirty(_levels);
                Select(_list.index);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New"))
                {
                    CreateLevel();
                }

                using (new EditorGUI.DisabledScope(_layout == null))
                {
                    if (GUILayout.Button("Duplicate"))
                    {
                        DuplicateLevel();
                    }

                    if (GUILayout.Button("Remove"))
                    {
                        RemoveLevel();
                    }
                }
            }
        }

        private void Select(int index)
        {
            SaveLayout();
            _layout = index >= 0 && index < _levels.Count ? _levels[index] : null;
            _list.index = index;
            SessionState.SetInt(SelectionKey, index);
            _highlight = null;
            _status = null;
            Reload();
            RefreshPreview(frame: true);
            Repaint();
        }

        private void CreateLevel()
        {
            var path = AssetDatabase.GenerateUniqueAssetPath($"{MapAssetBuilder.LevelFolder}/Level{_levels.Count + 1:00}.asset");
            var layout = CreateInstance<MapLayout>();
            layout.SetRows(new[] { "0 0 0 0", "0 0 0 0", "0 0 0 0", "S 0 0 0" });
            AssetDatabase.CreateAsset(layout, path);
            InsertLevel(layout, _levels.Count);
        }

        private void DuplicateLevel()
        {
            var source = AssetDatabase.GetAssetPath(_layout);
            var path = AssetDatabase.GenerateUniqueAssetPath(source);
            if (!AssetDatabase.CopyAsset(source, path))
            {
                return;
            }

            InsertLevel(AssetDatabase.LoadAssetAtPath<MapLayout>(path), _list.index + 1);
        }

        private void InsertLevel(MapLayout layout, int index)
        {
            var levels = _levels.Levels.ToList();
            levels.Insert(Mathf.Clamp(index, 0, levels.Count), layout);
            SetLevels(levels, "Add Level");
            Select(levels.IndexOf(layout));
        }

        private void RemoveLevel()
        {
            if (_levels.Count <= 1)
            {
                EditorUtility.DisplayDialog("Remove Level", "The game needs at least one level.", "OK");
                return;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "Remove Level", $"Remove {_layout.name} from the level list?", "Remove from List", "Cancel", "Remove and Delete Asset");
            if (choice == 1)
            {
                return;
            }

            var removed = _layout;
            var levels = _levels.Levels.ToList();
            var index = levels.IndexOf(removed);
            levels.RemoveAt(index);
            _layout = null;
            SetLevels(levels, "Remove Level");
            if (choice == 2)
            {
                AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(removed));
            }

            Select(Mathf.Min(index, levels.Count - 1));
        }

        private void SetLevels(List<MapLayout> levels, string undoName)
        {
            Undo.RecordObject(_levels, undoName);
            _levels.SetLevels(levels.ToArray());
            EditorUtility.SetDirty(_levels);
            AssetDatabase.SaveAssetIfDirty(_levels);
            _levelsObject.Update();
        }

        // ---------- Layout editor ----------

        private void DrawEditor()
        {
            if (_layout == null)
            {
                EditorGUILayout.HelpBox("Select or create a level.", MessageType.Info);
                return;
            }

            DrawHeader();
            if (_tiles == null)
            {
                EditorGUILayout.HelpBox($"The rows do not parse: {_parseError}", MessageType.Error);
                if (GUILayout.Button("Reset to a blank 4 × 4"))
                {
                    Undo.RecordObject(_layout, "Reset Layout");
                    _layout.SetRows(new[] { "0 0 0 0", "0 0 0 0", "0 0 0 0", "S 0 0 0" });
                    MarkChanged();
                }

                return;
            }

            DrawSizeControls();
            DrawItemControls();
            DrawBrushes();
            DrawGrid();
            DrawIssues();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newName = EditorGUILayout.DelayedTextField("Name", _layout.name);
                if (newName != _layout.name && !string.IsNullOrWhiteSpace(newName))
                {
                    var error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_layout), newName);
                    _status = string.IsNullOrEmpty(error) ? null : error;
                }

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    EditorGUIUtility.PingObject(_layout);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var preview = GUILayout.Toggle(_preview, "3D Preview in Scene View", "Button", GUILayout.Height(24f));
                if (preview != _preview)
                {
                    _preview = preview;
                    SessionState.SetBool(PreviewKey, preview);
                    if (preview)
                    {
                        RefreshPreview(frame: true);
                    }
                    else
                    {
                        MapToolPreview.Clear();
                    }
                }

                using (new EditorGUI.DisabledScope(_report == null || _report.HasErrors || EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button("▶  Play This Level", GUILayout.Height(24f)))
                    {
                        SaveLayout();
                        MapToolPlayMode.Play(_layout);
                    }
                }
            }

            if (_preview && !MapToolPreview.CanShow(out var reason))
            {
                EditorGUILayout.HelpBox(reason, MessageType.None);
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void DrawSizeControls()
        {
            var width = _tiles.GetLength(0);
            var depth = _tiles.GetLength(1);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Size", GUILayout.Width(40f));
                var newWidth = Mathf.Clamp(EditorGUILayout.DelayedIntField(width, GUILayout.Width(36f)), MapLayout.MinSize, MapLayout.MaxSize);
                GUILayout.Label("×", GUILayout.Width(12f));
                var newDepth = Mathf.Clamp(EditorGUILayout.DelayedIntField(depth, GUILayout.Width(36f)), MapLayout.MinSize, MapLayout.MaxSize);
                if (newWidth != width || newDepth != depth)
                {
                    Resize(newWidth, newDepth);
                }

                GUILayout.Space(16f);
                EditorGUILayout.LabelField("Shift", GUILayout.Width(36f));
                if (GUILayout.Button("◀", GUILayout.Width(28f)))
                {
                    Shift(Vector2Int.left);
                }

                if (GUILayout.Button("▶", GUILayout.Width(28f)))
                {
                    Shift(Vector2Int.right);
                }

                if (GUILayout.Button("▲", GUILayout.Width(28f)))
                {
                    Shift(Vector2Int.up);
                }

                if (GUILayout.Button("▼", GUILayout.Width(28f)))
                {
                    Shift(Vector2Int.down);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawItemControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var mode = (ItemMode)EditorGUILayout.EnumPopup("Items", _layout.ItemMode);
                if (mode != _layout.ItemMode)
                {
                    Undo.RecordObject(_layout, "Change Item Mode");
                    _layout.ItemMode = mode;
                    MarkChanged();
                }

                if (_layout.ItemMode == ItemMode.Random)
                {
                    var count = EditorGUILayout.IntSlider(_layout.RandomItemCount, 0, MaxRandomItems);
                    if (count != _layout.RandomItemCount)
                    {
                        Undo.RecordObject(_layout, "Change Item Count");
                        _layout.RandomItemCount = count;
                        MarkChanged();
                    }
                }
                else
                {
                    GUILayout.Label($"{_layout.Items.Count} placed", GUILayout.Width(80f));
                    if (GUILayout.Button("Clear Items", GUILayout.Width(90f)))
                    {
                        Undo.RecordObject(_layout, "Clear Items");
                        _layout.SetItems(System.Array.Empty<ItemPlacement>());
                        MarkChanged();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawBrushes()
        {
            EditorGUILayout.LabelField("Brush (keys 1–9; drag to paint)", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = 0; i < Brushes.Length; i++)
                {
                    var brush = Brushes[i];
                    var rect = GUILayoutUtility.GetRect(new GUIContent(brush.Label), "Button", GUILayout.Height(30f), GUILayout.MinWidth(64f));
                    if (GUI.Toggle(rect, _brush == brush.Brush, $"{i + 1}  {brush.Label}", "Button"))
                    {
                        _brush = brush.Brush;
                    }

                    EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.yMax - 5f, rect.width - 6f, 3f), brush.Color);
                }
            }
        }

        private void DrawGrid()
        {
            var width = _tiles.GetLength(0);
            var depth = _tiles.GetLength(1);
            GUILayout.Space(8f);
            var area = GUILayoutUtility.GetRect(AxisLabelSize + width * CellSize, AxisLabelSize * 2f + depth * CellSize);
            var origin = new Vector2(area.x + AxisLabelSize, area.y);
            var items = new Dictionary<Vector2Int, bool>();
            foreach (var item in _layout.Items)
            {
                items[item.Cell] = item.InAir;
            }

            var showItems = _layout.ItemMode == ItemMode.Fixed;
            var cells = _report != null && _report.Cells.GetLength(0) == width && _report.Cells.GetLength(1) == depth ? _report.Cells : null;

            for (var row = 0; row < depth; row++)
            {
                GUI.Label(new Rect(area.x, origin.y + (depth - 1 - row) * CellSize, AxisLabelSize, CellSize), row.ToString(), EditorStyles.centeredGreyMiniLabel);
                for (var column = 0; column < width; column++)
                {
                    var rect = CellRect(origin, depth, column, row);
                    var spec = _tiles[column, row];
                    EditorGUI.DrawRect(rect, ColorOf(spec));
                    GUI.Label(rect, LabelOf(spec), _cellStyle);
                    var cell = new Vector2Int(column, row);
                    if (showItems && items.TryGetValue(cell, out var inAir))
                    {
                        GUI.Label(rect, inAir ? "◆ ↑" : "◆", _gemStyle);
                    }

                    if (cells != null)
                    {
                        switch (cells[column, row])
                        {
                            case Reachability.Unreachable:
                                DrawOutline(rect, UnreachableColor, 3f);
                                break;
                            case Reachability.HardJump:
                                DrawOutline(rect, HardJumpColor, 3f);
                                break;
                        }
                    }

                    if (_highlight == cell)
                    {
                        DrawOutline(rect, Color.white, 2f);
                    }
                }
            }

            var bottom = origin.y + depth * CellSize;
            for (var column = 0; column < width; column++)
            {
                GUI.Label(new Rect(origin.x + column * CellSize, bottom, CellSize, AxisLabelSize), column.ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            GUI.Label(new Rect(area.x, bottom + AxisLabelSize, width * CellSize + AxisLabelSize, AxisLabelSize),
                "◣ camera side: row 0 is nearest, column 0 on the left", EditorStyles.miniLabel);
            HandlePainting(origin, width, depth);
        }

        private void DrawIssues()
        {
            GUILayout.Space(8f);
            EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Reachability is approximate: it follows the jump rules in plan.md §4.5.", EditorStyles.miniLabel);
            if (_report == null || _report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
                return;
            }

            foreach (var issue in _report.Issues)
            {
                var icon = EditorGUIUtility.IconContent(issue.Severity == IssueSeverity.Error ? "console.erroricon.sml" : "console.warnicon.sml");
                var content = new GUIContent(issue.Message, icon.image);
                if (GUILayout.Button(content, EditorStyles.label))
                {
                    _highlight = issue.Cell;
                }
            }
        }

        // ---------- Painting ----------

        private void HandlePainting(Vector2 origin, int width, int depth)
        {
            var current = Event.current;
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var gridRect = new Rect(origin.x, origin.y, width * CellSize, depth * CellSize);
            switch (current.GetTypeForControl(id))
            {
                case EventType.MouseDown when current.button == 0 && gridRect.Contains(current.mousePosition):
                    GUIUtility.hotControl = id;
                    Undo.IncrementCurrentGroup();
                    _strokeGroup = Undo.GetCurrentGroup();
                    _lastPainted = null;
                    PaintAt(CellAt(origin, depth, current.mousePosition));
                    current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    var cell = CellAt(origin, depth, current.mousePosition);
                    if (cell != _lastPainted)
                    {
                        PaintAt(cell);
                    }

                    current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    Undo.CollapseUndoOperations(_strokeGroup);
                    SaveLayout();
                    current.Use();
                    break;
            }
        }

        private void PaintAt(Vector2Int cell)
        {
            _lastPainted = cell;
            if (cell.x < 0 || cell.y < 0 || cell.x >= _tiles.GetLength(0) || cell.y >= _tiles.GetLength(1))
            {
                return;
            }

            _status = null;
            switch (_brush)
            {
                case Brush.ItemGround:
                case Brush.ItemAir:
                    PlaceItem(cell, _brush == Brush.ItemAir);
                    break;
                case Brush.EraseItem:
                    if (_layout.Items.Any(item => item.Cell == cell))
                    {
                        Undo.RecordObject(_layout, "Erase Item");
                        _layout.SetItems(_layout.Items.Where(item => item.Cell != cell).ToArray());
                        MarkChanged();
                    }

                    break;
                default:
                    PaintTile(cell, SpecOf(_brush));
                    break;
            }
        }

        private void PaintTile(Vector2Int cell, TileSpec spec)
        {
            if (_tiles[cell.x, cell.y].Equals(spec))
            {
                return;
            }

            Undo.RecordObject(_layout, "Paint Tiles");
            if (spec.Type == TileType.Start)
            {
                for (var column = 0; column < _tiles.GetLength(0); column++)
                {
                    for (var row = 0; row < _tiles.GetLength(1); row++)
                    {
                        if (_tiles[column, row].Type == TileType.Start)
                        {
                            _tiles[column, row] = new TileSpec(TileType.Solid, 0);
                        }
                    }
                }
            }

            _tiles[cell.x, cell.y] = spec;
            _layout.SetTiles(_tiles);
            if (spec.Type != TileType.Solid && _layout.Items.Any(item => item.Cell == cell))
            {
                _layout.SetItems(_layout.Items.Where(item => item.Cell != cell).ToArray());
            }

            MarkChanged();
        }

        private void PlaceItem(Vector2Int cell, bool inAir)
        {
            if (_tiles[cell.x, cell.y].Type != TileType.Solid)
            {
                _status = "Items go on solid tiles (levels 0–2).";
                return;
            }

            var placement = new ItemPlacement(cell, inAir);
            if (_layout.ItemMode == ItemMode.Fixed && _layout.Items.Contains(placement))
            {
                return;
            }

            Undo.RecordObject(_layout, "Place Item");
            if (_layout.ItemMode != ItemMode.Fixed)
            {
                _layout.ItemMode = ItemMode.Fixed;
                _status = "Switched the items to Fixed.";
            }

            _layout.SetItems(_layout.Items.Where(item => item.Cell != cell).Append(placement).ToArray());
            MarkChanged();
        }

        /// <summary>Grows or crops from the (0, 0) corner; new cells are level 0.</summary>
        private void Resize(int width, int depth)
        {
            var resized = new TileSpec[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    var inside = column < _tiles.GetLength(0) && row < _tiles.GetLength(1);
                    resized[column, row] = inside ? _tiles[column, row] : new TileSpec(TileType.Solid, 0);
                }
            }

            Undo.RecordObject(_layout, "Resize Layout");
            _layout.SetTiles(resized);
            _layout.SetItems(_layout.Items.Where(item => item.Cell.x < width && item.Cell.y < depth).ToArray());
            MarkChanged(frame: true);
        }

        /// <summary>Moves everything one cell, wrapping around, so nothing is lost.</summary>
        private void Shift(Vector2Int direction)
        {
            var width = _tiles.GetLength(0);
            var depth = _tiles.GetLength(1);
            Vector2Int Wrap(Vector2Int cell) => new((cell.x + direction.x + width) % width, (cell.y + direction.y + depth) % depth);

            var shifted = new TileSpec[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    var to = Wrap(new Vector2Int(column, row));
                    shifted[to.x, to.y] = _tiles[column, row];
                }
            }

            Undo.RecordObject(_layout, "Shift Layout");
            _layout.SetTiles(shifted);
            _layout.SetItems(_layout.Items.Select(item => new ItemPlacement(Wrap(item.Cell), item.InAir)).ToArray());
            MarkChanged();
            SaveLayout();
        }

        private void HandleShortcuts()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }

            var index = current.keyCode - KeyCode.Alpha1;
            if (index >= 0 && index < Brushes.Length)
            {
                _brush = Brushes[index].Brush;
                current.Use();
            }
        }

        // ---------- State ----------

        private void MarkChanged(bool frame = false)
        {
            EditorUtility.SetDirty(_layout);
            Reload();
            RefreshPreview(frame);
            Repaint();
        }

        private void Reload()
        {
            _report = null;
            _tiles = null;
            if (_layout == null)
            {
                return;
            }

            if (_layout.TryGetTiles(out var tiles, out var error))
            {
                _tiles = tiles;
                _parseError = null;
            }
            else
            {
                _parseError = error;
            }

            _report = MapLayoutValidator.Validate(_layout);
        }

        private void SaveLayout()
        {
            if (_layout != null)
            {
                AssetDatabase.SaveAssetIfDirty(_layout);
            }
        }

        private void RefreshPreview(bool frame)
        {
            if (!_preview || _layout == null || _tiles == null)
            {
                MapToolPreview.Clear();
                return;
            }

            MapToolPreview.Show(_layout, frame);
        }

        // ---------- Helpers ----------

        private static Rect CellRect(Vector2 origin, int depth, int column, int row) =>
            new(origin.x + column * CellSize, origin.y + (depth - 1 - row) * CellSize, CellSize - CellGap, CellSize - CellGap);

        private static Vector2Int CellAt(Vector2 origin, int depth, Vector2 position) =>
            new(Mathf.FloorToInt((position.x - origin.x) / CellSize), depth - 1 - Mathf.FloorToInt((position.y - origin.y) / CellSize));

        private static void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static Color ColorOf(TileSpec spec) => spec.Type switch
        {
            TileType.Hole => HoleColor,
            TileType.Crumbling => CrumblingColor,
            TileType.Start => StartColor,
            _ => LevelColors[Mathf.Clamp(spec.Level, 0, LevelColors.Length - 1)],
        };

        private static string LabelOf(TileSpec spec) => spec.Type switch
        {
            TileType.Hole => string.Empty,
            TileType.Crumbling => "C",
            TileType.Start => "S",
            _ => spec.Level.ToString(),
        };

        private static TileSpec SpecOf(Brush brush) => brush switch
        {
            Brush.Hole => new TileSpec(TileType.Hole, 0),
            Brush.Level1 => new TileSpec(TileType.Solid, 1),
            Brush.Level2 => new TileSpec(TileType.Solid, 2),
            Brush.Crumbling => new TileSpec(TileType.Crumbling, 0),
            Brush.Start => new TileSpec(TileType.Start, 0),
            _ => new TileSpec(TileType.Solid, 0),
        };

        private readonly struct BrushInfo
        {
            public BrushInfo(Brush brush, string label, Color color)
            {
                Brush = brush;
                Label = label;
                Color = color;
            }

            public Brush Brush { get; }

            public string Label { get; }

            public Color Color { get; }
        }
    }
}
