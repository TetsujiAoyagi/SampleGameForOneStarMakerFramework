#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// scene event の購読の解除・差し替えの異常系。購読の解除が例外を投げる場合と、
    /// BindSceneLifecycle の再入点（前の購読の解除、回収中の backend Destroy、Subscribe）で
    /// 別の BindSceneLifecycle が新しい購読まで終えた場合に、回収と新しい購読の状態が壊れないことを見る。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemSceneLifecycleFaultTests
    {
        private const string SceneA = "SceneA";
        private const string SceneB = "SceneB";

        private FakeVfxBackend _backend = null!;
        private FakeVfxLogger _logger = null!;
        private VfxSystem _system = null!;
        private FakeSceneQuery _query = null!;
        private GameObject _source = null!;
        private readonly List<GameObject> _createdObjects = new();

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeVfxBackend();
            _logger = new FakeVfxLogger();
            _system = new VfxSystem(_backend, _logger, gracefulStopTimeoutSeconds: 1f);
            _query = new FakeSceneQuery();
            _query.Loaded.Add(SceneA);
            _query.Loaded.Add(SceneB);
            _source = CreateObject("VfxSource");
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            foreach (var go in _createdObjects)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            _createdObjects.Clear();
        }

        private GameObject CreateObject(string name)
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        private VfxHandle SpawnLive(AssetOwner owner)
        {
            var handle = _system.Spawn(Request(), owner);
            Assert.That(handle, Is.InstanceOf<VfxHandle>(), "受理されるべき起動が拒否された");
            return (VfxHandle)handle;
        }

        private static void EmitPreUnloading(CountingSceneEventObservable events, string identity)
            => events.Emit(new SceneEvent(SceneEventType.StateChanged, identity, SceneState.PreUnloading));

        // ═══════════════════════════════════════════
        //  購読の解除が例外を投げる
        // ═══════════════════════════════════════════

        [Test]
        public void Dispose_WhenUnsubscribeThrows_StillReleasesEveryInstance()
        {
            var events = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(events, _query);
            var app = SpawnLive(AssetOwner.App);
            var manual = SpawnLive(AssetOwner.Manual);
            var scene = SpawnLive(AssetOwner.Scene(SceneA));
            var bound = SpawnLive(AssetOwner.Bind(CreateObject("Bound")));
            events.OnUnsubscribe = () => throw new InvalidOperationException("unsubscribe failed");

            Assert.DoesNotThrow(() => _system.Dispose());

            Assert.That(events.UnsubscribeCount, Is.EqualTo(1));
            foreach (var handle in new[] { app, manual, scene, bound })
            {
                Assert.That(_backend.DestroyCountOf(handle.Id), Is.EqualTo(1), $"id={handle.Id}");
                Assert.That(handle.IsActive, Is.False);
            }

            Assert.That(_system.LiveInstanceCount, Is.EqualTo(0));
            Assert.That(_logger.ErrorCount, Is.EqualTo(1), "解除の失敗を1件記録する");

            var destroyAfterFirstDispose = _backend.DestroyCount;
            Assert.DoesNotThrow(() => _system.Dispose());
            Assert.That(_backend.DestroyCount, Is.EqualTo(destroyAfterFirstDispose), "二回目の Dispose は何もしない");
        }

        [Test]
        public void Rebind_WhenPreviousUnsubscribeThrows_ReleasesSceneOwned_AndOpensNewLifecycle()
        {
            var first = new CountingSceneEventObservable();
            var second = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(first, _query);
            var scene = SpawnLive(AssetOwner.Scene(SceneA));
            var app = SpawnLive(AssetOwner.App);
            EmitPreUnloading(first, SceneB);
            first.OnUnsubscribe = () => throw new InvalidOperationException("unsubscribe failed");

            Assert.DoesNotThrow(() => _system.BindSceneLifecycle(second, _query));

            Assert.That(_backend.DestroyCountOf(scene.Id), Is.EqualTo(1), "差し替えで既存の Scene 所有を回収する");
            Assert.That(app.IsActive, Is.True);
            Assert.That(_logger.ErrorCount, Is.EqualTo(1), "解除の失敗を1件記録する");
            Assert.That((second.SubscribeCount, second.UnsubscribeCount), Is.EqualTo((1, 0)));
            SpawnLive(AssetOwner.Scene(SceneB));

            var after = SpawnLive(AssetOwner.Scene(SceneA));
            EmitPreUnloading(second, SceneA);
            Assert.That(_backend.DestroyCountOf(after.Id), Is.EqualTo(1), "新しい購読のイベントで回収する");
        }

        // ═══════════════════════════════════════════
        //  BindSceneLifecycle の再入
        // ═══════════════════════════════════════════

        /// <summary>
        /// 差し替えの回収中に backend の Destroy から BindSceneLifecycle が再入し、新しい購読で Scene B の
        /// PreUnloading を受けた。外側の差し替えが終端中の記録を消すと、Scene B への Spawn が Create を呼ぶ。
        /// </summary>
        [Test]
        public void Rebind_ReenteredFromDestroy_KeepsTerminalReceivedByNewSubscription()
        {
            var first = new CountingSceneEventObservable();
            var inner = new CountingSceneEventObservable();
            var outer = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(first, _query);
            var old = SpawnLive(AssetOwner.Scene(SceneA));
            _backend.OnDestroy = id =>
            {
                if (id != old.Id)
                {
                    return;
                }

                _backend.OnDestroy = null;
                _system.BindSceneLifecycle(inner, _query);
                EmitPreUnloading(inner, SceneB);
            };

            Assert.DoesNotThrow(() => _system.BindSceneLifecycle(outer, _query));

            Assert.That(_backend.DestroyCountOf(old.Id), Is.EqualTo(1));
            Assert.That(outer.SubscribeCount, Is.EqualTo(0), "再入で差し替えられた外側の差し替えは購読しない");
            Assert.That((inner.SubscribeCount, inner.UnsubscribeCount), Is.EqualTo((1, 0)));

            var createBefore = _backend.CreateCount;
            var rejected = _system.Spawn(Request(), AssetOwner.Scene(SceneB));
            Assert.That(_backend.CreateCount, Is.EqualTo(createBefore), "新しい購読で受けた終端が残る");
            Assert.That(rejected.IsActive, Is.False);
        }

        /// <summary>
        /// 前の購読の解除の中で BindSceneLifecycle が再入し、新しい購読のもとで Scene 所有の instance が受理された。
        /// 外側の差し替えがその instance まで回収してはならない。
        /// </summary>
        [Test]
        public void Rebind_ReenteredFromPreviousUnsubscribe_KeepsInstanceAcceptedByNewSubscription()
        {
            var first = new CountingSceneEventObservable();
            var inner = new CountingSceneEventObservable();
            var outer = new CountingSceneEventObservable();
            _system.BindSceneLifecycle(first, _query);
            var old = SpawnLive(AssetOwner.Scene(SceneA));
            VfxHandle? fresh = null;
            first.OnUnsubscribe = () =>
            {
                first.OnUnsubscribe = null;
                _system.BindSceneLifecycle(inner, _query);
                fresh = SpawnLive(AssetOwner.Scene(SceneA));
            };

            Assert.DoesNotThrow(() => _system.BindSceneLifecycle(outer, _query));

            Assert.That(_backend.DestroyCountOf(old.Id), Is.EqualTo(1));
            Assert.That(fresh, Is.Not.Null);
            Assert.That(_backend.DestroyCountOf(fresh!.Id), Is.EqualTo(0), "新しい購読で受理した instance は回収しない");
            Assert.That(fresh.IsActive, Is.True);
            Assert.That(outer.SubscribeCount, Is.EqualTo(0));
            Assert.That((inner.SubscribeCount, inner.UnsubscribeCount), Is.EqualTo((1, 0)));
            Assert.That(_logger.ErrorCount, Is.EqualTo(0));
        }

        /// <summary>
        /// Subscribe の中で BindSceneLifecycle が再入した。外側の購読は誰にも解除されなくなるので、
        /// 外側がその場で解除し、内側の購読だけが残って Dispose で解除される。
        /// </summary>
        [Test]
        public void Rebind_ReenteredFromSubscribe_KeepsOnlyTheInnerSubscription()
        {
            var outer = new CountingSceneEventObservable();
            var inner = new CountingSceneEventObservable();
            outer.OnSubscribe = () =>
            {
                outer.OnSubscribe = null;
                _system.BindSceneLifecycle(inner, _query);
            };

            Assert.DoesNotThrow(() => _system.BindSceneLifecycle(outer, _query));

            Assert.That((outer.SubscribeCount, outer.UnsubscribeCount), Is.EqualTo((1, 1)), "外側の購読はその場で解除する");
            Assert.That((inner.SubscribeCount, inner.UnsubscribeCount), Is.EqualTo((1, 0)));

            var scene = SpawnLive(AssetOwner.Scene(SceneA));
            EmitPreUnloading(inner, SceneA);
            Assert.That(_backend.DestroyCountOf(scene.Id), Is.EqualTo(1));

            _system.Dispose();
            Assert.That(inner.UnsubscribeCount, Is.EqualTo(1), "残った内側の購読は Dispose で解除する");
        }

        /// <summary>IsSceneLoaded だけを使う偽 query。</summary>
        private sealed class FakeSceneQuery : ISceneQuery
        {
            public HashSet<string> Loaded { get; } = new(StringComparer.Ordinal);

            public SceneBase? GetLoadedScene(string identity) => null;

            public bool IsSceneLoaded(string identity) => Loaded.Contains(identity);

            public bool IsSceneStable(string identity) => Loaded.Contains(identity);
        }
    }
}
