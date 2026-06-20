#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;

namespace OneStarMaker.Tests.UpdateSystem
{
    [TestFixture]
    public class JobSystemUpdateProcessorBackendTests
    {
        [Test]
        public void Test1()
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
