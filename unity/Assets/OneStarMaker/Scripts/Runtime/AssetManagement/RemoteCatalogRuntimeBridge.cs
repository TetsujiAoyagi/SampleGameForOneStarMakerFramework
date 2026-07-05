#nullable enable

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// Editor から Runtime へリモート Addressables カタログ URL を渡すための静的ブリッジ。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity のアセンブリ分離により、Runtime アセンブリ (OneStarMaker.Runtime) は
    /// Editor アセンブリ (OneStarMaker.Editor) を参照できない。
    /// 一方、開発ワークフローでは Editor 上で選択中の Developer Variant プロファイルに紐づく
    /// リモートカタログ URL を Play Mode 起動時に Addressables へ追加ロードしたい。
    /// </para>
    /// <para>
    /// この問題を解決するため、Editor 側 (<c>DeveloperVariantSettings</c> 等) が
    /// <see cref="InitializeOnLoad"/> タイミングで本デリゲートを設定し、
    /// Runtime 側の起動シーケンス (<c>AbstractApplicationInitializer</c>) が
    /// 最初の Addressables ロード (UICommon) より前にこの解決子を呼び出して URL を取得する。
    /// </para>
    /// <para>
    /// 実機ビルドや CI では AppConfig の <c>assetCheckout:remoteCatalogUrl</c> が優先され、
    /// 本ブリッジは Editor 専用の補助経路として機能する。
    /// デリゲートが未設定、または null / 空文字を返した場合はリモートフォールバックを行わず、
    /// ローカル Addressables のみで起動を続行する。
    /// </para>
    /// </remarks>
    public static class RemoteCatalogRuntimeBridge
    {
        /// <summary>
        /// Editor から注入されるリモートカタログ URL の解決子。
        /// </summary>
        /// <remarks>
        /// Runtime アセンブリは Editor アセンブリを参照できないため、
        /// Editor 側 (<c>DeveloperVariantSettings</c>) が InitializeOnLoad で本デリゲートを設定し、
        /// 起動シーケンスがこれを介して現在選択中プロファイルのリモート URL を取得する。
        /// null または空文字を返す場合はリモートフォールバックを行わない。
        /// </remarks>
        public static System.Func<string?>? EditorRemoteCatalogUrlResolver;

        /// <summary>
        /// Editor から注入されるローカル作業コピーの Git リビジョン解決子。
        /// </summary>
        /// <remarks>
        /// Runtime アセンブリは git コマンドを直接実行できないため、
        /// Editor 側 (<c>LocalRevisionEditorInjector</c>) が InitializeOnLoad で本デリゲートを設定し、
        /// 起動シーケンスがこれを介してローカル作業コピーの HEAD リビジョンを取得する。
        /// null または空文字を返す場合はリビジョン比較をスキップする。
        /// </remarks>
        public static System.Func<string?>? EditorLocalRevisionResolver;
    }
}
