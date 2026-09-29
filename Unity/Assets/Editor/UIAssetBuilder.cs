using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MyGame.EditorTools
{
    public static class UIAssetBuilder
    {
        public const string PanelSettingsPath = "Assets/UI/GamePanelSettings.asset";
        public const string GameUIPath = "Assets/UI/GameUI.uxml";

        private const string ThemePath = "Assets/UI/GameTheme.tss";

        [MenuItem("Tools/MyGame/Build UI Assets", priority = 22)]
        public static void Run()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);

            AssetDatabase.SaveAssets();
            Debug.Log("[UIAssetBuilder] UI assets built.");
        }
    }
}
