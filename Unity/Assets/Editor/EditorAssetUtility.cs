using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    internal static class EditorAssetUtility
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string ParticlesLitShaderName = "Universal Render Pipeline/Particles/Simple Lit";

        private const string SmoothnessProperty = "_Smoothness";

        private static readonly string[] ColorProperties = { "_BaseColor", "_Color" };

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>
        /// Loads the material at <paramref name="path"/> or creates it, keeping its GUID either way.
        /// An existing material that already matches is left untouched: URP rewrites the legacy
        /// <c>_Color</c> with float round-off, so re-saving it would churn the file on every build.
        /// </summary>
        public static Material GetOrCreateLitMaterial(string path, Color color, float smoothness = 0.1f) =>
            GetOrCreateMaterial(path, LitShaderName, color, smoothness);

        /// <summary>
        /// Like <see cref="GetOrCreateLitMaterial"/> for any shader with <c>_BaseColor</c> and
        /// <c>_Smoothness</c>; <c>_Color</c> is kept in sync only where the shader has it.
        /// </summary>
        public static Material GetOrCreateMaterial(string path, string shaderName, Color color, float smoothness)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var shader = Shader.Find(shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader == shader && Matches(material, color, smoothness))
            {
                return material;
            }

            material.shader = shader;
            foreach (var property in ColorProperties)
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, color);
                }
            }

            if (material.HasProperty(SmoothnessProperty))
            {
                material.SetFloat(SmoothnessProperty, smoothness);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool Matches(Material material, Color color, float smoothness)
        {
            foreach (var property in ColorProperties)
            {
                if (material.HasProperty(property) && !IsApproximately(material.GetColor(property), color))
                {
                    return false;
                }
            }

            return !material.HasProperty(SmoothnessProperty)
                   || Mathf.Approximately(material.GetFloat(SmoothnessProperty), smoothness);
        }

        private static bool IsApproximately(Color a, Color b)
        {
            const float Tolerance = 1e-4f;
            return Mathf.Abs(a.r - b.r) < Tolerance
                   && Mathf.Abs(a.g - b.g) < Tolerance
                   && Mathf.Abs(a.b - b.b) < Tolerance
                   && Mathf.Abs(a.a - b.a) < Tolerance;
        }

        public static GameObject CreateVisual(PrimitiveType type, string name, Transform parent, Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(parent, false);
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        /// <summary>
        /// Saves <paramref name="root"/> as a prefab (overwriting in place, so the GUID survives)
        /// and destroys the temporary scene instance.
        /// </summary>
        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component =>
            gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();

        /// <summary>
        /// Overwrites every serialized field of <paramref name="component"/> with its code defaults,
        /// so scene components follow the script rather than values saved by an older build.
        /// </summary>
        public static void ResetToDefaults<T>(T component) where T : MonoBehaviour
        {
            var fresh = new GameObject("DefaultsSource").AddComponent<T>();
            var source = new SerializedObject(fresh);
            var target = new SerializedObject(component);
            var property = source.GetIterator();
            property.NextVisible(true);
            do
            {
                if (property.name != "m_Script")
                {
                    target.CopyFromSerializedProperty(property);
                }
            }
            while (property.NextVisible(false));

            target.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(fresh.gameObject);
        }

        public static void SetFloat(Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
