using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// One-shot voxel particle bursts: small cubes tinted by the particle start color, so every
    /// effect shares one white material.
    /// </summary>
    public static class EffectAssetBuilder
    {
        public const string PickupBurstPath = "Assets/Prefabs/Effects/PickupBurst.prefab";
        public const string CrumbleDustPath = "Assets/Prefabs/Effects/CrumbleDust.prefab";
        public const string FootprintsPath = "Assets/Prefabs/Effects/Footprints.prefab";

        private const string MaterialPath = "Assets/Art/Materials/Effect_Voxel.mat";

        [MenuItem("Tools/MyGame/Build Effect Assets", priority = 19)]
        public static void Run()
        {
            var material = EditorAssetUtility.GetOrCreateMaterial(
                MaterialPath, EditorAssetUtility.ParticlesLitShaderName, Color.white, smoothness: 0.2f);

            BuildBurst(PickupBurstPath, material, new BurstSettings
            {
                Color = new Color(1f, 0.78f, 0.12f),
                Count = 18,
                Lifetime = new Vector2(0.35f, 0.6f),
                Speed = new Vector2(2f, 3.5f),
                Size = new Vector2(0.08f, 0.14f),
                Gravity = 0.8f,
                Shape = ParticleSystemShapeType.Sphere,
                ShapeScale = Vector3.one * 0.2f,
            });

            BuildBurst(CrumbleDustPath, material, new BurstSettings
            {
                Color = new Color(0.86f, 0.70f, 0.45f),
                Count = 18,
                Lifetime = new Vector2(0.5f, 0.9f),
                Speed = new Vector2(0.4f, 1.4f),
                Size = new Vector2(0.06f, 0.14f),
                Gravity = 1.2f,
                Shape = ParticleSystemShapeType.Box,
                ShapeScale = new Vector3(1.3f, 0.1f, 1.3f),
            });

            BuildFootprints(material);

            AssetDatabase.SaveAssets();
            Debug.Log("[EffectAssetBuilder] Effect assets built.");
        }

        /// <summary>
        /// An always-running, non-emitting system that <c>FootprintEmitter</c> feeds one print at a
        /// time: flat dark voxels that hold, then shrink away. Shrinking instead of fading keeps the
        /// material opaque.
        /// </summary>
        private static void BuildFootprints(Material material)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(FootprintsPath));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = 0.07f;
            main.startSizeY = 0.012f;
            main.startSizeZ = 0.08f;
            main.startRotation3D = true;
            main.startColor = new Color(0.30f, 0.22f, 0.14f);
            main.gravityModifier = 0f;

            var emission = particles.emission;
            emission.enabled = false;

            var shape = particles.shape;
            shape.enabled = false;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var holdThenShrink = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, holdThenShrink);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.alignment = ParticleSystemRenderSpace.World;

            EditorAssetUtility.SavePrefab(root, FootprintsPath);
        }

        private static void BuildBurst(string path, Material material, BurstSettings settings)
        {
            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = settings.Lifetime.y;
            main.loop = false;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(settings.Lifetime.x, settings.Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(settings.Speed.x, settings.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(settings.Size.x, settings.Size.y);
            main.startColor = settings.Color;
            main.gravityModifier = settings.Gravity;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)settings.Count) });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = settings.Shape;
            shape.scale = settings.ShapeScale;
            shape.radius = 1f;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var rotationOverLifetime = particles.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.alignment = ParticleSystemRenderSpace.Local;

            EditorAssetUtility.SavePrefab(root, path);
        }

        private struct BurstSettings
        {
            public Color Color;
            public int Count;
            public Vector2 Lifetime;
            public Vector2 Speed;
            public Vector2 Size;
            public float Gravity;
            public ParticleSystemShapeType Shape;
            public Vector3 ShapeScale;
        }
    }
}
