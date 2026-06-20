namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// Layer が組み立てた batch を実行する backend 境界。
    /// managed path / native path のどちらもここに集約し、
    /// Layer は「誰をこの phase で走らせるか」の決定だけに責務を絞る。
    /// </summary>
    public interface IUpdateExecutionBackend
    {
        void ExecuteManaged(in ManagedExecutionBatch batch);

        void ExecuteNative<TState>(NativeExecutionBatch<TState> batch)
            where TState : unmanaged;
    }
}
