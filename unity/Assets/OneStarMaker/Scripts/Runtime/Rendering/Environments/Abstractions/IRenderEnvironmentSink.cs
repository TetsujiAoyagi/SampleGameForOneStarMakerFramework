#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.Rendering.Environments
{
    /// <summary>
    /// ポリシー層（RenderEnvironment）が決めた state を、実装置へ翻訳する境界。
    /// CameraSystem の <c>ICameraBackend</c> と同型。ポリシー層は RenderSettings を直接触らない。
    /// </summary>
    public interface IRenderEnvironmentSink
    {
        /// <summary>
        /// いまの装置状態を退避する。Acquire 成功時に一度だけ呼ぶ。
        /// 太陽 Light は Scene とともに消えるので、ここには残さない。
        /// </summary>
        void CaptureBaseline();

        /// <summary>
        /// state を太陽と fog / ambient へ書く。
        /// <paramref name="sun"/> は nullable にしない。lease が Bind 済みだけを渡す。
        /// 実装は Unity 偽 null（<c>sun == null</c>）なら何も書かず例外。
        /// </summary>
        void Apply(in RenderEnvironmentState state, Light sun);

        /// <summary>
        /// Capture した装置状態へ戻す。matching Dispose と App 回収で呼ぶ。
        /// stale Dispose からは呼ばない。
        /// </summary>
        void RestoreBaseline();
    }
}
