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
    }
}
