using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// managed fallback path の基準実装。
    /// native path が未接続でも deterministic contract を保つための最小 backend であり、
    /// phase ごとに順番通り mirror を呼ぶだけの単純な実装にしている。
    /// </summary>
    public sealed class SequentialUpdateExecutionBackend : IUpdateExecutionBackend
    {
        public static readonly SequentialUpdateExecutionBackend Instance = new();

        private SequentialUpdateExecutionBackend()
        {
        }

        public void ExecuteManaged(in ManagedExecutionBatch batch)
        {
            var context = batch.Context;
            for (var i = 0; i < batch.Elements.Count; i++)
            {
                switch (batch.Phase)
                {
                    case UpdateExecutionPhase.Update:
                        batch.Elements[i].OnElementUpdate(in context);
                        break;

                    case UpdateExecutionPhase.LateUpdate:
                        batch.Elements[i].OnElementLateUpdate(in context);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(batch.Phase), batch.Phase, null);
                }
            }
        }

        public void ExecuteNative<TState>(NativeExecutionBatch<TState> batch)
            where TState : unmanaged
        {
            throw new NotSupportedException(
                "Native execution backend is not connected yet. Use a dedicated native backend for TState pipelines.");
        }
    }
}
