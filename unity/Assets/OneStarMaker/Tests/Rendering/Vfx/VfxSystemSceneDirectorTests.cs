#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Rendering.Vfx
{
    /// <summary>
    /// 実 SceneDirector（Unity Scene I/O だけを差し替えた TestableSceneDirector）の通知で、
    /// Scene 所有の instance が Unity Scene の unload より前に回収されることを見る。
    /// 観測点は UnitySceneUnloadAction（PerformUnitySceneUnload の中）で、その時点の Destroy 回数を捕捉する。
    /// </summary>
    [TestFixture]
    public sealed class VfxSystemSceneDirectorTests : SceneDirectorTestBase
    {
        private FakeVfxBackend _backend = null!;
        private FakeVfxLogger _logger = null!;
        private VfxSystem _system = null!;
        private GameObject _source = null!;
        private Dictionary<string, int> _destroyCountAtUnitySceneUnload = null!;

        [SetUp]
        public void SetUpVfx()
        {
            _backend = new FakeVfxBackend();
            _logger = new FakeVfxLogger();
            _system = new VfxSystem(_backend, _logger);
            _source = new GameObject("VfxSource");
            _destroyCountAtUnitySceneUnload = new Dictionary<string, int>();
        }

        [TearDown]
        public void TearDownVfx()
        {
            // 基底の TearDown が SceneDirector を Dispose する前に外す。
            _system.Dispose();
            if (_source != null)
            {
                UnityEngine.Object.DestroyImmediate(_source);
            }
        }

        private VfxSpawnRequest Request() => new(_source, Vector3.zero, Quaternion.identity);

        /// <summary>SceneDirector の生成直後、最初の Scene のロード前に接続する（BindSceneLifecycle の使用前提）。</summary>
        private void BindAndCaptureUnitySceneUnload()
        {
            _system.BindSceneLifecycle(Director.OnSceneEvent, Director);
            Director.UnitySceneUnloadAction = identity =>
            {
                // 同じ identity の最初の Unity Scene unload の時点を記録する。
                if (!_destroyCountAtUnitySceneUnload.ContainsKey(identity))
                {
                    _destroyCountAtUnitySceneUnload[identity] = _backend.DestroyCount;
                }

                return UniTask.CompletedTask;
            };
        }

        private IVfxHandle SpawnPlaying(string sceneIdentity)
        {
            var handle = _system.Spawn(Request(), AssetOwner.Scene(sceneIdentity));
            Assert.That(handle.IsActive, Is.True, $"Scene({sceneIdentity}) の起動が拒否された");
            Assert.That(handle.Play(), Is.True);
            return handle;
        }

        // ═══════════════════════════════════════════
        //  AC5 通常 Unload
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Unload_DestroysBeforeUnitySceneUnload() => UniTask.ToCoroutine(async () =>
        {
            SetupSingleScene("TestScene");
            BindAndCaptureUnitySceneUnload();
            await Director.AddScene("TestScene", null, CancellationToken.None);
            var handle = SpawnPlaying("TestScene");

            await Director.UnloadScene("TestScene");

            Assert.That(_destroyCountAtUnitySceneUnload["TestScene"], Is.EqualTo(1),
                "PreUnloading で回収されるべきで、Removed の保険に頼ってはいけない");
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(handle.IsActive, Is.False);
        });

        [UnityTest]
        public IEnumerator ParentUnload_ChildOwnedDestroyedBeforeFirstUnitySceneUnload() => UniTask.ToCoroutine(async () =>
        {
            SetupParentChild("Parent", "Child");
            BindAndCaptureUnitySceneUnload();
            var unloadOrder = new List<string>();
            var capture = Director.UnitySceneUnloadAction!;
            Director.UnitySceneUnloadAction = identity =>
            {
                unloadOrder.Add(identity);
                return capture(identity);
            };
            await Director.AddScene("Parent", null, CancellationToken.None);
            var child = SpawnPlaying("Child");
            var parent = SpawnPlaying("Parent");

            await Director.UnloadScene("Parent");

            Assert.That(unloadOrder, Is.Not.Empty);
            Assert.That(ChildDestroyedBefore(unloadOrder[0], (VfxHandle)child), Is.True,
                "子 Scene 所有は親 Unload の最初の Unity Scene unload の時点で回収済み");
            Assert.That(_destroyCountAtUnitySceneUnload["Parent"], Is.EqualTo(2),
                "親の Unity Scene unload の時点で親子とも回収済み");
            Assert.That(_backend.DestroyCountOf(((VfxHandle)child).Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCountOf(((VfxHandle)parent).Id), Is.EqualTo(1));
        });

        /// <summary>最初の Unity Scene unload の時点の Destroy 回数の中に、子の Destroy が含まれるか。</summary>
        private bool ChildDestroyedBefore(string firstUnloaded, VfxHandle child)
        {
            var destroyedSoFar = 0;
            foreach (var call in _backend.Calls)
            {
                if (call.Op != "Destroy")
                {
                    continue;
                }

                destroyedSoFar++;
                if (call.Id == child.Id.Value)
                {
                    return destroyedSoFar <= _destroyCountAtUnitySceneUnload[firstUnloaded];
                }
            }

            return false;
        }

        // ═══════════════════════════════════════════
        //  同じ identity の Scene の unload → 再ロード後の再受理（S-4d4b AC6）
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator ReloadSameIdentity_AcceptsNewSpawn_AndOldHandleStaysCollected() => UniTask.ToCoroutine(async () =>
        {
            SetupSingleScene("TestScene");
            BindAndCaptureUnitySceneUnload();
            await Director.AddScene("TestScene", null, CancellationToken.None);
            var old = (VfxHandle)SpawnPlaying("TestScene");
            await Director.UnloadScene("TestScene");
            Assert.That(_backend.DestroyCountOf(old.Id), Is.EqualTo(1), "前提: 1 回目の unload で回収された");
            var oldCalls = _backend.CallCountOf(old.Id);

            await Director.AddScene("TestScene", null, CancellationToken.None);
            var created = _backend.CreateCount;
            var renewed = (VfxHandle)SpawnPlaying("TestScene");

            Assert.That(_backend.CreateCount - created, Is.EqualTo(1), "再ロードした Scene の所有で受理される");
            Assert.That(old.IsActive, Is.False, "古い handle は再ロードで生き返らない");
            Assert.That(old.Play(), Is.False);
            Assert.That(_backend.CallCountOf(old.Id), Is.EqualTo(oldCalls), "古い handle の操作は backend に届かない");

            await Director.UnloadScene("TestScene");

            Assert.That(_backend.DestroyCountOf(renewed.Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCountOf(old.Id), Is.EqualTo(1), "古い instance を二重に回収しない");
            Assert.That(renewed.IsActive, Is.False);
        });

        // ═══════════════════════════════════════════
        //  AC6 Load キャンセルと失敗回収
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator CancelDuringPreLoad_DestroysAtLoadCanceled() => UniTask.ToCoroutine(async () =>
        {
            SetupSingleScene("TestScene");
            BindAndCaptureUnitySceneUnload();
            int? destroyAtLoadCanceled = null;
            int? destroyAtCancelCleanedUp = null;
            using var observer = Director.OnSceneEvent.Subscribe(e =>
            {
                if (e.SceneIdentify != "TestScene")
                {
                    return;
                }

                if (e.Type == SceneEventType.StateChanged && e.State == SceneState.LoadCanceled)
                {
                    destroyAtLoadCanceled ??= _backend.DestroyCount;
                }
                else if (e.Type == SceneEventType.CancelCleanedUp)
                {
                    destroyAtCancelCleanedUp ??= _backend.DestroyCount;
                }
            });

            var cts = new CancellationTokenSource();
            IVfxHandle? handle = null;
            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    handle = _system.Spawn(Request(), AssetOwner.Scene("TestScene"));
                    cts.Cancel();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            try
            {
                await Director.AddScene("TestScene", null, cts.Token);
                Assert.Fail("OperationCanceledException が throw されるべき");
            }
            catch (OperationCanceledException)
            {
            }

            cts.Dispose();
            Assert.That(handle, Is.Not.Null);
            Assert.That(_backend.CreateCount, Is.EqualTo(1), "PreLoading 中の自 Scene 所有は受理する");
            Assert.That(destroyAtLoadCanceled, Is.EqualTo(1));
            Assert.That(_destroyCountAtUnitySceneUnload["TestScene"], Is.EqualTo(1));
            Assert.That(destroyAtCancelCleanedUp, Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
            Assert.That(handle!.IsActive, Is.False);
        });

        [UnityTest]
        public IEnumerator OnLoadedFailure_DestroysAtObservedTerminalEvent() => UniTask.ToCoroutine(async () =>
        {
            SetupSingleScene("TestScene");
            BindAndCaptureUnitySceneUnload();
            string? terminalEvent = null;
            int? destroyAtTerminal = null;
            using var observer = Director.OnSceneEvent.Subscribe(e =>
            {
                if (e.SceneIdentify != "TestScene" || e.Type != SceneEventType.StateChanged || terminalEvent != null)
                {
                    return;
                }

                if (e.State == SceneState.PreUnloading || e.State == SceneState.LoadCanceled)
                {
                    terminalEvent = e.State.ToString();
                    destroyAtTerminal = _backend.DestroyCount;
                }
            });

            IVfxHandle? handle = null;
            Factory.OnCreated = scene =>
            {
                scene.LoadedAction = _ =>
                {
                    handle = _system.Spawn(Request(), AssetOwner.Scene("TestScene"));
                    throw new InvalidOperationException("OnLoaded failure");
                };
            };

            try
            {
                await Director.AddScene("TestScene", null, CancellationToken.None);
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
            }

            // 回収経路が実際に出した終端通知を記録する（架空のイベント列を期待値に固定しない）。
            TestContext.Out.WriteLine($"[VfxSystemSceneDirectorTests] OnLoaded failure terminal event: {terminalEvent ?? "(none)"}");
            Assert.That(handle, Is.Not.Null);
            Assert.That(_backend.CreateCount, Is.EqualTo(1), "WaitLoadChildScene 中の自 Scene 所有は受理する");
            Assert.That(terminalEvent, Is.Not.Null, "PreUnloading / LoadCanceled のどちらも出なかった");
            Assert.That(destroyAtTerminal, Is.EqualTo(1));
            Assert.That(_destroyCountAtUnitySceneUnload["TestScene"], Is.EqualTo(1));
            Assert.That(_backend.DestroyCount, Is.EqualTo(1));
        });

        // ═══════════════════════════════════════════
        //  AC10 Destroy 失敗の隔離
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator DestroyThrows_OtherInstanceStillReleased_AndUnloadCompletes() => UniTask.ToCoroutine(async () =>
        {
            SetupSingleScene("TestScene");
            BindAndCaptureUnitySceneUnload();
            var removedSeen = false;
            var destroyAttemptsAtRemoved = -1;
            using var observer = Director.OnSceneEvent.Subscribe(e =>
            {
                if (e.SceneIdentify == "TestScene" && e.Type == SceneEventType.Removed)
                {
                    removedSeen = true;
                    destroyAttemptsAtRemoved = _backend.DestroyCount;
                }
            });
            await Director.AddScene("TestScene", null, CancellationToken.None);
            var h1 = (VfxHandle)SpawnPlaying("TestScene");
            var h2 = (VfxHandle)SpawnPlaying("TestScene");
            _backend.Fault = (op, id) => op == "Destroy" && id == h1.Id ? new InvalidOperationException("destroy") : null;

            await Director.UnloadScene("TestScene");

            Assert.That(_destroyCountAtUnitySceneUnload["TestScene"], Is.EqualTo(2), "h1 の失敗で h2 の回収が止まらない");
            Assert.That(_backend.DestroyCountOf(h1.Id), Is.EqualTo(1));
            Assert.That(_backend.DestroyCountOf(h2.Id), Is.EqualTo(1));
            Assert.That(_logger.ErrorCount, Is.EqualTo(1));
            Assert.That(removedSeen, Is.True, "Unload は Removed まで完走する");
            Assert.That(destroyAttemptsAtRemoved, Is.EqualTo(2));
            Assert.That(Director.ContainsScene("TestScene"), Is.False);
        });
    }
}
