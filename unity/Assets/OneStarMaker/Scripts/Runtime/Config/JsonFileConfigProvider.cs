#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.Config;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;

namespace OneStarMaker.Runtime.Config
{
    /// <summary>
    /// JSON ファイルから設定値を読み込むプロバイダ。
    /// ネストされたオブジェクトは ":" 区切りのフラットキーに変換する。
    /// 配列はインデックス付きキー（例: "Array:0", "Array:1"）に展開する。
    /// ファイルが存在しない場合はスキップする（エラーにしない）。
    ///
    /// <para>対応する JSON 例:</para>
    /// <code>
    /// {
    ///   "Server": {
    ///     "Host": "localhost",
    ///     "Port": 8080
    ///   },
    ///   "Debug": {
    ///     "Enabled": true
    ///   }
    /// }
    /// </code>
    /// → Server:Host = "localhost", Server:Port = "8080", Debug:Enabled = "true"
    ///
    /// <para>制限事項:</para>
    /// <list type="bullet">
    ///   <item>Android の StreamingAssets は APK 内のため File.ReadAllText では読めない。
    ///         Android 対応が必要な場合は派生クラスで GetConfigFilePath を空文字にし、
    ///         Addressable ベースのプロバイダを別途追加すること。</item>
    /// </list>
    /// </summary>
    public sealed class JsonFileConfigProvider : IConfigProvider
    {
        private readonly string _filePath;
        private readonly IAssetManagement _assetManagement;

        /// <param name="filePath">JSON ファイルの Addressable アドレス（StreamingAssets パスではなく Addressables キー）。</param>
        /// <param name="assetManagement">
        /// App 常駐アセットの Load / Release 管理。
        /// BeforeSceneLoad で生成済みのインスタンスを渡す。LoadAppAsync で登録され ReleaseAppAll で解放される。
        /// </param>
        public JsonFileConfigProvider(string filePath, IAssetManagement assetManagement)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _assetManagement = assetManagement ?? throw new ArgumentNullException(nameof(assetManagement));
        }

        /// <summary>
        /// IConfigProvider.Load の実装。
        /// Addressables 経由で TextAsset を同期的に取得し、フラットキーへ展開する。
        ///
        /// <para>BuildConfig は BeforeSceneLoad の同期コンテキストで呼ばれるため、
        /// LoadAppAsync を GetAwaiter().GetResult() で同期的に待つ。
        /// 取得した TextAsset は App スコープとして ReleaseAppAll まで保持される。</para>
        /// </summary>
        /// <param name="store">フラット化した設定値の格納先。</param>
        public void Load(Dictionary<string, string> store)
        {
            // App 常駐としてロード。ReleaseAppAll までハンドルを保持する
            var textAssetHandle = _assetManagement.LoadAppAssetSync<TextAsset>(AssetKey.FromAddress(_filePath));
            var textAsset = textAssetHandle.Value;

            if (textAsset == null)
            {
                Debug.Log($"[Config] JSON ファイルが見つかりません（スキップ）: {_filePath}");
                return;
            }

            string json;
            try
            {
                json = textAsset.text;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] JSON ファイル読み取り失敗: {_filePath}: {ex.Message}");
                return;
            }

            try
            {
                JsonConfigFlattener.Flatten(json, store);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] JSON パース失敗: {_filePath}: {ex.Message}");
            }
        }
    }
}
