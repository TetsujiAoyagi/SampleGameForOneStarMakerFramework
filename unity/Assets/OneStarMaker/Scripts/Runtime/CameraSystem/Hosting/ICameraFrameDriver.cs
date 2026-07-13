#nullable enable

namespace OneStarMaker.Runtime.CameraSystem.Hosting
{
    /// <summary>
    /// Unity/Cinemachine 側のカメラを 1 フレーム進める内部契約。
    /// CameraSystem のポリシー契約である ICameraBackend とは分ける。前者は PlayerLoop の所有者だけが使い、
    /// 後者は View・論理カメラ・Snapshot を扱うポリシー層が使うため、同じ抽象にすると Unity 固有の更新責務が漏れる。
    /// </summary>
    public interface ICameraFrameDriver
    {
        /// <summary>
        /// 指定フレームの Unity/Cinemachine 側更新を一度だけ進める。
        /// 呼び出し側はこの直後に CameraSystem.Tick を実行し、Brain 出力に Modifier を重ねて Snapshot を確定する。
        /// </summary>
        void AdvanceFrame(uint frameIndex, float deltaTime);
    }
}
