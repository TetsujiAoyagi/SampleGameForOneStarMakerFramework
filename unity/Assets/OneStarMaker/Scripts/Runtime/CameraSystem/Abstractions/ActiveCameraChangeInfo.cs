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
    /// スタックの解決結果としてアクティブカメラが入れ替わった事実を通知する。
    /// BlendSpec は「その遷移を引き起こした操作」由来（Push なら入場側、Pop なら退場側）を運ぶ。
    /// </summary>
    public readonly struct ActiveCameraChangeInfo
    {
        /// <summary>切替前のアクティブカメラ。</summary>
        public LogicalCamera PreviousCamera { get; init; }

        /// <summary>切替後のアクティブカメラ。</summary>
        public LogicalCamera NewCamera { get; init; }

        /// <summary>この遷移に適用するブレンド設定。</summary>
        public CameraBlendSpec BlendSpec { get; init; }
    }
}
