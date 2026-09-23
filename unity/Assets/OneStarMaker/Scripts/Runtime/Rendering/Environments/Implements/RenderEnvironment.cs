#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// App 寿命の単一 owner 調停。実装置への書き込みは <see cref="IRenderEnvironmentSink"/> に委譲する。
    /// ここには季節語も URP 型も持たない。「誰が書いてよいか」と世代番号だけを決める。
    /// </summary>
    public sealed class RenderEnvironment : IRenderEnvironment, IDisposable
    {
        private readonly IRenderEnvironmentSink _sink;
        private object? _ownerKey;
        private int _generation;
        private Light? _boundSun;
        private bool _disposed;

        public RenderEnvironment(IRenderEnvironmentSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool HasActiveOwner => _ownerKey != null;

        public RenderEnvironmentLease Acquire(object ownerKey)
        {
            if (ownerKey == null)
            {
                throw new ArgumentNullException(nameof(ownerKey));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RenderEnvironment));
            }

            // 二件目は待たない。Try で false を返すと呼び出し側が無視し得るので即例外。
            if (_ownerKey != null)
            {
                throw new InvalidOperationException(
                    "RenderEnvironment already has an active owner. A second Acquire fails immediately.");
            }

            _sink.CaptureBaseline();
            _ownerKey = ownerKey;
            return new RenderEnvironmentLease(this, _generation);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // App 回収。生き残り lease を stale にするため、Restore の有無にかかわらず世代を進める。
            if (_ownerKey != null)
            {
                _sink.RestoreBaseline();
            }

            _boundSun = null;
            _ownerKey = null;
            _generation++;
            _disposed = true;
        }

        /// <summary>
        /// lease 経由の Bind。番号が今の世代と一致するときだけ太陽を覚える。
        /// Light は Scene 所有のまま。ここは参照を持つだけで Destroy しない。
        /// </summary>
        internal void BindSunFromLease(int generation, Light sun)
        {
            EnsureLeaseIsActive(generation);

            if (_boundSun != null)
            {
                throw new InvalidOperationException("BindSun can be called once per lease.");
            }

            // Unity 偽 null。is null / ?. だと破棄済み Light を通してしまう。
            if (sun == null)
            {
                throw new ArgumentNullException(nameof(sun));
            }

            _boundSun = sun;
        }

        /// <summary>
        /// lease 経由の Apply。権利証が生きていて Bind 済みのときだけ sink へ流す。
        /// 負値は装置へ出さず Validator で閉じる。
        /// </summary>
        internal void ApplyFromLease(int generation, in RenderEnvironmentState state)
        {
            EnsureLeaseIsActive(generation);

            if (_boundSun == null)
            {
                throw new InvalidOperationException("BindSun must be called before Apply.");
            }

            RenderEnvironmentValidator.Validate(state);
            _sink.Apply(state, _boundSun);
        }

        /// <summary>
        /// lease.Dispose。番号不一致や二度目は no-op。
        /// 一致したときだけ Restore し、参照を捨て、世代を進めて古い権利証を無効化する。
        /// </summary>
        internal void ReleaseFromLease(int generation)
        {
            if (_disposed)
            {
                return;
            }

            if (generation != _generation || _ownerKey == null)
            {
                return;
            }

            _sink.RestoreBaseline();
            _boundSun = null;
            _ownerKey = null;
            _generation++;
        }

        private void EnsureLeaseIsActive(int generation)
        {
            if (_disposed || generation != _generation || _ownerKey == null)
            {
                throw new InvalidOperationException("This RenderEnvironmentLease is no longer active.");
            }
        }
    }
}
