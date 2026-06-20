namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// state-specific native backend が 1 要素ずつ `TState` を更新するための processor 契約。
    /// Job 実行中に呼ばれる最小単位の処理本体を表し、state と dirty flag の更新責務だけを持つ。
    /// </summary>
    public interface INativeUpdateJobProcessor<TState>
        where TState : unmanaged
    {
        void Execute(
            int index,
            ref TState state,
            ref byte dirtyFlag,
            UpdateExecutionPhase phase,
            in UpdateFrameContext context);
    }
}
