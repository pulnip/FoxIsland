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
    /// top-down grid, with live checks, a Scene view preview, and test play. Layouts are edited as
    /// in-memory drafts; nothing reaches the asset until Save (unsaved levels show a *).
    /// </summary>
    internal sealed class MapToolWindow : EditorWindow
    {
        private const float CellSize = 52f;
        private const float CellGap = 2f;
        private const float AxisLabelSize = 18f;
        private const float ListWidth = 220f;
        private const string SelectionKey = "FoxIsland.MapTool.Selected";
        private const string PreviewKey = "FoxIsland.MapTool.Preview";
        private const string DraftListKey = "FoxIsland.MapTool.Drafts";
        private const string DraftKeyPrefix = "FoxIsland.MapTool.Draft.";
        private const int MaxRandomItems = 32;

        private static readonly Color HoleColor = new(0.16f, 0.36f, 0.58f);
        private static readonly Color[] LevelColors =
        {
            new(0.38f, 0.60f, 0.26f), new(0.49f, 0.74f, 0.33f), new(0.64f, 0.86f, 0.46f),
            new(0.20f, 0.40f, 0.32f), new(0.30f, 0.48f, 0.40f),
            new(0.84f, 0.88f, 0.94f), new(0.97f, 0.98f, 1f),
        };

        private static readonly Color IceColor = new(0.58f, 0.84f, 0.95f);
        private static readonly Color DarkText = new(0.15f, 0.2f, 0.28f);
        private static readonly Color CrumblingColor = new(0.86f, 0.70f, 0.45f);
        private static readonly Color StartColor = new(0.42f, 0.62f, 0.86f);
        private static readonly Color GemColor = new(1f, 0.82f, 0.22f);
        private static readonly Color UnreachableColor = new(0.95f, 0.25f, 0.2f);
        private static readonly Color HardJumpColor = new(1f, 0.6f, 0.1f);

        private static readonly BrushInfo[] Brushes =
        {
            new(Brush.Hole, "Hole", KeyCode.H, HoleColor),
            new(Brush.Level0, "0", KeyCode.Alpha0, LevelColors[0]),
            new(Brush.Level1, "1", KeyCode.Alpha1, LevelColors[1]),
            new(Brush.Level2, "2", KeyCode.Alpha2, LevelColors[2]),
            new(Brush.Level3, "3", KeyCode.Alpha3, LevelColors[3]),
            new(Brush.Level4, "4", KeyCode.Alpha4, LevelColors[4]),
            new(Brush.Level5, "5", KeyCode.Alpha5, LevelColors[5]),
            new(Brush.Level6, "6", KeyCode.Alpha6, LevelColors[6]),
            new(Brush.Ice, "Ice", KeyCode.I, IceColor),
            new(Brush.Crumbling, "Crumbling", KeyCode.C, CrumblingColor),
            new(Brush.Start, "Start", KeyCode.S, StartColor),
            new(Brush.ItemGround, "Item", KeyCode.G, GemColor),
            new(Brush.ItemAir, "Item (air)", KeyCode.A, GemColor),
            new(Brush.EraseItem, "Erase item", KeyCode.E, Color.gray),
        };

        [SerializeField] private Brush _brush = Brush.Level0;

        private readonly Dictionary<MapLayout, MapLayout> _drafts = new();
        private readonly Dictionary<MapLayout, string> _savedJson = new();
        private readonly HashSet<MapLayout> _modified = new();
        private LevelList _levels;
        private SerializedObject _levelsObject;
        private ReorderableList _list;
        private MapLayout _layout;
        private MapLayout _draft;
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
            Level3,
            Level4,
            Level5,
            Level6,
            Ice,
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
            saveChangesMessage = "Some levels in the Map Tool have unsaved changes.";
            _preview = SessionState.GetBool(PreviewKey, false);
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            LoadLevels();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            StashDrafts();
            MapToolPreview.Clear();
        }

        /// <summary>The window closed (after Unity asked about unsaved changes): forget the drafts.</summary>
        private void OnDestroy()
        {
            EraseStash();
            foreach (var draft in _drafts.Values)
            {
                if (draft != null)
                {
                    DestroyImmediate(draft);
                }
            }

            _drafts.Clear();
        }

        /// <summary>Clean drafts follow their asset in case it changed outside the tool (git, inspector).</summary>
        private void OnFocus()
        {
            foreach (var asset in _drafts.Keys.ToList())
            {
                if (asset == null)
                {
                    _drafts.Remove(asset);
                    continue;
                }

                if (!_modified.Contains(asset) && _drafts[asset] != null)
                {
                    _drafts[asset].CopyFrom(asset);
                    _savedJson[asset] = SavedJsonOf(asset);
                }
            }

            UpdateUnsavedState();
            Reload();
            RefreshPreview(frame: false);
        }

        public override void SaveChanges()
        {
            foreach (var asset in _modified.ToList())
            {
                Save(asset);
            }

            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            foreach (var asset in _modified.ToList())
            {
                Discard(asset);
            }

            base.DiscardChanges();
        }

        private void OnUndoRedo()
        {
            _levelsObject?.Update();
            UpdateUnsavedState();
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

            RestoreDrafts();
            Select(Mathf.Clamp(SessionState.GetInt(SelectionKey, 0), 0, Mathf.Max(0, _levels.Count - 1)));
        }

        private void DrawLevelElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var level = _list.serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as MapLayout;
            var label = level != null ? level.name + (_modified.Contains(level) ? " *" : string.Empty) : "(missing)";
            EditorGUI.LabelField(rect, $"{index + 1}.  {label}");
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
            _layout = index >= 0 && index < _levels.Count ? _levels[index] : null;
            _draft = GetDraft(_layout);
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

            // Duplicate what is on screen, unsaved edits included.
            var copy = AssetDatabase.LoadAssetAtPath<MapLayout>(path);
            if (_modified.Contains(_layout))
            {
                copy.CopyFrom(_draft);
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
            }

            InsertLevel(copy, _list.index + 1);
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
            ForgetDraft(removed);
            _layout = null;
            _draft = null;
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
                    Undo.RecordObject(_draft, "Reset Layout");
                    _draft.SetRows(new[] { "0 0 0 0", "0 0 0 0", "0 0 0 0", "S 0 0 0" });
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
                var modified = _modified.Contains(_layout);
                var newName = EditorGUILayout.DelayedTextField(modified ? "Name *" : "Name", _layout.name);
                if (newName != _layout.name && !string.IsNullOrWhiteSpace(newName))
                {
                    var error = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_layout), newName);
                    _status = string.IsNullOrEmpty(error) ? null : error;
                    _draft.name = _layout.name;
                    _savedJson[_layout] = SavedJsonOf(_layout);
                    UpdateUnsavedState();
                }

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                {
                    EditorGUIUtility.PingObject(_layout);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var modified = _modified.Contains(_layout);
                GUILayout.Label(modified ? "Unsaved changes" : "Saved", modified ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(120f));
                using (new EditorGUI.DisabledScope(!modified))
                {
                    if (GUILayout.Button("Save (Ctrl+S)", GUILayout.Height(22f)))
                    {
                        Save(_layout);
                    }

                    if (GUILayout.Button("Discard Changes", GUILayout.Height(22f))
                        && EditorUtility.DisplayDialog("Discard Changes", $"Throw away the unsaved changes to {_layout.name}?", "Discard", "Cancel"))
                    {
                        Discard(_layout);
                    }
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
                        MapToolPlayMode.Play(_draft);
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
                var mode = (ItemMode)EditorGUILayout.EnumPopup("Items", _draft.ItemMode);
                if (mode != _draft.ItemMode)
                {
                    Undo.RecordObject(_draft, "Change Item Mode");
                    _draft.ItemMode = mode;
                    MarkChanged();
                }

                if (_draft.ItemMode == ItemMode.Random)
                {
                    var count = EditorGUILayout.IntSlider(_draft.RandomItemCount, 0, MaxRandomItems);
                    if (count != _draft.RandomItemCount)
                    {
                        Undo.RecordObject(_draft, "Change Item Count");
                        _draft.RandomItemCount = count;
                        MarkChanged();
                    }
                }
                else
                {
                    GUILayout.Label($"{_draft.Items.Count} placed", GUILayout.Width(80f));
                    if (GUILayout.Button("Clear Items", GUILayout.Width(90f)))
                    {
                        Undo.RecordObject(_draft, "Clear Items");
                        _draft.SetItems(System.Array.Empty<ItemPlacement>());
                        MarkChanged();
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawBrushes()
        {
            EditorGUILayout.LabelField("Brush (key in brackets; drag to paint). Levels 0–2 lowland, 3–4 subalpine, 5–6 alpine snow; Ice on 5–6.",
                EditorStyles.miniBoldLabel);
            var itemStart = System.Array.FindIndex(Brushes, info => info.Brush == Brush.ItemGround);
            DrawBrushRow(0, itemStart);
            DrawBrushRow(itemStart, Brushes.Length);
        }

        private void DrawBrushRow(int from, int to)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var i = from; i < to; i++)
                {
                    var brush = Brushes[i];
                    var label = $"[{KeyLabel(brush.Key)}] {brush.Label}";
                    var rect = GUILayoutUtility.GetRect(new GUIContent(label), "Button", GUILayout.Height(30f), GUILayout.MinWidth(40f));
                    if (GUI.Toggle(rect, _brush == brush.Brush, label, "Button"))
                    {
                        _brush = brush.Brush;
                    }

                    EditorGUI.DrawRect(new Rect(rect.x + 3f, rect.yMax - 5f, rect.width - 6f, 3f), brush.Color);
                }

                GUILayout.FlexibleSpace();
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
            foreach (var item in _draft.Items)
            {
                items[item.Cell] = item.InAir;
            }

            var showItems = _draft.ItemMode == ItemMode.Fixed;
            var cells = _report != null && _report.Cells.GetLength(0) == width && _report.Cells.GetLength(1) == depth ? _report.Cells : null;

            for (var row = 0; row < depth; row++)
            {
                GUI.Label(new Rect(area.x, origin.y + (depth - 1 - row) * CellSize, AxisLabelSize, CellSize), row.ToString(), EditorStyles.centeredGreyMiniLabel);
                for (var column = 0; column < width; column++)
                {
                    var rect = CellRect(origin, depth, column, row);
                    var spec = _tiles[column, row];
                    var color = ColorOf(spec);
                    EditorGUI.DrawRect(rect, color);
                    _cellStyle.normal.textColor = color.grayscale > 0.6f ? DarkText : Color.white;
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
                case Brush.Ice:
                    PaintIce(cell);
                    break;
                case Brush.EraseItem:
                    if (_draft.Items.Any(item => item.Cell == cell))
                    {
                        Undo.RecordObject(_draft, "Erase Item");
                        _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).ToArray());
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

            Undo.RecordObject(_draft, "Paint Tiles");
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
            _draft.SetTiles(_tiles);
            if (spec.Type != TileType.Solid && _draft.Items.Any(item => item.Cell == cell))
            {
                _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).ToArray());
            }

            MarkChanged();
        }

        /// <summary>Turns a snow tile (level 5–6) to ice; paint a level brush over it to undo.</summary>
        private void PaintIce(Vector2Int cell)
        {
            var spec = _tiles[cell.x, cell.y];
            if (spec.Type != TileType.Solid || spec.Zone != ElevationZone.Alpine)
            {
                _status = "Ice goes on solid tiles at levels 5–6.";
                return;
            }

            PaintTile(cell, new TileSpec(TileType.Solid, spec.Level, isIce: true));
        }

        private void PlaceItem(Vector2Int cell, bool inAir)
        {
            if (_tiles[cell.x, cell.y].Type != TileType.Solid)
            {
                _status = "Items go on solid tiles (levels 0–6, snow or ice).";
                return;
            }

            var placement = new ItemPlacement(cell, inAir);
            if (_draft.ItemMode == ItemMode.Fixed && _draft.Items.Contains(placement))
            {
                return;
            }

            Undo.RecordObject(_draft, "Place Item");
            if (_draft.ItemMode != ItemMode.Fixed)
            {
                _draft.ItemMode = ItemMode.Fixed;
                _status = "Switched the items to Fixed.";
            }

            _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).Append(placement).ToArray());
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

            Undo.RecordObject(_draft, "Resize Layout");
            _draft.SetTiles(resized);
            _draft.SetItems(_draft.Items.Where(item => item.Cell.x < width && item.Cell.y < depth).ToArray());
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

            Undo.RecordObject(_draft, "Shift Layout");
            _draft.SetTiles(shifted);
            _draft.SetItems(_draft.Items.Select(item => new ItemPlacement(Wrap(item.Cell), item.InAir)).ToArray());
            MarkChanged();
        }

        private void HandleShortcuts()
        {
            var current = Event.current;
            if (current.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return;
            }

            if (current.control || current.command)
            {
                if (current.keyCode == KeyCode.S && _layout != null)
                {
                    Save(_layout);
                    current.Use();
                }

                return;
            }

            var key = current.keyCode >= KeyCode.Keypad0 && current.keyCode <= KeyCode.Keypad9
                ? KeyCode.Alpha0 + (current.keyCode - KeyCode.Keypad0)
                : current.keyCode;
            var index = System.Array.FindIndex(Brushes, info => info.Key == key);
            if (index >= 0)
            {
                _brush = Brushes[index].Brush;
                current.Use();
                Repaint();
            }
        }

        // ---------- State ----------

        private void MarkChanged(bool frame = false)
        {
            UpdateUnsavedState();
            Reload();
            RefreshPreview(frame);
            Repaint();
        }

        private void Reload()
        {
            _report = null;
            _tiles = null;
            if (_draft == null)
            {
                return;
            }

            if (_draft.TryGetTiles(out var tiles, out var error))
            {
                _tiles = tiles;
                _parseError = null;
            }
            else
            {
                _parseError = error;
            }

            _report = MapLayoutValidator.Validate(_draft);
        }

        private void RefreshPreview(bool frame)
        {
            if (!_preview || _draft == null || _tiles == null)
            {
                MapToolPreview.Clear();
                return;
            }

            MapToolPreview.Show(_draft, frame);
        }

        // ---------- Drafts ----------

        /// <summary>The in-memory copy the tool edits for <paramref name="asset"/>; created on first use.</summary>
        private MapLayout GetDraft(MapLayout asset)
        {
            if (asset == null)
            {
                return null;
            }

            if (!_drafts.TryGetValue(asset, out var draft) || draft == null)
            {
                draft = Instantiate(asset);
                draft.name = asset.name;
                draft.hideFlags = HideFlags.DontSave;
                _drafts[asset] = draft;
                _savedJson[asset] = SavedJsonOf(asset);
            }

            return draft;
        }

        /// <summary>What a draft of <paramref name="asset"/> serializes to with no edits.</summary>
        private static string SavedJsonOf(MapLayout asset)
        {
            var copy = Instantiate(asset);
            copy.name = asset.name;
            copy.hideFlags = HideFlags.DontSave;
            var json = EditorJsonUtility.ToJson(copy);
            DestroyImmediate(copy);
            return json;
        }

        private void UpdateUnsavedState()
        {
            _modified.Clear();
            foreach (var pair in _drafts)
            {
                if (pair.Key != null && pair.Value != null && EditorJsonUtility.ToJson(pair.Value) != _savedJson[pair.Key])
                {
                    _modified.Add(pair.Key);
                }
            }

            hasUnsavedChanges = _modified.Count > 0;
            Repaint();
        }

        private void Save(MapLayout asset)
        {
            if (!_drafts.TryGetValue(asset, out var draft) || !_modified.Contains(asset))
            {
                return;
            }

            asset.CopyFrom(draft);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            _savedJson[asset] = SavedJsonOf(asset);
            _status = $"Saved {asset.name}.";
            UpdateUnsavedState();
        }

        private void Discard(MapLayout asset)
        {
            if (!_drafts.TryGetValue(asset, out var draft))
            {
                return;
            }

            Undo.ClearUndo(draft);
            draft.CopyFrom(asset);
            _savedJson[asset] = SavedJsonOf(asset);
            UpdateUnsavedState();
            if (asset == _layout)
            {
                _status = null;
                Reload();
                RefreshPreview(frame: false);
            }
        }

        private void ForgetDraft(MapLayout asset)
        {
            if (_drafts.TryGetValue(asset, out var draft) && draft != null)
            {
                Undo.ClearUndo(draft);
                DestroyImmediate(draft);
            }

            _drafts.Remove(asset);
            _savedJson.Remove(asset);
            UpdateUnsavedState();
        }

        /// <summary>A script reload rebuilds the window; unsaved drafts wait in <c>SessionState</c>.</summary>
        private void StashDrafts()
        {
            var guids = new List<string>();
            foreach (var asset in _modified)
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                SessionState.SetString(DraftKeyPrefix + guid, EditorJsonUtility.ToJson(_drafts[asset]));
                guids.Add(guid);
            }

            SessionState.SetString(DraftListKey, string.Join(";", guids));
        }

        private void RestoreDrafts()
        {
            foreach (var guid in SessionState.GetString(DraftListKey, string.Empty).Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var asset = AssetDatabase.LoadAssetAtPath<MapLayout>(AssetDatabase.GUIDToAssetPath(guid));
                var json = SessionState.GetString(DraftKeyPrefix + guid, string.Empty);
                if (asset != null && json.Length > 0)
                {
                    var draft = GetDraft(asset);
                    EditorJsonUtility.FromJsonOverwrite(json, draft);
                    draft.name = asset.name;
                    draft.hideFlags = HideFlags.DontSave;
                }
            }

            EraseStash();
            UpdateUnsavedState();
        }

        private static void EraseStash()
        {
            foreach (var guid in SessionState.GetString(DraftListKey, string.Empty).Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                SessionState.EraseString(DraftKeyPrefix + guid);
            }

            SessionState.EraseString(DraftListKey);
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
            _ when spec.IsIce => IceColor,
            _ => LevelColors[Mathf.Clamp(spec.Level, 0, LevelColors.Length - 1)],
        };

        private static string LabelOf(TileSpec spec) => spec.Type switch
        {
            TileType.Hole => string.Empty,
            TileType.Crumbling => "C",
            TileType.Start => "S",
            _ => spec.ToToken(),
        };

        private static TileSpec SpecOf(Brush brush) => brush switch
        {
            Brush.Hole => new TileSpec(TileType.Hole, 0),
            >= Brush.Level0 and <= Brush.Level6 => new TileSpec(TileType.Solid, brush - Brush.Level0),
            Brush.Crumbling => new TileSpec(TileType.Crumbling, 0),
            Brush.Start => new TileSpec(TileType.Start, 0),
            _ => new TileSpec(TileType.Solid, 0),
        };

        private static string KeyLabel(KeyCode key) =>
            key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9 ? ((char)('0' + (key - KeyCode.Alpha0))).ToString() : key.ToString();

        private readonly struct BrushInfo
        {
            public BrushInfo(Brush brush, string label, KeyCode key, Color color)
            {
                Brush = brush;
                Label = label;
                Key = key;
                Color = color;
            }

            public Brush Brush { get; }

            public string Label { get; }

            public KeyCode Key { get; }

            public Color Color { get; }
        }
    }
}
