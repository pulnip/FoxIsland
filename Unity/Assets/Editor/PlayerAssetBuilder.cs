using MyGame.Player;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class PlayerAssetBuilder
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        private const string MaterialFolder = "Assets/Art/Materials";
        private const float Height = 0.6f;
        private const float Radius = 0.2f;

        [MenuItem("Tools/MyGame/Build Player Prefab", priority = 21)]
        public static void Run()
        {
            var bodyMaterial = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Player_Body.mat", new Color(0.93f, 0.47f, 0.13f));
            var noseMaterial = EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/Player_Nose.mat", new Color(0.15f, 0.12f, 0.10f));

            var root = new GameObject("Player") { tag = "Player" };
            root.AddComponent<PlayerController>();

            var controller = root.GetComponent<CharacterController>();
            controller.height = Height;
            controller.radius = Radius;
            controller.center = new Vector3(0f, Height * 0.5f, 0f);
            controller.skinWidth = 0.02f;
            controller.stepOffset = 0.1f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;

            var body = EditorAssetUtility.CreateVisual(PrimitiveType.Capsule, "Body", root.transform, bodyMaterial);
            body.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);
            body.transform.localScale = new Vector3(Radius * 2f, Height * 0.5f, Radius * 2f);

            var nose = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, "Nose", root.transform, noseMaterial);
            nose.transform.localPosition = new Vector3(0f, Height * 0.7f, Radius);
            nose.transform.localScale = Vector3.one * 0.12f;

            EditorAssetUtility.SavePrefab(root, PlayerPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAssetBuilder] Player prefab built.");
        }
    }
}
