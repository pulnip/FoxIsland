using MyGame.Map;
using UnityEngine;

namespace MyGame.Core
{
    /// <summary>
    /// Progress that survives scene reloads: which level to load and whether to open at the title.
    /// Reset on every launch, since domain reload is off in the editor.
    /// </summary>
    public static class LevelSession
    {
#if UNITY_EDITOR
        /// <summary>
        /// <c>SessionState</c> key holding the layout the map tool test-plays, as JSON, so unsaved
        /// edits can be played.
        /// </summary>
        public const string TestLayoutKey = "FoxIsland.MapTool.TestLayoutJson";

        private static string _testJson;
        private static MapLayout _testLayout;
#endif

        public static bool OpenAtTitle { get; set; } = true;

        /// <summary>Zero-based index into the <see cref="LevelList"/>.</summary>
        public static int LevelIndex { get; set; }

        /// <summary>True while the map tool's "Play This Level" is running.</summary>
        public static bool IsTestPlay => TestLayout != null;

        /// <summary>The layout the map tool asked to test-play; always null in builds.</summary>
        public static MapLayout TestLayout
        {
            get
            {
#if UNITY_EDITOR
                var json = UnityEditor.SessionState.GetString(TestLayoutKey, string.Empty);
                if (json.Length > 0)
                {
                    if (_testLayout == null || json != _testJson)
                    {
                        _testLayout = ScriptableObject.CreateInstance<MapLayout>();
                        UnityEditor.EditorJsonUtility.FromJsonOverwrite(json, _testLayout);
                        _testLayout.name = "Test Play";
                        _testLayout.hideFlags = HideFlags.DontSave;
                        _testJson = json;
                    }

                    return _testLayout;
                }
#endif
                return null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OpenAtTitle = true;
            LevelIndex = 0;
        }
    }
}
