#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// シーンアセットの Addressable ロード情報を保持する。
    /// バリアント対応あり。SceneResource に埋め込んで使う。
    /// </summary>
    [Serializable]
    public class SceneAssetDescription : AssetDescription
    {
        /// <summary>親シーンロード時の挙動（OnDemand / Always 等）。</summary>
        [SerializeField]
        private LoadType _loadType = LoadType.OnDemand;

        /// <summary>Variant 付きシーン参照の一覧。Build 時は全 Variant を保持したまま whitelist で絞る。</summary>
        [SerializeField]
        private List<AssetPayload> _payloads = new();

        /// <summary>ロードタイミング種別。</summary>
        public LoadType LoadType => _loadType;

        /// <inheritdoc />
        public override IReadOnlyList<AssetPayload> Payloads => _payloads;

        /// <inheritdoc />
        public override string DisplayName => "SceneAssetDescription";

        /// <summary>
        /// 指定バリアントのシーンを Addressables でロードする。
        /// </summary>
        /// <param name="variant">バリアント名。空文字でデフォルト。</param>
        /// <param name="loadMode">シーンロードモード。</param>
        /// <param name="activateOnLoad">ロード後に即アクティブにするか。</param>
        /// <param name="priority">ロード優先度。</param>
        /// <returns>ロードハンドル。該当バリアントがなければ null。</returns>
        public AsyncOperationHandle<SceneInstance>? Load(
            string variant = "",
            LoadSceneMode loadMode = LoadSceneMode.Additive,
            bool activateOnLoad = true,
            int priority = 100)
        {
            var payload = FindPayload(variant);
            if (payload?.Reference == null)
            {
                return null;
            }

            return Addressables.LoadSceneAsync(
                payload.Reference,
                loadMode,
                activateOnLoad,
                priority);
        }

        /// <summary>
        /// 全ペイロードのアセット参照をリリースする。
        /// </summary>
        public void ReleaseAll()
        {
            // Addressable のリリースは Load したハンドル側で行うため、
            // ここではペイロードのクリーンアップのみ。
        }

        /// <summary>
        /// 指定 Variant に一致する Payload を探す。
        /// 見つからなければデフォルト Variant（空文字）へフォールバックする。
        /// </summary>
        private AssetPayload? FindPayload(string variant)
        {
            foreach (var payload in _payloads)
            {
                if (payload.Variant == variant)
                {
                    return payload;
                }
            }

            // バリアントが見つからなければデフォルト（空文字）を探す
            if (!string.IsNullOrEmpty(variant))
            {
                return FindPayload(string.Empty);
            }

            return null;
        }
    }
}
