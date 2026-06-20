#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;

namespace OneStarMaker.Editor.Build
{
    /// <summary>
    /// AssetDescription 走査結果 1 件。
    /// Whitelist 構築時に「どこから来た Payload か」を Build Report へ出すために使う。
    /// </summary>
    public readonly struct CollectedAssetDescription
    {
        public CollectedAssetDescription(
            IAssetPayloadProvider provider,
            bool isOptional,
            string sourceName)
        {
            Provider = provider;
            IsOptional = isOptional;
            SourceName = sourceName;
        }

        /// <summary>Payload を列挙する Provider（通常は AssetDescription）。</summary>
        public IAssetPayloadProvider Provider { get; }

        /// <summary>true なら whitelist 不一致でも Error にしない。</summary>
        public bool IsOptional { get; }

        /// <summary>Build Report 用の識別子（例: SceneResourceMap:Title）。</summary>
        public string SourceName { get; }
    }

    /// <summary>
    /// AssetDescription 走査元。
    /// 新しいアセット種別を Build 対象に足す場合は Source 実装を追加する。
    /// </summary>
    public interface IAssetDescriptionSource
    {
        /// <summary>Profile を参照して AssetDescription / Provider を列挙する。</summary>
        IEnumerable<CollectedAssetDescription> Collect(BuildVariantProfile profile);
    }
}
