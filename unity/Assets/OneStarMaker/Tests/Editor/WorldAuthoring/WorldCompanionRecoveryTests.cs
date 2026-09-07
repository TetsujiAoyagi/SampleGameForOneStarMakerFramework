#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.WorldAuthoring
{
    [TestFixture]
    public sealed class WorldCompanionRecoveryTests
    {
        [SetUp]
        public void SetUp()
        {
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.False,
                "A real pending World Workspace operation must be recovered before these tests run.");
            DeleteJournalSidecars();
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
        }

        [TearDown]
        public void TearDown()
        {
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
            WorldCompanionRecoveryJournal.Delete();
            DeleteJournalSidecars();
        }

        [Test]
        public void Journal_RoundTripsOwnershipPathsAndBarrierState_ThroughAtomicReplace()
        {
            var data = CreateJournalData("first");
            WorldCompanionRecoveryJournal.Save(data);
            data.completedRecoveryBarrier = (int)WorldCompanionRecoveryBarrier.GraphUnlinked;
            WorldCompanionRecoveryJournal.Save(data);

            var loaded = WorldCompanionRecoveryJournal.Load();

            Assert.That(loaded.identity, Is.EqualTo("first"));
            Assert.That(loaded.sceneGuid, Is.EqualTo(data.sceneGuid));
            Assert.That(loaded.resourceOutputPath, Is.EqualTo(data.resourceOutputPath));
            Assert.That(loaded.sceneResourceMapPath, Is.EqualTo(data.sceneResourceMapPath));
            Assert.That(loaded.sourceSearchRoot, Is.EqualTo(data.sourceSearchRoot));
            Assert.That(loaded.completedRecoveryBarrier, Is.EqualTo(data.completedRecoveryBarrier));
            Assert.That(File.Exists(WorldCompanionRecoveryJournal.JournalPath + ".tmp"), Is.False);
            Assert.That(File.Exists(WorldCompanionRecoveryJournal.JournalPath + ".bak"), Is.False);
        }

        [Test]
        public void MissingJournal_DoesNotBlockWorkspace()
        {
            Assert.That(WorldCompanionRecoveryService.IsBlocked(out var message), Is.False);
            Assert.That(message, Is.Empty);
        }

        [Test]
        public void ValidJournal_BlocksWorkspaceWithoutChangingJournal()
        {
            var data = CreateJournalData("pending");
            WorldCompanionRecoveryJournal.Save(data);
            var before = File.ReadAllBytes(WorldCompanionRecoveryJournal.JournalPath);

            Assert.That(WorldCompanionRecoveryService.IsBlocked(out var message), Is.True);
            Assert.That(message, Does.Contain("pending"));
            Assert.That(File.ReadAllBytes(WorldCompanionRecoveryJournal.JournalPath), Is.EqualTo(before));
        }

        [Test]
        public void CorruptJournal_BlocksAndIsNotDeleted()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WorldCompanionRecoveryJournal.JournalPath)!);
            File.WriteAllText(WorldCompanionRecoveryJournal.JournalPath, "not-json");

            Assert.That(WorldCompanionRecoveryService.IsBlocked(out var message), Is.True);
            Assert.That(message, Does.Contain("corrupt"));
            Assert.That(File.Exists(WorldCompanionRecoveryJournal.JournalPath), Is.True);
        }

        [TestCaseSource(nameof(MutationPoints))]
        public void PendingJournal_FromEveryForwardPhase_RecoversAfterDomainReloadEquivalent(
            int mutationPointValue)
        {
            using var scope = new WorldCompanionTestScope();
            var mutationPoint = (WorldCompanionMutationPoint)mutationPointValue;
            var sentinelPath = scope.CreateSentinel();
            WorldCompanionCreationTransaction.FaultInjector = point =>
            {
                if (point == mutationPoint) throw new InjectedWorldCompanionFault(point.ToString());
            };
            WorldCompanionCreationTransaction.RecoveryFaultInjector = barrier =>
            {
                if (barrier == WorldCompanionRecoveryBarrier.ScenesRestored)
                    throw new InjectedWorldCompanionFault("domain-reload");
            };
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);

            Assert.Throws<AggregateException>(() => transaction.Execute());
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.True);
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;

            WorldCompanionCreationTransaction.RecoverPending();

            scope.AssertRolledBack();
            Assert.That(AssetDatabase.LoadMainAssetAtPath(sentinelPath), Is.Not.Null);
        }

        [TestCaseSource(nameof(RecoveryBarriers))]
        public void RecoveryBarrierFailure_RetainsSameJournal_AndSecondAttemptResumes(
            int recoveryBarrierValue)
        {
            using var scope = new WorldCompanionTestScope();
            var recoveryBarrier = (WorldCompanionRecoveryBarrier)recoveryBarrierValue;
            WorldCompanionCreationTransaction.FaultInjector = point =>
            {
                if (point == WorldCompanionMutationPoint.Regenerated)
                    throw new InjectedWorldCompanionFault("forward-complete");
            };
            WorldCompanionCreationTransaction.RecoveryFaultInjector = barrier =>
            {
                if (barrier == recoveryBarrier)
                    throw new InjectedWorldCompanionFault(barrier.ToString());
            };
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);

            Assert.Throws<AggregateException>(() => transaction.Execute());
            var retained = WorldCompanionRecoveryJournal.Load();
            Assert.That(retained.identity, Is.EqualTo(scope.Plan.Identity));
            Assert.That(retained.completedRecoveryBarrier, Is.EqualTo((int)recoveryBarrier - 1));
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;

            WorldCompanionCreationTransaction.RecoverPending();

            scope.AssertRolledBack();
        }

        [Test]
        public void Recovery_DoesNotDeleteUnexpectedCompanionFolderEntry()
        {
            using var scope = new WorldCompanionTestScope();
            WorldCompanionCreationTransaction.FaultInjector = point =>
            {
                if (point == WorldCompanionMutationPoint.Regenerated)
                    throw new InjectedWorldCompanionFault("forward-complete");
            };
            WorldCompanionCreationTransaction.RecoveryFaultInjector = barrier =>
            {
                if (barrier == WorldCompanionRecoveryBarrier.AssetsDeleted)
                    throw new InjectedWorldCompanionFault("before-assets");
            };
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);
            Assert.Throws<AggregateException>(() => transaction.Execute());
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
            var folder = Path.GetDirectoryName(scope.Plan.ScenePath)!.Replace('\\', '/');
            var sentinelPath = scope.CreateSentinel($"{folder}/UnownedSentinel.asset");

            Assert.Throws<InvalidOperationException>(() => WorldCompanionCreationTransaction.RecoverPending());
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.True);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(sentinelPath), Is.Not.Null);

            Assert.That(AssetDatabase.DeleteAsset(sentinelPath), Is.True);
            WorldCompanionCreationTransaction.RecoverPending();
            scope.AssertRolledBack();
        }

        [TestCase("Scene")]
        [TestCase("Node")]
        [TestCase("Resource")]
        public void Recovery_DoesNotDeleteExternallyChangedPendingAsset(string assetKind)
        {
            using var scope = new WorldCompanionTestScope();
            LeaveFullyCreatedPending(scope);
            var path = assetKind switch
            {
                "Scene" => scope.Plan.ScenePath,
                "Node" => scope.Plan.NodePath,
                "Resource" => scope.Plan.ResourcePath,
                _ => throw new ArgumentOutOfRangeException(nameof(assetKind)),
            };
            var guid = AssetDatabase.AssetPathToGUID(path);
            MutateAsset(path, assetKind);

            var exception = Assert.Throws<InvalidOperationException>(
                () => WorldCompanionCreationTransaction.RecoverPending());
            Assert.That(exception!.Message, Does.Contain("Content ownership mismatch"));
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null);
        }

        [Test]
        public void Recovery_DoesNotDeleteExternallyChangedAddressableEntry_AndRetriesAfterCorrection()
        {
            using var scope = new WorldCompanionTestScope();
            LeaveFullyCreatedPending(scope);
            var externalAddress = scope.Plan.ScenePath + ".external";
            scope.SetAddressableAddress(externalAddress);

            var exception = Assert.Throws<InvalidOperationException>(
                () => WorldCompanionCreationTransaction.RecoverPending());
            Assert.That(exception!.Message, Does.Contain("Addressables ownership mismatch"));
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.True);
            Assert.That(scope.GetAddressableAddress(), Is.EqualTo(externalAddress));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(scope.Plan.ScenePath), Is.Not.Null);

            scope.SetAddressableAddress(scope.Plan.ScenePath);
            WorldCompanionCreationTransaction.RecoverPending();
            scope.AssertRolledBack();
        }

        private static void LeaveFullyCreatedPending(WorldCompanionTestScope scope)
        {
            WorldCompanionCreationTransaction.FaultInjector = point =>
            {
                if (point == WorldCompanionMutationPoint.Regenerated)
                    throw new InjectedWorldCompanionFault("forward-complete");
            };
            WorldCompanionCreationTransaction.RecoveryFaultInjector = barrier =>
            {
                if (barrier == WorldCompanionRecoveryBarrier.ScenesRestored)
                    throw new InjectedWorldCompanionFault("before-recovery");
            };
            using var transaction = new WorldCompanionCreationTransaction(scope.Plan);
            Assert.Throws<AggregateException>(() => transaction.Execute());
            WorldCompanionCreationTransaction.FaultInjector = null;
            WorldCompanionCreationTransaction.RecoveryFaultInjector = null;
            Assert.That(WorldCompanionRecoveryJournal.Exists, Is.True);
        }

        private static void MutateAsset(string path, string assetKind)
        {
            if (string.Equals(assetKind, "Scene", StringComparison.Ordinal))
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var external = new GameObject("ExternalChange");
                SceneManager.MoveGameObjectToScene(external, scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                Assert.That(EditorSceneManager.CloseScene(scene, removeScene: true), Is.True);
                return;
            }
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(asset, Is.Not.Null);
            asset!.name += "_ExternalChange";
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static IEnumerable<int> MutationPoints()
        {
            foreach (WorldCompanionMutationPoint value in Enum.GetValues(typeof(WorldCompanionMutationPoint)))
                yield return (int)value;
        }

        private static IEnumerable<int> RecoveryBarriers()
        {
            foreach (WorldCompanionRecoveryBarrier value in Enum.GetValues(typeof(WorldCompanionRecoveryBarrier)))
                if (value != WorldCompanionRecoveryBarrier.None) yield return (int)value;
        }

        private static WorldCompanionJournalData CreateJournalData(string identity)
            => new()
            {
                identity = identity,
                parentIdentity = "parent",
                scenePath = $"Assets/__WorldCompanionJournalTests__/{identity}/{identity}.unity",
                resourcePath = $"Assets/__WorldCompanionJournalTests__/{identity}/{identity}.asset",
                nodePath = $"Assets/__WorldCompanionJournalTests__/Source/{identity}.asset",
                companionFolderPath = $"Assets/__WorldCompanionJournalTests__/{identity}",
                resourceOutputPath = "Assets/__WorldCompanionJournalTests__/Generated",
                sceneResourceMapPath = "Assets/__WorldCompanionJournalTests__/Map.asset",
                sourceSearchRoot = "Assets/__WorldCompanionJournalTests__/Source",
                startingSourceHash = "0123456789abcdef",
                parentNodeGuid = "11111111111111111111111111111111",
                graphGuid = "22222222222222222222222222222222",
                sceneGuid = "33333333333333333333333333333333",
                sceneFingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                resourceFingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                nodeFingerprint = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                addressableFingerprint = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            };

        private static void DeleteJournalSidecars()
        {
            var temp = WorldCompanionRecoveryJournal.JournalPath + ".tmp";
            var backup = WorldCompanionRecoveryJournal.JournalPath + ".bak";
            if (File.Exists(temp)) File.Delete(temp);
            if (File.Exists(backup)) File.Delete(backup);
        }
    }
}
