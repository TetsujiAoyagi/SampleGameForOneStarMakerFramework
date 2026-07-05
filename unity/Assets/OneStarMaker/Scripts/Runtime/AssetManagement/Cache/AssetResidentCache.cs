#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;
using UnityEngine.Profiling;

namespace OneStarMaker.Runtime.AssetManagement.Cache
{
    /// <summary>
    /// LFU + 時間減衰による常駐アセットキャッシュ。
    /// </summary>
    internal sealed class AssetResidentCache : IAssetResidentCache
    {
        private const double Ln2 = 0.6931471805599453;
        private const float MinHalfLifeSeconds = 1e-6f;

        private readonly IBudgetProvider _budgetProvider;
        private readonly float _halfLifeSeconds;
        private readonly Action<IBackendAsset> _releaseAsset;
        private readonly Func<UnityEngine.Object?, long> _estimateBytes;
        private readonly Func<double> _clock;

        private readonly Dictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<AssetType, long> _residentBytes = new();
        private readonly Dictionary<string, (int count, double last)> _accessCarryover = new(StringComparer.Ordinal);

        private int _hitCount;
        private int _missCount;
        private int _evictionCount;

        /// <summary>
        /// 常駐キャッシュを構築する。
        /// </summary>
        public AssetResidentCache(
            IBudgetProvider budgetProvider,
            float halfLifeSeconds,
            Action<IBackendAsset> releaseAsset,
            Func<UnityEngine.Object?, long>? estimateBytes = null,
            Func<double>? clock = null)
        {
            _budgetProvider = budgetProvider;
            _halfLifeSeconds = halfLifeSeconds;
            _releaseAsset = releaseAsset;
            _estimateBytes = estimateBytes ?? DefaultEstimateBytes;
            _clock = clock ?? DefaultClock;
        }

        /// <inheritdoc />
        public bool TryTake(string key, out IBackendAsset asset)
        {
            if (!_entries.TryGetValue(key, out var entry))
            {
                _missCount++;
                asset = null!;
                return false;
            }

            var now = _clock();
            _entries.Remove(key);
            SubtractResidentBytes(entry.Type, entry.EstimatedBytes);

            var nextCount = entry.AccessCount >= int.MaxValue ? int.MaxValue : entry.AccessCount + 1;
            _accessCarryover[key] = (nextCount, now);

            _hitCount++;
            asset = entry.Backend;
            return true;
        }

        /// <inheritdoc />
        public void Store(string key, AssetType type, IBackendAsset asset)
        {
            if (!asset.IsValid)
            {
                _releaseAsset(asset);
                return;
            }

            if (_budgetProvider.GetBudgetBytes(type) <= 0)
            {
                _releaseAsset(asset);
                return;
            }

            if (_entries.TryGetValue(key, out var existing))
            {
                RemoveEntry(existing);
            }

            var now = _clock();
            var estimatedBytes = _estimateBytes(asset.Asset);
            var accessCount = 1;
            if (_accessCarryover.TryGetValue(key, out var carryover))
            {
                accessCount = carryover.count;
                _accessCarryover.Remove(key);
            }

            var newEntry = new CacheEntry(key, type, asset, estimatedBytes, accessCount, now);
            _entries[key] = newEntry;
            AddResidentBytes(type, estimatedBytes);

            EvictWhileOverBudget(type);
        }

        /// <inheritdoc />
        public void Clear()
        {
            foreach (var entry in _entries.Values)
            {
                _releaseAsset(entry.Backend);
            }

            _entries.Clear();
            _residentBytes.Clear();
            _accessCarryover.Clear();
        }

        /// <inheritdoc />
        public CacheStatsSnapshot GetSnapshot()
        {
            return new CacheStatsSnapshot(
                _hitCount,
                _missCount,
                _evictionCount,
                new Dictionary<AssetType, long>(_residentBytes));
        }

        private void EvictWhileOverBudget(AssetType type)
        {
            var budget = _budgetProvider.GetBudgetBytes(type);
            while (_residentBytes.TryGetValue(type, out var total) && total > budget)
            {
                if (!TryFindLowestFrequencyEntry(type, out var victimKey, out var victim))
                {
                    break;
                }

                _entries.Remove(victimKey);
                SubtractResidentBytes(victim.Type, victim.EstimatedBytes);
                _releaseAsset(victim.Backend);
                _evictionCount++;
            }
        }

        private bool TryFindLowestFrequencyEntry(AssetType type, out string victimKey, out CacheEntry victim)
        {
            victimKey = string.Empty;
            victim = default;

            var now = _clock();
            var halfLife = Math.Max(_halfLifeSeconds, MinHalfLifeSeconds);
            var found = false;
            var lowestFrequency = double.MaxValue;
            var oldestAccessTime = double.MaxValue;

            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (entry.Type != type)
                {
                    continue;
                }

                var frequency = ComputeEffectiveFrequency(entry.AccessCount, entry.LastAccessTime, now, halfLife);
                if (!found
                    || frequency < lowestFrequency
                    || (frequency == lowestFrequency && entry.LastAccessTime < oldestAccessTime))
                {
                    found = true;
                    lowestFrequency = frequency;
                    oldestAccessTime = entry.LastAccessTime;
                    victimKey = pair.Key;
                    victim = entry;
                }
            }

            return found;
        }

        private static double ComputeEffectiveFrequency(int accessCount, double lastAccessTime, double now, float halfLife)
        {
            var elapsed = now - lastAccessTime;
            if (elapsed < 0.0)
            {
                elapsed = 0.0;
            }

            return accessCount * Math.Exp(-Ln2 * elapsed / halfLife);
        }

        private void RemoveEntry(CacheEntry entry)
        {
            _entries.Remove(entry.Key);
            SubtractResidentBytes(entry.Type, entry.EstimatedBytes);
            _releaseAsset(entry.Backend);
        }

        private void AddResidentBytes(AssetType type, long bytes)
        {
            _residentBytes.TryGetValue(type, out var current);
            _residentBytes[type] = current + bytes;
        }

        private void SubtractResidentBytes(AssetType type, long bytes)
        {
            if (!_residentBytes.TryGetValue(type, out var current))
            {
                return;
            }

            var next = current - bytes;
            if (next <= 0)
            {
                _residentBytes.Remove(type);
            }
            else
            {
                _residentBytes[type] = next;
            }
        }

        private static long DefaultEstimateBytes(UnityEngine.Object? asset)
        {
            return asset == null ? 0L : Profiler.GetRuntimeMemorySizeLong(asset);
        }

        private static double DefaultClock() => Time.realtimeSinceStartupAsDouble;

        private readonly struct CacheEntry
        {
            public CacheEntry(
                string key,
                AssetType type,
                IBackendAsset backend,
                long estimatedBytes,
                int accessCount,
                double lastAccessTime)
            {
                Key = key;
                Type = type;
                Backend = backend;
                EstimatedBytes = estimatedBytes;
                AccessCount = accessCount;
                LastAccessTime = lastAccessTime;
            }

            public string Key { get; }

            public AssetType Type { get; }

            public IBackendAsset Backend { get; }

            public long EstimatedBytes { get; }

            public int AccessCount { get; }

            public double LastAccessTime { get; }
        }
    }
}
