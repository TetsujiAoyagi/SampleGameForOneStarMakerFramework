#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.AssetDescriptions
{
    /// <summary>
    /// BuildSystem が Payload を列挙するための共通インターフェース。
    /// AssetDescription 以外の型が Payload を提供する場合もこの interface 経由で扱う。
    /// </summary>
    public interface IAssetPayloadProvider
    {
        /// <summary>Variant 付き Payload 一覧。</summary>
        IReadOnlyList<AssetPayload> Payloads { get; }

        /// <summary>ビルドレポート等で使う表示名。</summary>
        string DisplayName { get; }
    }
}
