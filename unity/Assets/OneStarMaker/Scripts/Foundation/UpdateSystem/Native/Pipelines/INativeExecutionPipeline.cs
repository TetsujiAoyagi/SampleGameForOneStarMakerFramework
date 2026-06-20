using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// world 全体、および Layer 内で管理される native pipeline の共通境界。
    /// `UpdateCoordinator` はこの interface 越しに pipeline を扱い、
    /// state 型依存の詳細には立ち入らない。
    /// </summary>
    internal interface INativeExecutionPipeline
    {
        int PipelineOrder { get; }

        string LayerId { get; }

        Type StateType { get; }

        bool UsesElement(UpdateHandle elementHandle);

        bool DetachElement(UpdateHandle elementHandle);

        bool TryReorder(UpdateHandle elementHandle, int executionOrder);

        void Run(
            UpdateExecutionPhase phase,
            in UpdateFrameContext context,
            Action<UpdateHandle> requestMainThreadApply);
    }
}
