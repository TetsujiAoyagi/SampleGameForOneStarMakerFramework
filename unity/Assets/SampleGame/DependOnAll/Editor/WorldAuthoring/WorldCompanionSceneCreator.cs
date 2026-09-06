#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal static class WorldCompanionSceneCreator
    {
        private const string MapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        internal static bool CreateAndOpen(WorldWorkspaceSelection selection, out string message)
        {
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (WorldCompanionRecoveryService.IsBlocked(out message)) return false;
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            if (map == null)
            {
                message = $"SceneResourceMap is missing: {MapPath}";
                return false;
            }

            try
            {
                var resolution = WorldWorkspaceSceneOpener.Resolve(map, selection.BuildOpenPlan());
                if (!resolution.CanOpen)
                {
                    message = "Required scenes are missing:\n" + string.Join("\n", resolution.MissingRequired);
                    return false;
                }
                if (resolution.MissingOptional.Count != 1 || !resolution.MissingOptional[0].CanCreate)
                {
                    message = resolution.MissingOptional.Count == 0
                        ? "The optional companion already exists."
                        : "The selection does not identify exactly one creatable optional companion.";
                    return false;
                }

                var plan = WorldCompanionCreationPlan.Create(selection);
                using (var transaction = new WorldCompanionCreationTransaction(plan))
                {
                    transaction.Execute();
                }

                map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
                if (map == null) throw new InvalidOperationException("SceneResourceMap disappeared after generation.");
                return WorldWorkspaceSceneOpener.OpenAfterCreation(map, selection, out message);
            }
            catch (OperationCanceledException ex)
            {
                message = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"World Workspace create failed: {ex}");
                message = ex.Message;
                return false;
            }
        }
    }
}
