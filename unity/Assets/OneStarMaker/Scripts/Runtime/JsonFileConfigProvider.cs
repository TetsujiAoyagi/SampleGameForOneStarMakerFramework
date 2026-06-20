#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.Config;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

        /// <param name="filePath">JSON ファイルのパス。</param>
        public JsonFileConfigProvider(string filePath)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public void Load(Dictionary<string, string> store)
        {
            var textAsset = Addressables.LoadAssetAsync<TextAsset>(_filePath).WaitForCompletion();

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
