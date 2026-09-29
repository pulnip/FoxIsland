using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    internal static class EditorAssetUtility
    {
        private const string LitShaderName = "Universal Render Pipeline/Lit";

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
        public static Material GetOrCreateLitMaterial(string path, Color color, float smoothness = 0.1f, Color emission = default)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var shader = Shader.Find(LitShaderName);
            var isEmissive = emission.maxColorComponent > 0f;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader == shader
                     && IsApproximately(material.GetColor("_BaseColor"), color)
                     && IsApproximately(material.GetColor("_Color"), color)
                     && Mathf.Approximately(material.GetFloat("_Smoothness"), smoothness)
                     && material.IsKeywordEnabled("_EMISSION") == isEmissive
                     && (!isEmissive || IsApproximately(material.GetColor("_EmissionColor"), emission)))
            {
                return material;
            }

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            if (isEmissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
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
