#nullable enable

using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// refcount 0 のアセットを退避し、同一 key の再ロードで再利用する常駐キャッシュ。
    /// </summary>
    internal interface IAssetResidentCache
    {
        /// <summary>key がキャッシュにあれば取り出して返す（エントリはキャッシュから除去され、統計は復帰用に保持される）。</summary>
        bool TryTake(string key, out IBackendAsset asset);

        /// <summary>refcount 0 のアセットを退避する。バジェット超過分は effectiveFrequency 最小からエビクトされる。</summary>
        void Store(string key, AssetType type, IBackendAsset asset);

        /// <summary>全エントリをエビクトする（ReleaseAll 用）。</summary>
        void Clear();

        /// <summary>ヒット/ミス/エビクション数と type 別使用バイトのスナップショット。</summary>
        CacheStatsSnapshot GetSnapshot();
    }
}
