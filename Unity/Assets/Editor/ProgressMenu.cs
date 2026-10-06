using System.IO;
using MyGame.Core;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Editor shortcuts for the saved progress (plan.md §8.2).</summary>
    internal static class ProgressMenu
    {
        [MenuItem("Tools/MyGame/Reset Progress", priority = 60)]
        public static void ResetProgress()
        {
            if (!EditorUtility.DisplayDialog("Reset Progress", $"Delete the saved clears and best times?\n{PlayerProgress.DefaultPath}", "Delete", "Cancel"))
            {
                return;
            }

            File.Delete(PlayerProgress.DefaultPath);
            Debug.Log($"[ProgressMenu] Deleted {PlayerProgress.DefaultPath}.");
        }

        [MenuItem("Tools/MyGame/Show Progress File", priority = 61)]
        public static void ShowProgressFile() => EditorUtility.RevealInFinder(PlayerProgress.DefaultPath);
    }
}
