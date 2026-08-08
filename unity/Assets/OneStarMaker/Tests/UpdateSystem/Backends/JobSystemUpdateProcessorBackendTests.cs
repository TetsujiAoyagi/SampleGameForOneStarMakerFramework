#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Tests.UpdateSystem
{
    /// <summary>
    /// native state を job 経由で更新する backend の書き戻し契約を検証する。
    ///
    /// <para>
    /// batch 実行の結果が registry の正本へ writeBack されること、
    /// および writeBack しても dirty は明示的に clear するまで残ることを主張する。
    /// </para>
    /// </summary>
    [TestFixture]
    public class JobSystemUpdateProcessorBackendTests
    {
        [Test]
        public void ExecuteNative_batch実行結果がwriteBackされdirtyは残る()
        {
            using var registry = new NativeStateRegistry<TestState>();
            var context = new UpdateFrameContext(5, 2f, 2f, 1f, isPaused: false);
            var handle = registry.Register(new TestState { Value = 10, AppliedFrame = 0 }, executionOrder: 100);
            var backend = new JobSystemUpdateProcessorBackend<TestState, IncrementProcessor>(new IncrementProcessor());

            using var batch = registry.BuildExecutionBatch(UpdateExecutionPhase.Update, in context);
            backend.ExecuteNative(batch);
            registry.ApplyExecutionResult(batch, clearDirtyAfterWriteBack: false);

            Assert.That(registry.TryGetState(handle, out var state), Is.True);
            Assert.That(state.Value, Is.EqualTo(12));
            Assert.That(state.AppliedFrame, Is.EqualTo(5u));
            Assert.That(registry.IsDirty(handle), Is.True);
        }

        private struct TestState
        {
            public int Value;
            public uint AppliedFrame;
        }

        private struct IncrementProcessor : INativeUpdateJobProcessor<TestState>
        {
            public void Execute(
                int index,
                ref TestState state,
                ref byte dirtyFlag,
                UpdateExecutionPhase phase,
                in UpdateFrameContext context)
            {
                if (phase != UpdateExecutionPhase.Update)
                {
                    return;
                }

                state.Value += (int)context.DeltaTime;
                state.AppliedFrame = context.FrameIndex;
                dirtyFlag = 1;
            }
        }
    }
}
