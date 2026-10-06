using System.Linq;
using MyGame.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools.MapTool
{
    /// <summary>
    /// Shows the edited layout in the Scene view: a <c>[Map Preview]</c> hierarchy in the Main scene
    /// with its own map, grass, and water, copied from the scene's originals. Everything in it is
    /// <see cref="HideFlags.DontSave"/>, so it never reaches the scene file.
    /// </summary>
    internal static class MapToolPreview
    {
        private const string RootName = "[Map Preview]";

        public static bool CanShow(out string reason)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                reason = "The preview is off during play mode.";
                return false;
            }

            if (SceneManager.GetActiveScene().path != MainSceneSetup.ScenePath)
            {
                reason = "Open the Main scene to preview.";
                return false;
            }

            if (FindSource<MapBuilder>() == null)
            {
                reason = "The Main scene has no Map; run Tools/MyGame/Build All.";
                return false;
            }

            reason = null;
            return true;
        }

        public static void Show(MapLayout layout, bool frame)
        {
            if (!CanShow(out _))
            {
                return;
            }

            var root = FindRoot();
            if (root == null)
            {
                root = CreateRoot();
            }

            var builder = root.GetComponentInChildren<MapBuilder>();
            var grass = builder.GetComponent<GrassField>();
            var water = root.GetComponentInChildren<WaterSurface>();

            builder.Build(layout);
            grass.Build();
            builder.GetComponent<TerrainDecor>().Build();
            builder.GetComponent<SnowField>().Build();
            var bounds = MapMetrics.GridBounds(layout.Width, layout.Depth, layout.MaxHeight);
            water.transform.position = new Vector3(bounds.center.x, MapMetrics.WaterLevel, bounds.center.z);
            water.Build();

            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = HideFlags.DontSave;
            }

            if (frame && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(bounds, false);
            }
        }

        public static void Clear()
        {
            var root = FindRoot();
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateRoot()
        {
            var root = EditorUtility.CreateGameObjectWithHideFlags(RootName, HideFlags.DontSave);

            var map = EditorUtility.CreateGameObjectWithHideFlags(
                "Map", HideFlags.DontSave, typeof(MapBuilder), typeof(GrassField), typeof(TerrainDecor), typeof(SnowField));
            map.transform.SetParent(root.transform, false);
            var builder = map.GetComponent<MapBuilder>();
            EditorUtility.CopySerialized(FindSource<MapBuilder>(), builder);
            CopyFromScene<GrassField>(map, builder);
            CopyFromScene<TerrainDecor>(map, builder);
            CopyFromScene<SnowField>(map, builder);

            var water = EditorUtility.CreateGameObjectWithHideFlags("Water", HideFlags.DontSave, typeof(WaterSurface));
            water.transform.SetParent(root.transform, false);
            var sourceWater = FindSource<WaterSurface>();
            if (sourceWater != null)
            {
                EditorUtility.CopySerialized(sourceWater, water.GetComponent<WaterSurface>());
                EditorUtility.CopySerialized(sourceWater.GetComponent<MeshRenderer>(), water.GetComponent<MeshRenderer>());
            }

            return root;
        }

        /// <summary>Copies the scene's component settings and points it at the preview's builder.</summary>
        private static void CopyFromScene<T>(GameObject map, MapBuilder builder) where T : MonoBehaviour
        {
            var target = map.GetComponent<T>();
            var source = FindSource<T>();
            if (source != null)
            {
                EditorUtility.CopySerialized(source, target);
            }

            EditorAssetUtility.SetReference(target, "_mapBuilder", builder);
        }

        private static GameObject FindRoot()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                var root = scene.GetRootGameObjects().FirstOrDefault(gameObject => gameObject.name == RootName);
                if (root != null)
                {
                    return root;
                }
            }

            return null;
        }

        /// <summary>The scene's own component, not one inside the preview.</summary>
        private static T FindSource<T>() where T : Component =>
            Object.FindObjectsByType<T>().FirstOrDefault(component => component.gameObject.hideFlags == HideFlags.None);
    }
}
