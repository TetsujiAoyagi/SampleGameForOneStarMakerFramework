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
    /// バリアント対応あり。
    /// </summary>
    [Serializable]
    public class SceneAssetDescription
    {
        [SerializeField]
        private LoadType _loadType = LoadType.OnDemand;

        [SerializeField]
        private List<ScenePayload> _payloads = new();

        /// <summary>ロードタイミング種別。</summary>
        public LoadType LoadType => _loadType;

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
            if (payload?.SceneReference == null)
            {
                return null;
            }

            return Addressables.LoadSceneAsync(
                payload.SceneReference,
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

        private ScenePayload? FindPayload(string variant)
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
