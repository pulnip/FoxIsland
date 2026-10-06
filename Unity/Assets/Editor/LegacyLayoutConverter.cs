using System;
using System.Linq;
using MyGame.Map;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Rewrites layouts saved before blocks had skins (plan.md §4.3): <c>0</c>–<c>2</c> become meadow,
    /// <c>3</c>–<c>4</c> forest, <c>5</c>–<c>6</c> snow (<c>5i</c>/<c>6i</c> stay ice), <c>C</c> a
    /// crumbling sand block, and <c>S</c> a meadow block that becomes the layout's start cell.
    /// Layouts already in the new notation are left alone.
    /// </summary>
    internal static class LegacyLayoutConverter
    {
        public static void ConvertFolder(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MapLayout", new[] { folder }))
            {
                var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(AssetDatabase.GUIDToAssetPath(guid));
                if (layout != null && TryConvert(layout))
                {
                    EditorUtility.SetDirty(layout);
                    AssetDatabase.SaveAssetIfDirty(layout);
                    Debug.Log($"[LegacyLayoutConverter] Converted {layout.name} to height + skin tokens.", layout);
                }
            }
        }

        private static bool TryConvert(MapLayout layout)
        {
            var rows = layout.Rows.ToArray();
            var tokens = rows.Select(row => row.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();
            if (tokens.All(row => row.All(token => CellSpec.TryParse(token, out _))))
            {
                return false;
            }

            var start = layout.Start;
            for (var i = 0; i < tokens.Length; i++)
            {
                for (var column = 0; column < tokens[i].Length; column++)
                {
                    var token = tokens[i][column];
                    if (token == "S")
                    {
                        start = new Vector2Int(column, tokens.Length - 1 - i);
                    }

                    tokens[i][column] = Convert(token);
                }

                rows[i] = string.Join(" ", tokens[i]);
            }

            layout.SetRows(rows);
            layout.Start = start;
            return true;
        }

        private static string Convert(string token)
        {
            switch (token)
            {
                case ".":
                    return ".";
                case "C":
                    return "0sc";
                case "S":
                    return "0m";
                case "5i":
                case "6i":
                    return token;
            }

            if (token.Length == 1 && token[0] >= '0' && token[0] <= '6')
            {
                var height = token[0] - '0';
                var skin = height <= 2 ? 'm' : height <= 4 ? 'f' : 'n';
                return $"{token[0]}{skin}";
            }

            return token;
        }
    }
}
