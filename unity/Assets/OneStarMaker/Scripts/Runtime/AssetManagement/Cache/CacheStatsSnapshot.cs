#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// 常駐キャッシュの統計スナップショット。
    /// </summary>
    public readonly struct CacheStatsSnapshot
    {
        /// <summary>キャッシュヒット回数。</summary>
        public int HitCount { get; }

        /// <summary>キャッシュミス回数。</summary>
        public int MissCount { get; }

        /// <summary>バジェット超過によるエビクション回数。</summary>
        public int EvictionCount { get; }

        /// <summary>AssetType 別の常駐バイト合計。</summary>
        public IReadOnlyDictionary<AssetType, long> ResidentBytes { get; }

        /// <summary>
        /// スナップショットを構築する。
        /// </summary>
        public CacheStatsSnapshot(
            int hitCount,
            int missCount,
            int evictionCount,
            IReadOnlyDictionary<AssetType, long> residentBytes)
        {
            HitCount = hitCount;
            MissCount = missCount;
            EvictionCount = evictionCount;
            ResidentBytes = residentBytes;
        }
    }
}
