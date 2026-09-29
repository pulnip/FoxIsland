using System.Linq;
using MyGame.CameraRig;
using MyGame.Core;
using MyGame.Items;
using MyGame.Map;
using MyGame.Player;
using MyGame.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace MyGame.EditorTools
{
    public static class MainSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const float SpawnClearance = 0.05f;

        [MenuItem("Tools/MyGame/Build All", priority = 0)]
        public static void BuildAll()
        {
            MapAssetBuilder.Run();
            PlayerAssetBuilder.Run();
            ItemAssetBuilder.Run();
            UIAssetBuilder.Run();
            Run();
        }

        [MenuItem("Tools/MyGame/Setup Main Scene", priority = 40)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SetupLight();
            var mapBuilder = SetupMap();
            var layout = mapBuilder.Layout;
            var player = SetupPlayer(layout);
            SetupCamera(layout, player);
            var itemSpawner = SetupItems(mapBuilder);
            var gameManager = SetupGameManager(player, itemSpawner);
            SetupUI(gameManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainSceneSetup] {ScenePath} set up.");
        }

        /// <summary>
        /// Frames the whole grid plus the player's jump height above the highest level.
        /// </summary>
        private static void SetupCamera(MapLayout layout, PlayerController player)
        {
            var cameraObject = GameObject.FindWithTag("MainCamera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.AddComponent<AudioListener>();
            }

            var camera = EditorAssetUtility.GetOrAddComponent<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.78f, 0.90f);

            var jumpHeight = new SerializedObject(player).FindProperty("_jumpHeight").floatValue;
            var bounds = MapMetrics.GridBounds(layout.Width, layout.Depth);
            bounds.SetMinMax(bounds.min, bounds.max + Vector3.up * jumpHeight);

            var quarterView = EditorAssetUtility.GetOrAddComponent<QuarterViewCamera>(cameraObject);
            EditorAssetUtility.SetFloat(quarterView, "_screenFill", 0.85f);
            quarterView.FramedBounds = bounds;
            quarterView.Apply();
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

        private static MapBuilder SetupMap()
        {
            var builder = Object.FindObjectsByType<MapBuilder>().FirstOrDefault();
            if (builder == null)
            {
                builder = new GameObject("Map").AddComponent<MapBuilder>();
            }

            builder.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(MapAssetBuilder.LayoutPath);
            EditorAssetUtility.SetReference(builder, "_layout", layout);
            EditorAssetUtility.SetReference(builder, "_solidPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.SolidPrefabPath));
            EditorAssetUtility.SetReference(builder, "_crumblingPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.CrumblingPrefabPath));
            EditorAssetUtility.SetReference(builder, "_startPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.StartPrefabPath));
            return builder;
        }

        private static PlayerController SetupPlayer(MapLayout layout)
        {
            var player = Object.FindObjectsByType<PlayerController>().FirstOrDefault();
            if (player == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerAssetBuilder.PlayerPrefabPath);
                player = ((GameObject)PrefabUtility.InstantiatePrefab(prefab)).GetComponent<PlayerController>();
            }

            var start = layout != null && layout.TryGetStart(out var coordinates) ? coordinates : Vector2Int.zero;
            var position = MapMetrics.TopCenter(start, 0) + Vector3.up * SpawnClearance;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 45f, 0f));
            return player;
        }

        private static ItemSpawner SetupItems(MapBuilder mapBuilder)
        {
            var spawner = Object.FindObjectsByType<ItemSpawner>().FirstOrDefault();
            if (spawner == null)
            {
                spawner = new GameObject("Items").AddComponent<ItemSpawner>();
            }

            spawner.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            EditorAssetUtility.SetReference(spawner, "_mapBuilder", mapBuilder);
            EditorAssetUtility.SetReference(spawner, "_itemPrefab", AssetDatabase.LoadAssetAtPath<Item>(ItemAssetBuilder.ItemPrefabPath));
            return spawner;
        }

        private static GameManager SetupGameManager(PlayerController player, ItemSpawner itemSpawner)
        {
            var gameManager = Object.FindObjectsByType<GameManager>().FirstOrDefault();
            if (gameManager == null)
            {
                gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            }

            EditorAssetUtility.SetReference(gameManager, "_config", AssetDatabase.LoadAssetAtPath<GameConfig>(ItemAssetBuilder.GameConfigPath));
            EditorAssetUtility.SetReference(gameManager, "_player", player);
            EditorAssetUtility.SetReference(gameManager, "_itemSpawner", itemSpawner);
            return gameManager;
        }

        private static void SetupUI(GameManager gameManager)
        {
            var gameUI = Object.FindObjectsByType<GameUI>().FirstOrDefault();
            if (gameUI == null)
            {
                gameUI = new GameObject("UI").AddComponent<GameUI>();
            }

            var document = gameUI.GetComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(UIAssetBuilder.PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIAssetBuilder.GameUIPath);
            EditorAssetUtility.SetReference(gameUI, "_gameManager", gameManager);
        }
    }
}
