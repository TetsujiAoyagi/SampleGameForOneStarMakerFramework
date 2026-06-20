#nullable enable

using System;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// シーンアセットのペイロード。バリアント対応用。
    /// </summary>
    [Serializable]
    public class ScenePayload
    {
        /// <summary>Addressables のシーンアセット参照。</summary>
        public AssetReference? SceneReference;

        /// <summary>バリアント名（空文字はデフォルト）。</summary>
        public string Variant = string.Empty;
    }
}
