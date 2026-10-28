#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// <c>OnLoadedImpl</c> の中で Scene 所有（<c>Scene(identity)</c>）の asset のロードを待っている間に、その Scene の unload を
    /// 要求しても、<c>ReleaseScene</c> はロードの完了より前に起きず、<c>Removed</c> の時点で asset が残らないことを見る。
    /// <para>
    /// <c>AssetManagement.LoadAssetAsync</c> は backend のロードを await した後に所有者の Scene の終端を確かめずに登録するので、
    /// 所有者の Scene が先に解放されるとその asset は誰にも解放されない。このテストは、<c>OnLoadedImpl</c> の実行中は
    /// キャンセル窓の外で、unload が Stable の後まで保留される（直接の unload も親の再帰 unload も）ために、
    /// その欠落に当たらないことを確かめる。Scene の中で動的に asset をロードする呼び出し側（Events Scene の trigger）の前提。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class SceneDirectorSceneScopedAssetLoadTests : SceneDirectorTestBase
    {
        private const string Address = "Assets/Test/SceneScopedSource.asset";

        private HoldingAssetBackend _backend = null!;

        [SetUp]
        public void SetUpHoldingBackend()
        {
            // 基底の SetUp の後に差し替える（Director はこのテストの Setup* で作る）。
            _backend = new HoldingAssetBackend();
            AssetManagement = new Runtime.AssetManagement.AssetManagement(_backend);
        }

        [UnityTest]
        public IEnumerator DirectUnloadDuringOnLoadedAssetLoad_RunsOnceAfterStable_AndLeavesNoOwner()
            => UniTask.ToCoroutine(async () =>
            {
                SetupSingleScene("Events");
                var events = Record("Events");
                var loadStarted = HoldSceneScopedLoadInOnLoaded("Events");

                var add = Director.AddScene("Events", null, CancellationToken.None);
                await loadStarted.Task;

                await Director.UnloadScene("Events");
                Assert.That(events.States, Has.No.Member(SceneState.PreUnloading), "ロードの完了より前に unload が始まった");

                _backend.CompletePending();
                await add;

                AssertUnloadedOnceAfterStableWithoutOwner(events);
            });

        [UnityTest]
        public IEnumerator ParentUnloadDuringChildOnLoadedAssetLoad_RunsOnceAfterStable_AndLeavesNoOwner()
            => UniTask.ToCoroutine(async () =>
            {
                SetupParentChild("Cell", "Events", LoadType.OnDemand);
                await Director.AddScene("Cell", null, CancellationToken.None);
                var events = Record("Events");
                var loadStarted = HoldSceneScopedLoadInOnLoaded("Events");

                var add = Director.AddScene("Events", null, CancellationToken.None);
                await loadStarted.Task;

                var parentUnload = Director.UnloadScene("Cell");
                Assert.That(events.States, Has.No.Member(SceneState.PreUnloading), "ロードの完了より前に子の unload が始まった");

                _backend.CompletePending();
                await add;
                await parentUnload;

                AssertUnloadedOnceAfterStableWithoutOwner(events);
                Assert.That(Director.ContainsScene("Cell"), Is.False);
            });

        // ─── helpers ───

        /// <summary>Scene の OnLoadedImpl で Scene 所有のロードを始め、backend で保留させる。ロードの開始を返す。</summary>
        private UniTaskCompletionSource HoldSceneScopedLoadInOnLoaded(string identity)
        {
            var started = new UniTaskCompletionSource();
            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity != identity)
                {
                    return;
                }

                scene.LoadedAction = async ct =>
                {
                    var load = scene.LoadSceneScopedAssetForTest<UnityEngine.Object>(AssetKey.FromAddress(Address), ct);
                    started.TrySetResult();
                    var handle = await load;
                    Assert.That(handle.Value != null, Is.True, "前提: 保留したロードが値を返した");
                };
            };

            return started;
        }

        private SceneRecord Record(string identity)
        {
            var record = new SceneRecord();
            record.Subscription = Director.OnSceneEvent.Subscribe(e =>
            {
                if (e.SceneIdentify != identity)
                {
                    return;
                }

                if (e.Type == SceneEventType.StateChanged)
                {
                    record.States.Add(e.State);
                }
                else if (e.Type == SceneEventType.Removed)
                {
                    record.RemovedCount++;
                    record.OwnersAtRemoved = AssetManagement.GetOwners(AssetKey.FromAddress(Address)).Count;
                }
            });
            return record;
        }

        private void AssertUnloadedOnceAfterStableWithoutOwner(SceneRecord events)
        {
            events.Subscription?.Dispose();
            TestContext.Out.WriteLine($"[SceneScopedAssetLoad] states={string.Join(",", events.States)} removed={events.RemovedCount}");
            Assert.That(_backend.LoadAssetCallCount, Is.EqualTo(1), "前提: Scene 所有のロードが 1 回走った");
            var stable = events.States.IndexOf(SceneState.Stable);
            var preUnloading = events.States.IndexOf(SceneState.PreUnloading);
            Assert.That(stable, Is.GreaterThanOrEqualTo(0), "Stable に到達していない");
            Assert.That(preUnloading, Is.GreaterThan(stable), "unload が Stable の後に走っていない");
            Assert.That(events.States.FindAll(state => state == SceneState.PreUnloading), Has.Count.EqualTo(1), "unload が 1 回でない");
            Assert.That(events.RemovedCount, Is.EqualTo(1));
            Assert.That(events.OwnersAtRemoved, Is.Zero, "Removed の時点で Scene 所有の asset が残る");
            Assert.That(_backend.ReleasedCount, Is.EqualTo(1), "ロードした asset が backend に返されていない");
            Assert.That(Director.ContainsScene("Events"), Is.False);
        }

        private sealed class SceneRecord
        {
            public List<SceneState> States { get; } = new();

            public int RemovedCount { get; set; }

            public int OwnersAtRemoved { get; set; } = -1;

            public IDisposable? Subscription { get; set; }
        }

        /// <summary>
        /// asset のロードを test が完了させるまで保留する backend。Scene の I/O は既存の <see cref="FakeAssetBackend"/> に任せる
        /// （<see cref="FakeAssetBackend"/> の asset のロードは即時に完了するので、保留の経路を作れない）。
        /// </summary>
        private sealed class HoldingAssetBackend : IAssetBackend
        {
            private readonly FakeAssetBackend _scenes = new();
            private readonly List<(UniTaskCompletionSource<IBackendAsset> Source, HeldAsset Asset)> _pending = new();

            public int LoadAssetCallCount { get; private set; }

            public int ReleasedCount { get; private set; }

            public void CompletePending()
            {
                var pending = _pending.ToArray();
                _pending.Clear();
                foreach (var (source, asset) in pending)
                {
                    source.TrySetResult(asset);
                }
            }

            public UniTask<IBackendAsset> LoadAssetAsync<T>(string address, CancellationToken ct) where T : UnityEngine.Object
            {
                LoadAssetCallCount++;
                var source = new UniTaskCompletionSource<IBackendAsset>();
                _pending.Add((source, new HeldAsset(new Texture2D(1, 1))));
                return source.Task;
            }

            public IBackendAsset LoadAssetSync<T>(string address) where T : UnityEngine.Object
                => throw new NotSupportedException("このテストは同期ロードを使わない");

            public UniTask<IBackendScene> LoadSceneAsync(string address, SceneLoadOptions options, CancellationToken ct)
                => _scenes.LoadSceneAsync(address, options, ct);

            public UniTask UnloadSceneAsync(IBackendScene scene, CancellationToken ct)
                => _scenes.UnloadSceneAsync(scene, ct);

            public UniTask<IBackendInstance> InstantiateAsync(string address, Transform? parent, bool worldSpace, CancellationToken ct)
                => _scenes.InstantiateAsync(address, parent, worldSpace, ct);

            public void Release(IBackendAsset asset)
            {
                if (asset is HeldAsset held)
                {
                    ReleasedCount++;
                    held.Release();
                    return;
                }

                _scenes.Release(asset);
            }
        }

        private sealed class HeldAsset : IBackendAsset
        {
            private UnityEngine.Object? _asset;

            public HeldAsset(UnityEngine.Object asset)
            {
                _asset = asset;
            }

            public UnityEngine.Object? Asset => _asset;

            public bool IsValid => _asset != null;

            public void Release()
            {
                if (_asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(_asset);
                }

                _asset = null;
            }
        }
    }
}
