#nullable enable

using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.AssetManagement
{
    /// <summary>
    /// AssetDescription の EstimatedMemoryBytes を Editor から概算するツール（Track 2 拡張点）。
    /// </summary>
    public static class AssetMemoryEstimator
    {
        [MenuItem("OneStarMaker/Asset Management/Estimate Selected Asset Memory")]
        private static void EstimateSelected()
        {
            var selection = Selection.activeObject;
            if (selection == null)
            {
                Debug.LogWarning("[AssetMemoryEstimator] アセットが選択されていません。");
                return;
            }

            var path = AssetDatabase.GetAssetPath(selection);
            var importer = AssetImporter.GetAtPath(path);
            Debug.Log($"[AssetMemoryEstimator] Selected: {path}, Importer: {importer?.GetType().Name ?? "none"}");
        }
    }
}
