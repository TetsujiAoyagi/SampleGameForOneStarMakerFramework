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

        /// <summary>
        /// ゲームの世界の時間の Layer。<see cref="Adapters.UpdateBehaviourAdapter"/> の既定の登録先で、
        /// VFX の既定の時間の領域でもある（この Layer の <c>IsPaused</c> / <c>TimeScale</c> に VFX の再生が追従する）。
        /// ゲームを一時停止する・遅くする処理は、この Layer を pause / time scale する。
        /// </summary>
        public const string Gameplay = "Gameplay";

        /// <summary>Brain 更新・Modifier・Snapshot 確定を担当する Layer。</summary>
        public const string Camera = "Camera";

        /// <summary>確定済み Camera Snapshot を消費してストリーミングを更新する Layer。</summary>
        public const string Streaming = "Streaming";

        /// <summary>
        /// VFX の再生状態（pause・再生速度）を時間の領域から書き写し、消え切り待ちと破棄済み所有者の回収を進める Layer
        /// （Framework の VFX composition が登録する）。
        /// この Layer は時間の領域ではない。pause / time scale すると全 VFX への反映が止まるので、しない。
        /// VFX の所属先にも指定できない（引数の誤りとして拒否される）。
        /// </summary>
        public const string Vfx = "Vfx";

        /// <summary>
        /// 入力サンプリングは UpdateBehaviourAdapter の既定順序 0、Camera、Streaming より前に置く。
        /// -100 は、既定のゲームプレイ登録より前であり、かつ Camera の 50 と衝突しない下限である。
        /// ゲームプレイ側がこれ以下の順序を独自に選ぶと契約は崩れる。この定数はその下限を共有する。
        /// </summary>
        public const int InputLayerOrder = -100;

        /// <summary>
        /// ゲームプレイの既定順序。入力のサンプリング（-100）の後、Camera（50）の前に置く。
        /// 同じ id に別の順序を渡すと <c>GetOrCreateLayer</c> が例外を投げるので、登録側は全てこの定数を使う。
        /// </summary>
        public const int GameplayLayerOrder = 0;

        /// <summary>Gameplay の target 更新後、Streaming の Snapshot 消費前に Camera を実行する。</summary>
        public const int CameraLayerOrder = 50;

        /// <summary>Camera Snapshot を読む処理は必ず Camera Layer より後に置く。</summary>
        public const int StreamingLayerOrder = 60;

        /// <summary>
        /// そのフレームの Camera 位置が確定した後に VFX の回収判定を走らせるため、Camera の後に置く。
        /// Scene 所有の VFX は scene event で同期に回収されるので、Streaming との前後には依存しない。
        /// </summary>
        public const int VfxLayerOrder = 55;
    }
}
