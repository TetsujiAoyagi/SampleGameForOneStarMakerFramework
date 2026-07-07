#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Stacking
{
    /// <summary>
    /// Push で積んだスタックエントリの所有権を表すハンドル。Dispose で対応エントリを Pop する。
    /// 二重 Dispose は冪等（無害）にするが、内部 owner 参照は初回で手放して以後の誤操作を防ぐ。
    /// </summary>
    public sealed class CameraStackHandle : IDisposable
    {
        private CameraStack? _owner;
        private readonly int _entryId;
        private bool _disposed;

        internal CameraStackHandle(CameraStack owner, int entryId)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _entryId = entryId;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner?.RemoveEntry(_entryId);
            _owner = null;
        }

        internal bool IsDisposed => _disposed;
    }
}
