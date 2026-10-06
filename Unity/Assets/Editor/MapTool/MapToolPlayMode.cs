using MyGame.Core;
using MyGame.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools.MapTool
{
    /// <summary>
    /// "Play This Level": enters play mode on one layout, unsaved edits included. The layout goes to
    /// <c>SessionState</c> as JSON, which <see cref="LevelSession.TestLayout"/> reads back; the key is
    /// cleared when play mode ends.
    /// </summary>
    [InitializeOnLoad]
    internal static class MapToolPlayMode
    {
        static MapToolPlayMode() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        public static void Play(MapLayout layout)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (SceneManager.GetActiveScene().path != MainSceneSetup.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(MainSceneSetup.ScenePath, OpenSceneMode.Single);
            }

            SessionState.SetString(LevelSession.TestLayoutKey, EditorJsonUtility.ToJson(layout));
            MapToolPreview.Clear();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                    MapToolPreview.Clear();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SessionState.EraseString(LevelSession.TestLayoutKey);
                    break;
            }
        }
    }
}
