#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SampleGame.InGame.Streaming;

namespace OneStarMaker.Tests.Streaming
{
    /// <summary>
    /// S-4b: issued registry は Dispose / Stop で完了扱いしない。回収 Unload は未完了 Add に限る。
    /// </summary>
    [TestFixture]
    public sealed class IssuedSceneOperationRegistryTests
    {
        [Test]
        public void Dispose_DoesNotCompleteIncompleteOps()
        {
            var registry = new IssuedSceneOperationRegistry();
            Assert.That(registry.TryRegisterAdd("Season_Spring", out var opId), Is.True);
            Assert.That(registry.IsComplete(opId), Is.False);
            Assert.That(registry.HasIncomplete(), Is.True);

            registry.Dispose();

            Assert.That(registry.IsComplete(opId), Is.False);
            Assert.That(registry.HasIncomplete(), Is.True);
        }

        [Test]
        public void StopNewIssues_RejectsNewAdd_AllowsRecoveryUnload()
        {
            var registry = new IssuedSceneOperationRegistry();
            Assert.That(registry.TryRegisterAdd("Spring_Cell_0_4", out _), Is.True);
            registry.StopNewIssues();

            Assert.That(registry.TryRegisterAdd("Spring_Cell_1_4", out _), Is.False);
            Assert.That(registry.TryRegisterUnload("Spring_Cell_0_4", out var unloadOp), Is.True);
            Assert.That(registry.IsComplete(unloadOp), Is.False);
            Assert.That(registry.TryRegisterUnload("Spring_Cell_1_4", out _), Is.False);
        }

        [Test]
        public async Task CompleteInstance_CompletesBoundOps()
        {
            var registry = new IssuedSceneOperationRegistry();
            Assert.That(registry.TryRegisterAdd("Season_Spring", out var opId), Is.True);
            registry.BindInstance(opId, instanceGen: 3);

            var wait = registry.WaitAllIncomplete(CancellationToken.None);
            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));

            registry.CompleteInstance("Season_Spring", 3);
            await wait;
            Assert.That(registry.IsComplete(opId), Is.True);
        }

        [Test]
        public void CompleteInstance_DoesNotCompleteDifferentGeneration()
        {
            var registry = new IssuedSceneOperationRegistry();
            Assert.That(registry.TryRegisterAdd("Season_Spring", out var oldOp), Is.True);
            Assert.That(registry.TryRegisterAdd("Season_Spring", out var laterOp), Is.True);
            registry.BindInstance(oldOp, instanceGen: 1);
            registry.BindInstance(laterOp, instanceGen: 2);

            registry.CompleteInstance("Season_Spring", 1);

            Assert.That(registry.IsComplete(oldOp), Is.True);
            Assert.That(registry.IsComplete(laterOp), Is.False);
        }

        [Test]
        public void CompleteInstance_SyncContinuationRegister_DoesNotThrowOrCompleteLaterOp()
        {
            var registry = new IssuedSceneOperationRegistry();
            Assert.That(registry.TryRegisterAdd("Season_Spring", out var addOp), Is.True);
            Assert.That(registry.TryRegisterUnload("Season_Spring", out var unloadOp), Is.True);
            registry.BindInstance(addOp, instanceGen: 1);
            registry.BindInstance(unloadOp, instanceGen: 1);

            var laterOp = 0;
            var continuationRan = false;
            var wait = registry.WaitAllIncomplete(CancellationToken.None);
            wait.GetAwaiter().OnCompleted(() =>
            {
                continuationRan = true;
                Assert.That(registry.TryRegisterAdd("Season_Spring", out laterOp), Is.True);
                registry.BindInstance(laterOp, instanceGen: 1);
            });

            Assert.DoesNotThrow(() => registry.CompleteInstance("Season_Spring", 1));

            Assert.That(continuationRan, Is.True);
            Assert.That(registry.IsComplete(addOp), Is.True);
            Assert.That(registry.IsComplete(unloadOp), Is.True);
            Assert.That(laterOp, Is.Not.EqualTo(0));
            Assert.That(registry.IsComplete(laterOp), Is.False);
        }
    }
}
