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
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace MyGame.EditorTools
{
    public static class MainSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        private const float SpawnClearance = 0.05f;
        private const float LakeBedThickness = 0.2f;

        [MenuItem("Tools/MyGame/Build All", priority = 0)]
        public static void BuildAll()
        {
            EffectAssetBuilder.Run();
            MapAssetBuilder.Run();
            CharacterAssetBuilder.Run();
            PlayerAssetBuilder.Run();
            ItemAssetBuilder.Run();
            UIAssetBuilder.Run();
            PlayerSettingsBuilder.Run();
            Run();
        }

        [MenuItem("Tools/MyGame/Setup Main Scene", priority = 40)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Load assets after opening the scene: opening it unloads unreferenced assets.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var levels = AssetDatabase.LoadAssetAtPath<LevelList>(MapAssetBuilder.LevelListPath);
            if (levels == null || levels.Count == 0 || levels[0] == null)
            {
                Debug.LogError($"[MainSceneSetup] {MapAssetBuilder.LevelListPath} needs at least one level.");
                return;
            }

            // The scene is laid out for level 1 so it reads right in edit mode; LevelLoader refits
            // it to the loaded level at runtime.
            var layout = levels[0];
            SetupLight();
            var mapBuilder = SetupMap();
            var player = SetupPlayer(layout);
            SetupGrass(mapBuilder, player);
            SetupAlpine(mapBuilder);
            var water = SetupLake(layout);
            var quarterView = SetupCamera(layout, player);
            var itemSpawner = SetupItems(mapBuilder);
            var levelLoader = SetupLevelLoader(levels, mapBuilder, player, quarterView, water);
            var gameManager = SetupGameManager(levelLoader, player, itemSpawner);
            SetupUI(gameManager, player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainSceneSetup] {ScenePath} set up.");
        }

        /// <summary>
        /// Frames the whole grid plus the player's jump height above the highest level.
        /// </summary>
        private static QuarterViewCamera SetupCamera(MapLayout layout, PlayerController player)
        {
            var cameraObject = GameObject.FindWithTag("MainCamera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.AddComponent<AudioListener>();
            }

            var camera = EditorAssetUtility.GetOrAddComponent<Camera>(cameraObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.36f, 0.56f);
            camera.GetUniversalAdditionalCameraData().requiresDepthOption = CameraOverrideOption.On;

            var quarterView = EditorAssetUtility.GetOrAddComponent<QuarterViewCamera>(cameraObject);
            EditorAssetUtility.SetFloat(quarterView, "_screenFill", 0.85f);
            quarterView.FramedBounds = MapMetrics.FramedBounds(layout.Width, layout.Depth, layout.MaxLevel, player.JumpHeight);
            quarterView.Apply();
            return quarterView;
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
            EditorAssetUtility.SetReference(builder, "_solidPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.SolidPrefabPath));
            EditorAssetUtility.SetReference(builder, "_crumblingPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.CrumblingPrefabPath));
            EditorAssetUtility.SetReference(builder, "_startPrefab", AssetDatabase.LoadAssetAtPath<Tile>(MapAssetBuilder.StartPrefabPath));
            EditorAssetUtility.SetReference(builder, "_subalpineTop", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.SubalpineTopMaterialPath));
            EditorAssetUtility.SetReference(builder, "_subalpineBody", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.DarkEarthMaterialPath));
            EditorAssetUtility.SetReference(builder, "_snowTop", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.SnowTopMaterialPath));
            EditorAssetUtility.SetReference(builder, "_iceTop", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.IceTopMaterialPath));
            EditorAssetUtility.SetReference(builder, "_alpineBody", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.RockMaterialPath));
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

        private static void SetupGrass(MapBuilder mapBuilder, PlayerController player)
        {
            var grass = EditorAssetUtility.GetOrAddComponent<GrassField>(mapBuilder.gameObject);
            EditorAssetUtility.ResetToDefaults(grass);
            EditorAssetUtility.SetReference(grass, "_mapBuilder", mapBuilder);
            EditorAssetUtility.SetReference(grass, "_material", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.GrassBladeMaterialPath));
            EditorAssetUtility.SetReference(grass, "_subalpineMaterial", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.SubalpineGrassMaterialPath));
            EditorAssetUtility.SetReference(grass, "_trampler", player.transform);
        }

        /// <summary>Subalpine and alpine decorations and the deformable snow (plan.md §4.9).</summary>
        private static void SetupAlpine(MapBuilder mapBuilder)
        {
            var decor = EditorAssetUtility.GetOrAddComponent<TerrainDecor>(mapBuilder.gameObject);
            EditorAssetUtility.ResetToDefaults(decor);
            EditorAssetUtility.SetReference(decor, "_mapBuilder", mapBuilder);
            EditorAssetUtility.SetReference(decor, "_material", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.DecorMaterialPath));

            var snow = EditorAssetUtility.GetOrAddComponent<SnowField>(mapBuilder.gameObject);
            EditorAssetUtility.ResetToDefaults(snow);
            EditorAssetUtility.SetReference(snow, "_mapBuilder", mapBuilder);
            EditorAssetUtility.SetReference(snow, "_material", AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.SnowMaterialPath));
        }

        /// <summary>
        /// Water centered on the grid at <see cref="MapMetrics.WaterLevel"/>, over a sand bed the
        /// tile pillars stand on. The water shader reads the camera depth texture for its depth
        /// tint and foam.
        /// </summary>
        private static WaterSurface SetupLake(MapLayout layout)
        {
            var water = Object.FindObjectsByType<WaterSurface>().FirstOrDefault();
            if (water == null)
            {
                water = new GameObject("Lake").AddComponent<WaterSurface>();
            }

            EditorAssetUtility.ResetToDefaults(water);
            var center = MapMetrics.GridBounds(layout.Width, layout.Depth, layout.MaxLevel).center;
            water.transform.SetPositionAndRotation(new Vector3(center.x, MapMetrics.WaterLevel, center.z), Quaternion.identity);

            var renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.WaterMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            var bed = water.transform.Find("Bed");
            if (bed == null)
            {
                bed = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Bed", water.transform,
                    AssetDatabase.LoadAssetAtPath<Material>(MapAssetBuilder.LakeBedMaterialPath)).transform;
            }

            var size = new SerializedObject(water).FindProperty("_size").floatValue;
            bed.localPosition = Vector3.up * (MapMetrics.LakeBedY - MapMetrics.WaterLevel - LakeBedThickness * 0.5f);
            bed.localRotation = Quaternion.identity;
            bed.localScale = new Vector3(size, LakeBedThickness, size);
            return water;
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

        private static LevelLoader SetupLevelLoader(
            LevelList levels, MapBuilder mapBuilder, PlayerController player, QuarterViewCamera quarterView, WaterSurface water)
        {
            var loader = Object.FindObjectsByType<LevelLoader>().FirstOrDefault();
            if (loader == null)
            {
                loader = new GameObject("Level").AddComponent<LevelLoader>();
            }

            EditorAssetUtility.SetReference(loader, "_levels", levels);
            EditorAssetUtility.SetReference(loader, "_mapBuilder", mapBuilder);
            EditorAssetUtility.SetReference(loader, "_player", player);
            EditorAssetUtility.SetReference(loader, "_camera", quarterView);
            EditorAssetUtility.SetReference(loader, "_water", water);
            return loader;
        }

        private static GameManager SetupGameManager(LevelLoader levelLoader, PlayerController player, ItemSpawner itemSpawner)
        {
            var gameManager = Object.FindObjectsByType<GameManager>().FirstOrDefault();
            if (gameManager == null)
            {
                gameManager = new GameObject("GameManager").AddComponent<GameManager>();
            }

            EditorAssetUtility.SetReference(gameManager, "_levelLoader", levelLoader);
            EditorAssetUtility.SetReference(gameManager, "_player", player);
            EditorAssetUtility.SetReference(gameManager, "_itemSpawner", itemSpawner);
            EditorAssetUtility.SetReference(gameManager, "_splashEffect", AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectAssetBuilder.SplashPath));
            return gameManager;
        }

        private static void SetupUI(GameManager gameManager, PlayerController player)
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
            EditorAssetUtility.SetReference(gameUI, "_player", player);
        }
    }
}
