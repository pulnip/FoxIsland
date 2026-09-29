using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Imports character FBX materials as matte URP Lit using the FBX diffuse color unchanged,
    /// so the Blender palette renders like the builder-made tile materials.
    /// </summary>
    public class CharacterMaterialPostprocessor : AssetPostprocessor
    {
        private const string CharacterFolder = "Assets/Art/Character/";
        private const float Smoothness = 0.1f;

        public override uint GetVersion() => 1;

        public override int GetPostprocessOrder() => 100;

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (!assetPath.StartsWith(CharacterFolder))
            {
                return;
            }

            var color = description.TryGetProperty("DiffuseColor", out Vector4 diffuse)
                ? new Color(diffuse.x, diffuse.y, diffuse.z, 1f)
                : Color.white;
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", Smoothness);
            material.SetFloat("_Metallic", 0f);
        }
    }
}
