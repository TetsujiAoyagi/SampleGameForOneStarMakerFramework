#nullable enable

using System;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        /// <summary>
        /// 選択中の季節の Atmosphere VFX を作って単独で開く。既にあれば作らずに開く。
        /// Role 別の open plan には入れない（VFX role で Atmosphere も開くかは後続の authoring 改善）。
        /// </summary>
        internal static bool CreateOrOpenSeasonAtmosphere(WorldSeason season, out string message)
        {
            if (WorldCompanionRecoveryService.IsBlocked(out message)) return false;
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            if (map == null)
            {
                message = $"SceneResourceMap is missing: {MapPath}";
                return false;
            }

            try
            {
                var plan = WorldCompanionCreationPlan.CreateSeasonAtmosphere(season);
                var existing = ResolveSingle(map, plan.Identity);
                if (existing.Count == 1)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        message = "Open cancelled.";
                        return false;
                    }
                    EditorSceneManager.OpenScene(existing[0], OpenSceneMode.Single);
                    message = $"{plan.Identity} already exists; opened it.";
                    return true;
                }

                // Execute が保存の確認と、作成前の source / Map の整合の検査を行う。
                using (var transaction = new WorldCompanionCreationTransaction(plan))
                {
                    transaction.Execute();
                }

                map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
                if (map == null) throw new InvalidOperationException("SceneResourceMap disappeared after generation.");
                var created = ResolveSingle(map, plan.Identity);
                if (created.Count != 1) throw new InvalidOperationException($"Created scene is not resolvable: {plan.Identity}");
                EditorSceneManager.OpenScene(created[0], OpenSceneMode.Single);
                message = $"{plan.Identity} created and opened.";
                return true;
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

        private static System.Collections.Generic.List<string> ResolveSingle(SceneResourceMap map, string identity)
            => WorldWorkspaceSceneOpener.Resolve(
                map,
                new[] { new WorldWorkspaceOpenItem(identity, string.Empty, required: true, canCreate: false) }).ScenePaths;
    }
}
