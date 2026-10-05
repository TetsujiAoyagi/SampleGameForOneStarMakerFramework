#nullable enable

namespace OneStarMaker.Runtime.UpdateSystem.Api
{
    /// <summary>
    /// Runtime サブシステム間で共有する UpdateSystem Layer の識別子と順序。
    /// 数値を呼び出し側ごとに書くと、入力のサンプリングがゲームプレイや Camera より後ろに回り、
    /// Camera Snapshot を読む Streaming が Camera より前に回る。
    /// </summary>
    public static class UpdateLayerIds
    {
        /// <summary>
        /// 入力アクションをサンプリングし、公開バッファを更新する Layer。
        /// ゲームプレイや Camera はこの公開値の消費者なので、この Layer より大きい順序で登録する。
        /// </summary>
        public const string Input = "Input";

        /// <summary>Brain 更新・Modifier・Snapshot 確定を担当する Layer。</summary>
        public const string Camera = "Camera";

        /// <summary>確定済み Camera Snapshot を消費してストリーミングを更新する Layer。</summary>
        public const string Streaming = "Streaming";

        /// <summary>
        /// 入力サンプリングは UpdateBehaviourAdapter の既定順序 0、Camera、Streaming より前に置く。
        /// -100 は、既定のゲームプレイ登録より前であり、かつ Camera の 50 と衝突しない下限である。
        /// ゲームプレイ側がこれ以下の順序を独自に選ぶと契約は崩れる。この定数はその下限を共有する。
        /// </summary>
        public const int InputLayerOrder = -100;

        /// <summary>Gameplay の target 更新後、Streaming の Snapshot 消費前に Camera を実行する。</summary>
        public const int CameraLayerOrder = 50;

        /// <summary>Camera Snapshot を読む処理は必ず Camera Layer より後に置く。</summary>
        public const int StreamingLayerOrder = 60;
    }
}
