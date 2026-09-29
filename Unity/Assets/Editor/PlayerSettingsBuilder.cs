using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Player settings owned by the project: the product name and the default app icon, from which
    /// Unity derives every platform size it needs.
    /// </summary>
    public static class PlayerSettingsBuilder
    {
        public const string ProductName = "Fox Island";
        public const string AppIconPath = "Assets/Art/Icons/AppIcon.png";

        private const int IconMaxSize = 1024;

        [MenuItem("Tools/MyGame/Build Player Settings", priority = 25)]
        public static void Run()
        {
            if (PlayerSettings.productName != ProductName)
            {
                PlayerSettings.productName = ProductName;
                AssetDatabase.SaveAssets();
            }

            ConfigureIconImporter();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            var current = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (current.Length != 1 || current[0] != icon)
            {
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("[PlayerSettingsBuilder] Player settings built.");
        }

        /// <summary>
        /// Uncompressed, no mipmaps, capped at 1024: the source image is 1254 px and Unity scales the
        /// icon down per platform anyway. Reimports only when a setting differs.
        /// </summary>
        private static void ConfigureIconImporter()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AppIconPath);
            var matches = importer.textureType == TextureImporterType.Default
                          && !importer.mipmapEnabled
                          && importer.alphaIsTransparency
                          && importer.npotScale == TextureImporterNPOTScale.None
                          && importer.maxTextureSize == IconMaxSize
                          && importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (matches)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = IconMaxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
