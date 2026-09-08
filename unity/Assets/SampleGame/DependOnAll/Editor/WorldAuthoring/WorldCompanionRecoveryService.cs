#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    /// <summary>pending journal を自動変更せず検出し、Workspace を fail-closed にする。</summary>
    [InitializeOnLoad]
    internal static class WorldCompanionRecoveryService
    {
        static WorldCompanionRecoveryService()
        {
            if (IsBlocked(out var message)) Debug.LogWarning(message);
        }

        internal static bool IsBlocked(out string message)
        {
            if (!WorldCompanionRecoveryJournal.Exists)
            {
                message = string.Empty;
                return false;
            }

            try
            {
                var pending = WorldCompanionRecoveryJournal.Load();
                message = $"World Workspace is blocked by pending creation '{pending.identity}' at '{pending.scenePath}'. "
                    + "Choose Rollback Pending Creation to recover.";
            }
            catch (Exception ex)
            {
                message = $"World Workspace is blocked by a corrupt journal at '{WorldCompanionRecoveryJournal.JournalPath}': {ex.Message}";
            }
            return true;
        }

        [MenuItem("OneStarMaker/World Workspace/Rollback Pending Creation")]
        private static void RollbackPendingCreation()
        {
            if (!WorldCompanionRecoveryJournal.Exists)
            {
                EditorUtility.DisplayDialog("World Workspace", "No pending creation journal exists.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Rollback Pending Creation",
                    $"Recover the exact pending operation recorded at:\n{WorldCompanionRecoveryJournal.JournalPath}",
                    "Rollback",
                    "Cancel"))
            {
                return;
            }

            try
            {
                WorldCompanionCreationTransaction.RecoverPending();
                EditorUtility.DisplayDialog("World Workspace", "Pending creation was rolled back.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"World Workspace rollback failed; journal retained: {ex}");
                EditorUtility.DisplayDialog(
                    "Rollback Failed",
                    $"The journal was retained. Retry after resolving the reported barrier.\n\n{ex.Message}",
                    "OK");
            }
        }

        [MenuItem("OneStarMaker/World Workspace/Rollback Pending Creation", true)]
        private static bool ValidateRollbackPendingCreation() => WorldCompanionRecoveryJournal.Exists;
    }
}
