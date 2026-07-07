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
    /// カメラ制御のポリシー層エントリポイント。View の生成/解放と MainView へのアクセスのみを公開し、
    /// Cinemachine 等の具体的な描画メカニズムは <see cref="ICameraBackend"/> の裏に隠す。
    /// </summary>
    public interface ICameraSystem
    {
        /// <summary>常に存在する全画面 View。解放は不可。</summary>
        ICameraView MainView { get; }

        /// <summary>分割画面や RT ミニマップ用の追加 View を生成する。</summary>
        ICameraView CreateView(in CameraViewConfig config);

        /// <summary>この System が生成した追加 View を解放する。所有権外の View は例外にする。</summary>
        void ReleaseView(ICameraView view);
    }
}
