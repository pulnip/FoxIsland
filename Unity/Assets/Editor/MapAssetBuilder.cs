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

        private const string MaterialFolder = "Assets/Art/Materials";

        [MenuItem("Tools/MyGame/Build Map Assets", priority = 20)]
        public static void Run()
        {
            var dirt = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Dirt.mat", new Color(0.55f, 0.38f, 0.24f));
            var grass = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Grass.mat", new Color(0.49f, 0.74f, 0.33f));
            var crumbling = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Crumbling.mat", new Color(0.86f, 0.70f, 0.45f));
            var start = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Tile_Start.mat", new Color(0.42f, 0.62f, 0.86f));

            BuildTilePrefab(SolidPrefabPath, grass, dirt);
            BuildTilePrefab(CrumblingPrefabPath, crumbling, dirt);
            BuildTilePrefab(StartPrefabPath, start, dirt);
            EnsureLayout();

            AssetDatabase.SaveAssets();
            Debug.Log("[MapAssetBuilder] Map assets built.");
        }

        private static void BuildTilePrefab(string path, Material topMaterial, Material bodyMaterial)
        {
            var root = new GameObject(Path.GetFileNameWithoutExtension(path));
            var tile = root.AddComponent<Tile>();
            var top = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Top", root.transform, topMaterial);
            var body = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Body", root.transform, bodyMaterial);
            EditorAssetUtility.SetReference(tile, "_top", top.transform);
            EditorAssetUtility.SetReference(tile, "_body", body.transform);
            tile.ApplyLevel(0);
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
