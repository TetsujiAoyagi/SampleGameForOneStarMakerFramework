#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Tests.ContentResolution;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.AssetManagement
{
    /// <summary>
    /// Scene の台帳が外（預け元）で読んだ Scene を預かる口と、預け元の identity の予約（AC3）。
    /// 実物の AssetManagement に directory を install して封じた状態で、預け元と directory の port は fake にする。
    /// </summary>
    [TestFixture]
    public sealed class AdoptedSceneTests
    {
        private Runtime.AssetManagement.AssetManagement _assets = null!;
        private CountingDirectoryPort _port = null!;
        private FakeAdoptedSource _source = null!;

        [SetUp]
        public void SetUp()
        {
            _assets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            _port = new CountingDirectoryPort();
            _assets.InstallContentDirectory(_port);
            _assets.SealAddressables();
            _source = new FakeAdoptedSource("SnapA", "SnapB");
            _assets.InstallAdoptedSceneSource(_source);
        }

        [Test]
        public void AdoptedScene_IsUnloadedThroughTheSource_AndLeavesTheRegistry()
        {
            var handle = _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(handle.IsLoaded, Is.True);
            Assert.That(_source.Acquires, Is.EqualTo(1));

            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Unloads, Is.EqualTo(1));
            _assets.ReleaseScene("SnapA");

            // 台帳に無いので、もう一度の unload は預け元を呼ばない。次の預かりは取得し直す。
            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Unloads, Is.EqualTo(1));
            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Acquires, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseAll_EmptiesTheRegistry_WithoutCallingTheSourceUnload()
        {
            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();

            _assets.ReleaseAll();

            Assert.That(_source.Unloads, Is.Zero, "Play 停止では Unity が解体する");
            Assert.That(_assets.IsAdoptedSceneSource(_source), Is.False, "預け元は 1 回の Play の物なので外れる");
            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Unloads, Is.Zero, "台帳の印が倒れている");
        }

        // gate の待ちの継続は main thread の次の tick で走るので、coroutine で待つ。
        [UnityTest]
        public IEnumerator ConcurrentAdoptionsOfTheSameIdentity_AcquireOnce_AndReturnTheSameScene() => UniTask.ToCoroutine(async () =>
        {
            _source.Gate = new UniTaskCompletionSource();
            var first = _assets.AdoptSceneAsync("SnapA");
            var second = _assets.AdoptSceneAsync("SnapA");
            _source.Gate.TrySetResult();

            var a = await first;
            var b = await second;

            Assert.That(_source.Acquires, Is.EqualTo(1));
            Assert.That(a.Name, Is.EqualTo(b.Name));
            await _assets.UnloadSceneAsync("SnapA");
            Assert.That(_source.Unloads, Is.EqualTo(1));
            Assert.That(b.IsLoaded, Is.False, "2 つの handle は同じ Scene を指す");
        });

        [Test]
        public void CloseContentDirectory_KeepsTheAdoptedScene_WhichIsStillReleasable()
        {
            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();

            _assets.CloseContentDirectoryAsync().GetAwaiter().GetResult();

            Assert.That(_source.Unloads, Is.Zero, "close は directory の token の Scene だけを unload する");
            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Unloads, Is.EqualTo(1));
            Assert.DoesNotThrow(() => _assets.ReleaseScene("SnapA"));
        }

        [Test]
        public void ReservedIdentity_IsRejectedByTheContentEntry_WithOrWithoutAnAdoptedScene()
        {
            var before = Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.LoadContentSceneAsync("SnapB", "Full"));
            Assert.That(before!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous), "未要求の複製が load 済みのまま lifecycle の外に居る状態");

            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();
            var after = Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.LoadContentSceneAsync("SnapA", "Full"));
            Assert.That(after!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
            Assert.That(_port.SceneLoads, Is.Zero);
        }

        [Test]
        public void UnreservedIdentity_AndDirectoryScene_AreNotAdopted()
        {
            var unreserved = Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.AdoptSceneAsync("DirC"));
            Assert.That(unreserved!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));

            _assets.LoadContentSceneAsync("DirC", "Full").GetAwaiter().GetResult();
            Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.AdoptSceneAsync("DirC"));
            Assert.That(_source.Acquires, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AdoptionDuringADirectoryLoad_IsSerialized_AndBothEntriesRemain() => UniTask.ToCoroutine(async () =>
        {
            _port.SceneGate = new UniTaskCompletionSource();
            var directoryLoad = _assets.LoadContentSceneAsync("DirC", "Full");
            var adoption = _assets.AdoptSceneAsync("SnapA");

            Assert.That(_source.Acquires, Is.Zero, "directory の load が gate を持つ間は預からない");
            _port.SceneGate.TrySetResult();
            await directoryLoad;
            await adoption;

            Assert.That(_source.Acquires, Is.EqualTo(1));
            await _assets.UnloadSceneAsync("DirC");
            await _assets.UnloadSceneAsync("SnapA");
            Assert.That(_port.SceneUnloads, Is.EqualTo(1), "directory の entry が台帳に残っていた");
            Assert.That(_source.Unloads, Is.EqualTo(1), "預かった entry が台帳に残っていた");
        });

        [Test]
        public void DuringAndAfterClose_NothingIsAdopted()
        {
            _port.Accepting = false;
            Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.AdoptSceneAsync("SnapA"));

            _port.Accepting = true;
            _assets.CloseContentDirectoryAsync().GetAwaiter().GetResult();
            Assert.ThrowsAsync<ContentDirectoryException>(async () => await _assets.AdoptSceneAsync("SnapA"));
            Assert.That(_source.Acquires, Is.Zero);
        }

        [Test]
        public void CanceledToken_DoesNotCallTheSource()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.That(async () => await _assets.AdoptSceneAsync("SnapA", default, cts.Token), Throws.InstanceOf<OperationCanceledException>());
            Assert.That(_source.Acquires, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancelDuringAcquire_WaitsForCompletion_UnloadsAndDoesNotRegister() => UniTask.ToCoroutine(async () =>
        {
            using var cts = new CancellationTokenSource();
            _source.Gate = new UniTaskCompletionSource();
            var adoption = _assets.AdoptSceneAsync("SnapA", default, cts.Token);

            cts.Cancel();
            Assert.That(_source.Unloads, Is.Zero, "取得の完了を待つ");
            _source.Gate.TrySetResult();

            var canceled = false;
            try { await adoption; }
            catch (OperationCanceledException) { canceled = true; }
            Assert.That(canceled, Is.True);
            Assert.That(_source.Unloads, Is.EqualTo(1), "完了した Scene を unload してから取消を返す");
            await _assets.UnloadSceneAsync("SnapA");
            Assert.That(_source.Unloads, Is.EqualTo(1), "台帳に入っていない");
        });

        [Test]
        public void AlreadyInvalidScene_IsTreatedAsUnloaded()
        {
            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();
            _source.Last!.IsLoaded = false;

            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();

            Assert.That(_source.Unloads, Is.Zero);
            Assert.DoesNotThrow(() => _assets.ReleaseScene("SnapA"), "台帳の印が倒れている");
        }

        [Test]
        public void FailedUnloadOfALoadedScene_Throws_AndCanBeRetried()
        {
            _assets.AdoptSceneAsync("SnapA").GetAwaiter().GetResult();
            _source.FailUnload = true;

            Assert.ThrowsAsync<InvalidOperationException>(async () => await _assets.UnloadSceneAsync("SnapA"));
            Assert.Throws<InvalidOperationException>(() => _assets.ReleaseScene("SnapA"), "台帳は未 unload のまま");

            _source.FailUnload = false;
            _assets.UnloadSceneAsync("SnapA").GetAwaiter().GetResult();
            Assert.That(_source.Last!.IsLoaded, Is.False);
            Assert.DoesNotThrow(() => _assets.ReleaseScene("SnapA"));
        }

        [Test]
        public void Reservation_FailsWhenTheIdentityIsAlreadyInTheRegistry()
        {
            var assets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            assets.InstallContentDirectory(new CountingDirectoryPort());
            assets.LoadContentSceneAsync("SnapA", "Full").GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => assets.InstallAdoptedSceneSource(new FakeAdoptedSource("SnapA")));
            Assert.That(assets.IsAdoptedSceneSource(_source), Is.False);
        }

        /// <summary>預け元の fake。<see cref="Gate"/> があれば取得の完了をそこで止める。</summary>
        private sealed class FakeAdoptedSource : IAdoptedSceneSource
        {
            internal FakeAdoptedSource(params string[] reserved) => ReservedIdentities = reserved;

            public IReadOnlyCollection<string> ReservedIdentities { get; }

            internal int Acquires { get; private set; }

            internal int Unloads { get; private set; }

            internal bool FailUnload { get; set; }

            internal UniTaskCompletionSource? Gate { get; set; }

            internal FakeSnapshotScene? Last { get; private set; }

            public async UniTask<IBackendScene> AcquireAsync(string sceneIdentity)
            {
                Acquires++;
                if (Gate != null) await Gate.Task;
                Last = new FakeSnapshotScene(sceneIdentity, "Assets/OsmPlaySnapshot/x/" + sceneIdentity + ".unity");
                return Last;
            }

            public UniTask UnloadAsync(IBackendScene scene)
            {
                if (FailUnload) throw new InvalidOperationException("Unity did not unload the scene.");
                Unloads++;
                ((FakeSnapshotScene)scene).IsLoaded = false;
                return UniTask.CompletedTask;
            }
        }
    }
}
