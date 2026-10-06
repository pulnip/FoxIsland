using MyGame.Map;
using MyGame.Puzzle;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Materials and prefabs for the puzzle elements (plan.md §4.10), built from cubes.</summary>
    public static class PuzzleAssetBuilder
    {
        public const string SwitchPrefabPath = PrefabFolder + "/FloorSwitch.prefab";
        public const string RisingMarkerPrefabPath = PrefabFolder + "/RisingMarker.prefab";
        public const string CratePrefabPath = PrefabFolder + "/Crate.prefab";
        public const string SpringboardPrefabPath = PrefabFolder + "/Springboard.prefab";
        public const string PlatformPrefabPath = PrefabFolder + "/MovingPlatform.prefab";

        private const string PrefabFolder = "Assets/Prefabs/Puzzle";
        private const string MaterialFolder = "Assets/Art/Materials";

        [MenuItem("Tools/MyGame/Build Puzzle Assets", priority = 21)]
        public static void Run()
        {
            var metal = Lit("Puzzle_Metal", new Color(0.30f, 0.32f, 0.36f), 0.3f);
            var groups = GroupMaterials();
            var wood = Lit("Puzzle_Wood", new Color(0.76f, 0.55f, 0.33f));
            var woodDark = Lit("Puzzle_WoodDark", new Color(0.48f, 0.32f, 0.19f));
            var spring = Lit("Puzzle_Spring", new Color(0.78f, 0.80f, 0.84f), 0.5f);
            var pad = Lit("Puzzle_Pad", new Color(1f, 0.58f, 0.16f));
            var platform = Lit("Puzzle_Platform", new Color(0.56f, 0.62f, 0.70f));
            var platformTop = Lit("Puzzle_PlatformTop", new Color(0.86f, 0.89f, 0.93f));

            BuildSwitch(metal, groups[0]);
            BuildRisingMarker(groups[0]);
            BuildCrate(wood, woodDark);
            BuildSpringboard(metal, spring, pad);
            BuildPlatform(metal, platform, platformTop);
            AssetDatabase.SaveAssets();
            Debug.Log("[PuzzleAssetBuilder] Puzzle assets built.");
        }

        /// <summary>One material per <see cref="PuzzleGroup"/>, in order.</summary>
        public static Material[] GroupMaterials()
        {
            var materials = new Material[PuzzleGroups.Count];
            for (var i = 0; i < materials.Length; i++)
            {
                var group = (PuzzleGroup)i;
                materials[i] = Lit($"Puzzle_Group{group}", PuzzleGroups.ColorOf(group), 0.3f);
            }

            return materials;
        }

        private static Material Lit(string name, Color color, float smoothness = 0.1f) =>
            EditorAssetUtility.GetOrCreateLitMaterial($"{MaterialFolder}/{name}.mat", color, smoothness);

        private static GameObject Box(string name, Transform parent, Material material, Vector3 center, Vector3 size)
        {
            var box = EditorAssetUtility.CreateVisual(PrimitiveType.Cube, name, parent, material);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            return box;
        }

        private static void BuildSwitch(Material metal, Material button)
        {
            var root = new GameObject("FloorSwitch");
            var floorSwitch = root.AddComponent<FloorSwitch>();
            Box("Base", root.transform, metal, new Vector3(0f, 0.015f, 0f), new Vector3(1f, 0.03f, 1f));
            var top = Box("Button", root.transform, button, new Vector3(0f, 0.06f, 0f), new Vector3(0.7f, 0.06f, 0.7f));
            EditorAssetUtility.SetReference(floorSwitch, "_button", top.transform);
            EditorAssetUtility.SavePrefab(root, SwitchPrefabPath);
        }

        /// <summary>A frame along the top edges of a rising block, in its group's color.</summary>
        private static void BuildRisingMarker(Material material)
        {
            const float Edge = 0.68f;
            const float Length = 1.46f;
            const float Width = 0.1f;
            const float Height = 0.05f;
            var root = new GameObject("RisingMarker");
            Box("North", root.transform, material, new Vector3(0f, Height * 0.5f, Edge), new Vector3(Length, Height, Width));
            Box("South", root.transform, material, new Vector3(0f, Height * 0.5f, -Edge), new Vector3(Length, Height, Width));
            Box("East", root.transform, material, new Vector3(Edge, Height * 0.5f, 0f), new Vector3(Width, Height, Length));
            Box("West", root.transform, material, new Vector3(-Edge, Height * 0.5f, 0f), new Vector3(Width, Height, Length));
            EditorAssetUtility.SavePrefab(root, RisingMarkerPrefabPath);
        }

        private static void BuildCrate(Material wood, Material woodDark)
        {
            const float Size = 1.44f;
            var height = Crate.Height;
            var root = new GameObject("Crate");
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.size = new Vector3(Size, height, Size);
            root.AddComponent<Crate>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Box("Body", visual, wood, new Vector3(0f, height * 0.5f, 0f), new Vector3(Size - 0.1f, height - 0.08f, Size - 0.1f));
            Box("RimTop", visual, woodDark, new Vector3(0f, height - 0.04f, 0f), new Vector3(Size - 0.02f, 0.08f, Size - 0.02f));
            Box("RimBottom", visual, woodDark, new Vector3(0f, 0.04f, 0f), new Vector3(Size - 0.02f, 0.08f, Size - 0.02f));
            var post = Size * 0.5f - 0.07f;
            foreach (var (name, x, z) in new[] { ("PostNE", 1f, 1f), ("PostSE", 1f, -1f), ("PostNW", -1f, 1f), ("PostSW", -1f, -1f) })
            {
                Box(name, visual, woodDark, new Vector3(x * post, height * 0.5f, z * post), new Vector3(0.12f, height, 0.12f));
            }

            Box("Plank", visual, woodDark, new Vector3(0f, height * 0.5f, 0f), new Vector3(Size - 0.06f, 0.05f, Size - 0.06f));
            EditorAssetUtility.SavePrefab(root, CratePrefabPath);
        }

        private static void BuildSpringboard(Material metal, Material spring, Material pad)
        {
            var root = new GameObject("Springboard");
            var springboard = root.AddComponent<Springboard>();
            Box("Base", root.transform, metal, new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.04f, 0.95f));
            var coil = Box("Spring", root.transform, spring, new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.1f, 0.36f));
            var top = Box("Pad", root.transform, pad, new Vector3(0f, 0.17f, 0f), new Vector3(0.8f, 0.06f, 0.8f));
            EditorAssetUtility.SetReference(springboard, "_pad", top.transform);
            EditorAssetUtility.SetReference(springboard, "_spring", coil.transform);
            EditorAssetUtility.SavePrefab(root, SpringboardPrefabPath);
        }

        /// <summary>A slab whose top is the pivot, so the platform's position is the floor height.</summary>
        private static void BuildPlatform(Material metal, Material body, Material top)
        {
            const float Size = 1.44f;
            const float Thickness = 0.3f;
            var root = new GameObject("MovingPlatform");
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -Thickness * 0.5f, 0f);
            collider.size = new Vector3(Size, Thickness, Size);
            root.AddComponent<MovingPlatform>();

            Box("Top", root.transform, top, new Vector3(0f, -0.015f, 0f), new Vector3(Size, 0.03f, Size));
            Box("Slab", root.transform, body, new Vector3(0f, -0.14f, 0f), new Vector3(Size - 0.04f, 0.22f, Size - 0.04f));
            Box("Keel", root.transform, metal, new Vector3(0f, -0.27f, 0f), new Vector3(Size - 0.4f, 0.06f, Size - 0.4f));
            EditorAssetUtility.SavePrefab(root, PlatformPrefabPath);
        }
    }
}
