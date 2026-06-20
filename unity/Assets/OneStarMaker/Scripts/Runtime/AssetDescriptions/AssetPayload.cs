#nullable enable

using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// 論理アセットに対する Addressables 参照と Variant 名。
    /// 同じ論理アセットの差し替え候補（Whitebox / Full 等）を 1 箇所に並べる。
    /// </summary>
    [Serializable]
    public class AssetPayload
    {
        /// <summary>Addressables アセット参照。</summary>
        /// <remarks>
        /// 旧 ScenePayload.SceneReference からの移行互換のため FormerlySerializedAs を付与。
        /// </remarks>
        [FormerlySerializedAs("SceneReference")]
        public AssetReference? Reference;

        /// <summary>
        /// バリアント名（空文字はデフォルト）。
        /// 名前の意味は Framework が解釈せず、BuildVariantProfile の whitelist と完全一致で判定する。
        /// </summary>
        public string Variant = string.Empty;
    }
}
