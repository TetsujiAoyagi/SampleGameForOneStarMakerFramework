using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// `TState` ごとの lease 実行・backend 実行・dirty export を担当する generic 実行層。
    /// world 管理層から state 型依存を剥がしつつ、
    /// 既存の Phase A/B 契約はこの runtime 側で維持する。
    /// </summary>
    internal sealed class NativeExecutionRuntime<TState> : INativeExecutionRuntime
        where TState : unmanaged
    {
        // 実際の native state 正本。
        private readonly NativeStateRegistry<TState> _registry;

        // この state 系統をどう実行するかを知っている backend。
        private readonly IUpdateExecutionBackend _backend;

        public NativeExecutionRuntime(
            NativeStateRegistry<TState> registry,
            IUpdateExecutionBackend backend)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }

        public Type StateType => typeof(TState);

        public int Count => _registry.Count;

        public UpdateHandle Register(in TState initialState, int executionOrder)
        {
            return _registry.Register(initialState, executionOrder);
        }

        public void ValidateConfiguration(IUpdateExecutionBackend backend)
        {
            if (!ReferenceEquals(_backend, backend))
            {
                throw new InvalidOperationException(
                    "The same native registry cannot be attached to multiple backend instances.");
            }
        }

        public bool Unregister(UpdateHandle nativeHandle)
        {
            return _registry.Unregister(nativeHandle);
        }

        public void SetExecutionOrder(UpdateHandle nativeHandle, int executionOrder)
        {
            _registry.SetExecutionOrder(nativeHandle, executionOrder);
        }

        public void Execute(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Action<UpdateHandle> publishDirtyNativeHandle)
        {
            // Phase A では snapshot batch をやめ、
            // registry 正本を direct view で貸し出す lease 契約へ切り替える。
            // これにより copy-back は不要になるが、
            // 代わりに lease 中は structural mutation を明示的に禁止して ownership を守る。
            using var lease = _registry.BeginExecutionLease(phase, in context);
            var batch = lease.Batch;
            var exportCompleted = false;

            try
            {
                _backend.ExecuteNative(batch);

                for (var i = 0; i < batch.ElementCount; i++)
                {
                    if (batch.DirtyFlags[i] == 0)
                    {
                        continue;
                    }

                    publishDirtyNativeHandle(batch.Handles[i]);
                }

                exportCompleted = true;
            }
            finally
            {
                // direct view では dirty flag 自体も正本配列を job が直接更新している。
                // そのため clear は「backend 実行が終わった」だけではなく、
                // mirror handle への export まで成功した場合にだけ行う。
                // 途中で例外が出たフレームは dirty を残し、
                // 後続調査時に『何が未反映か』を失わないようにする。
                if (exportCompleted)
                {
                    _registry.ClearAllDirtyForLease(lease.LeaseEpoch);
                }
            }
        }
    }
}
