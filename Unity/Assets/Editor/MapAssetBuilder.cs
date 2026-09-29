using System.IO;
using MyGame.Map;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class MapAssetBuilder
    {
        public const string LayoutPath = "Assets/ScriptableObjects/MapLayout_Main.asset";
        public const string SolidPrefabPath = "Assets/Prefabs/Tiles/Tile_Solid.prefab";
        public const string CrumblingPrefabPath = "Assets/Prefabs/Tiles/Tile_Crumbling.prefab";
        public const string StartPrefabPath = "Assets/Prefabs/Tiles/Tile_Start.prefab";
        public const string GrassBladeMaterialPath = "Assets/Art/Materials/Grass_Blade.mat";

        private const string MaterialFolder = "Assets/Art/Materials";
        private const string GrassShaderName = "MyGame/Grass";

        [MenuItem("Tools/MyGame/Build Map Assets", priority = 20)]
        public static void Run()
        {
            var dirt = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Dirt.mat", new Color(0.55f, 0.38f, 0.24f));
            var grass = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Grass.mat", new Color(0.49f, 0.74f, 0.33f));
            var crumbling = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Crumbling.mat", new Color(0.86f, 0.70f, 0.45f));
            var start = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Start.mat", new Color(0.42f, 0.62f, 0.86f));
            EditorAssetUtility.GetOrCreateMaterial(GrassBladeMaterialPath, GrassShaderName, new Color(0.64f, 0.86f, 0.36f), smoothness: 0f);

            BuildTilePrefab(SolidPrefabPath, grass, dirt, isCrumbling: false);
            BuildTilePrefab(CrumblingPrefabPath, crumbling, dirt, isCrumbling: true);
            BuildTilePrefab(StartPrefabPath, start, dirt, isCrumbling: false);
            EnsureLayout();

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
            }

            EditorAssetUtility.SavePrefab(root, path);
        }

        /// <summary>
        /// Creates the layout only when missing, so hand-tuned layouts are never overwritten.
        /// </summary>
        private static void EnsureLayout()
        {
            if (AssetDatabase.LoadAssetAtPath<MapLayout>(LayoutPath) != null)
            {
                return;
            }

            EditorAssetUtility.EnsureFolder(Path.GetDirectoryName(LayoutPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MapLayout>(), LayoutPath);
        }
    }
}
