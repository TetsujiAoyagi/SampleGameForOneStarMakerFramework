#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// アセットキャッシュのメモリ上限設定。
    /// 現行 AssetManagement コアからは未配線で、次パスの LFU 実装で使用する。
    /// </summary>
    [CreateAssetMenu(fileName = "MemoryBudgetConfig", menuName = "OneStarMaker/Memory Budget Config")]
    public sealed class MemoryBudgetConfig : ScriptableObject, IBudgetProvider
    {
        [Serializable]
        private struct AssetTypeBudgetEntry
        {
            public AssetType Type;

            [Tooltip("概算は参考値(特に Prefab は依存 Texture/Mesh を含まない)。実測して調整する前提の仮値。")]
            public int BudgetMB;
        }

        [SerializeField]
        private List<AssetTypeBudgetEntry> _budgets = new();

        [SerializeField]
        [Tooltip("LFU エビクション時の参照カウント減衰 half-life（秒）。")]
        private float _halfLifeSeconds = 300f;

        /// <summary>LFU 時間減衰の half-life（秒）。</summary>
        public float HalfLifeSeconds => _halfLifeSeconds > 0f ? _halfLifeSeconds : 300f;

        /// <inheritdoc />
        public long GetBudgetBytes(AssetType type)
        {
            if (_budgets == null || _budgets.Count == 0)
            {
                return 0;
            }

            for (var i = 0; i < _budgets.Count; i++)
            {
                var entry = _budgets[i];
                if (entry.Type == type)
                {
                    return (long)entry.BudgetMB * 1024 * 1024;
                }
            }

            return 0;
        }
    }
}
