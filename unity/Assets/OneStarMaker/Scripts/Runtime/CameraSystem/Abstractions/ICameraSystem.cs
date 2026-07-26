#nullable enable

using OneStarMaker.Runtime.CameraSystem.Stacking;
using UnityEngine;

namespace OneStarMaker.Runtime.CameraSystem.Abstractions
{
    /// <summary>
    /// カメラ制御のポリシー層エントリポイント。
    /// View 管理に加え、Game 層が Cinemachine 型を知らなくても追従カメラを組める薄いファサードを提供する。
    /// 具体的な描画メカニズムは <see cref="ICameraBackend"/> の裏に隠す（正典 D-2 / Bootstrap 計画 §6）。
    /// </summary>
    public interface ICameraSystem
    {
        /// <summary>常に存在する全画面 View。解放は不可。</summary>
        ICameraView MainView { get; }

        /// <summary>分割画面や RT ミニマップ用の追加 View を生成する。</summary>
        ICameraView CreateView(in CameraViewConfig config);

        /// <summary>この System が生成した追加 View を解放する。所有権外の View は例外にする。</summary>
        void ReleaseView(ICameraView view);

        /// <summary>
        /// 指定 View 向けの Backend 管理論理カメラを生成する。
        /// 生成直後は非アクティブであり、<see cref="ICameraView.Push"/> でスタックに載せると描画される。
        /// </summary>
        /// <param name="view">紐付ける View（通常は <see cref="MainView"/>）。</param>
        /// <param name="id">論理カメラ識別子（テレメトリ / デバッグ用）。</param>
        LogicalCamera CreateManagedCamera(ICameraView view, string id);

        /// <summary>
        /// 論理カメラの Follow ターゲットを設定する。null で解除。
        /// 追従ダンピング等の構図は Backend（Cinemachine）側の責務であり、ここでは Transform を渡すだけ。
        /// </summary>
        void SetFollow(LogicalCamera camera, Transform? follow);

        /// <summary>
        /// 論理カメラの LookAt ターゲットを設定する。null で解除。
        /// </summary>
        void SetLookAt(LogicalCamera camera, Transform? lookAt);

        /// <summary>
        /// 論理カメラのレンズ設定を Backend 実体へ再反映する。
        /// <see cref="CreateManagedCamera"/> 後に FOV 等を書き換えたら必ず呼ぶ。
        /// </summary>
        void ApplyLens(LogicalCamera camera);

        /// <summary>
        /// <see cref="CreateManagedCamera"/> で作った論理カメラを解放する。
        /// Push ハンドルの Dispose（Pop）とは別で、Backend 実体ごと破棄する。
        /// </summary>
        void ReleaseManagedCamera(LogicalCamera camera);
    }
}
