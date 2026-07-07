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
    /// ポリシー層（CameraSystem）が決めた「どのカメラをどう切り替えるか」を、
    /// 実際の描画メカニズム（Cinemachine 等）へ翻訳する境界。ポリシー層は Cinemachine 型に一切依存しない。
    /// </summary>
    public interface ICameraBackend
    {
        /// <summary>
        /// View に対応する描画リソース（Camera / Brain 等）を確保する。
        /// isMainView は「常駐する全画面 View かどうか」をポリシー層から明示的に伝える
        /// （Backend 側が ViewId の採番規則に依存して主 View を推測しないようにするため）。
        /// </summary>
        void RegisterView(ViewId view, in CameraViewConfig config, bool isMainView);

        /// <summary>View の描画リソースを破棄する。</summary>
        void ReleaseView(ViewId view);

        /// <summary>アクティブカメラを切り替え、指定のブレンド設定を Backend へ伝える。</summary>
        void SetActiveCamera(ViewId view, LogicalCamera camera, in CameraBlendSpec blend);

        /// <summary>Backend が算出した現在の確定 Pose（Modifier 適用前）を読み取る。</summary>
        CameraPose GetCurrentPose(ViewId view);

        /// <summary>個別論理カメラ単体の Pose。ブレンド先の先読みに使う。</summary>
        CameraPose GetCameraPose(LogicalCamera camera);

        /// <summary>View がブレンド遷移中かどうか。Snapshot の確定と switch span 完了判定に使う。</summary>
        bool IsBlending(ViewId view);

        /// <summary>Modifier 適用後の最終 Pose を実カメラへ書き戻す。</summary>
        void ApplyPostModifier(ViewId view, in CameraPose finalPose);
    }
}
