#nullable enable

using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Abstractions
{
    /// <summary>
    /// RenderTexture 出力 View の描画頻度ポリシー。ミニマップ等は毎フレーム描く必要がないため、
    /// 更新間隔を落として GPU コストを抑える。
    /// </summary>
    public enum RenderTextureUpdateMode
    {
        /// <summary>毎フレーム描画する（既定・全画面 View）。</summary>
        EveryFrame = 0,

        /// <summary><see cref="CameraViewConfig.UpdateEveryNFrames"/> フレームごとに 1 回だけ描画する。</summary>
        EveryNFrames = 1,

        /// <summary>自動描画せず、明示要求時のみ描画する。</summary>
        Manual = 2,
    }

    /// <summary>
    /// View 生成時の不変設定。画面領域と、任意の RenderTexture 出力および更新頻度を保持する。
    /// </summary>
    public readonly struct CameraViewConfig
    {
        /// <summary>正規化された画面領域（0..1）。分割画面のレイアウトを表す。</summary>
        public Rect ViewportRect { get; init; }

        /// <summary>出力先 RenderTexture。null なら画面へ直接描画する。</summary>
        public RenderTexture? TargetTexture { get; init; }

        /// <summary>RenderTexture の更新頻度モード。</summary>
        public RenderTextureUpdateMode UpdateMode { get; init; }

        /// <summary><see cref="RenderTextureUpdateMode.EveryNFrames"/> 時の描画間隔（フレーム数）。</summary>
        public int UpdateEveryNFrames { get; init; }
    }
}
