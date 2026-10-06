using System.IO;
using MyGame.Map;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class MapAssetBuilder
    {
        public const string LevelFolder = "Assets/ScriptableObjects/Levels";
        public const string FirstLevelPath = LevelFolder + "/Level01.asset";
        public const string LevelListPath = "Assets/ScriptableObjects/LevelList.asset";
        public const string SolidPrefabPath = "Assets/Prefabs/Tiles/Tile_Solid.prefab";
        public const string CrumblingPrefabPath = "Assets/Prefabs/Tiles/Tile_Crumbling.prefab";
        public const string StartPrefabPath = "Assets/Prefabs/Tiles/Tile_Start.prefab";
        public const string GrassBladeMaterialPath = "Assets/Art/Materials/Grass_Blade.mat";
        public const string WaterMaterialPath = "Assets/Art/Materials/Water.mat";
        public const string LakeBedMaterialPath = "Assets/Art/Materials/Lake_Bed.mat";

        private const string LegacyLayoutPath = "Assets/ScriptableObjects/MapLayout_Main.asset";
        private const string MaterialFolder = "Assets/Art/Materials";
        private const string GrassShaderName = "MyGame/Grass";
        private const string WaterShaderName = "MyGame/Water";

        [MenuItem("Tools/MyGame/Build Map Assets", priority = 20)]
        public static void Run()
        {
            var dirt = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Dirt.mat", new Color(0.55f, 0.38f, 0.24f));
            var grass = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Grass.mat", new Color(0.49f, 0.74f, 0.33f));
            var crumbling = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Crumbling.mat", new Color(0.86f, 0.70f, 0.45f));
            var start = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Start.mat", new Color(0.42f, 0.62f, 0.86f));
            EditorAssetUtility.GetOrCreateMaterial(GrassBladeMaterialPath, GrassShaderName, new Color(0.64f, 0.86f, 0.36f), smoothness: 0f);
            EditorAssetUtility.GetOrCreateLitMaterial(LakeBedMaterialPath, new Color(0.78f, 0.70f, 0.50f));
            BuildWaterMaterial();

            BuildTilePrefab(SolidPrefabPath, grass, dirt, isCrumbling: false);
            BuildTilePrefab(CrumblingPrefabPath, crumbling, dirt, isCrumbling: true);
            BuildTilePrefab(StartPrefabPath, start, dirt, isCrumbling: false);
            EnsureLevels();

            AssetDatabase.SaveAssets();
            Debug.Log("[MapAssetBuilder] Map assets built.");
        }

        /// <summary>
        /// Builds <c>Root (Tile, BoxCollider) → Visual → Top, Body</c>. The Visual container lets a
        /// crumbling tile shake and fall without moving its collider.
        /// </summary>
        private static void BuildTilePrefab(string path, Material topMaterial, Material bodyMaterial, bool isCrumbling)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var tile = root.AddComponent<Tile>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);

            var top = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Top", visual, topMaterial);
            var body = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Body", visual, bodyMaterial);
            EditorAssetUtility.SetReference(tile, "_top", top.transform);
            EditorAssetUtility.SetReference(tile, "_body", body.transform);
            tile.ApplyLevel(0);

            if (isCrumbling)
            {
                var crumblingTile = root.AddComponent<CrumblingTile>();
                EditorAssetUtility.SetReference(crumblingTile, "_visual", visual);
                EditorAssetUtility.SetReference(crumblingTile, "_dustEffect",
                    AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectAssetBuilder.CrumbleDustPath));
                EditorAssetUtility.SetReference(crumblingTile, "_splashEffect",
                    AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectAssetBuilder.TileSplashPath));
            }

            EditorAssetUtility.SavePrefab(root, path);
        }

        private static void BuildWaterMaterial()
        {
            var material = EditorAssetUtility.GetOrCreateMaterial(WaterMaterialPath, WaterShaderName, Color.white, smoothness: 0f);
            var changed = EditorAssetUtility.SetColorIfChanged(material, "_ShallowColor", new Color(0.36f, 0.78f, 0.80f, 0.45f));
            changed |= EditorAssetUtility.SetColorIfChanged(material, "_DeepColor", new Color(0.12f, 0.38f, 0.62f, 0.92f));
            changed |= EditorAssetUtility.SetColorIfChanged(material, "_FoamColor", new Color(0.92f, 0.97f, 1f));
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_DepthFade", 0.8f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_FoamWidth", 0.06f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_WaveHeight", 0.02f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_WaveLength", 2.5f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_WaveSpeed", 0.4f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_NormalStrength", 3f);
            if (changed)
            {
                EditorUtility.SetDirty(material);
            }
        }

        /// <summary>
        /// Creates the first level and the level list only when missing: both are edited with the
        /// map tool and never overwritten. The original single layout moves (GUID kept) to Level01.
        /// </summary>
        private static void EnsureLevels()
        {
            EditorAssetUtility.EnsureFolder(LevelFolder);
            if (AssetDatabase.LoadAssetAtPath<MapLayout>(LegacyLayoutPath) != null
                && AssetDatabase.LoadAssetAtPath<MapLayout>(FirstLevelPath) == null)
            {
                var error = AssetDatabase.MoveAsset(LegacyLayoutPath, FirstLevelPath);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"[MapAssetBuilder] Could not move {LegacyLayoutPath}: {error}");
                }
            }

            if (AssetDatabase.LoadAssetAtPath<LevelList>(LevelListPath) != null)
            {
                return;
            }

            var firstLevel = AssetDatabase.LoadAssetAtPath<MapLayout>(FirstLevelPath);
            if (firstLevel == null)
            {
                firstLevel = ScriptableObject.CreateInstance<MapLayout>();
                AssetDatabase.CreateAsset(firstLevel, FirstLevelPath);
            }

            var levels = ScriptableObject.CreateInstance<LevelList>();
            levels.SetLevels(new[] { firstLevel });
            AssetDatabase.CreateAsset(levels, LevelListPath);
        }
    }
}
