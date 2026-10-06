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
        /// <summary><c>SessionState</c> key holding the GUID of the layout the map tool test-plays.</summary>
        public const string TestLayoutKey = "FoxIsland.MapTool.TestLayout";
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
                var guid = UnityEditor.SessionState.GetString(TestLayoutKey, string.Empty);
                if (guid.Length > 0)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<MapLayout>(path);
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
