#nullable enable

using System;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Runtime.CameraSystem.Cinemachine;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace OneStarMaker.Runtime.CameraSystem.Hosting
{
    /// <summary>
    /// UpdateSystem から CameraSystem を駆動する唯一の managed Element。
    /// Unity の LateUpdate 順に依存せず、Cinemachine の確定結果を取得してから
    /// Modifier と Snapshot を更新する順序を、この純 C# の呼び出しスタック内で固定する。
    /// </summary>
    public sealed class CameraSystemUpdateElement : IUpdateElement
    {
        private readonly ICameraFrameDriver _frameDriver;
        private readonly RuntimeCameraSystem _cameraSystem;
        private bool _isActive = true;

        private CameraSystemUpdateElement(
            ICameraFrameDriver frameDriver,
            RuntimeCameraSystem cameraSystem)
        {
            _frameDriver = frameDriver ?? throw new ArgumentNullException(nameof(frameDriver));
            _cameraSystem = cameraSystem ?? throw new ArgumentNullException(nameof(cameraSystem));
        }

        /// <summary>
        /// Cinemachine backend と CameraSystem を UpdateSystem 用 Element に組み立てる。
        /// Bootstrap はこのファクトリだけを使い、Cinemachine のフレーム更新契約を Game 層へ公開しない。
        /// </summary>
        public static CameraSystemUpdateElement Create(
            CinemachineCameraBackend backend,
            RuntimeCameraSystem cameraSystem)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            return new CameraSystemUpdateElement((ICameraFrameDriver)backend, cameraSystem);
        }

        /// <summary>
        /// CameraSystem には通常 Update で進める仕事を持たせない。
        /// target を動かす Gameplay Element は Camera Layer より前に配置し、カメラは LateUpdate で一度だけ確定する。
        /// </summary>
        public void OnElementUpdate(in UpdateFrameContext context)
        {
        }

        /// <summary>
        /// 登録直後の初期化は不要。View の確保とフォールバックカメラ設定は CameraSystem 構築時に完了している。
        /// </summary>
        public void OnElementStart()
        {
        }

        /// <summary>
        /// Brain を手動更新してからポリシー Tick を実行する。
        /// 両者に同じ context.DeltaTime を渡すことで、UpdateSystem の Layer timeScale と
        /// Cinemachine のブレンド・Modifier の減衰・Snapshot の時計を一致させる。
        /// </summary>
        public void OnElementLateUpdate(in UpdateFrameContext context)
        {
            if (!_isActive)
            {
                return;
            }

            _frameDriver.AdvanceFrame(context.FrameIndex, context.DeltaTime);
            _cameraSystem.Tick(context.DeltaTime);
        }

        /// <summary>
        /// Unregister は UpdateSystem の構造変更フェーズまで遅延する。
        /// Host を先に Dispose して破棄済み Brain に触れないよう、所有者は Unregister より先に必ず呼ぶ。
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
        }

        internal static CameraSystemUpdateElement CreateForTests(
            ICameraFrameDriver frameDriver,
            RuntimeCameraSystem cameraSystem) =>
            new(frameDriver, cameraSystem);
    }
}
