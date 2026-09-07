#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal enum WorldCompanionOwnedAssetKind
    {
        Scene,
        Node,
        Resource,
    }

    internal enum WorldCompanionResourceCreationState
    {
        None,
        CreatingReserved,
        Initialized,
    }

    internal static class WorldCompanionOwnershipProof
    {
        internal static void CheckpointAsset(
            WorldCompanionJournalData journal,
            string path,
            ref string guid,
            ref string fingerprint)
        {
            AssetDatabase.ImportAsset(path);
            guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"Asset GUID is missing: {path}");
            fingerprint = AssetFingerprint(path);
            WorldCompanionRecoveryJournal.Save(journal);
        }

        internal static void CheckpointAddressable(
            WorldCompanionJournalData journal,
            AddressableAssetEntry entry)
        {
            journal.addressableFingerprint = AddressableFingerprint(entry);
            WorldCompanionRecoveryJournal.Save(journal);
        }

        /// <summary>Generate は所有済み asset を in-place で書き換える。書き換え途中で crash しても
        /// recovery が外部改変と誤判定しないよう、直前に fingerprint を空へ戻す。空の間の所有証明は
        /// GUID 一致と期待 shape が担う。</summary>
        internal static void BeginGeneratedMutation(WorldCompanionJournalData journal)
        {
            journal.sceneFingerprint = string.Empty;
            journal.nodeFingerprint = string.Empty;
            journal.resourceFingerprint = string.Empty;
            WorldCompanionRecoveryJournal.Save(journal);
        }

        internal static void RefreshGeneratedAssets(WorldCompanionJournalData journal)
        {
            RefreshAsset(journal.scenePath, journal.sceneGuid, ref journal.sceneFingerprint);
            RefreshAsset(journal.nodePath, journal.nodeGuid, ref journal.nodeFingerprint);
            RefreshAsset(journal.resourcePath, journal.resourceGuid, ref journal.resourceFingerprint);
            WorldCompanionRecoveryJournal.Save(journal);
        }

        internal static void VerifyPending(WorldCompanionJournalData journal)
        {
            if (journal.completedRecoveryBarrier < (int)WorldCompanionRecoveryBarrier.AssetsDeleted)
            {
                VerifyOrCaptureAsset(journal, journal.scenePath, ref journal.sceneGuid, ref journal.sceneFingerprint, WorldCompanionOwnedAssetKind.Scene);
                VerifyOrCaptureAsset(journal, journal.nodePath, ref journal.nodeGuid, ref journal.nodeFingerprint, WorldCompanionOwnedAssetKind.Node);
                VerifyOrCaptureAsset(journal, journal.resourcePath, ref journal.resourceGuid, ref journal.resourceFingerprint, WorldCompanionOwnedAssetKind.Resource);
            }
            if (journal.completedRecoveryBarrier < (int)WorldCompanionRecoveryBarrier.AddressableRemoved)
                VerifyOrCaptureAddressable(journal);
        }

        internal static void VerifyOrCaptureAsset(
            WorldCompanionJournalData journal,
            string path,
            ref string guid,
            ref string fingerprint,
            WorldCompanionOwnedAssetKind kind)
        {
            if (!AssetExists(path)) return;
            var currentGuid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(currentGuid)) throw new InvalidOperationException($"Cannot verify GUID ownership: {path}");
            if (!string.IsNullOrEmpty(guid) && !string.Equals(currentGuid, guid, StringComparison.Ordinal))
                throw new InvalidOperationException($"GUID ownership mismatch: {path}");
            if (string.IsNullOrEmpty(fingerprint))
            {
                VerifyExpectedShape(journal, path, kind);
                guid = currentGuid;
                fingerprint = AssetFingerprint(path);
                WorldCompanionRecoveryJournal.Save(journal);
                return;
            }
            if (!string.Equals(AssetFingerprint(path), fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException($"Content ownership mismatch: {path}");
        }

        private static void VerifyOrCaptureAddressable(WorldCompanionJournalData journal)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings == null || string.IsNullOrEmpty(journal.sceneGuid)
                ? null
                : settings.FindAssetEntry(journal.sceneGuid);
            if (entry == null) return;
            if (string.IsNullOrEmpty(journal.addressableFingerprint))
            {
                if (!string.Equals(entry.address, journal.scenePath, StringComparison.Ordinal)
                    || entry.parentGroup != settings!.DefaultGroup || entry.ReadOnly || entry.labels.Count != 0)
                    throw new InvalidOperationException($"Addressables ownership mismatch: {journal.scenePath}");
                journal.addressableFingerprint = AddressableFingerprint(entry);
                WorldCompanionRecoveryJournal.Save(journal);
                return;
            }
            if (!string.Equals(AddressableFingerprint(entry), journal.addressableFingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException($"Addressables ownership mismatch: {journal.scenePath}");
        }

        private static void VerifyExpectedShape(
            WorldCompanionJournalData journal,
            string path,
            WorldCompanionOwnedAssetKind kind)
        {
            switch (kind)
            {
                case WorldCompanionOwnedAssetKind.Scene:
                    VerifyEmptyScene(path);
                    return;
                case WorldCompanionOwnedAssetKind.Node:
                    var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);
                    var payloads = node == null ? null : node.Payloads;
                    if (node == null || !string.Equals(node.Identity, journal.identity, StringComparison.Ordinal)
                        || node.NodeLoadType != LoadType.OnDemand || payloads == null || payloads.Count != 1
                        || payloads[0].Reference == null || !string.Equals(payloads[0].Variant, string.Empty, StringComparison.Ordinal)
                        || !string.Equals(payloads[0].Reference!.AssetGUID, journal.sceneGuid, StringComparison.Ordinal))
                        throw new InvalidOperationException($"Node ownership mismatch: {path}");
                    return;
                case WorldCompanionOwnedAssetKind.Resource:
                    var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                    var resourcePayloads = resource == null ? null : resource.GetPayloads();
                    var state = (WorldCompanionResourceCreationState)journal.resourceCreationState;
                    var identityShape = state == WorldCompanionResourceCreationState.CreatingReserved
                        ? resource != null && (string.IsNullOrEmpty(resource.Identity)
                            || string.Equals(resource.Identity, journal.identity, StringComparison.Ordinal))
                        : state == WorldCompanionResourceCreationState.Initialized
                          && resource != null && string.Equals(resource.Identity, journal.identity, StringComparison.Ordinal);
                    var payloadShape = resourcePayloads != null && (resourcePayloads.Count == 0
                        || (resourcePayloads.Count == 1 && resourcePayloads[0].Reference != null
                            && string.Equals(resourcePayloads[0].Variant, string.Empty, StringComparison.Ordinal)
                            && string.Equals(resourcePayloads[0].Reference!.AssetGUID, journal.sceneGuid, StringComparison.Ordinal)));
                    if (resource == null || !identityShape
                        || resource.LoadType != LoadType.OnDemand || resource.StreamByDistance
                        || resource.Volume.center != Vector3.zero || resource.Volume.size != Vector3.zero
                        || resource.Children.Count != 0 || !payloadShape
                        || (resource.Parent != null && !string.Equals(resource.Parent.Identity, journal.parentIdentity, StringComparison.Ordinal)))
                        throw new InvalidOperationException($"Resource ownership mismatch: {path}");
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static void VerifyEmptyScene(string path)
        {
            var scene = SceneManager.GetSceneByPath(path);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (!scene.IsValid() || scene.rootCount != 0)
                    throw new InvalidOperationException($"Scene ownership mismatch: {path}");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void RefreshAsset(string path, string guid, ref string fingerprint)
        {
            if (!string.Equals(AssetDatabase.AssetPathToGUID(path), guid, StringComparison.Ordinal))
                throw new InvalidOperationException($"GUID ownership mismatch: {path}");
            fingerprint = AssetFingerprint(path);
        }

        private static bool AssetExists(string path)
            => AssetDatabase.LoadMainAssetAtPath(path) != null
               || File.Exists(FullPath(path)) || File.Exists(FullPath(path) + ".meta");

        private static string AssetFingerprint(string path)
        {
            var fullPath = FullPath(path);
            if (!File.Exists(fullPath) || !File.Exists(fullPath + ".meta"))
                throw new InvalidOperationException($"Asset content is incomplete: {path}");
            return HashText($"{HashFile(fullPath)}:{HashFile(fullPath + ".meta")}");
        }

        private static string AddressableFingerprint(AddressableAssetEntry entry)
        {
            var labels = string.Join("\n", entry.labels.OrderBy(label => label, StringComparer.Ordinal));
            return HashText($"{entry.guid}\n{entry.address}\n{entry.parentGroup.Guid}\n{entry.ReadOnly}\n{labels}");
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(stream));
        }

        private static string HashText(string value)
        {
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
        }

        private static string Hex(byte[] bytes)
            => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        private static string FullPath(string assetPath)
            => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);
    }
}
