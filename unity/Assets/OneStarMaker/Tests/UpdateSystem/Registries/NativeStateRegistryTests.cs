#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Tests.UpdateSystem
{
    [TestFixture]
    public class NativeStateRegistryTests
    {
        [Test]
        public void Register_Handleとstateとorderを保持できる()
        {
            using var registry = new NativeStateRegistry<TestState>();

            var first = registry.Register(new TestState { Value = 10 }, executionOrder: 20);
            var second = registry.Register(new TestState { Value = 30 }, executionOrder: 40, isDirty: true);

            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.Contains(first), Is.True);
            Assert.That(registry.Contains(second), Is.True);
            Assert.That(registry.TryGetState(first, out var firstState), Is.True);
            Assert.That(registry.TryGetExecutionOrder(second, out var secondOrder), Is.True);
            Assert.That(firstState.Value, Is.EqualTo(10));
            Assert.That(secondOrder, Is.EqualTo(40));
            Assert.That(registry.IsDirty(second), Is.True);
        }

        [Test]
        public void Unregister_SwapBack後も残存handleが正しく引ける()
        {
            using var registry = new NativeStateRegistry<TestState>();

            var first = registry.Register(new TestState { Value = 10 }, executionOrder: 10);
            var second = registry.Register(new TestState { Value = 20 }, executionOrder: 20);
            var third = registry.Register(new TestState { Value = 30 }, executionOrder: 30);

            Assert.That(registry.Unregister(second), Is.True);
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.Contains(second), Is.False);
            Assert.That(registry.TryGetDenseIndex(third, out var thirdDenseIndex), Is.True);
            Assert.That(thirdDenseIndex, Is.EqualTo(1));
            Assert.That(registry.TryGetState(third, out var movedState), Is.True);
            Assert.That(movedState.Value, Is.EqualTo(30));
            Assert.That(registry.Contains(first), Is.True);
        }

        [Test]
        public void ReuseSlot_世代が進み古いhandleは無効になる()
        {
            using var registry = new NativeStateRegistry<TestState>();

            var original = registry.Register(new TestState { Value = 10 });
            Assert.That(registry.Unregister(original), Is.True);

            var reused = registry.Register(new TestState { Value = 20 });

            Assert.That(reused.Slot, Is.EqualTo(original.Slot));
            Assert.That(reused.Generation, Is.Not.EqualTo(original.Generation));
            Assert.That(registry.Contains(original), Is.False);
            Assert.That(registry.Contains(reused), Is.True);
        }

        [Test]
        public void BuildExecutionBatch_NativeArrayViewを返せる()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(
                frameIndex: 7,
                deltaTime: 0.5f,
                unscaledDeltaTime: 1f,
                timeScale: 0.5f,
                isPaused: false);

            registry.Register(new TestState { Value = 10 }, executionOrder: 100);
            registry.Register(new TestState { Value = 20 }, executionOrder: 200, isDirty: true);

            using var batch = registry.BuildExecutionBatch(UpdateExecutionPhase.Update, in context);

            Assert.That(batch.ElementCount, Is.EqualTo(2));
            Assert.That(batch.States[0].Value, Is.EqualTo(10));
            Assert.That(batch.States[1].Value, Is.EqualTo(20));
            Assert.That(batch.ExecutionOrders[0], Is.EqualTo(100));
            Assert.That(batch.ExecutionOrders[1], Is.EqualTo(200));
            Assert.That(batch.DirtyFlags[0], Is.EqualTo(0));
            Assert.That(batch.DirtyFlags[1], Is.EqualTo(1));
            Assert.That(batch.Context.FrameIndex, Is.EqualTo(7u));
        }

        [Test]
        public void BuildExecutionBatch_生成後にregistryが変化してもsnapshotは不変()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(1, 1f, 1f, 1f, isPaused: false);

            var first = registry.Register(new TestState { Value = 10 }, executionOrder: 100);
            registry.Register(new TestState { Value = 20 }, executionOrder: 200);

            using var batch = registry.BuildExecutionBatch(UpdateExecutionPhase.Update, in context);

            registry.SetState(first, new TestState { Value = 999 });
            Assert.That(registry.Unregister(first), Is.True);

            Assert.That(batch.ElementCount, Is.EqualTo(2));
            Assert.That(batch.States[0].Value, Is.EqualTo(10));
            Assert.That(batch.States[1].Value, Is.EqualTo(20));
        }

        [Test]
        public void ApplyExecutionResult_dirtyExport後clear指定ならregistry側dirtyを戻せる()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(3, 1f, 1f, 1f, isPaused: false);
            var handle = registry.Register(new TestState { Value = 10 }, executionOrder: 100);

            using var batch = registry.BuildExecutionBatch(UpdateExecutionPhase.Update, in context);
            var states = batch.States;
            var dirtyFlags = batch.DirtyFlags;
            states[0] = new TestState { Value = 42 };
            dirtyFlags[0] = 1;

            registry.ApplyExecutionResult(batch, clearDirtyAfterWriteBack: true);

            Assert.That(registry.TryGetState(handle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(42));
            Assert.That(registry.IsDirty(handle), Is.False);
        }

        [Test]
        public void ApplyExecutionResult_snapshot生成後にunregisterされたhandleは安全に無視する()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(4, 1f, 1f, 1f, isPaused: false);
            var first = registry.Register(new TestState { Value = 10 }, executionOrder: 100);
            var second = registry.Register(new TestState { Value = 20 }, executionOrder: 200);

            using var batch = registry.BuildExecutionBatch(UpdateExecutionPhase.Update, in context);
            var states = batch.States;
            var dirtyFlags = batch.DirtyFlags;
            states[0] = new TestState { Value = 111 };
            states[1] = new TestState { Value = 222 };
            dirtyFlags[0] = 1;
            dirtyFlags[1] = 1;

            Assert.That(registry.Unregister(first), Is.True);

            registry.ApplyExecutionResult(batch, clearDirtyAfterWriteBack: true);

            Assert.That(registry.Contains(first), Is.False);
            Assert.That(registry.TryGetState(second, out var secondState), Is.True);
            Assert.That(secondState.Value, Is.EqualTo(222));
            Assert.That(registry.IsDirty(second), Is.False);
        }

        [Test]
        public void BeginExecutionLease_directView更新はwriteBackなしで正本へ反映される()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(5, 1f, 1f, 1f, isPaused: false);
            var handle = registry.Register(new TestState { Value = 10 }, executionOrder: 100);

            using var lease = registry.BeginExecutionLease(UpdateExecutionPhase.Update, in context);
            var states = lease.Batch.States;
            var dirtyFlags = lease.Batch.DirtyFlags;
            states[0] = new TestState { Value = 88 };
            dirtyFlags[0] = 1;
            registry.ClearAllDirtyForLease(lease.LeaseEpoch);

            Assert.That(registry.TryGetState(handle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(88));
            Assert.That(registry.IsDirty(handle), Is.False);
        }

        [Test]
        public void Register_lease中はmutationを拒否する()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(6, 1f, 1f, 1f, isPaused: false);

            using var lease = registry.BeginExecutionLease(UpdateExecutionPhase.Update, in context);

            Assert.Throws<InvalidOperationException>(
                () => registry.Register(new TestState { Value = 1 }, executionOrder: 10));
        }

        [Test]
        public void CompleteExecutionLease_完了後に同じepochを再度完了するとstaleとして拒否する()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(7, 1f, 1f, 1f, isPaused: false);

            var lease = registry.BeginExecutionLease(UpdateExecutionPhase.Update, in context);
            var epoch = lease.LeaseEpoch;
            lease.Dispose();

            Assert.Throws<InvalidOperationException>(() => registry.CompleteExecutionLease(epoch));
        }

        private struct TestState
        {
            public int Value;
        }
    }
}
