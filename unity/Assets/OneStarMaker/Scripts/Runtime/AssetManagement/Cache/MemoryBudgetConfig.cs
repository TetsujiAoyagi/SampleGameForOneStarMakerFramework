#nullable enable

using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// アセットキャッシュのメモリ上限設定。
    /// 現行 AssetManagement コアからは未配線で、次パスの LFU 実装で使用する。
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryBudgetConfig", menuName = "OneStarMaker/Memory Budget Config")]
    public sealed class MemoryBudgetConfig : ScriptableObject
    {
        [SerializeField]
        [Tooltip("キャッシュ全体の上限（MB）。0 以下なら無制限。")]
        private long _totalBudgetMB = 256;

        [SerializeField]
        [Tooltip("LFU エビクション時の参照カウント減衰 half-life（秒）。")]
        private float _halfLifeSeconds = 300f;

        /// <summary>キャッシュ全体の上限（バイト）。0 以下なら無制限。</summary>
        public long TotalBudgetBytes => _totalBudgetMB > 0 ? _totalBudgetMB * 1024 * 1024 : long.MaxValue;

        /// <summary>LFU 時間減衰の half-life（秒）。</summary>
        public float HalfLifeSeconds => _halfLifeSeconds > 0f ? _halfLifeSeconds : 300f;
    }
}
