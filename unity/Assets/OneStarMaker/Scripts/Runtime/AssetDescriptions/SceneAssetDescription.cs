#nullable enable

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

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

        public string SceneIdentity = "";

        public SceneAssetDescription() { }

        public SceneAssetDescription(string sceneIdentity, LoadType loadType, List<AssetPayload> payloads)
        {
            SceneIdentity = sceneIdentity;
            _loadType = loadType;
            _payloads = payloads;
        }

        public void AddPayload(string variant, AssetReference reference)
        {
            _payloads.Add(new AssetPayload(variant, reference));
        }

        /// <inheritdoc />
        public override IReadOnlyList<AssetPayload> Payloads => _payloads;

        /// <inheritdoc />
        public override string DisplayName => "SceneAssetDescription";

        internal override AssetReference? ResolveReference(string variant)
        {
            return FindPayload(variant)?.Reference;
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
