using System.IO;
using MyGame.Core;
using MyGame.Items;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class ItemAssetBuilder
    {
        public const string ItemPrefabPath = "Assets/Prefabs/Item.prefab";
        public const string GameConfigPath = "Assets/ScriptableObjects/GameConfig.asset";

        private const string MaterialFolder = "Assets/Art/Materials";
        private const float TriggerRadius = 0.3f;
        private const float GemSize = 0.34f;

        [MenuItem("Tools/MyGame/Build Item Assets", priority = 23)]
        public static void Run()
        {
            var gemMaterial = EditorAssetUtility.GetOrCreateLitMaterial(
                $"{MaterialFolder}/Item_Gem.mat", new Color(1f, 0.78f, 0.12f), smoothness: 0.7f);

            var root = new GameObject("Item");
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;
            var item = root.AddComponent<Item>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var gem = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Gem", visual, gemMaterial);
            gem.transform.localRotation = Quaternion.FromToRotation(Vector3.one, Vector3.up);
            gem.transform.localScale = Vector3.one * GemSize;
            EditorAssetUtility.SetReference(item, "_visual", visual);

            EditorAssetUtility.SavePrefab(root, ItemPrefabPath);
            EnsureGameConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[ItemAssetBuilder] Item assets built.");
        }

        /// <summary>
        /// Creates the config only when missing, so tuned values are never overwritten.
        /// </summary>
        private static void EnsureGameConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath) != null)
            {
                return;
            }

            EditorAssetUtility.EnsureFolder(Path.GetDirectoryName(GameConfigPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), GameConfigPath);
        }
    }
}
