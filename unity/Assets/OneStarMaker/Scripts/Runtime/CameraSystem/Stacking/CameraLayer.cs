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
    /// カメラの優先レイヤー。数値ではなく用途で優先度を表し、上位レイヤーが下位を覆う（Debug &gt; Cutscene &gt; Gameplay）。
    /// この序列は <see cref="CameraStack"/> のアクティブ解決順（LayerOrder）で使う。
    /// </summary>
    public enum CameraLayer
    {
        /// <summary>通常プレイ時のカメラ。最下位。</summary>
        Gameplay = 0,

        /// <summary>演出/カットシーン。Gameplay を覆う。</summary>
        Cutscene = 1,

        /// <summary>デバッグ用オーバーライド。最優先。</summary>
        Debug = 2,
    }
}
