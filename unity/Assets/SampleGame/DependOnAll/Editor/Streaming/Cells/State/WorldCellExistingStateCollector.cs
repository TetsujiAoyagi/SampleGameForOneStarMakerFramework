#nullable enable

using System.Collections.Generic;
using System.IO;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Generation;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.Streaming.Cells.State
{
    /// <summary>Cells 直下の identity フォルダから既存状態を収集する。</summary>
    public static class WorldCellExistingStateCollector
    {
        private const string CellsRootFolder = "Assets/SampleGame/InGame/InGameSession/World/Cells";
        private const string WorldScenePath = "Assets/SampleGame/InGame/InGameSession/World/World.unity";

        public static IReadOnlyList<CellExistingState> Collect(
            IReadOnlyList<WorldCellGenerationTarget> targets)
            => Collect(targets, CellsRootFolder, WorldScenePath);

        /// <summary>
        /// テスト用に走査ルートと復帰先シーンを差し替えて収集する。
        /// targets は正規 identity 列の重複検証に使い、範囲外フォルダも削除計画用に収集する。
        /// </summary>
        public static IReadOnlyList<CellExistingState> Collect(
            IReadOnlyList<WorldCellGenerationTarget> targets,
            string cellsRootFolder,
            string worldScenePath)
        {
            if (targets == null)
            {
                throw new System.ArgumentNullException(nameof(targets));
            }

            if (string.IsNullOrWhiteSpace(cellsRootFolder))
            {
                throw new System.ArgumentException("Cells ルートが未指定です。", nameof(cellsRootFolder));
            }

            if (string.IsNullOrWhiteSpace(worldScenePath))
            {
                throw new System.ArgumentException("復帰先シーンが未指定です。", nameof(worldScenePath));
            }

            WorldCellGenerationTarget.Validate(targets);
            var result = new List<CellExistingState>();
            if (AssetDatabase.IsValidFolder(cellsRootFolder))
            {
                var subFolders = AssetDatabase.GetSubFolders(cellsRootFolder);
                for (var i = 0; i < subFolders.Length; i++)
                {
                    var folder = subFolders[i].Replace('\\', '/');
                    var folderName = Path.GetFileName(folder);
                    var cellScenePath = $"{folder}/{folderName}.unity";
                    var hasCellRoot = AssetDatabase.LoadAssetAtPath<SceneAsset>(cellScenePath) != null
                        && SceneHasAuthoredRoot(cellScenePath, DemoCellScene.AuthoredRootName);

                    var hasEnvironmentScene = false;
                    var hasEnvironmentAuthoredRoot = false;
                    var environmentIdentities = new HashSet<string>(System.StringComparer.Ordinal);
                    var resourcesByIdentity = new Dictionary<string, SceneResource>(System.StringComparer.Ordinal);
                    var cellResource = AssetDatabase.LoadAssetAtPath<SceneResource>($"{folder}/{folderName}.asset");
                    if (cellResource != null)
                    {
                        foreach (var child in cellResource.Children)
                        {
                            if (child != null && !string.Equals(child.Identity, folderName, System.StringComparison.Ordinal))
                            {
                                environmentIdentities.Add(child.Identity);
                            }
                        }
                    }

                    var resourceGuids = AssetDatabase.FindAssets("t:SceneResource", new[] { folder });
                    for (var r = 0; r < resourceGuids.Length; r++)
                    {
                        var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(
                            AssetDatabase.GUIDToAssetPath(resourceGuids[r]));
                        if (resource != null
                            && !string.Equals(resource.Identity, folderName, System.StringComparison.Ordinal))
                        {
                            environmentIdentities.Add(resource.Identity);
                            resourcesByIdentity.TryAdd(resource.Identity, resource);
                        }
                    }

                    foreach (var environmentIdentity in environmentIdentities)
                    {
                        hasEnvironmentScene = true;
                        var environmentResource = FindResourceByIdentity(
                            cellResource, resourcesByIdentity, environmentIdentity);
                        if (environmentResource != null
                            && TryGetScenePath(environmentResource, folder, out var environmentScenePath)
                            && SceneHasAuthoredRoot(environmentScenePath, EnvironmentScene.AuthoredRootName))
                        {
                            hasEnvironmentAuthoredRoot = true;
                        }
                    }

                    result.Add(new CellExistingState(
                        folderName,
                        hasCellRoot,
                        hasEnvironmentScene,
                        hasEnvironmentAuthoredRoot));
                }
            }

            EditorSceneManager.OpenScene(worldScenePath, OpenSceneMode.Single);
            return result;
        }
        // 範囲外フォルダも削除計画のため収集する。target は正本 identity 集合を検証する。
        private static SceneResource? FindResourceByIdentity(
            SceneResource? cellResource,
            IReadOnlyDictionary<string, SceneResource> resourcesByIdentity,
            string identity)
        {
            if (cellResource != null)
            {
                foreach (var child in cellResource.Children)
                {
                    if (child != null && string.Equals(child.Identity, identity, System.StringComparison.Ordinal))
                    {
                        return child;
                    }
                }
            }

            resourcesByIdentity.TryGetValue(identity, out var resourceByIdentity);
            return resourceByIdentity;
        }
        private static bool TryGetScenePath(
            SceneResource resource,
            string folder,
            out string scenePath)
        {
            var payloads = resource.GetPayloads();
            for (var i = 0; i < payloads.Count; i++)
            {
                var payload = payloads[i];
                if (payload == null || payload.Reference == null || string.IsNullOrEmpty(payload.Reference.AssetGUID))
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(payload.Reference.AssetGUID);
                if (!string.IsNullOrEmpty(path)
                    && path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                {
                    scenePath = path;
                    return true;
                }
            }

            // 候補 resource に payload が無くても、同じ identity の対応 scene だけを解決する。
            if (!string.IsNullOrWhiteSpace(resource.Identity)
                && resource.Identity.IndexOf('/') < 0
                && resource.Identity.IndexOf('\\') < 0)
            {
                var fallbackPath = $"{folder}/{resource.Identity}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(fallbackPath) != null)
                {
                    scenePath = fallbackPath;
                    return true;
                }
            }

            scenePath = string.Empty;
            return false;
        }

        private static bool SceneHasAuthoredRoot(string scenePath, string authoredRootName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root != null && root.name == authoredRootName)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
