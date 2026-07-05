#nullable enable

using System;
using R3;

namespace OneStarMaker.Runtime.UISystem.Mvvm
{
    /// <summary>
    /// ViewModel の基底クラス。R3 購読の寿命管理を統一する。
    /// </summary>
    public abstract class ViewModelBase : IDisposable
    {
        private bool _disposed;

        /// <summary>購読を集約する CompositeDisposable。</summary>
        protected CompositeDisposable Disposables { get; } = new();

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Disposables.Dispose();
            DisposeCore();
        }

        /// <summary>派生クラス用の追加クリーンアップ。</summary>
        protected virtual void DisposeCore() { }
    }
}
