#nullable enable

using System;
using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// 環境書き込みの権利証。token は整数 <see cref="Generation"/> で、
    /// Dispose 後や新しい owner のあとでは Apply は失敗し、Dispose は新しい owner を消さない。
    /// 本体の状態は持たず <see cref="RenderEnvironment"/> へ委譲する。
    /// </summary>
    public sealed class RenderEnvironmentLease : IDisposable
    {
        private readonly RenderEnvironment _environment;

        internal RenderEnvironmentLease(RenderEnvironment environment, int generation)
        {
            _environment = environment;
            Generation = generation;
        }

        /// <summary>Acquire 時点の世代番号。以後変えない。照合は Environment 側が行う。</summary>
        public int Generation { get; }

        public void BindSun(Light sun)
        {
            _environment.BindSunFromLease(Generation, sun);
        }

        public void Apply(in RenderEnvironmentState state)
        {
            _environment.ApplyFromLease(Generation, state);
        }

        public void Dispose()
        {
            _environment.ReleaseFromLease(Generation);
        }
    }
}
