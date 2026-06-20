using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// `NativeStateRegistry<TState>` の direct view 実行区間を表す lease。
    /// 
    /// snapshot batch では「切り出したコピーが独立して生きる」ため、
    /// registry 正本と batch 寿命の衝突を気にしなくてよかった。
    /// しかし direct view では、job が触る `NativeArray` 自体が registry 正本を指すため、
    /// 実行中に register/unregister/reorder/dispose が混ざるとメモリ安全性も契約整合性も崩れる。
    /// 
    /// そこで lease を導入し、
    /// 「いまこの registry 正本は実行中なので structural mutation を禁止する」
    /// という所有権を明示的に表す。
    /// 
    /// ここではあえて epoch を外部へ見せている。
    /// 理由は stale lease 完了や二重完了を検知しやすくし、
    /// テストでも『どの lease が有効か』を契約として固定できるようにするため。
    /// </summary>
    public sealed class NativeExecutionLease<TState> : IDisposable
        where TState : unmanaged
    {
        private readonly Action<uint> _onDispose;
        private bool _disposed;

        internal NativeExecutionLease(
            NativeExecutionBatch<TState> batch,
            uint leaseEpoch,
            Action<uint> onDispose)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            LeaseEpoch = leaseEpoch;
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public NativeExecutionBatch<TState> Batch { get; }

        public uint LeaseEpoch { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose(LeaseEpoch);
        }
    }
}
