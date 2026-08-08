#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using UnityEngine;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// 常駐キャッシュの予算管理と追い出し方針を検証する。
    ///
    /// <para>
    /// 追い出し順は「実効頻度（アクセス回数の時間減衰込み）が最小のもの」から。
    /// 予算は asset 型ごとに独立しており、単体で予算を超える asset は
    /// キャッシュに載せず即解放する。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AssetResidentCacheTests
    {
        private double _clock;
        private readonly List<IBackendAsset> _releasedAssets = new();
        private Dictionary<AssetType, long> _budgets = null!;
        private AssetResidentCache _cache = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = 0.0;
            _releasedAssets.Clear();
            _budgets = new Dictionary<AssetType, long>
            {
                [AssetType.Texture] = 100,
                [AssetType.Prefab] = 1000,
            };
            _cache = CreateCache(fixedBytes: 50);
        }

        [Test]
        public void Store_Then_TryTake_Hit_ReturnsSameBackend_AndHitCountIsOne()
        {
            var backend = new TestBackendAsset(isValid: true);

            _cache.Store("key-a", AssetType.Texture, backend);

            var hit = _cache.TryTake("key-a", out var taken);

            Assert.That(hit, Is.True);
            Assert.That(taken, Is.SameAs(backend));
            Assert.That(_cache.GetSnapshot().HitCount, Is.EqualTo(1));
            Assert.That(_cache.GetSnapshot().MissCount, Is.EqualTo(0));
        }

        [Test]
        public void TryTake_Miss_IncreasesMissCount()
        {
            var hit = _cache.TryTake("missing", out var taken);

            Assert.That(hit, Is.False);
            Assert.That(taken, Is.Null);
            Assert.That(_cache.GetSnapshot().MissCount, Is.EqualTo(1));
            Assert.That(_cache.GetSnapshot().HitCount, Is.EqualTo(0));
        }

        [Test]
        public void Store_OverBudget_EvictsLowestEffectiveFrequencyEntry()
        {
            _cache = CreateCache(fixedBytes: 60, halfLifeSeconds: 300f);

            var first = new TestBackendAsset(isValid: true, name: "first");
            var second = new TestBackendAsset(isValid: true, name: "second");

            _cache.Store("first", AssetType.Texture, first);
            AdvanceClock(1.0);
            _cache.Store("second", AssetType.Texture, second);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(first));
            Assert.That(_cache.GetSnapshot().EvictionCount, Is.EqualTo(1));
            Assert.That(_cache.TryTake("first", out _), Is.False);
            Assert.That(_cache.TryTake("second", out var remaining), Is.True);
            Assert.That(remaining, Is.SameAs(second));
        }

        [Test]
        public void Store_OverBudget_TimeDecay_KeepsRecentlyAccessedEntry()
        {
            _cache = CreateCache(fixedBytes: 50, halfLifeSeconds: 300f);

            var oldAsset = new TestBackendAsset(isValid: true, name: "old");
            _cache.Store("old", AssetType.Texture, oldAsset);
            Assert.That(_cache.TryTake("old", out _), Is.True);
            _cache.Store("old", AssetType.Texture, oldAsset);

            AdvanceClock(600.0);

            var newAsset = new TestBackendAsset(isValid: true, name: "new");
            _cache.Store("new", AssetType.Texture, newAsset);

            var overflow = new TestBackendAsset(isValid: true, name: "overflow");
            _cache.Store("overflow", AssetType.Texture, overflow);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(oldAsset));
            Assert.That(_cache.TryTake("old", out _), Is.False);
            Assert.That(_cache.TryTake("new", out _), Is.True);
            Assert.That(_cache.TryTake("overflow", out _), Is.True);
        }

        [Test]
        public void Store_TryTake_Store_CarriesAccessCount_AndPrefersOverNewcomer()
        {
            _budgets[AssetType.Texture] = 50;
            _cache = CreateCache(fixedBytes: 50, halfLifeSeconds: 300f);

            var prioritized = new TestBackendAsset(isValid: true, name: "prioritized");
            _cache.Store("prioritized", AssetType.Texture, prioritized);
            Assert.That(_cache.TryTake("prioritized", out _), Is.True);
            _cache.Store("prioritized", AssetType.Texture, prioritized);

            var newcomer = new TestBackendAsset(isValid: true, name: "newcomer");
            _cache.Store("newcomer", AssetType.Texture, newcomer);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(newcomer));
            Assert.That(_cache.TryTake("newcomer", out _), Is.False);
            Assert.That(_cache.TryTake("prioritized", out var kept), Is.True);
            Assert.That(kept, Is.SameAs(prioritized));
        }

        [Test]
        public void Store_OverBudget_EvictsOnlyMatchingAssetType()
        {
            _cache = CreateCache(fixedBytes: 60, halfLifeSeconds: 300f);

            var prefab = new TestBackendAsset(isValid: true, name: "prefab");
            var textureA = new TestBackendAsset(isValid: true, name: "texture-a");
            var textureB = new TestBackendAsset(isValid: true, name: "texture-b");

            _cache.Store("prefab", AssetType.Prefab, prefab);
            _cache.Store("texture-a", AssetType.Texture, textureA);
            AdvanceClock(1.0);
            _cache.Store("texture-b", AssetType.Texture, textureB);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(textureA));
            Assert.That(_cache.TryTake("prefab", out var keptPrefab), Is.True);
            Assert.That(keptPrefab, Is.SameAs(prefab));
        }

        [Test]
        public void Store_ZeroBudgetType_ReleasesImmediately_WithoutEvictionCount()
        {
            var asset = new TestBackendAsset(isValid: true, name: "audio");

            _cache.Store("audio", AssetType.Audio, asset);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(asset));
            Assert.That(_cache.GetSnapshot().EvictionCount, Is.EqualTo(0));
            Assert.That(_cache.GetSnapshot().ResidentBytes, Is.Empty);
        }

        [Test]
        public void Store_SingleAssetOverBudget_IsReleasedImmediately()
        {
            _budgets[AssetType.Texture] = 50;
            _cache = CreateCache(fixedBytes: 100, halfLifeSeconds: 300f);

            var asset = new TestBackendAsset(isValid: true, name: "oversized");

            _cache.Store("oversized", AssetType.Texture, asset);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(asset));
            Assert.That(_cache.GetSnapshot().EvictionCount, Is.EqualTo(1));
            Assert.That(_cache.TryTake("oversized", out _), Is.False);
            Assert.That(_cache.GetSnapshot().ResidentBytes, Is.Empty);
        }

        [Test]
        public void Store_InvalidAsset_ReleasesImmediately_AndDoesNotCache()
        {
            var asset = new TestBackendAsset(isValid: false, name: "invalid");

            _cache.Store("invalid", AssetType.Texture, asset);

            Assert.That(_releasedAssets, Has.Count.EqualTo(1));
            Assert.That(_releasedAssets[0], Is.SameAs(asset));
            Assert.That(_cache.GetSnapshot().EvictionCount, Is.EqualTo(0));
            Assert.That(_cache.TryTake("invalid", out _), Is.False);
            Assert.That(_cache.GetSnapshot().ResidentBytes, Is.Empty);
        }

        [Test]
        public void Clear_ReleasesAllEntries_AndResidentBytesBecomeZero()
        {
            _cache.Store("a", AssetType.Texture, new TestBackendAsset(isValid: true, name: "a"));
            _cache.Store("b", AssetType.Prefab, new TestBackendAsset(isValid: true, name: "b"));

            _cache.Clear();

            Assert.That(_releasedAssets, Has.Count.EqualTo(2));
            var snapshot = _cache.GetSnapshot();
            Assert.That(snapshot.ResidentBytes, Is.Empty);
            foreach (var pair in snapshot.ResidentBytes)
            {
                Assert.That(pair.Value, Is.EqualTo(0));
            }
        }

        private AssetResidentCache CreateCache(long fixedBytes, float halfLifeSeconds = 300f)
        {
            return new AssetResidentCache(
                new FakeBudgetProvider(_budgets),
                halfLifeSeconds,
                ReleaseAsset,
                _ => fixedBytes,
                () => _clock);
        }

        private void ReleaseAsset(IBackendAsset asset)
        {
            _releasedAssets.Add(asset);
        }

        private void AdvanceClock(double deltaSeconds)
        {
            _clock += deltaSeconds;
        }

        private sealed class FakeBudgetProvider : IBudgetProvider
        {
            private readonly Dictionary<AssetType, long> _budgets;

            public FakeBudgetProvider(Dictionary<AssetType, long> budgets)
            {
                _budgets = budgets;
            }

            public long GetBudgetBytes(AssetType type)
            {
                return _budgets.TryGetValue(type, out var bytes) ? bytes : 0;
            }
        }

        private sealed class TestBackendAsset : IBackendAsset
        {
            public TestBackendAsset(bool isValid, string? name = null)
            {
                IsValid = isValid;
                Asset = name == null ? null : new GameObject(name);
            }

            public UnityEngine.Object? Asset { get; }

            public bool IsValid { get; }
        }
    }
}
