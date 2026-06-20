using System;
using Unity.Collections;
using Unity.Jobs;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// 単一 `TState` / 単一 processor を担当する JobSystem native backend。
    /// native batch の state 配列を並列実行し、各 index の更新処理を processor へ委譲する。
    /// </summary>
    public sealed class JobSystemUpdateProcessorBackend<TState, TProcessor> : IUpdateExecutionBackend
        where TState : unmanaged
        where TProcessor : struct, INativeUpdateJobProcessor<TState>
    {
        private readonly TProcessor _processor;
        private readonly int _innerloopBatchCount;

        public JobSystemUpdateProcessorBackend(TProcessor processor, int innerloopBatchCount = 64)
        {
            if (innerloopBatchCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(innerloopBatchCount));
            }

            _processor = processor;
            _innerloopBatchCount = innerloopBatchCount;
        }

        public void ExecuteManaged(in ManagedExecutionBatch batch)
        {
            throw new NotSupportedException(
                "JobSystemUpdateProcessorBackend is a state-specific native backend and does not execute managed fallback batches.");
        }

        public void ExecuteNative<TBatchState>(NativeExecutionBatch<TBatchState> batch)
            where TBatchState : unmanaged
        {
            if (batch == null)
            {
                throw new ArgumentNullException(nameof(batch));
            }

            if (batch is not NativeExecutionBatch<TState> typedBatch)
            {
                throw new NotSupportedException(
                    $"This backend handles only '{typeof(TState).Name}', but '{typeof(TBatchState).Name}' was requested.");
            }

            if (!typedBatch.IsCreated || typedBatch.ElementCount == 0)
            {
                return;
            }

            var job = new NativeUpdateJob
            {
                States = typedBatch.States,
                DirtyFlags = typedBatch.DirtyFlags,
                Phase = typedBatch.Phase,
                Context = typedBatch.Context,
                Processor = _processor,
            };

            var handle = job.Schedule(typedBatch.ElementCount, _innerloopBatchCount);
            handle.Complete();
        }

        private struct NativeUpdateJob : IJobParallelFor
        {
            public NativeArray<TState> States;

            public NativeArray<byte> DirtyFlags;

            [ReadOnly]
            public UpdateExecutionPhase Phase;

            [ReadOnly]
            public UpdateFrameContext Context;

            [ReadOnly]
            public TProcessor Processor;

            public void Execute(int index)
            {
                var state = States[index];
                var dirtyFlag = DirtyFlags[index];
                Processor.Execute(index, ref state, ref dirtyFlag, Phase, in Context);
                States[index] = state;
                DirtyFlags[index] = dirtyFlag;
            }
        }
    }
}
