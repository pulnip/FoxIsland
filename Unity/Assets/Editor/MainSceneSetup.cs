using System.Linq;
using MyGame.CameraRig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    public static class MainSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/MyGame/Setup Main Scene")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetupCamera();
            SetupLight();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainSceneSetup] {ScenePath} set up.");
        }

        private static void SetupCamera()
        {
            var cameraObject = GameObject.FindWithTag("MainCamera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.AddComponent<AudioListener>();
            }

            var camera = GetOrAddComponent<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.78f, 0.90f);

            GetOrAddComponent<QuarterViewCamera>(cameraObject).Apply();
        }

        private static void SetupLight()
        {
            var light = Object.FindObjectsByType<Light>()
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (light == null)
            {
                light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.SetPositionAndRotation(new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f));
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.80f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.60f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.30f);
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component =>
            gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();
    }
}
