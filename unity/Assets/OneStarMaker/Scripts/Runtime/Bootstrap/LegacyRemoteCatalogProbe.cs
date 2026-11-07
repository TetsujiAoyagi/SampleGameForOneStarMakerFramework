#nullable enable

using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// 旧リモート Addressables catalog の追加ロードと、リモートのビルド元とのリビジョン乖離の警告。
    /// <para>
    /// 通常起動からは外した旧経路で、呼び出し元は無い（<c>20-variant-checkout-workflow.md</c> が案内として残している）。
    /// 参照 0 でも削除せず、状態を持たない 1 ファイルに隔離して、退役するときはこのファイルだけを消せるようにしてある。
    /// 本文は <c>AbstractApplicationInitializer</c> から移しただけで、initializer の <c>_config</c> の読み取りを引数に替えた以外は変えていない。
    /// </para>
    /// </summary>
    internal static class LegacyRemoteCatalogProbe
    {
        /// <summary>
        /// リモート Addressables カタログを追加ロードする(開発ワークフロー専用のフォールバック)。
        /// URL の解決順序:
        ///   1. AppConfig のキー "assetCheckout:remoteCatalogUrl"(開発ビルド・実機で使用)
        ///   2. Editor のみ: RemoteCatalogRuntimeBridge 経由で現在選択中プロファイルの URL
        /// URL が空ならロードをスキップする(ローカル完結の開発者に一切影響を与えない)。
        /// ロード失敗(サーバ不達等)は警告ログのみで起動を継続する。
        /// サーバ不達で起動が長時間ブロックされるのを避けるためタイムアウトを設ける。
        /// </summary>
        internal static async UniTask TryLoadAsync(AppConfig config)
        {
            var url = ResolveRemoteCatalogUrl(config);
            if (string.IsNullOrEmpty(url))
            {
                return;
            }

            try
            {
                Debug.Log($"[AppInit] Loading remote content catalog: {url}");
                // サーバ不達時に無限待機しないよう短いタイムアウトを設ける。
                var handle = UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync(url);
                await handle.ToUniTask().Timeout(System.TimeSpan.FromSeconds(10));
                Debug.Log("[AppInit] Remote content catalog loaded. Missing local assets will resolve from remote bundles.");
                await WarnOnRevisionMismatchAsync(config, url);
            }
            catch (System.Exception ex)
            {
                // フォールバック不能でも起動は続行する。ローカルに揃っている開発者には無害。
                Debug.LogWarning($"[AppInit] Failed to load remote content catalog ('{url}'). Continuing with local assets only. Reason: {ex.Message}");
            }
        }

        /// <summary>
        /// リモートカタログ URL を解決する。Config 優先、次に Editor 注入。
        /// </summary>
        private static string ResolveRemoteCatalogUrl(AppConfig config)
        {
            var fromConfig = config?.GetString("assetCheckout:remoteCatalogUrl", string.Empty) ?? string.Empty;
            if (!string.IsNullOrEmpty(fromConfig))
            {
                return fromConfig;
            }

#if UNITY_EDITOR
            var resolver = OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorRemoteCatalogUrlResolver;
            if (resolver != null)
            {
                var url = resolver();
                if (!string.IsNullOrEmpty(url))
                {
                    return url!;
                }
            }
#endif

            return string.Empty;
        }

        /// <summary>
        /// リモートのビルド元リビジョンとローカル作業コピーのリビジョンを比較し、乖離を警告する。
        /// </summary>
        /// <remarks>
        /// <para>
        /// catalog と同じディレクトリにある build-info.json を取得し、
        /// <c>revision</c> フィールドをローカルリビジョンと比較する。
        /// </para>
        /// <para>
        /// ベストエフォート。取得/解析失敗やローカルリビジョン不明時は静かにスキップし、
        /// 起動をブロックしない。
        /// </para>
        /// </remarks>
        /// <param name="config">起動設定。ローカルリビジョンの解決に使う。</param>
        /// <param name="catalogUrl">ロード済みリモートカタログの URL。</param>
        private static async UniTask WarnOnRevisionMismatchAsync(AppConfig config, string catalogUrl)
        {
            try
            {
                var buildInfoUrl = DeriveBuildInfoUrl(catalogUrl);
                if (string.IsNullOrEmpty(buildInfoUrl)) return;

                string json;
                using (var request = UnityWebRequest.Get(buildInfoUrl))
                {
                    await request.SendWebRequest().ToUniTask();
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        // build-info.json が無い/取得失敗。比較はスキップ(警告のみ debug)。
                        Debug.Log($"[AppInit] build-info.json を取得できませんでした（比較スキップ）: {buildInfoUrl}");
                        return;
                    }
                    json = request.downloadHandler.text;
                }

                var info = JsonUtility.FromJson<RemoteBuildInfo>(json);
                var remoteRevision = info?.revision ?? string.Empty;
                var localRevision = ResolveLocalRevision(config);

                if (string.IsNullOrEmpty(remoteRevision) || string.IsNullOrEmpty(localRevision))
                {
                    // どちらか不明なら比較不能。
                    return;
                }

                if (!string.Equals(remoteRevision, localRevision, System.StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[AppInit] リビジョン乖離を検出しました。リモートのビルド済みアセットは古い/新しい可能性があります。\n" +
                        $"  local  = {localRevision}\n" +
                        $"  remote = {remoteRevision} (builtAtUtc={info?.builtAtUtc})\n" +
                        "  混在ロードで不具合が出る場合はリモートを再ビルドするか、ローカルをリモートのリビジョンへ合わせてください。");
                }
                else
                {
                    Debug.Log($"[AppInit] リビジョン一致: {localRevision}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AppInit] リビジョン比較に失敗しました（無視して続行）: {ex.Message}");
            }
        }

        /// <summary>catalog URL と同じディレクトリの build-info.json の URL を導出する。</summary>
        /// <param name="catalogUrl">リモートカタログの URL。</param>
        /// <returns>build-info.json の URL。導出不能時は空文字。</returns>
        internal static string DeriveBuildInfoUrl(string catalogUrl)
        {
            if (string.IsNullOrEmpty(catalogUrl)) return string.Empty;
            var lastSlash = catalogUrl.LastIndexOf('/');
            if (lastSlash < 0) return string.Empty;
            return catalogUrl.Substring(0, lastSlash + 1) + "build-info.json";
        }

        /// <summary>
        /// ローカル作業コピーのリビジョンを解決する。
        /// </summary>
        /// <remarks>
        /// 解決順序:
        /// <list type="number">
        /// <item><description>AppConfig の <c>assetCheckout:localRevision</c>(ビルド時に焼き込み)</description></item>
        /// <item><description>Editor 注入 (<see cref="OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver"/>)</description></item>
        /// </list>
        /// どちらも無ければ空文字を返す。
        /// </remarks>
        /// <returns>ローカル Git リビジョン。不明時は空文字。</returns>
        private static string ResolveLocalRevision(AppConfig config)
        {
            var fromConfig = config?.GetString("assetCheckout:localRevision", string.Empty) ?? string.Empty;
            if (!string.IsNullOrEmpty(fromConfig)) return fromConfig;
#if UNITY_EDITOR
            var resolver = OneStarMaker.Runtime.AssetManagement.RemoteCatalogRuntimeBridge.EditorLocalRevisionResolver;
            if (resolver != null)
            {
                var rev = resolver();
                if (!string.IsNullOrEmpty(rev)) return rev!;
            }
#endif
            return string.Empty;
        }
    }
}
