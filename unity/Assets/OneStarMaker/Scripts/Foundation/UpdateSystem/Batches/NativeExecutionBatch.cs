using System;
using Unity.Collections;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// native/job path 用の dispatch 単位。
    /// managed path と違い、こちらは正本の NativeArray view をそのまま持つ。
    /// ただし現段階では registry の compaction / dispose と batch の寿命管理をまだ統合していないため、
    /// batch 自体は snapshot を所有する disposable 値として扱う。
    /// 将来 lease 管理を導入したら direct view へ切り替える余地を残す。
    /// </summary>
    public sealed class NativeExecutionBatch<TState> : IDisposable
        where TState : unmanaged
    {
        public NativeExecutionBatch(
            UpdateExecutionPhase phase,
            NativeArray<TState> states,
            NativeArray<UpdateHandle> handles,
            NativeArray<int> executionOrders,
            NativeArray<byte> dirtyFlags,
            in UpdateFrameContext context,
            bool ownsBuffers = true)
        {
            if (states.Length != handles.Length ||
                states.Length != executionOrders.Length ||
                states.Length != dirtyFlags.Length)
            {
                throw new ArgumentException("All native arrays in the batch must have the same length.");
            }

            Phase = phase;
            States = states;
            Handles = handles;
            ExecutionOrders = executionOrders;
            DirtyFlags = dirtyFlags;
            Context = context;
            OwnsBuffers = ownsBuffers;
        }

        public UpdateExecutionPhase Phase { get; }

        public NativeArray<TState> States { get; }

        public NativeArray<UpdateHandle> Handles { get; }

        public NativeArray<int> ExecutionOrders { get; }

        public NativeArray<byte> DirtyFlags { get; }

        public UpdateFrameContext Context { get; }

        public int ElementCount => States.Length;

        public bool IsCreated => States.IsCreated;

        /// <summary>
        /// この batch が内部 NativeArray の解放責任を持つか。
        /// snapshot batch では true、direct view / lease batch では false になる。
        /// 
        /// ここを分けている理由は、
        /// 現段階では snapshot と direct view が一定期間併存するため。
        /// Dispose の責任を batch ごとに明示することで、
        /// 将来 direct view 化を進めても「間違って registry 正本の NativeArray を Dispose してしまう」事故を防ぐ。
        /// </summary>
        public bool OwnsBuffers { get; }

        public void Dispose()
        {
            if (!OwnsBuffers)
            {
                return;
            }

            if (States.IsCreated)
            {
                States.Dispose();
            }

            if (Handles.IsCreated)
            {
                Handles.Dispose();
            }

            if (ExecutionOrders.IsCreated)
            {
                ExecutionOrders.Dispose();
            }

            if (DirtyFlags.IsCreated)
            {
                DirtyFlags.Dispose();
            }
        }
    }
}
