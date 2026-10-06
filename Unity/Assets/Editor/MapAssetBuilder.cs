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
        public const string GrassBladeMaterialPath = "Assets/Art/Materials/Grass_Blade.mat";
        public const string WaterMaterialPath = "Assets/Art/Materials/Water.mat";
        public const string PoolWaterMaterialPath = "Assets/Art/Materials/Water_Pool.mat";
        public const string LakeBedMaterialPath = "Assets/Art/Materials/Lake_Bed.mat";
        public const string SubalpineTopMaterialPath = "Assets/Art/Materials/Tile_Subalpine.mat";
        public const string DarkEarthMaterialPath = "Assets/Art/Materials/Tile_DarkEarth.mat";
        public const string SnowTopMaterialPath = "Assets/Art/Materials/Tile_Snow.mat";
        public const string IceTopMaterialPath = "Assets/Art/Materials/Tile_Ice.mat";
        public const string RockMaterialPath = "Assets/Art/Materials/Tile_Rock.mat";
        public const string ForestGrassMaterialPath = "Assets/Art/Materials/Grass_Subalpine.mat";
        public const string DecorMaterialPath = "Assets/Art/Materials/Decor_Voxel.mat";
        public const string SnowMaterialPath = "Assets/Art/Materials/Snow.mat";

        private const string LegacyLayoutPath = "Assets/ScriptableObjects/MapLayout_Main.asset";
        private const string LegacyStartPrefabPath = "Assets/Prefabs/Tiles/Tile_Start.prefab";
        private const string LegacyStartMaterialPath = "Assets/Art/Materials/Tile_Start.mat";
        private const string MaterialFolder = "Assets/Art/Materials";
        private const string GrassShaderName = "MyGame/Grass";
        private const string WaterShaderName = "MyGame/Water";
        private const string VoxelLitShaderName = "MyGame/VoxelLit";
        private const string SnowShaderName = "MyGame/Snow";

        [MenuItem("Tools/MyGame/Build Map Assets", priority = 20)]
        public static void Run()
        {
            var dirt = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Dirt.mat", new Color(0.55f, 0.38f, 0.24f));
            var grass = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Grass.mat", new Color(0.49f, 0.74f, 0.33f));
            var crumbling = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Crumbling.mat", new Color(0.86f, 0.70f, 0.45f));
            EditorAssetUtility.GetOrCreateMaterial(GrassBladeMaterialPath, GrassShaderName, new Color(0.64f, 0.86f, 0.36f), smoothness: 0f);
            EditorAssetUtility.GetOrCreateLitMaterial(LakeBedMaterialPath, new Color(0.78f, 0.70f, 0.50f));
            EditorAssetUtility.GetOrCreateLitMaterial(SubalpineTopMaterialPath, new Color(0.24f, 0.42f, 0.33f));
            EditorAssetUtility.GetOrCreateLitMaterial(DarkEarthMaterialPath, new Color(0.38f, 0.28f, 0.21f));
            EditorAssetUtility.GetOrCreateLitMaterial(SnowTopMaterialPath, new Color(0.90f, 0.93f, 0.98f), smoothness: 0.2f);
            EditorAssetUtility.GetOrCreateLitMaterial(IceTopMaterialPath, new Color(0.62f, 0.85f, 0.94f), smoothness: 0.85f);
            EditorAssetUtility.GetOrCreateLitMaterial(RockMaterialPath, new Color(0.47f, 0.47f, 0.50f));
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Sand.mat", new Color(0.90f, 0.81f, 0.58f));
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Sandstone.mat", new Color(0.78f, 0.64f, 0.44f));
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_WaterBed.mat", new Color(0.72f, 0.68f, 0.52f));
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_RockTop.mat", new Color(0.58f, 0.58f, 0.60f));
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_IcebergBody.mat", new Color(0.80f, 0.92f, 0.98f), smoothness: 0.6f);
            EditorAssetUtility.GetOrCreateMaterial(ForestGrassMaterialPath, GrassShaderName, new Color(0.33f, 0.52f, 0.33f), smoothness: 0f);
            EditorAssetUtility.GetOrCreateMaterial(DecorMaterialPath, VoxelLitShaderName, Color.white, smoothness: 0f);
            BuildSnowMaterial();
            BuildWaterMaterial(WaterMaterialPath, foamWidth: 0.06f, waveHeight: 0.02f);
            BuildWaterMaterial(PoolWaterMaterialPath, foamWidth: 0.025f, waveHeight: 0.008f);

            BuildTilePrefab(SolidPrefabPath, grass, dirt, isCrumbling: false);
            BuildTilePrefab(CrumblingPrefabPath, crumbling, dirt, isCrumbling: true);
            AssetDatabase.DeleteAsset(LegacyStartPrefabPath);
            AssetDatabase.DeleteAsset(LegacyStartMaterialPath);
            EnsureLevels();
            LegacyLayoutConverter.ConvertFolder(LevelFolder);

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

        /// <summary>Slab (top) and pillar (body) material of every skin, as asset paths.</summary>
        public static (Skin Skin, string Top, string Body)[] SkinMaterialPaths() => new[]
        {
            (Skin.Sand, $"{MaterialFolder}/Tile_Sand.mat", $"{MaterialFolder}/Tile_Sandstone.mat"),
            (Skin.Meadow, $"{MaterialFolder}/Tile_Grass.mat", $"{MaterialFolder}/Tile_Dirt.mat"),
            (Skin.Forest, SubalpineTopMaterialPath, DarkEarthMaterialPath),
            (Skin.Rock, $"{MaterialFolder}/Tile_RockTop.mat", RockMaterialPath),
            (Skin.Snow, SnowTopMaterialPath, RockMaterialPath),
            (Skin.Ice, IceTopMaterialPath, RockMaterialPath),
            (Skin.Water, $"{MaterialFolder}/Tile_WaterBed.mat", $"{MaterialFolder}/Tile_Sandstone.mat"),
            (Skin.Iceberg, IceTopMaterialPath, $"{MaterialFolder}/Tile_IcebergBody.mat"),
        };

        private static void BuildSnowMaterial()
        {
            var material = EditorAssetUtility.GetOrCreateMaterial(SnowMaterialPath, SnowShaderName, new Color(0.93f, 0.95f, 1f), smoothness: 0f);
            var changed = EditorAssetUtility.SetColorIfChanged(material, "_PressTint", new Color(0.72f, 0.80f, 0.95f));
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_PressDepth", 0.065f);
            if (changed)
            {
                EditorUtility.SetDirty(material);
            }
        }

        /// <summary>
        /// The lake, or a shallow-water pool: a pool is only 0.13 deep, so its foam band is narrower
        /// (the drifting band must stay shallower than the pool) and its waves are smaller.
        /// </summary>
        private static void BuildWaterMaterial(string path, float foamWidth, float waveHeight)
        {
            var material = EditorAssetUtility.GetOrCreateMaterial(path, WaterShaderName, Color.white, smoothness: 0f);
            var changed = EditorAssetUtility.SetColorIfChanged(material, "_ShallowColor", new Color(0.36f, 0.78f, 0.80f, 0.45f));
            changed |= EditorAssetUtility.SetColorIfChanged(material, "_DeepColor", new Color(0.12f, 0.38f, 0.62f, 0.92f));
            changed |= EditorAssetUtility.SetColorIfChanged(material, "_FoamColor", new Color(0.92f, 0.97f, 1f));
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_DepthFade", 0.8f);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_FoamWidth", foamWidth);
            changed |= EditorAssetUtility.SetFloatIfChanged(material, "_WaveHeight", waveHeight);
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
