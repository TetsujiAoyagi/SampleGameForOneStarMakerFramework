#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Rendering.Vfx;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// フレームの時間の値と Layer の状態をテストが直接決める偽の時間源。
    /// 実 UpdateCoordinator を使わずに、Pause・倍率・頭打ちの delta・異常な delta のフレーム列を組み立てるために使う。
    /// </summary>
    internal sealed class FakeVfxTimeSource : IVfxTimeSource
    {
        private readonly Dictionary<string, (bool Paused, float Scale)> _layers = new(StringComparer.Ordinal);

        public float DeltaTime { get; set; } = 0.016f;

        public float UnscaledDeltaTime { get; set; } = 0.016f;

        /// <summary>Layer を作る（または状態を書き換える）。作っていない Layer は「無い」と答える。</summary>
        public void SetLayer(string layerId, bool paused = false, float scale = 1f) => _layers[layerId] = (paused, scale);

        public void SetPaused(string layerId, bool paused)
        {
            var scale = _layers.TryGetValue(layerId, out var state) ? state.Scale : 1f;
            _layers[layerId] = (paused, scale);
        }

        public void SetScale(string layerId, float scale)
        {
            var paused = _layers.TryGetValue(layerId, out var state) && state.Paused;
            _layers[layerId] = (paused, scale);
        }

        /// <summary>そのフレームの delta を決める（unscaled を省略すると scaled と同じ）。</summary>
        public void SetFrame(float deltaTime, float? unscaledDeltaTime = null)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime ?? deltaTime;
        }

        public bool TryGetLayerState(string layerId, out bool isPaused, out float timeScale)
        {
            if (_layers.TryGetValue(layerId, out var state))
            {
                isPaused = state.Paused;
                timeScale = state.Scale;
                return true;
            }

            isPaused = false;
            timeScale = 1f;
            return false;
        }
    }
}
