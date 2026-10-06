using MyGame.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static class PlayerAssetBuilder
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        private const float Height = 0.6f;
        private const float Radius = 0.2f;

        [MenuItem("Tools/MyGame/Build Player Prefab", priority = 22)]
        public static void Run()
        {
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

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterAssetBuilder.FoxModelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);

            var animator = EditorAssetUtility.GetOrAddComponent<Animator>(model);
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAssetBuilder.AnimatorControllerPath);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var playerAnimator = root.AddComponent<PlayerAnimator>();
            EditorAssetUtility.SetReference(playerAnimator, "_animator", animator);

            var footprintsPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectAssetBuilder.FootprintsPath);
            var footprints = (ParticleSystem)PrefabUtility.InstantiatePrefab(footprintsPrefab);
            footprints.transform.SetParent(root.transform, false);
            var footprintEmitter = model.AddComponent<FootprintEmitter>();
            EditorAssetUtility.SetReference(footprintEmitter, "_player", root.GetComponent<PlayerController>());
            EditorAssetUtility.SetReference(footprintEmitter, "_footprints", footprints);
            EditorAssetUtility.SetReference(footprintEmitter, "_stepSplash",
                AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectAssetBuilder.StepSplashPath));

            EditorAssetUtility.SavePrefab(root, PlayerPrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAssetBuilder] Player prefab built.");
        }
    }
}
