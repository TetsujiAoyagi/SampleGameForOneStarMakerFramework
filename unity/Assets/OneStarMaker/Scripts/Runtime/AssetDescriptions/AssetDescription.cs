#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// AssetDescription の共通 Payload 列挙 API。
    /// ScriptableObject ではなく、SO 内に埋め込む Serializable 基底として使う。
    /// SceneResource 等の YAML 構造を壊さないため、abstract SO にはしない。
    /// </summary>
    [Serializable]
    public abstract class AssetDescription : IAssetPayloadProvider
    {
        /// <summary>Variant 付き Payload 一覧。</summary>
        public abstract IReadOnlyList<AssetPayload> Payloads { get; }

        /// <summary>ビルドレポート等で使う表示名。</summary>
        public virtual string DisplayName => GetType().Name;
    }
}
