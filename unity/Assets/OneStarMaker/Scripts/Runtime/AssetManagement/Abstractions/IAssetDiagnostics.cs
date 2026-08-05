#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.AssetManagement
{
    /// <summary>
    /// ロード済みアセットの所有関係を照会する診断用 API。
    /// 本番ロジックから使うことは想定していない（リーク調査・デバッグ用）。
    /// </summary>
    public interface IAssetDiagnostics
    {
        /// <summary>指定キーを現在所有している owner を列挙する。未ロードなら空。</summary>
        IReadOnlyList<AssetOwner> GetOwners(AssetKey key);

        /// <summary>指定 owner が現在所有しているアセットキーを列挙する。無ければ空。</summary>
        IReadOnlyList<AssetKey> GetOwnedAssets(AssetOwner owner);
    }
}
