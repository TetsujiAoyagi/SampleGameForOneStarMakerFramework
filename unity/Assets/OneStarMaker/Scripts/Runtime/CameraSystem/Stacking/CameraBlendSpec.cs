#nullable enable

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
    /// ブレンド遷移の補間カーブ。
    /// </summary>
    public enum CameraBlendEasing
    {
        /// <summary>線形補間。</summary>
        Linear = 0,

        /// <summary>両端を滑らかにする smoothstep 補間。</summary>
        EaseInOut = 1,
    }

    /// <summary>
    /// カメラ切替の遷移設定（所要時間と補間）。ポリシー側で組み立て、Backend が具体的なブレンドへ翻訳する。
    /// DurationSec が 0 以下なら「カット」（即時切替）として扱う。
    /// </summary>
    public readonly struct CameraBlendSpec
    {
        /// <summary>ブレンド所要秒数。0 以下はカット。</summary>
        public float DurationSec { get; init; }

        /// <summary>補間カーブ。</summary>
        public CameraBlendEasing Easing { get; init; }

        /// <summary>即時切替（カット）を表す共有値。</summary>
        public static CameraBlendSpec Cut => new()
        {
            DurationSec = 0f,
            Easing = CameraBlendEasing.Linear,
        };
    }
}
