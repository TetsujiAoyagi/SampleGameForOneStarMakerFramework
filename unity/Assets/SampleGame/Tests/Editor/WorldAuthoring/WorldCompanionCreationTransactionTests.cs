#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    [TestFixture]
    public sealed class WorldCompanionCreationTransactionTests
    {
        [Test]
        public void Execute_CreatesExactGraphProjection_AndSurvivesStandardRegenerate()
        {
            using var scope = new WorldCompanionTestScope();
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);
            transaction.Execute();

            scope.AssertCommitted();
            var resourceGuid = AssetDatabase.AssetPathToGUID(scope.Plan.ResourcePath);
            Assert.That(SceneResourceGenerator.Generate(
                scope.LoadNodes(), scope.LoadGraphs(), scope.ResourceOutputPath, scope.MapPath), Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(scope.Plan.ResourcePath), Is.EqualTo(resourceGuid));
            scope.AssertCommitted();
        }

        [TestCaseSource(nameof(MutationPoints))]
        public void Execute_FaultAtEveryMutationPoint_RollsBackExactOwnedState(
            int mutationPointValue)
        {
            using var scope = new WorldCompanionTestScope();
            var mutationPoint = (WorldCompanionMutationPoint)mutationPointValue;
            WorldCompanionCreationTransaction.FaultInjector = point =>
            {
                if (point == mutationPoint) throw new InjectedWorldCompanionFault(point.ToString());
            };
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);

            Assert.Throws<InjectedWorldCompanionFault>(() => transaction.Execute());
            scope.AssertRolledBack();
        }

        [Test]
        public void Preflight_ProjectWideResourceIdentityCollision_ChangesNothing()
        {
            using var scope = new WorldCompanionTestScope();
            var collisionPath = $"{scope.Root}/Collision.asset";
            var collision = ScriptableObject.CreateInstance<SceneResource>();
            AssetDatabase.CreateAsset(collision, collisionPath);
            var serialized = new SerializedObject(collision);
            serialized.FindProperty("_identity").stringValue = scope.Plan.Identity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Assert.Throws<InvalidOperationException>(() => WorldCompanionCreationTransaction.Preflight(scope.Plan));
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneResource>(collisionPath), Is.SameAs(collision));
            scope.AssertRolledBack();
        }

        [TestCaseSource(nameof(PlannedPathCollisions))]
        public void Preflight_PlannedPathSidecarOrDirectoryCollision_ChangesNothing(
            string pathKind,
            bool sidecar)
        {
            using var scope = new WorldCompanionTestScope();
            var path = pathKind switch
            {
                "Scene" => scope.Plan.ScenePath,
                "Resource" => scope.Plan.ResourcePath,
                "Node" => scope.Plan.NodePath,
                _ => throw new ArgumentOutOfRangeException(nameof(pathKind)),
            };
            var fullPath = Path.GetFullPath(path);
            var collisionPath = sidecar ? fullPath + ".meta" : fullPath;
            if (sidecar)
            {
                var assetParent = Path.GetDirectoryName(path)!.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(assetParent))
                {
                    AssetDatabase.CreateFolder(
                        Path.GetDirectoryName(assetParent)!.Replace('\\', '/'),
                        Path.GetFileName(assetParent));
                }
                File.WriteAllText(collisionPath, $"fileFormatVersion: 2\nguid: {Guid.NewGuid():N}\n");
            }
            else
            {
                Directory.CreateDirectory(collisionPath);
            }

            Assert.Throws<InvalidOperationException>(() => WorldCompanionCreationTransaction.Preflight(scope.Plan));
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False);
            Assert.That(sidecar ? File.Exists(collisionPath) : Directory.Exists(collisionPath), Is.True);
            scope.AssertSourceStateUnchanged();
        }

        private static IEnumerable<int> MutationPoints()
            => ((WorldCompanionMutationPoint[])Enum.GetValues(typeof(WorldCompanionMutationPoint))).Select(value => (int)value);

        private static IEnumerable<TestCaseData> PlannedPathCollisions()
        {
            foreach (var pathKind in new[] { "Scene", "Resource", "Node" })
            {
                yield return new TestCaseData(pathKind, true).SetName($"Preflight_{pathKind}_SidecarCollision");
                yield return new TestCaseData(pathKind, false).SetName($"Preflight_{pathKind}_DirectoryCollision");
            }
        }
    }

    internal sealed class InjectedWorldCompanionFault : Exception
    {
        internal InjectedWorldCompanionFault(string checkpoint) : base(checkpoint) { }
    }

    internal sealed class WorldCompanionTestScope : IDisposable
    {
        private readonly SceneSetup[] _outerSetup;
        private readonly string _startingSetup;
        private readonly string _startingActiveScenePath;
        private bool _disposed;

        internal WorldCompanionTestScope()
        {
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False,
                "A real pending World Workspace operation must be recovered before these tests run.");
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
            _outerSetup = EditorSceneManager.GetSceneManagerSetup();

            var token = Guid.NewGuid().ToString("N").Substring(0, 12);
            Root = $"Assets/__WorldCompanionTests_{token}";
            SourceRoot = $"{Root}/Source";
            NodeRoot = $"{SourceRoot}/Nodes";
            ResourceOutputPath = $"{Root}/Generated";
            MapPath = $"{Root}/SceneResourceMap.asset";
            var parentIdentity = $"S4aTest{token}_Cell";
            var identity = $"S4aTest{token}_Events";
            var parentCellFolder = $"{Root}/World/Seasons/Spring/Cells/{parentIdentity}";
            EnsureFolder(NodeRoot);
            EnsureFolder(ResourceOutputPath);
            EnsureFolder(parentCellFolder);

            CreateSavedScene($"{Root}/OriginalSetup.unity", leaveOpen: true, NewSceneMode.Single);
            var parentSceneGuid = CreateSavedScene($"{parentCellFolder}/{parentIdentity}.unity", leaveOpen: false);
            ParentNode = ScriptableObject.CreateInstance<SceneNodeData>();
            ParentNode.name = parentIdentity;
            ParentNode.Identity = parentIdentity;
            ParentNode.NodeLoadType = LoadType.OnDemand;
            ParentNode.Payloads.Add(new AssetPayload(string.Empty, new AssetReference(parentSceneGuid)));
            AssetDatabase.CreateAsset(ParentNode, $"{NodeRoot}/{parentIdentity}.asset");

            Graph = ScriptableObject.CreateInstance<SceneGraphEdges>();
            Graph.GraphName = $"S4aTest{token}";
            Graph.AddNode(ParentNode);
            AssetDatabase.CreateAsset(Graph, $"{SourceRoot}/Graph.asset");

            var parentResource = ScriptableObject.CreateInstance<SceneResource>();
            var parentResourcePath = $"{ResourceOutputPath}/{parentIdentity}.asset";
            AssetDatabase.CreateAsset(parentResource, parentResourcePath);
            var parentSerialized = new SerializedObject(parentResource);
            parentSerialized.FindProperty("_identity").stringValue = parentIdentity;
            parentSerialized.FindProperty("_streamByDistance").boolValue = true;
            parentSerialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Assert.That(SceneResourceGenerator.Generate(LoadNodes(), LoadGraphs(), ResourceOutputPath, MapPath), Is.True);

            Plan = WorldCompanionCreationPlan.CreateForTests(
                identity, parentIdentity, parentCellFolder, NodeRoot,
                ResourceOutputPath, MapPath, SourceRoot);
            StartingHash = SceneResourceGenerator.ComputeCurrentHash(LoadNodes(), LoadGraphs());
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(map, Is.Not.Null);
            Assert.That(map!.GenerateHash, Is.EqualTo(StartingHash));
            SceneResourceGenerator.RebuildMapLookup(map);
            Assert.That(map.GetSceneResource(parentIdentity)!.StreamByDistance, Is.True);

            _startingSetup = SetupFingerprint(EditorSceneManager.GetSceneManagerSetup());
            _startingActiveScenePath = SceneManager.GetActiveScene().path;
        }

        internal string Root { get; }
        internal string SourceRoot { get; }
        internal string NodeRoot { get; }
        internal string ResourceOutputPath { get; }
        internal string MapPath { get; }
        internal string StartingHash { get; }
        internal SceneNodeData ParentNode { get; }
        internal SceneGraphEdges Graph { get; }
        internal WorldCompanionCreationPlan Plan { get; }

        internal List<SceneNodeData> LoadNodes() => LoadAll<SceneNodeData>(SourceRoot);
        internal List<SceneGraphEdges> LoadGraphs() => LoadAll<SceneGraphEdges>(SourceRoot);

        internal void AssertCommitted()
        {
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False);
            var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(Plan.NodePath);
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(Plan.ResourcePath);
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(node, Is.Not.Null);
            Assert.That(resource, Is.Not.Null);
            Assert.That(map, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(Plan.ResourcePath), Is.Not.Empty);
            Assert.That(resource!.Identity, Is.EqualTo(Plan.Identity));
            Assert.That(resource.StreamByDistance, Is.False);
            Assert.That(resource.Volume.size, Is.EqualTo(Vector3.zero));
            Assert.That(resource.Parent, Is.Not.Null);
            Assert.That(resource.Parent!.Identity, Is.EqualTo(Plan.ParentIdentity));
            Assert.That(resource.Parent.Children, Does.Contain(resource));
            Assert.That(Graph.ContainsNode(node!), Is.True);
            Assert.That(Graph.GetParent(node!), Is.SameAs(ParentNode));
            var payloads = resource.GetPayloads();
            Assert.That(payloads, Has.Count.EqualTo(1));
            Assert.That(payloads[0].Variant, Is.Empty);
            Assert.That(payloads[0].Reference, Is.Not.Null);
            Assert.That(payloads[0].Reference!.AssetGUID,
                Is.EqualTo(AssetDatabase.AssetPathToGUID(Plan.ScenePath)));
            SceneResourceGenerator.RebuildMapLookup(map!);
            Assert.That(map.GetSceneResource(Plan.Identity), Is.SameAs(resource));
            Assert.That(map.GenerateHash,
                Is.EqualTo(SceneResourceGenerator.ComputeCurrentHash(LoadNodes(), LoadGraphs())));
            Assert.That(AddressableAddressCount(Plan.ScenePath), Is.EqualTo(1));
            Assert.That(SceneManager.GetSceneByPath(Plan.ScenePath).isLoaded, Is.False);
        }

        internal void AssertRolledBack()
        {
            Assert.That(AssetDatabase.LoadMainAssetAtPath(Plan.ScenePath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(Plan.ResourcePath), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(Plan.NodePath), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(Path.GetDirectoryName(Plan.ScenePath)!.Replace('\\', '/')), Is.False);
            AssertSourceStateUnchanged();
        }

        internal void AssertSourceStateUnchanged()
        {
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False);
            Assert.That(Graph.GraphNodes.Where(node => node != null).Select(node => node!.Identity),
                Does.Not.Contain(Plan.Identity));
            Assert.That(AddressableAddressCount(Plan.ScenePath), Is.Zero);
            Assert.That(SceneResourceGenerator.ComputeCurrentHash(LoadNodes(), LoadGraphs()), Is.EqualTo(StartingHash));
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            Assert.That(map, Is.Not.Null);
            Assert.That(map!.GenerateHash, Is.EqualTo(StartingHash));
            Assert.That(SetupFingerprint(EditorSceneManager.GetSceneManagerSetup()), Is.EqualTo(_startingSetup));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(_startingActiveScenePath));
        }

        internal string CreateSentinel(string? path = null)
        {
            path ??= $"{Root}/UnrelatedSentinel.asset";
            var sentinel = ScriptableObject.CreateInstance<SceneResourceMap>();
            AssetDatabase.CreateAsset(sentinel, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        internal string GetAddressableAddress()
            => GetStringMember(GetAddressableEntry(), "address");

        internal void SetAddressableAddress(string address)
        {
            var entry = GetAddressableEntry();
            entry.GetType().GetProperty("address")!.SetValue(entry, address);
            var settings = GetAddressableSettings();
            Assert.That(settings, Is.Not.Null);
            EditorUtility.SetDirty((UnityEngine.Object)settings!);
            AssetDatabase.SaveAssets();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
            if (WorldCompanionRecoveryJournal.Exists)
            {
                try { WorldCompanionCreationTransaction.RecoverPending(); }
                catch { WorldCompanionRecoveryJournal.Delete(); }
            }
            RemoveExactAddress(Plan.ScenePath);
            RestoreOuterSetup();
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string CreateSavedScene(
            string path,
            bool leaveOpen,
            NewSceneMode mode = NewSceneMode.Additive)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
            var guid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(guid, Is.Not.Empty);
            if (leaveOpen)
            {
                if (SceneManager.GetActiveScene().handle != scene.handle)
                    Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            }
            else
            {
                Assert.That(EditorSceneManager.CloseScene(scene, removeScene: true), Is.True);
            }
            return guid;
        }

        private void RestoreOuterSetup()
        {
            if (_outerSetup.Any(item => item.isLoaded && !string.IsNullOrEmpty(item.path)))
            {
                EditorSceneManager.RestoreSceneManagerSetup(_outerSetup);
                return;
            }

            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        }

        private static List<T> LoadAll<T>(string root) where T : UnityEngine.Object
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList()!;

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SetupFingerprint(IEnumerable<SceneSetup> setup)
            => string.Join("|", setup.Select(item => $"{item.path}:{item.isLoaded}:{item.isActive}"));

        private static int AddressableAddressCount(string address)
        {
            var settings = GetAddressableSettings();
            return settings == null ? 0 : GetAddressableEntries(settings)
                .Count(entry => string.Equals(GetStringMember(entry, "address"), address, StringComparison.Ordinal));
        }

        private static void RemoveExactAddress(string address)
        {
            var settings = GetAddressableSettings();
            if (settings == null) return;
            var guids = GetAddressableEntries(settings)
                .Where(entry => string.Equals(GetStringMember(entry, "address"), address, StringComparison.Ordinal))
                .Select(entry => GetStringMember(entry, "guid"))
                .ToArray();
            var remove = settings.GetType().GetMethod(
                "RemoveAssetEntry",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: new[] { typeof(string), typeof(bool) },
                modifiers: null)!;
            foreach (var guid in guids) remove.Invoke(settings, new object[] { guid, false });
            if (guids.Length > 0)
            {
                EditorUtility.SetDirty((UnityEngine.Object)settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static object? GetAddressableSettings()
        {
            var type = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            return type?.GetProperty("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        }

        private object GetAddressableEntry()
        {
            var settings = GetAddressableSettings();
            Assert.That(settings, Is.Not.Null);
            var guid = AssetDatabase.AssetPathToGUID(Plan.ScenePath);
            return GetAddressableEntries(settings!).Single(
                entry => string.Equals(GetStringMember(entry, "guid"), guid, StringComparison.Ordinal));
        }

        private static IEnumerable<object> GetAddressableEntries(object settings)
        {
            var groups = (IEnumerable)settings.GetType().GetProperty("groups")!.GetValue(settings)!;
            foreach (var group in groups)
            {
                if (group == null) continue;
                var entries = (IEnumerable)group.GetType().GetProperty("entries")!.GetValue(group)!;
                foreach (var entry in entries)
                    if (entry != null) yield return entry;
            }
        }

        private static string GetStringMember(object target, string name)
        {
            var type = target.GetType();
            return (string?)(type.GetProperty(name)?.GetValue(target)
                ?? type.GetField(name)?.GetValue(target)) ?? string.Empty;
        }
    }
}
