#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Tests.AssetManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace OneStarMaker.Tests.ContentResolution
{
    /// <summary>
    /// 要求時解決の層を、Scene の lifecycle を持つ層なしで、既存の AssetManagement の上で合成できることを確かめる。
    /// AssetManagement は実物を使い、Addressables 側は <see cref="FakeAssetBackend"/>、Content Directory 側は
    /// この file 内の fake port で置き換える（既存テストの private fake は共有できず、動かさないため複製する）。
    /// 探す段（resolver）とロードを分けて観測するため、fake port のロード回数を数える。
    /// 供給元は照合（<see cref="IContentSourceBinding"/>）を答える fake で、loader は照合が真の時だけ content 入口を呼ぶ。
    /// </summary>
    [TestFixture]
    public sealed class ContentRequestLoaderTests
    {
        private const string ProviderId = "fake-directory";
        private static readonly ContentIdentity Identity = new("set", "r1", "target", "digest");

        private FakeAssetBackend _backend = null!;
        private Runtime.AssetManagement.AssetManagement _assets = null!;
        private DirectoryPort _port = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeAssetBackend();
            // cache を渡さない構成にして、Release がそのまま port の Release に届くようにする。
            _assets = new Runtime.AssetManagement.AssetManagement(_backend);
            _port = new DirectoryPort();
            _assets.InstallContentDirectory(_port);
        }

        [TearDown]
        public void TearDown()
        {
            _port.DestroyObjects();
        }

        // E1
        [Test]
        public void Scene_IsResolvedLoadedAndReleasedThroughExistingApi()
        {
            var provider = Provider.Answering(Available(Candidate("Entry", ContentKind.Scene)));
            var loader = Loader(provider);

            var outcome = loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();

            Assert.That(outcome.Handle.IsLoaded, Is.True);
            Assert.That(outcome.Record.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
            Assert.That(outcome.Record.Candidate.ProviderId, Is.EqualTo(ProviderId));
            Assert.That(outcome.Record.OwnerLabel, Is.EqualTo("Scene(Entry)"));
            Assert.That(outcome.Record.SourceBindingVerified, Is.True, "照合が真の時だけ読み、記録は確かめた事実を持つ");
            Assert.That(provider.BindingCalls, Is.EqualTo(1));
            Assert.That(_port.SceneLoads, Is.EqualTo(1));
            Assert.That(_port.LastSceneRepresentation, Is.EqualTo("Full"), "候補の表現をそのまま content 入口へ渡す");
            Assert.That(_backend.LoadSceneCallCount, Is.Zero, "Addressables 側を通らない");

            _assets.UnloadSceneAsync("Entry").GetAwaiter().GetResult();
            _assets.ReleaseScene("Entry");
            Assert.That(_port.SceneUnloads, Is.EqualTo(1));
        }

        // E2
        [Test]
        public void Object_IsOwnedByTheGivenOwnerAndReleasedThroughExistingApi()
        {
            var provider = Provider.Answering(Available(Candidate("Widget", ContentKind.Object)));
            var loader = Loader(provider);

            var outcome = loader.LoadAssetAsync<GameObject>(new ContentRequest("Widget", ContentKind.Object, "Full"), AssetOwner.Manual).GetAwaiter().GetResult();

            Assert.That(outcome.Handle.Value != null, Is.True);
            Assert.That(outcome.Record.OwnerLabel, Is.EqualTo("Manual"));
            var key = _port.GetObjectKey("Widget", "Full");
            Assert.That(_assets.GetOwners(key), Is.EqualTo(new[] { AssetOwner.Manual }));

            _assets.Release(outcome.Handle);
            Assert.That(_assets.GetOwners(key), Is.Empty);
            Assert.That(_port.Releases, Is.EqualTo(1));
        }

        // E2（入口の取り違え）
        [Test]
        public void WrongEntryForKind_IsRejectedBeforeResolving()
        {
            var provider = Provider.Answering(Available(Candidate("Entry", ContentKind.Scene)));
            var loader = Loader(provider);

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await loader.LoadAssetAsync<GameObject>(new ContentRequest("Entry", ContentKind.Scene, "Full"), AssetOwner.Manual));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Widget", ContentKind.Object, "Full")));
            Assert.That(provider.Calls, Is.Zero, "resolver を呼ぶ前に拒む");
            Assert.That(_port.AssetLoads + _port.SceneLoads, Is.Zero);
        }

        // E3
        [Test]
        public void Record_MatchesRequestAndAdoptedCandidate()
        {
            var loader = Loader(Provider.Answering(Available(Candidate("Entry", ContentKind.Scene))));
            var request = new ContentRequest("Entry", ContentKind.Scene, "Full");

            var record = loader.LoadSceneAsync(request).GetAwaiter().GetResult().Record;

            Assert.That(record.Request, Is.SameAs(request));
            Assert.That(record.Candidate.Representation, Is.EqualTo("Full"));
            Assert.That(record.Traceability, Is.EqualTo(ContentTraceability.Traceable));
            Assert.That(record.Probes.Count, Is.EqualTo(1));
            Assert.That(record.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.Adopted));
        }

        // E4
        [Test]
        public void NotFound_ThrowsWithResultAndDoesNotLoad()
        {
            var loader = Loader(Provider.Answering(new ContentAvailability(ContentAvailabilityStatus.NotFound)));

            var exception = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));

            Assert.That(exception!.Result.IsResolved, Is.False);
            Assert.That(exception.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.NoCandidate));
            Assert.That(exception.Result.Probes.Count, Is.EqualTo(1));
            Assert.That(exception.Result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.NotFound));
            Assert.That(exception.Rejection, Is.EqualTo(ContentLoadRejection.None));
            Assert.That(_port.SceneLoads, Is.Zero);
        }

        // E5
        [Test]
        public void RejectedWithoutIdentity_FailsClosedAndDoesNotLoad()
        {
            var loader = Loader(Provider.Answering(new ContentAvailability(ContentAvailabilityStatus.Rejected, reason: "permission denied")));

            var exception = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));

            Assert.That(exception!.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.IntegrityRejected));
            Assert.That(exception.Result.Probes.Count, Is.EqualTo(1));
            Assert.That(exception.Result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.Rejected));
            Assert.That(exception.Rejection, Is.EqualTo(ContentLoadRejection.None));
            Assert.That(_port.SceneLoads, Is.Zero);
        }

        // E6
        [TestCase(ContentSourceKind.EditorSnapshot, ContentAcceptanceBasis.OpenSceneSnapshot, ContentLoadPath.AlreadyLoadedScene)]
        [TestCase(ContentSourceKind.ContentDirectory, ContentAcceptanceBasis.VerifiedDigest, ContentLoadPath.Addressables)]
        public void UnsupportedLoadPath_IsRejectedBeforeLoadWithoutRewritingTheResult(
            ContentSourceKind source, ContentAcceptanceBasis basis, ContentLoadPath loadPath)
        {
            var candidate = Candidate("Entry", ContentKind.Scene, source, basis, loadPath);
            var loader = Loader(Provider.Answering(Available(candidate), source));

            var exception = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));

            Assert.That(exception!.Result.IsResolved, Is.True, "resolver の成功を「候補が無かった」に書き換えない");
            Assert.That(exception.Result.Candidate, Is.SameAs(candidate));
            Assert.That(exception.Result.Probes.Count, Is.EqualTo(1));
            Assert.That(exception.Result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.Adopted));
            Assert.That(exception.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.None));
            Assert.That(exception.Rejection, Is.EqualTo(ContentLoadRejection.LoadPathUnsupported));
            Assert.That(_port.SceneLoads, Is.Zero);
        }

        // E6（対照: snapshot 由来でも一時 directory 経由の候補は拒まない）
        [Test]
        public void SnapshotSourceThroughDirectoryPath_IsLoaded()
        {
            var candidate = Candidate("Entry", ContentKind.Scene, ContentSourceKind.EditorSnapshot, ContentAcceptanceBasis.OpenSceneSnapshot, ContentLoadPath.ContentDirectory);
            var loader = Loader(Provider.Answering(Available(candidate), ContentSourceKind.EditorSnapshot));

            var outcome = loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();

            Assert.That(outcome.Record.Candidate, Is.SameAs(candidate));
            Assert.That(_port.SceneLoads, Is.EqualTo(1));
        }

        // AC1: 照合が偽なら読まない
        [Test]
        public void BindingMismatch_IsRejectedBeforeLoad_ForSceneAndObject()
        {
            var sceneProvider = Provider.Answering(Available(Candidate("Entry", ContentKind.Scene)));
            sceneProvider.Binding = (_, _) => false;
            var sceneFailure = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await Loader(sceneProvider).LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));
            Assert.That(sceneFailure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingMismatch));
            Assert.That(sceneFailure.Result.IsResolved, Is.True, "resolver の成功を書き換えない");

            var objectProvider = Provider.Answering(Available(Candidate("Widget", ContentKind.Object)));
            objectProvider.Binding = (_, _) => false;
            var objectFailure = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await Loader(objectProvider).LoadAssetAsync<GameObject>(new ContentRequest("Widget", ContentKind.Object, "Full"), AssetOwner.Manual));
            Assert.That(objectFailure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingMismatch));

            Assert.That(_port.SceneLoads + _port.AssetLoads, Is.Zero, "port の load は 0 回");
        }

        // AC1: 照合を答えない供給元の候補は読まない
        [Test]
        public void ProviderWithoutBinding_IsRejectedBeforeLoad()
        {
            var provider = new UnboundProvider(Available(Candidate("Entry", ContentKind.Scene)));
            var loader = new ContentRequestLoader(_assets, new ResolutionPolicy(new IContentProvider[] { provider }, new AcceptAllRule()));

            var failure = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));

            Assert.That(failure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingUnavailable));
            Assert.That(_port.SceneLoads, Is.Zero);
        }

        // AC1: 照合は loader に渡した IAssetManagement と候補で尋ねる
        [Test]
        public void Binding_IsAskedWithTheAdoptedCandidateAndTheLoaderAssetManagement()
        {
            var candidate = Candidate("Entry", ContentKind.Scene);
            var provider = Provider.Answering(Available(candidate));
            ContentCandidate? askedCandidate = null;
            IAssetManagement? askedAssets = null;
            provider.Binding = (c, a) => { askedCandidate = c; askedAssets = a; return true; };

            Loader(provider).LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();

            Assert.That(askedCandidate, Is.SameAs(candidate));
            Assert.That(askedAssets, Is.SameAs(_assets));
        }

        // AC1: 探索の途中で directory が入れ替わると、前の候補の記録で成功しない
        [Test]
        public void DirectorySwappedDuringFind_DoesNotSucceedWithThePreviousCandidate()
        {
            var original = _port;
            var replacement = new DirectoryPort();
            var provider = Provider.Answering(Available(Candidate("Entry", ContentKind.Scene)));
            // 供給元は自分が答えた directory（original）が今も登録中かで照合する（実物の供給元と同じ答え方）。
            provider.Binding = (_, assets) => assets is Runtime.AssetManagement.AssetManagement concrete && concrete.IsInstalledContentDirectory(original);
            provider.OnFind = () =>
            {
                _assets.CloseContentDirectoryAsync().GetAwaiter().GetResult();
                _assets.InstallContentDirectory(replacement);
            };

            var failure = Assert.ThrowsAsync<ContentResolutionException>(async () =>
                await Loader(provider).LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));

            Assert.That(failure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingMismatch));
            Assert.That(original.SceneLoads + replacement.SceneLoads, Is.Zero);
        }

        // I-1 の経路 (6): directory の Scene を unload した後に同じ identity の Addressables の Scene が載ると、
        // 表現の記録は残っている。封じていない実物の AssetManagement でも、その Scene を directory の記録で返さない。
        [Test]
        public void AddressablesSceneWithSameIdentity_AfterDirectorySceneUnload_IsNotReturnedWithADirectoryRecord()
        {
            var loader = Loader(Provider.Answering(Available(Candidate("Entry", ContentKind.Scene))));
            loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();
            _assets.UnloadSceneAsync("Entry").GetAwaiter().GetResult();
            _assets.ReleaseScene("Entry");
            _assets.LoadSceneAsync("Entry", AddressablesScene("Entry")).GetAwaiter().GetResult();
            Assert.That(_backend.LoadSceneCallCount, Is.EqualTo(1), "前提: Addressables の Scene が同じ identity で台帳にある");

            var failure = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                await loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")));
            Assert.That(failure!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));

            // resolver を使わない content 入口も同じ（表現の記録が無い時と同じ拒否）。
            var direct = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                await _assets.LoadContentSceneAsync("Entry", "Full"));
            Assert.That(direct!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryAmbiguous));
            Assert.That(_port.SceneLoads, Is.EqualTo(1), "directory から読み直さない");
        }

        // 対照: directory が読んだ Scene が載ったままなら、同じ表現の要求は台帳の Scene を返す（既存の挙動）。
        [Test]
        public void DirectorySceneStillLoaded_IsReusedForTheSameRepresentation()
        {
            var loader = Loader(Provider.Answering(Available(Candidate("Entry", ContentKind.Scene))));
            loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();

            var outcome = loader.LoadSceneAsync(new ContentRequest("Entry", ContentKind.Scene, "Full")).GetAwaiter().GetResult();

            Assert.That(outcome.Handle.IsLoaded, Is.True);
            Assert.That(outcome.Record.SourceBindingVerified, Is.True);
            Assert.That(_port.SceneLoads, Is.EqualTo(1), "再利用で port を呼ばない");
        }

        private static SceneAssetDescription AddressablesScene(string identity)
            => new(identity, LoadType.OnDemand, new List<AssetPayload> { new(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")) });

        private ContentRequestLoader Loader(Provider provider)
            => new(_assets, new ResolutionPolicy(new IContentProvider[] { provider }, new AcceptAllRule()));

        private static ContentCandidate Candidate(
            string key,
            ContentKind kind,
            ContentSourceKind source = ContentSourceKind.ContentDirectory,
            ContentAcceptanceBasis basis = ContentAcceptanceBasis.VerifiedDigest,
            ContentLoadPath loadPath = ContentLoadPath.ContentDirectory)
            => new(ProviderId, source, key, kind, "Full", Identity, "manifest://opaque", basis, loadPath);

        private static ContentAvailability Available(ContentCandidate candidate)
            => new(ContentAvailabilityStatus.Available, candidate);

        /// <summary>候補を返し、照合を答える fake の供給元。既定の照合は真。</summary>
        private sealed class Provider : IContentProvider, IContentSourceBinding
        {
            private readonly ContentAvailability _answer;

            private Provider(ContentAvailability answer, ContentSourceKind sourceKind)
            {
                _answer = answer;
                SourceKind = sourceKind;
            }

            internal static Provider Answering(ContentAvailability answer, ContentSourceKind sourceKind = ContentSourceKind.ContentDirectory)
                => new(answer, sourceKind);

            public string Id => ProviderId;

            public ContentSourceKind SourceKind { get; }

            internal int Calls { get; private set; }

            internal int BindingCalls { get; private set; }

            internal Func<ContentCandidate, IAssetManagement, bool> Binding { get; set; } = (_, _) => true;

            internal Action? OnFind { get; set; }

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
            {
                Calls++;
                OnFind?.Invoke();
                return new ValueTask<ContentAvailability>(_answer);
            }

            public bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement)
            {
                BindingCalls++;
                return Binding(candidate, assetManagement);
            }
        }

        /// <summary>照合を答えない供給元（<see cref="IContentSourceBinding"/> を実装しない）。</summary>
        private sealed class UnboundProvider : IContentProvider
        {
            private readonly ContentAvailability _answer;

            internal UnboundProvider(ContentAvailability answer) => _answer = answer;

            public string Id => ProviderId;

            public ContentSourceKind SourceKind => ContentSourceKind.ContentDirectory;

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
                => new(_answer);
        }

        private sealed class AcceptAllRule : IContentCompatibilityRule
        {
            public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason)
            {
                reason = "accepted";
                return true;
            }
        }

        private sealed class DirectoryAsset : IBackendAsset, IContentDirectoryToken
        {
            internal DirectoryAsset(UnityEngine.Object asset) => Asset = asset;
            public UnityEngine.Object? Asset { get; }
            public bool IsValid => Asset != null;
        }

        private sealed class DirectoryScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded { get; private set; } = true;
            public string Name => "directory-scene";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
            internal void Unload() => IsLoaded = false;
        }

        /// <summary>完了済みの結果を返し、呼び出し回数を数える Content Directory port。</summary>
        private sealed class DirectoryPort : IContentDirectoryBackend
        {
            private GameObject? _created;
            internal int AssetLoads;
            internal int SceneLoads;
            internal int SceneUnloads;
            internal int Releases;
            internal string? LastSceneRepresentation;
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64-Player";
            public string CachePrefix => "directory:resolution-test:";
            public AssetKey GetObjectKey(string logicalKey, string representation)
                => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            {
                AssetLoads++;
                _created = new GameObject(logicalKey);
                return UniTask.FromResult<IBackendAsset>(new DirectoryAsset(_created));
            }
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
            {
                SceneLoads++;
                LastSceneRepresentation = representation;
                return UniTask.FromResult<IBackendScene>(new DirectoryScene());
            }
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
                => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene)
            {
                SceneUnloads++;
                if (scene is DirectoryScene directoryScene) directoryScene.Unload();
                return UniTask.CompletedTask;
            }
            public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { }
            public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene) { }
            public void Release(IBackendAsset asset) => Releases++;
            public void ConfigureCacheEviction(Action evictRevisionEntries) { }
            public UniTask StopAndDrainAsync() => UniTask.CompletedTask;
            public void EnsureAccepting() { }
            public UniTask CloseAsync() => UniTask.CompletedTask;
            public void CompletePlayStop() { }
            public void BeginSynchronousShutdown() { }

            internal void DestroyObjects()
            {
                if (_created != null) UnityEngine.Object.DestroyImmediate(_created);
                _created = null;
            }
        }
    }
}
