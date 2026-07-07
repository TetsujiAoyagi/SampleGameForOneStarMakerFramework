#nullable enable

using System;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Modifiers
{
    /// <summary>
    /// AddModifier で登録した Modifier の所有権ハンドル。Dispose でスタックから取り外す。
    /// 二重 Dispose は冪等。owner は初回で手放して以後の誤除去を防ぐ。
    /// </summary>
    public sealed class CameraModifierHandle : IDisposable
    {
        private CameraModifierStack? _owner;
        private readonly ICameraPoseModifier _modifier;
        private bool _disposed;

        internal CameraModifierHandle(CameraModifierStack owner, ICameraPoseModifier modifier)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _modifier = modifier ?? throw new ArgumentNullException(nameof(modifier));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _owner?.RemoveModifier(_modifier);
            _owner = null;
        }

        internal bool IsDisposed => _disposed;
    }
}
