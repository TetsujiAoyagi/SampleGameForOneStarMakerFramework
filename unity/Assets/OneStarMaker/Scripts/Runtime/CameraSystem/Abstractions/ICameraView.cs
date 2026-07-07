#nullable enable

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
    /// 1 つの描画対象（画面領域 / RenderTexture）に対する論理カメラの操作面。
    /// カメラの積み上げ（Push）と姿勢後処理（AddModifier）を受け付け、確定結果を Snapshot として読み取らせる。
    /// </summary>
    public interface ICameraView
    {
        /// <summary>指定レイヤーへ論理カメラを積む。戻り値ハンドルの Dispose が Pop に対応する。</summary>
        CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend);

        /// <summary>確定 Pose に後段の補正（シェイク等）を掛ける Modifier を追加する。</summary>
        CameraModifierHandle AddModifier(ICameraPoseModifier modifier);

        /// <summary>直近 Tick で確定した Pose / Frustum / 速度。</summary>
        CameraViewSnapshot Snapshot { get; }

        /// <summary>ブレンド中のみ有効なブレンド先 Pose。ストリーミング等の先読み入力に使う。非ブレンド時は null。</summary>
        CameraViewSnapshot? IncomingSnapshot { get; }
    }
}
