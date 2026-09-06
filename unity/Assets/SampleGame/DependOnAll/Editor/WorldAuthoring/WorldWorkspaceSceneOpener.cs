#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal sealed class WorldWorkspaceOpenResolution
    {
        internal List<string> ScenePaths { get; } = new();
        internal List<string> MissingRequired { get; } = new();
        internal List<WorldWorkspaceOpenItem> MissingOptional { get; } = new();
        internal bool CanOpen => MissingRequired.Count == 0;
    }

    internal static class WorldWorkspaceSceneOpener
    {
        internal static WorldWorkspaceOpenResolution Resolve(
            SceneResourceMap map,
            IReadOnlyList<WorldWorkspaceOpenItem> plan)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var result = new WorldWorkspaceOpenResolution();
            for (var i = 0; i < plan.Count; i++)
            {
                var item = plan[i];
                var resource = map.GetSceneResource(item.Identity);
                var path = resource == null ? string.Empty : ResolveExactPayloadPath(resource, item.Variant);
                if (!string.IsNullOrEmpty(path))
                {
                    result.ScenePaths.Add(path);
                }
                else if (item.Required)
                {
                    result.MissingRequired.Add($"{item.Identity} [Variant='{item.Variant}']");
                }
                else
                {
                    result.MissingOptional.Add(item);
                }
            }
            return result;
        }

        internal static bool Open(SceneResourceMap map, WorldWorkspaceSelection selection, out string message)
            => Open(map, selection, confirmSave: true, out message);

        internal static bool OpenAfterCreation(SceneResourceMap map, WorldWorkspaceSelection selection, out string message)
            => Open(map, selection, confirmSave: false, out message);

        private static bool Open(
            SceneResourceMap map,
            WorldWorkspaceSelection selection,
            bool confirmSave,
            out string message)
        {
            if (WorldCompanionRecoveryService.IsBlocked(out message)) return false;
            var resolution = Resolve(map, selection.BuildOpenPlan());
            if (!resolution.CanOpen)
            {
                message = "Required scenes are missing:\n" + string.Join("\n", resolution.MissingRequired);
                return false;
            }
            if (confirmSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                message = "Open cancelled.";
                return false;
            }
            try
            {
                for (var i = 0; i < resolution.ScenePaths.Count; i++)
                {
                    EditorSceneManager.OpenScene(
                        resolution.ScenePaths[i],
                        i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                }
                message = resolution.MissingOptional.Count == 0
                    ? "Workspace opened."
                    : $"Workspace opened; {resolution.MissingOptional.Count} optional companion(s) are missing.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Open stopped after Editor failure: {ex.Message}";
                return false;
            }
        }

        private static string ResolveExactPayloadPath(SceneResource resource, string variant)
        {
            var payloads = resource.GetPayloads();
            for (var i = 0; i < payloads.Count; i++)
            {
                var payload = payloads[i];
                if (payload != null
                    && string.Equals(payload.Variant, variant, StringComparison.Ordinal)
                    && payload.Reference != null
                    && !string.IsNullOrEmpty(payload.Reference.AssetGUID))
                {
                    var path = AssetDatabase.GUIDToAssetPath(payload.Reference.AssetGUID);
                    if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) return path;
                }
            }
            return string.Empty;
        }
    }
}
