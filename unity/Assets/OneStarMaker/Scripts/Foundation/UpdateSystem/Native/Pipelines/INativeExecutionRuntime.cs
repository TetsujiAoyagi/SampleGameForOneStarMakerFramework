using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// `TState` 依存の native 実行詳細を表す共通境界。
    /// pipeline 管理層はこれを保持することで generic 実装を外へ逃がし、
    /// world 側に state 型依存を持ち込まない。
    /// </summary>
    internal interface INativeExecutionRuntime
    {
        Type StateType { get; }

        int Count { get; }

        void ValidateConfiguration(IUpdateExecutionBackend backend);

        bool Unregister(UpdateHandle nativeHandle);

        void SetExecutionOrder(UpdateHandle nativeHandle, int executionOrder);

        void Execute(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Action<UpdateHandle> publishDirtyNativeHandle);
    }
}
