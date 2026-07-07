#nullable enable

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
    /// 確定 Pose に対する後段の補正処理（シェイク等）。スタックに積まれて登録順に適用される。
    /// </summary>
    public interface ICameraPoseModifier
    {
        /// <summary>
        /// Pose を補正する。継続する場合は true、寿命が尽きた場合は false を返し、false のときスタックから自動除去される。
        /// </summary>
        bool Apply(ref CameraPose pose, float deltaTime);
    }
}
