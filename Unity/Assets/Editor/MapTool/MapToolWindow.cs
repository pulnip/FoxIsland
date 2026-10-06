using System.Collections.Generic;
using System.Linq;
using MyGame.Map;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MyGame.EditorTools.MapTool
{
    /// <summary>
    /// Tools/MyGame/Map Tool (plan.md §4.8): manages the worlds' stage lists and paints one layout on a
    /// top-down grid, with live checks, a Scene view preview, and test play. Layouts are edited as
    /// in-memory drafts; nothing reaches the asset until Save (unsaved levels show a *).
    /// </summary>
    internal sealed class MapToolWindow : EditorWindow
    {
        private const float CellSize = 52f;
        private const float CellGap = 2f;
        private const float AxisLabelSize = 18f;
        private const float ListWidth = 220f;
        private const string WorldKey = "FoxIsland.MapTool.World";
        private const string IndexKey = "FoxIsland.MapTool.Index";
        private const string PreviewKey = "FoxIsland.MapTool.Preview";
        private const string DraftListKey = "FoxIsland.MapTool.Drafts";
        private const string DraftKeyPrefix = "FoxIsland.MapTool.Draft.";
        private const int MaxRandomItems = 32;

        private static readonly Color HoleColor = new(0.16f, 0.36f, 0.58f);
        private static readonly Color[] SkinColors =
        {
            new(0.90f, 0.81f, 0.58f), new(0.49f, 0.74f, 0.33f), new(0.24f, 0.42f, 0.33f), new(0.58f, 0.58f, 0.60f),
            new(0.93f, 0.95f, 1f), new(0.62f, 0.85f, 0.94f), new(0.35f, 0.65f, 0.85f), new(0.80f, 0.92f, 0.98f),
        };

        private static readonly Color HeightColor = new(0.55f, 0.55f, 0.55f);
        private static readonly Color DarkText = new(0.15f, 0.2f, 0.28f);
        private static readonly Color CrumblingColor = new(0.86f, 0.70f, 0.45f);
        private static readonly Color StartColor = new(0.42f, 0.62f, 0.86f);
        private static readonly Color GemColor = new(1f, 0.82f, 0.22f);
        private static readonly Color UnreachableColor = new(0.95f, 0.25f, 0.2f);
        private static readonly Color HardJumpColor = new(1f, 0.6f, 0.1f);

        private static readonly BrushInfo[] Brushes =
        {
            new(Brush.Hole, "Hole", KeyCode.H, false, HoleColor),
            new(Brush.Height0, "0", KeyCode.Alpha0, false, HeightColor),
            new(Brush.Height1, "1", KeyCode.Alpha1, false, HeightColor),
            new(Brush.Height2, "2", KeyCode.Alpha2, false, HeightColor),
            new(Brush.Height3, "3", KeyCode.Alpha3, false, HeightColor),
            new(Brush.Height4, "4", KeyCode.Alpha4, false, HeightColor),
            new(Brush.Height5, "5", KeyCode.Alpha5, false, HeightColor),
            new(Brush.Height6, "6", KeyCode.Alpha6, false, HeightColor),
            new(Brush.Sand, "Sand", KeyCode.S, true, SkinColors[(int)Skin.Sand]),
            new(Brush.Meadow, "Meadow", KeyCode.M, true, SkinColors[(int)Skin.Meadow]),
            new(Brush.Forest, "Forest", KeyCode.F, true, SkinColors[(int)Skin.Forest]),
            new(Brush.Rock, "Rock", KeyCode.R, true, SkinColors[(int)Skin.Rock]),
            new(Brush.Snow, "Snow", KeyCode.N, true, SkinColors[(int)Skin.Snow]),
            new(Brush.Ice, "Ice", KeyCode.I, true, SkinColors[(int)Skin.Ice]),
            new(Brush.Water, "Water", KeyCode.W, true, SkinColors[(int)Skin.Water]),
            new(Brush.Iceberg, "Iceberg", KeyCode.B, true, SkinColors[(int)Skin.Iceberg]),
            new(Brush.Crumbling, "Crumbling", KeyCode.C, false, CrumblingColor),
            new(Brush.Firm, "Firm", KeyCode.V, false, Color.gray),
            new(Brush.Start, "Start", KeyCode.T, false, StartColor),
            new(Brush.ItemGround, "Item", KeyCode.G, false, GemColor),
            new(Brush.ItemAir, "Item (air)", KeyCode.A, false, GemColor),
            new(Brush.EraseItem, "Erase item", KeyCode.E, false, Color.gray),
        };

        [SerializeField] private Brush _brush = Brush.Height0;

        [Tooltip("Skin given to a hole when a height brush turns it into a block.")]
        [SerializeField] private Skin _lastSkin = Skin.Meadow;

        private readonly Dictionary<MapLayout, MapLayout> _drafts = new();
        private readonly Dictionary<MapLayout, string> _savedJson = new();
        private readonly HashSet<MapLayout> _modified = new();
        private WorldList _worlds;
        private SerializedObject _worldsObject;
        private ReorderableList[] _lists;
        private int _selectedWorld;
        private int _selectedIndex;
        private Vector2 _listScroll;
        private MapLayout _layout;
        private MapLayout _draft;
        private CellSpec[,] _cells;
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
            Height0,
            Height1,
            Height2,
            Height3,
            Height4,
            Height5,
            Height6,
            Sand,
            Meadow,
            Forest,
            Rock,
            Snow,
            Ice,
            Water,
            Iceberg,
            Crumbling,
            Firm,
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
            LoadWorlds();
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
            _worldsObject?.Update();
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

            if (_worlds == null)
            {
                EditorGUILayout.HelpBox($"No world list at {MapAssetBuilder.WorldListPath}. Run Tools/MyGame/Build All.", MessageType.Error);
                if (GUILayout.Button("Reload"))
                {
                    LoadWorlds();
                }

                return;
            }

            HandleShortcuts();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    DrawWorldLists();
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
                {
                    _scroll = scroll.scrollPosition;
                    DrawEditor();
                }
            }
        }

        // ---------- World and stage lists ----------

        private void LoadWorlds()
        {
            _worlds = AssetDatabase.LoadAssetAtPath<WorldList>(MapAssetBuilder.WorldListPath);
            if (_worlds == null)
            {
                return;
            }

            _worldsObject = new SerializedObject(_worlds);
            _lists = new ReorderableList[_worlds.Count];
            var worldsProperty = _worldsObject.FindProperty("_worlds");
            for (var w = 0; w < _worlds.Count; w++)
            {
                var world = w;
                var stages = worldsProperty.GetArrayElementAtIndex(world).FindPropertyRelative("_stages");
                _lists[world] = new ReorderableList(_worldsObject, stages, true, true, false, false)
                {
                    drawHeaderCallback = rect => DrawWorldHeader(rect, world),
                    drawElementCallback = (rect, index, isActive, isFocused) => DrawStageElement(rect, world, index),
                    onSelectCallback = list => Select(world, list.index),
                };
            }

            RestoreDrafts();
            Select(SessionState.GetInt(WorldKey, 0), SessionState.GetInt(IndexKey, 0));
        }

        private void DrawWorldHeader(Rect rect, int world)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3f, 4f, rect.height - 6f), _worlds[world].Color);
            EditorGUI.LabelField(new Rect(rect.x + 10f, rect.y, rect.width - 10f, rect.height), $"{world + 1}. {_worlds[world].Name}", EditorStyles.boldLabel);
        }

        private void DrawStageElement(Rect rect, int world, int index)
        {
            var stage = _lists[world].serializedProperty.GetArrayElementAtIndex(index).objectReferenceValue as MapLayout;
            var label = stage != null ? stage.name + (_modified.Contains(stage) ? " *" : string.Empty) : "(missing)";
            EditorGUI.LabelField(rect, $"{new StageRef(world, index).Label}   {label}");
        }

        private void DrawWorldLists()
        {
            _worldsObject.Update();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll))
            {
                _listScroll = scroll.scrollPosition;
                foreach (var list in _lists)
                {
                    list.DoLayoutList();
                }
            }

            if (_worldsObject.ApplyModifiedProperties())
            {
                AssetDatabase.SaveAssetIfDirty(_worlds);
                Select(_selectedWorld, _lists[_selectedWorld].index);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("New"))
                {
                    CreateStage();
                }

                using (new EditorGUI.DisabledScope(_layout == null))
                {
                    if (GUILayout.Button("Duplicate"))
                    {
                        DuplicateStage();
                    }

                    if (GUILayout.Button("Remove"))
                    {
                        RemoveStage();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(_layout == null))
            {
                var names = _worlds.Worlds.Select(world => $"Move to {world.Name}").ToArray();
                var target = EditorGUILayout.Popup(-1, names);
                if (target >= 0 && target != _selectedWorld)
                {
                    MoveStage(target);
                }
            }
        }

        private void Select(int world, int index)
        {
            _selectedWorld = Mathf.Clamp(world, 0, _worlds.Count - 1);
            var stages = _worlds[_selectedWorld].Stages;
            _selectedIndex = index >= 0 && index < stages.Count ? index : -1;
            _layout = _selectedIndex >= 0 ? stages[_selectedIndex] : null;
            for (var w = 0; w < _lists.Length; w++)
            {
                _lists[w].index = w == _selectedWorld ? _selectedIndex : -1;
            }

            SessionState.SetInt(WorldKey, _selectedWorld);
            SessionState.SetInt(IndexKey, _selectedIndex);
            _draft = GetDraft(_layout);
            _highlight = null;
            _status = null;
            Reload();
            RefreshPreview(frame: true);
            Repaint();
        }

        /// <summary>A blank 4 × 4 meadow stage at the end of the selected world.</summary>
        private void CreateStage()
        {
            var world = _worlds[_selectedWorld];
            var path = AssetDatabase.GenerateUniqueAssetPath($"{MapAssetBuilder.LevelFolder}/{world.Name}{world.Stages.Count + 1:00}.asset");
            var layout = CreateInstance<MapLayout>();
            layout.SetRows(new[] { "0m 0m 0m 0m", "0m 0m 0m 0m", "0m 0m 0m 0m", "0m 0m 0m 0m" });
            layout.Start = Vector2Int.zero;
            AssetDatabase.CreateAsset(layout, path);
            InsertStage(_selectedWorld, layout, world.Stages.Count);
        }

        private void DuplicateStage()
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

            InsertStage(_selectedWorld, copy, _selectedIndex + 1);
        }

        private void InsertStage(int world, MapLayout layout, int index)
        {
            var stages = _worlds[world].Stages.ToList();
            stages.Insert(Mathf.Clamp(index, 0, stages.Count), layout);
            SetStages(world, stages, "Add Stage");
            Select(world, stages.IndexOf(layout));
        }

        private void RemoveStage()
        {
            if (_worlds.AllStages().Count() <= 1)
            {
                EditorUtility.DisplayDialog("Remove Stage", "The game needs at least one stage.", "OK");
                return;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "Remove Stage", $"Remove {_layout.name} from {_worlds[_selectedWorld].Name}?", "Remove from World", "Cancel", "Remove and Delete Asset");
            if (choice == 1)
            {
                return;
            }

            var removed = _layout;
            var world = _selectedWorld;
            var stages = _worlds[world].Stages.ToList();
            var index = stages.IndexOf(removed);
            stages.RemoveAt(index);
            ForgetDraft(removed);
            _layout = null;
            _draft = null;
            SetStages(world, stages, "Remove Stage");
            if (choice == 2)
            {
                AssetDatabase.MoveAssetToTrash(AssetDatabase.GetAssetPath(removed));
            }

            Select(world, Mathf.Min(index, stages.Count - 1));
        }

        /// <summary>Moves the selected stage to the end of another world.</summary>
        private void MoveStage(int target)
        {
            var moved = _layout;
            var from = _worlds[_selectedWorld].Stages.ToList();
            from.Remove(moved);
            SetStages(_selectedWorld, from, "Move Stage");
            var to = _worlds[target].Stages.ToList();
            to.Add(moved);
            SetStages(target, to, "Move Stage");
            Select(target, to.Count - 1);
        }

        private void SetStages(int world, List<MapLayout> stages, string undoName)
        {
            Undo.RecordObject(_worlds, undoName);
            _worlds[world].SetStages(stages.ToArray());
            EditorUtility.SetDirty(_worlds);
            AssetDatabase.SaveAssetIfDirty(_worlds);
            _worldsObject.Update();
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
            if (_cells == null)
            {
                EditorGUILayout.HelpBox($"The rows do not parse: {_parseError}", MessageType.Error);
                if (GUILayout.Button("Reset to a blank 4 × 4"))
                {
                    Undo.RecordObject(_draft, "Reset Layout");
                    _draft.SetRows(new[] { "0m 0m 0m 0m", "0m 0m 0m 0m", "0m 0m 0m 0m", "0m 0m 0m 0m" });
                    _draft.Start = Vector2Int.zero;
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
            var width = _cells.GetLength(0);
            var depth = _cells.GetLength(1);
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
            EditorGUILayout.LabelField("Brushes (key in brackets, ⇧ = Shift; drag to paint). Height and skin are painted separately.",
                EditorStyles.miniBoldLabel);
            var skinStart = System.Array.FindIndex(Brushes, info => info.Brush == Brush.Sand);
            var otherStart = System.Array.FindIndex(Brushes, info => info.Brush == Brush.Crumbling);
            DrawBrushRow("Height", 0, skinStart);
            DrawBrushRow("Skin", skinStart, otherStart);
            DrawBrushRow("Other", otherStart, Brushes.Length);
        }

        private void DrawBrushRow(string title, int from, int to)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, GUILayout.Width(44f));
                for (var i = from; i < to; i++)
                {
                    var brush = Brushes[i];
                    var label = $"[{KeyLabel(brush)}] {brush.Label}";
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
            var width = _cells.GetLength(0);
            var depth = _cells.GetLength(1);
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
                    var spec = _cells[column, row];
                    var color = ColorOf(spec);
                    EditorGUI.DrawRect(rect, color);
                    _cellStyle.normal.textColor = color.grayscale > 0.6f ? DarkText : Color.white;
                    GUI.Label(rect, LabelOf(spec), _cellStyle);
                    var cell = new Vector2Int(column, row);
                    if (cell == _draft.Start)
                    {
                        EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 2f, 16f, 14f), StartColor);
                        GUI.Label(new Rect(rect.x + 2f, rect.y + 1f, 16f, 14f), "S", EditorStyles.whiteMiniLabel);
                    }

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
            if (cell.x < 0 || cell.y < 0 || cell.x >= _cells.GetLength(0) || cell.y >= _cells.GetLength(1))
            {
                return;
            }

            _status = null;
            var spec = _cells[cell.x, cell.y];
            switch (_brush)
            {
                case Brush.Hole:
                    PaintCell(cell, CellSpec.Hole);
                    break;
                case >= Brush.Height0 and <= Brush.Height6:
                    var height = _brush - Brush.Height0;
                    PaintCell(cell, spec.IsBlock ? spec.WithHeight(height) : new CellSpec(height, _lastSkin));
                    break;
                case >= Brush.Sand and <= Brush.Iceberg:
                    var skin = (Skin)(_brush - Brush.Sand);
                    PaintCell(cell, spec.IsBlock ? spec.WithSkin(skin) : new CellSpec(0, skin));
                    break;
                case Brush.Crumbling:
                case Brush.Firm:
                    if (!spec.IsBlock)
                    {
                        _status = "Crumbling and Firm apply to blocks, not holes.";
                        break;
                    }

                    PaintCell(cell, spec.WithCrumbling(_brush == Brush.Crumbling));
                    break;
                case Brush.Start:
                    SetStart(cell);
                    break;
                case Brush.ItemGround:
                case Brush.ItemAir:
                    PlaceItem(cell, _brush == Brush.ItemAir);
                    break;
                case Brush.EraseItem:
                    if (_draft.Items.Any(item => item.Cell == cell))
                    {
                        Undo.RecordObject(_draft, "Erase Item");
                        _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).ToArray());
                        MarkChanged();
                    }

                    break;
            }
        }

        /// <summary>Writes one cell; an item left on a cell that can no longer hold it goes away.</summary>
        private void PaintCell(Vector2Int cell, CellSpec spec)
        {
            if (_cells[cell.x, cell.y].Equals(spec))
            {
                return;
            }

            Undo.RecordObject(_draft, "Paint Cells");
            _cells[cell.x, cell.y] = spec;
            _draft.SetCells(_cells);
            if (!MapLayoutValidator.CanHoldItem(_draft, _cells, cell) && _draft.Items.Any(item => item.Cell == cell))
            {
                _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).ToArray());
            }

            MarkChanged();
        }

        private void SetStart(Vector2Int cell)
        {
            if (!_cells[cell.x, cell.y].IsBlock)
            {
                _status = "The start goes on a block.";
                return;
            }

            if (_draft.Start == cell)
            {
                return;
            }

            Undo.RecordObject(_draft, "Move Start");
            _draft.Start = cell;
            _draft.SetItems(_draft.Items.Where(item => item.Cell != cell).ToArray());
            MarkChanged();
        }

        private void PlaceItem(Vector2Int cell, bool inAir)
        {
            if (!MapLayoutValidator.CanHoldItem(_draft, _cells, cell))
            {
                _status = "Items go on firm blocks other than the start (no hole, crumbling, or iceberg).";
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

        /// <summary>Grows or crops from the (0, 0) corner; new cells are height-0 blocks of the last skin.</summary>
        private void Resize(int width, int depth)
        {
            var resized = new CellSpec[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    var inside = column < _cells.GetLength(0) && row < _cells.GetLength(1);
                    resized[column, row] = inside ? _cells[column, row] : new CellSpec(0, _lastSkin);
                }
            }

            Undo.RecordObject(_draft, "Resize Layout");
            _draft.SetCells(resized);
            _draft.SetItems(_draft.Items.Where(item => item.Cell.x < width && item.Cell.y < depth).ToArray());
            MarkChanged(frame: true);
        }

        /// <summary>Moves everything one cell, wrapping around, so nothing is lost.</summary>
        private void Shift(Vector2Int direction)
        {
            var width = _cells.GetLength(0);
            var depth = _cells.GetLength(1);
            Vector2Int Wrap(Vector2Int cell) => new((cell.x + direction.x + width) % width, (cell.y + direction.y + depth) % depth);

            var shifted = new CellSpec[width, depth];
            for (var column = 0; column < width; column++)
            {
                for (var row = 0; row < depth; row++)
                {
                    var to = Wrap(new Vector2Int(column, row));
                    shifted[to.x, to.y] = _cells[column, row];
                }
            }

            Undo.RecordObject(_draft, "Shift Layout");
            _draft.SetCells(shifted);
            _draft.Start = Wrap(_draft.Start);
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
            var index = System.Array.FindIndex(Brushes, info => info.Key == key && info.Shift == current.shift);
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
            _cells = null;
            if (_draft == null)
            {
                return;
            }

            if (_draft.TryGetCells(out var cells, out var error))
            {
                _cells = cells;
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
            if (!_preview || _draft == null || _cells == null)
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

        /// <summary>Skin color, a little lighter the higher the block.</summary>
        private static Color ColorOf(CellSpec spec) => spec.IsBlock
            ? Color.Lerp(SkinColors[(int)spec.Skin], Color.white, spec.Height * 0.05f)
            : HoleColor;

        private static string LabelOf(CellSpec spec) => spec.IsBlock ? spec.Height + (spec.IsCrumbling ? "c" : string.Empty) : string.Empty;

        private static string KeyLabel(BrushInfo brush)
        {
            var key = brush.Key >= KeyCode.Alpha0 && brush.Key <= KeyCode.Alpha9
                ? ((char)('0' + (brush.Key - KeyCode.Alpha0))).ToString()
                : brush.Key.ToString();
            return brush.Shift ? "⇧" + key : key;
        }

        private readonly struct BrushInfo
        {
            public BrushInfo(Brush brush, string label, KeyCode key, bool shift, Color color)
            {
                Brush = brush;
                Label = label;
                Key = key;
                Shift = shift;
                Color = color;
            }

            public Brush Brush { get; }

            public string Label { get; }

            public KeyCode Key { get; }

            /// <summary>The shortcut needs Shift (skins), so it never clashes with the other keys.</summary>
            public bool Shift { get; }

            public Color Color { get; }
        }
    }
}
