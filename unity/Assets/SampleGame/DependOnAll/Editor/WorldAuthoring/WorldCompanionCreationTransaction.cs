#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor.WorldAuthoring
{
    internal enum WorldCompanionMutationPoint
    {
        JournalWritten, FolderCreated, SceneCreated, NodeCreated, GraphLinked,
        ResourceCreated, AddressableCreated, Generated, Verified, Regenerated,
    }
    internal enum WorldCompanionRecoveryBarrier
    {
        None, ScenesRestored, AddressableRemoved, GraphUnlinked,
        AssetsDeleted, SourceGenerated, HashVerified, Saved,
    }
    internal sealed class WorldCompanionPreflight
    {
        internal SceneResourceMap Map = null!;
        internal SceneNodeData ParentNode = null!;
        internal SceneGraphEdges Graph = null!;
        internal List<SceneNodeData> Nodes = null!;
        internal List<SceneGraphEdges> Graphs = null!;
        internal string SourceHash = string.Empty;
    }
    /// <summary>一つの optional companion を SceneGraph source と runtime projection へ atomically 追加する。</summary>
    internal sealed class WorldCompanionCreationTransaction : IDisposable
    {
        private readonly WorldCompanionCreationPlan _plan;
        private bool _completed;
        internal static Action<WorldCompanionMutationPoint>? FaultInjector;
        internal static Action<WorldCompanionRecoveryBarrier>? RecoveryFaultInjector;
        internal WorldCompanionCreationTransaction(WorldCompanionCreationPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }
        public void Dispose()
        {
            if (!_completed && WorldCompanionRecoveryJournal.Exists)
            {
                Debug.LogWarning("World Workspace transaction disposed with a pending recovery journal.");
            }
        }
        internal void Execute()
        {
            if (WorldCompanionRecoveryJournal.Exists)
                throw new InvalidOperationException($"Pending World Workspace journal blocks creation: {WorldCompanionRecoveryJournal.JournalPath}");
            var preflight = Preflight(_plan);
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Scene save was cancelled before World Workspace creation.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var activePath = SceneManager.GetActiveScene().path;
            var journal = WorldCompanionRecoveryJournal.Create(
                _plan,
                preflight.SourceHash,
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preflight.ParentNode)),
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preflight.Graph)),
                setup,
                activePath);
            try
            {
                Fault(WorldCompanionMutationPoint.JournalWritten);
                EnsureFolders(journal);
                Fault(WorldCompanionMutationPoint.FolderCreated);
                CreateScene(journal);
                Fault(WorldCompanionMutationPoint.SceneCreated);
                var node = CreateNode(journal);
                Fault(WorldCompanionMutationPoint.NodeCreated);
                LinkGraph(preflight.Graph, preflight.ParentNode, node);
                Fault(WorldCompanionMutationPoint.GraphLinked);
                CreateReservedResource(journal);
                Fault(WorldCompanionMutationPoint.ResourceCreated);
                CreateAddressableEntry(journal);
                Fault(WorldCompanionMutationPoint.AddressableCreated);

                var nodes = LoadAll<SceneNodeData>(journal.sourceSearchRoot);
                var graphs = LoadAll<SceneGraphEdges>(journal.sourceSearchRoot);
                GenerateOrThrow(nodes, graphs, journal);
                WorldCompanionOwnershipProof.RefreshGeneratedAssets(journal);
                Fault(WorldCompanionMutationPoint.Generated);
                VerifyCommitted(journal, preflight.ParentNode, preflight.Graph, node, nodes, graphs);
                Fault(WorldCompanionMutationPoint.Verified);
                GenerateOrThrow(nodes, graphs, journal);
                WorldCompanionOwnershipProof.RefreshGeneratedAssets(journal);
                VerifyCommitted(journal, preflight.ParentNode, preflight.Graph, node, nodes, graphs);
                Fault(WorldCompanionMutationPoint.Regenerated);
                WorldCompanionRecoveryJournal.Delete();
                _completed = true;
            }
            catch (Exception original)
            {
                try
                {
                    Rollback(journal);
                }
                catch (Exception recovery)
                {
                    throw new AggregateException("World companion creation and rollback both failed.", original, recovery);
                }
                throw;
            }
        }
        internal static WorldCompanionPreflight Preflight(WorldCompanionCreationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var companionFolder = System.IO.Path.GetDirectoryName(plan.ScenePath)!.Replace('\\', '/');
            var parentFolder = System.IO.Path.GetDirectoryName(companionFolder)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parentFolder))
                throw new InvalidOperationException($"Parent Cell folder is missing: {parentFolder}");
            if (AssetDatabase.IsValidFolder(companionFolder) || System.IO.Directory.Exists(FullPath(companionFolder)) || System.IO.File.Exists(FullPath(companionFolder) + ".meta"))
                throw new InvalidOperationException($"Planned companion folder already exists: {companionFolder}");
            foreach (var path in new[] { plan.ScenePath, plan.ResourcePath, plan.NodePath })
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(FullPath(path)) || System.IO.File.Exists(FullPath(path) + ".meta") || System.IO.Directory.Exists(FullPath(path)))
                throw new InvalidOperationException($"Planned path already exists: {path}");
            var nodes = LoadAll<SceneNodeData>(plan.SourceSearchRoot);
            if (nodes.Count(node => string.Equals(node.Identity, plan.Identity, StringComparison.Ordinal)) != 0)
                throw new InvalidOperationException($"SceneNode identity already exists: {plan.Identity}");
            var parents = nodes.Where(node => string.Equals(node.Identity, plan.ParentIdentity, StringComparison.Ordinal)).ToList();
            if (parents.Count != 1) throw new InvalidOperationException($"Parent node count must be one: {plan.ParentIdentity} ({parents.Count})");
            var graphs = LoadAll<SceneGraphEdges>(plan.SourceSearchRoot);
            var containing = graphs.Where(graph => graph.ContainsNode(parents[0])).ToList();
            if (containing.Count != 1) throw new InvalidOperationException($"Parent graph count must be one: {containing.Count}");
            var validation = SceneGraphValidator.ValidateAll(nodes, graphs);
            if (validation.Any(item => item.Severity == SceneGraphValidator.Severity.Error))
                throw new InvalidOperationException("SceneGraph contains validation errors.");
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(plan.SceneResourceMapPath);
            if (map == null) throw new InvalidOperationException($"SceneResourceMap is missing: {plan.SceneResourceMapPath}");
            var sourceHash = SceneResourceGenerator.ComputeCurrentHash(nodes, graphs);
            if (!string.Equals(map.GenerateHash, sourceHash, StringComparison.Ordinal))
                throw new InvalidOperationException($"SceneResourceMap hash is stale: map={map.GenerateHash}, source={sourceHash}");
            var parentResource = map.GetSceneResource(plan.ParentIdentity);
            if (parentResource == null || !parentResource.StreamByDistance)
                throw new InvalidOperationException($"Parent is not a streaming Cell: {plan.ParentIdentity}");
            var resourceMatches = LoadAll<SceneResource>(string.Empty).Count(resource => string.Equals(resource.Identity, plan.Identity, StringComparison.Ordinal));
            if (resourceMatches != 0) throw new InvalidOperationException($"SceneResource identity already exists: {plan.Identity}");
            if (AddressableAddressExists(plan.ScenePath) || AddressableAddressExists(plan.Identity))
                throw new InvalidOperationException($"Addressables address collision: {plan.ScenePath}");
            return new WorldCompanionPreflight
            {
                Map = map, ParentNode = parents[0], Graph = containing[0], Nodes = nodes, Graphs = graphs, SourceHash = sourceHash,
            };
        }
        internal static void RecoverPending()
        {
            Rollback(WorldCompanionRecoveryJournal.Load());
        }
        private static void Rollback(WorldCompanionJournalData journal)
        {
            WorldCompanionOwnershipProof.VerifyPending(journal);
            Barrier(journal, WorldCompanionRecoveryBarrier.ScenesRestored, () => RestoreScenes(journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.AddressableRemoved, () => RemoveAddressable(journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.GraphUnlinked, () => UnlinkGraph(journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.AssetsDeleted, () => DeleteOwnedAssets(journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.SourceGenerated, () => GenerateOrThrow(
                LoadAll<SceneNodeData>(journal.sourceSearchRoot), LoadAll<SceneGraphEdges>(journal.sourceSearchRoot), journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.HashVerified, () => VerifyStartingHash(journal));
            Barrier(journal, WorldCompanionRecoveryBarrier.Saved, () => { AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); });
            WorldCompanionRecoveryJournal.Delete();
        }
        private static void Barrier(WorldCompanionJournalData journal, WorldCompanionRecoveryBarrier barrier, Action action)
        {
            if (journal.completedRecoveryBarrier >= (int)barrier) return;
            RecoveryFaultInjector?.Invoke(barrier);
            action();
            journal.completedRecoveryBarrier = (int)barrier;
            WorldCompanionRecoveryJournal.Save(journal);
        }
        private static void EnsureFolders(WorldCompanionJournalData journal)
        {
            var nodeFolder = System.IO.Path.GetDirectoryName(journal.nodePath)!.Replace('\\', '/');
            var parentFolder = System.IO.Path.GetDirectoryName(journal.companionFolderPath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(nodeFolder) || !AssetDatabase.IsValidFolder(parentFolder))
                throw new InvalidOperationException("A required source or parent Cell folder disappeared after preflight.");
            if (!AssetDatabase.IsValidFolder(journal.companionFolderPath))
                journal.companionFolderGuid = AssetDatabase.CreateFolder(
                    System.IO.Path.GetDirectoryName(journal.companionFolderPath)!.Replace('\\', '/'),
                    System.IO.Path.GetFileName(journal.companionFolderPath));
            WorldCompanionRecoveryJournal.Save(journal);
        }
        private static void CreateScene(WorldCompanionJournalData journal)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!EditorSceneManager.SaveScene(scene, journal.scenePath)) throw new InvalidOperationException("Failed to save companion scene.");
            WorldCompanionOwnershipProof.CheckpointAsset(journal, journal.scenePath, ref journal.sceneGuid, ref journal.sceneFingerprint);
            if (!EditorSceneManager.CloseScene(scene, removeScene: true)) throw new InvalidOperationException("Failed to close companion scene.");
        }
        private static SceneNodeData CreateNode(WorldCompanionJournalData journal)
        {
            var node = ScriptableObject.CreateInstance<SceneNodeData>();
            node.name = journal.identity;
            node.Identity = journal.identity;
            node.NodeLoadType = LoadType.OnDemand;
            node.Payloads.Add(new AssetPayload(string.Empty, new AssetReference(journal.sceneGuid)));
            AssetDatabase.CreateAsset(node, journal.nodePath);
            WorldCompanionOwnershipProof.CheckpointAsset(journal, journal.nodePath, ref journal.nodeGuid, ref journal.nodeFingerprint);
            return node;
        }
        private static void LinkGraph(SceneGraphEdges graph, SceneNodeData parent, SceneNodeData child)
        {
            graph.AddNode(child);
            graph.AddEdge(parent, child);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            if (!graph.ContainsNode(child) || graph.GetParent(child) != parent) throw new InvalidOperationException("Graph link verification failed.");
        }
        private static void CreateReservedResource(WorldCompanionJournalData journal)
        {
            var resource = ScriptableObject.CreateInstance<SceneResource>();
            AssetDatabase.CreateAsset(resource, journal.resourcePath);
            var so = new SerializedObject(resource);
            so.FindProperty("_identity").stringValue = journal.identity;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            WorldCompanionOwnershipProof.CheckpointAsset(journal, journal.resourcePath, ref journal.resourceGuid, ref journal.resourceFingerprint);
        }
        private static void CreateAddressableEntry(WorldCompanionJournalData journal)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("AddressableAssetSettings is missing.");
            var entry = settings.CreateOrMoveEntry(journal.sceneGuid, settings.DefaultGroup, readOnly: false, postEvent: false);
            entry.address = journal.scenePath;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            WorldCompanionOwnershipProof.CheckpointAddressable(journal, entry);
        }
        private static void VerifyCommitted(WorldCompanionJournalData j, SceneNodeData parent, SceneGraphEdges graph, SceneNodeData node, IReadOnlyList<SceneNodeData> nodes, IReadOnlyList<SceneGraphEdges> graphs)
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(j.scenePath);
            var nodeAsset = AssetDatabase.LoadAssetAtPath<SceneNodeData>(j.nodePath);
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(j.resourcePath);
            if (scene == null || AssetDatabase.AssetPathToGUID(j.scenePath) != j.sceneGuid
                || nodeAsset == null || nodeAsset != node || AssetDatabase.AssetPathToGUID(j.nodePath) != j.nodeGuid
                || resource == null || AssetDatabase.AssetPathToGUID(j.resourcePath) != j.resourceGuid
                || resource.LoadType != LoadType.OnDemand || resource.StreamByDistance
                || resource.Volume.center != Vector3.zero || resource.Volume.size != Vector3.zero
                || resource.Parent == null || !string.Equals(resource.Parent.Identity, j.parentIdentity, StringComparison.Ordinal))
                throw new InvalidOperationException("Generated scene/node/resource invariant failed.");
            if (!graph.ContainsNode(node) || graph.GetParent(node) != parent) throw new InvalidOperationException("Generated graph invariant failed.");
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(j.sceneResourceMapPath);
            var expectedHash = SceneResourceGenerator.ComputeCurrentHash(nodes, graphs);
            if (map == null || !string.Equals(map.GenerateHash, expectedHash, StringComparison.Ordinal)) throw new InvalidOperationException("Generated map hash invariant failed.");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings == null ? null : settings.FindAssetEntry(j.sceneGuid);
            var payloads = resource.GetPayloads();
            var inParent = resource.Parent.Children.Any(child => child == resource);
            SceneResourceGenerator.RebuildMapLookup(map);
            if (SceneManager.GetSceneByPath(j.scenePath).isLoaded || !inParent || map.GetSceneResource(j.identity) != resource
                || payloads.Count != 1 || payloads[0].Reference == null
                || !string.Equals(payloads[0].Variant, string.Empty, StringComparison.Ordinal)
                || !string.Equals(payloads[0].Reference!.AssetGUID, j.sceneGuid, StringComparison.Ordinal)
                || entry == null || !string.Equals(entry.address, j.scenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Generated link, payload, map, or Addressables invariant failed.");
        }
        private static void RestoreScenes(WorldCompanionJournalData journal)
        {
            var temporary = SceneManager.GetSceneByPath(journal.scenePath);
            if (temporary.IsValid() && temporary.isLoaded && !EditorSceneManager.CloseScene(temporary, true))
                throw new InvalidOperationException("Failed to close temporary companion scene.");
            EditorSceneManager.RestoreSceneManagerSetup(WorldCompanionRecoveryJournal.RestoreSetup(journal));
            var restored = EditorSceneManager.GetSceneManagerSetup();
            if (!WorldCompanionRecoveryJournal.SetupMatches(restored, journal.originalSceneSetup))
                throw new InvalidOperationException("Failed to restore the original scene setup.");
            if (!string.IsNullOrEmpty(journal.originalActiveScenePath))
            {
                var active = SceneManager.GetSceneByPath(journal.originalActiveScenePath);
                if (!active.IsValid() || (SceneManager.GetActiveScene().handle != active.handle && !SceneManager.SetActiveScene(active))) throw new InvalidOperationException("Failed to restore active scene.");
            }
        }
        private static void RemoveAddressable(WorldCompanionJournalData journal)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null && !string.IsNullOrEmpty(journal.sceneGuid) && settings.FindAssetEntry(journal.sceneGuid) != null)
            {
                settings.RemoveAssetEntry(journal.sceneGuid, postEvent: false);
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                if (settings.FindAssetEntry(journal.sceneGuid) != null) throw new InvalidOperationException("Addressables removal verification failed.");
            }
        }
        private static void UnlinkGraph(WorldCompanionJournalData journal)
        {
            var graphPath = AssetDatabase.GUIDToAssetPath(journal.graphGuid);
            var graph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(graphPath);
            var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(journal.nodePath);
            if (graph != null && node != null)
            {
                graph.RemoveEdgeByChild(node);
                graph.RemoveNode(node);
                EditorUtility.SetDirty(graph);
                AssetDatabase.SaveAssets();
                if (graph.ContainsNode(node) || graph.GetParent(node) != null) throw new InvalidOperationException("Graph unlink verification failed.");
            }
        }
        private static void DeleteOwnedAssets(WorldCompanionJournalData journal)
        {
            DeleteOwned(journal, journal.nodePath, ref journal.nodeGuid, ref journal.nodeFingerprint, WorldCompanionOwnedAssetKind.Node);
            DeleteOwned(journal, journal.resourcePath, ref journal.resourceGuid, ref journal.resourceFingerprint, WorldCompanionOwnedAssetKind.Resource);
            DeleteOwned(journal, journal.scenePath, ref journal.sceneGuid, ref journal.sceneFingerprint, WorldCompanionOwnedAssetKind.Scene);
            if (string.IsNullOrEmpty(journal.companionFolderGuid) && AssetDatabase.IsValidFolder(journal.companionFolderPath))
            {
                journal.companionFolderGuid = AssetDatabase.AssetPathToGUID(journal.companionFolderPath);
                WorldCompanionRecoveryJournal.Save(journal);
            }
            if (AssetDatabase.IsValidFolder(journal.companionFolderPath))
            {
                var currentFolderGuid = AssetDatabase.AssetPathToGUID(journal.companionFolderPath);
                if (!string.Equals(currentFolderGuid, journal.companionFolderGuid, StringComparison.Ordinal))
                    throw new InvalidOperationException($"GUID ownership mismatch: {journal.companionFolderPath}");
                if (System.IO.Directory.EnumerateFileSystemEntries(FullPath(journal.companionFolderPath)).Any())
                    throw new InvalidOperationException($"Transaction folder contains an unowned entry: {journal.companionFolderPath}");
                if (!AssetDatabase.DeleteAsset(journal.companionFolderPath)
                    || AssetDatabase.IsValidFolder(journal.companionFolderPath))
                    throw new InvalidOperationException($"Failed to delete transaction folder: {journal.companionFolderPath}");
            }
            WorldCompanionRecoveryJournal.Save(journal);
        }

        private static void DeleteOwned(WorldCompanionJournalData journal, string path, ref string checkpointGuid, ref string fingerprint, WorldCompanionOwnedAssetKind kind)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) == null && !System.IO.File.Exists(FullPath(path)) && !System.IO.File.Exists(FullPath(path) + ".meta")) return;
            WorldCompanionOwnershipProof.VerifyOrCaptureAsset(journal, path, ref checkpointGuid, ref fingerprint, kind);
            if (!AssetDatabase.DeleteAsset(path) || AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(FullPath(path)) || System.IO.File.Exists(FullPath(path) + ".meta"))
                throw new InvalidOperationException($"Failed to delete transaction asset: {path}");
        }

        private static void VerifyStartingHash(WorldCompanionJournalData journal)
        {
            var source = SceneResourceGenerator.ComputeCurrentHash(
                LoadAll<SceneNodeData>(journal.sourceSearchRoot), LoadAll<SceneGraphEdges>(journal.sourceSearchRoot));
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(journal.sceneResourceMapPath);
            if (!string.Equals(source, journal.startingSourceHash, StringComparison.Ordinal)
                || map == null || !string.Equals(map.GenerateHash, journal.startingSourceHash, StringComparison.Ordinal))
            {
                var mapHash = map == null ? "<null>" : map.GenerateHash;
                throw new InvalidOperationException($"Rollback hash mismatch: expected={journal.startingSourceHash}, source={source}, map={mapHash}");
            }
        }

        private static void GenerateOrThrow(
            IReadOnlyList<SceneNodeData> nodes,
            IReadOnlyList<SceneGraphEdges> graphs,
            WorldCompanionJournalData journal)
        {
            if (!SceneResourceGenerator.Generate(nodes, graphs, journal.resourceOutputPath, journal.sceneResourceMapPath))
                throw new InvalidOperationException("SceneResourceGenerator.Generate failed.");
        }

        private static List<T> LoadAll<T>(string sourceSearchRoot) where T : UnityEngine.Object
        {
            var guids = string.IsNullOrEmpty(sourceSearchRoot)
                ? AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                : AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { sourceSearchRoot });
            return guids.Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null).ToList()!;
        }

        private static bool AddressableAddressExists(string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return false;
            foreach (var group in settings.groups)
                if (group != null && group.entries.Any(entry => string.Equals(entry.address, address, StringComparison.Ordinal))) return true;
            return false;
        }
        private static string FullPath(string assetPath)
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath)!, assetPath);

        private static void Fault(WorldCompanionMutationPoint point) => FaultInjector?.Invoke(point);
    }
}
