#nullable enable

namespace OneStarMaker.Runtime.UpdateSystem.Api
{
    /// <summary>
    /// Runtime サブシステム間で共有する UpdateSystem Layer の識別子と順序。
    /// 数値を Bootstrap ごとに書くと Camera Snapshot を読む Streaming より後に Camera を置く契約が崩れやすいため、
    /// Camera と Streaming の依存関係だけをここで明示する。
    /// </summary>
    public static class UpdateLayerIds
    {
        /// <summary>Brain 更新・Modifier・Snapshot 確定を担当する Layer。</summary>
        public const string Camera = "Camera";

        /// <summary>確定済み Camera Snapshot を消費してストリーミングを更新する Layer。</summary>
        public const string Streaming = "Streaming";

        /// <summary>Gameplay の target 更新後、Streaming の Snapshot 消費前に Camera を実行する。</summary>
        public const int CameraLayerOrder = 50;

        /// <summary>Camera Snapshot を読む処理は必ず Camera Layer より後に置く。</summary>
        public const int StreamingLayerOrder = 60;
    }
}
