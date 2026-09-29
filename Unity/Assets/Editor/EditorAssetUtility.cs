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
        /// </summary>
        public static Material GetOrCreateLitMaterial(string path, Color color, float smoothness = 0.1f)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var shader = Shader.Find(LitShaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
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

        public static void SetReference(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
