#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.ContentResolution;
using OneStarMaker.Tests.SceneSystem.Helpers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// 要求時解決の loader を渡した SceneDirector。PerformUnitySceneLoad を差し替えず、実物の AssetManagement と fake の directory の port、
    /// 照合を答える fake の供給元で、AddScene・UnloadScene を通して記録の寿命と要求の組み立て、失敗の後始末を見る。
    /// </summary>
    [TestFixture]
    public sealed class SceneDirectorContentLoaderTests : SceneDirectorTestBase
    {
        private const string ProviderId = "fake-registered-directory";
        private static readonly ContentIdentity Identity = new("set", "r1", "StandaloneWindows64-Player", new string('d', 64));

        private Runtime.AssetManagement.AssetManagement _assets = null!;
        private DirectoryPort _port = null!;
        private SceneDirector? _director;

        [SetUp]
        public void SetUpLoader()
        {
            _assets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            _port = new DirectoryPort();
            _assets.InstallContentDirectory(_port);
        }

        [TearDown]
        public void TearDownLoader()
        {
            _director?.Dispose();
            _director = null;
        }

        // ─── 成功経路: 記録の寿命 ───

        [UnityTest]
        public IEnumerator Record_IsReadableAfterAdd_AndNotAfterUnload_WithTheSameUnloadCalls()
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            provider.Representations["Entry"] = new[] { "Full", "Whitebox" };
            _director = CreateDirector(provider, "Whitebox", Resource("Entry"));

            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Null, "追加していない id は null");
            await _director.AddScene("Entry", null, CancellationToken.None);

            var record = _director.GetContentLoadRecord("Entry");
            Assert.That(record, Is.Not.Null);
            Assert.That(record!.Candidate.Representation, Is.EqualTo("Whitebox"));
            Assert.That(record.Candidate.Identity, Is.EqualTo(Identity));
            Assert.That(record.SourceBindingVerified, Is.True);
            Assert.That(_port.SceneLoads, Is.EqualTo(1));
            Assert.That(_port.LastSceneRepresentation, Is.EqualTo("Whitebox"));

            await _director.UnloadScene("Entry");
            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Null, "unload の後は読めない");
            var withLoader = (_port.SceneLoads, _port.SceneUnloads);

            // 同じ操作を loader なしの SceneDirector で行い、unload の呼び出しが同じであることを見る。
            _director.Dispose();
            var plainPort = new DirectoryPort();
            var plainAssets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            plainAssets.InstallContentDirectory(plainPort);
            var map = Map;
            _director = new SceneDirector(Factory, UICommon, map, new NoLoadingDisplay(), plainAssets, "Whitebox", true);
            await _director.AddScene("Entry", null, CancellationToken.None);
            await _director.UnloadScene("Entry");
            Assert.That((plainPort.SceneLoads, plainPort.SceneUnloads), Is.EqualTo(withLoader));
            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Null);

            // 解放まで済んでいれば同じ Scene をもう一度読める（台帳に残っていない）。
            await _director.AddScene("Entry", null, CancellationToken.None);
            Assert.That(plainPort.SceneLoads, Is.EqualTo(2));
        });

        [Test]
        public void Record_ForNullOrUnknownId_IsNull()
        {
            _director = CreateDirector(new Provider(), "Whitebox", Resource("Entry"));
            Assert.That(_director.GetContentLoadRecord(null!), Is.Null);
            Assert.That(_director.GetContentLoadRecord("Unknown"), Is.Null);
        }

        // ─── 要求の組み立て（fallback） ───

        [UnityTest]
        public IEnumerator WhiteboxVariant_FullOnlyScene_LoadsFull_WithExplicitFallback()
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            provider.Representations["Next"] = new[] { "Full" };
            _director = CreateDirector(provider, "Whitebox", Resource("Next"));

            await _director.AddScene("Next", null, CancellationToken.None);

            Assert.That(provider.LastRequest!.Representation, Is.EqualTo("Whitebox"));
            Assert.That(provider.LastRequest.FallbackRepresentation, Is.EqualTo("Full"));
            Assert.That(_director.GetContentLoadRecord("Next")!.Candidate.Representation, Is.EqualTo("Full"));
            Assert.That(_port.LastSceneRepresentation, Is.EqualTo("Full"), "content 入口へは実効の表現を渡す");
        });

        [UnityTest]
        public IEnumerator FullOrEmptyVariant_RequestsWithoutFallback([Values("Full", "")] string variant)
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            provider.Representations["Entry"] = new[] { "Full", "Whitebox" };
            _director = CreateDirector(provider, variant, Resource("Entry"));

            await _director.AddScene("Entry", null, CancellationToken.None);

            Assert.That(provider.LastRequest!.Representation, Is.EqualTo(variant));
            Assert.That(provider.LastRequest.FallbackRepresentation, Is.Empty, "fallback と同じ表現・空の表現には付けない");
            Assert.That(_director.GetContentLoadRecord("Entry")!.Candidate.Representation, Is.EqualTo("Full"));
        });

        // ─── loader を通らない経路 ───

        [UnityTest]
        public IEnumerator WithoutLoader_DirectEntryIsUsed_AndNoRecord()
            => UniTask.ToCoroutine(async () =>
        {
            var resource = Resource("Entry");
            var map = SceneTestHelper.CreateSceneResourceMap(resource);
            CreatedSOs.Add(map);
            _director = new SceneDirector(Factory, UICommon, map, new NoLoadingDisplay(), _assets, "Whitebox", true);

            await _director.AddScene("Entry", null, CancellationToken.None);

            Assert.That(_port.LastSceneRepresentation, Is.EqualTo("Whitebox"), "今までどおり起動時の表現で content 入口を呼ぶ");
            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Null);
        });

        [UnityTest]
        public IEnumerator LogicalNodeWithoutPayload_DoesNotCallTheLoader()
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            var resource = SceneTestHelper.CreateSceneResource("Group");
            SetDescription(resource, new SceneAssetDescription("Group", LoadType.OnDemand, new List<AssetPayload>()));
            CreatedSOs.Add(resource);
            _director = CreateDirector(provider, "Whitebox", resource);

            await _director.AddScene("Group", null, CancellationToken.None);

            Assert.That(provider.Calls, Is.Zero);
            Assert.That(_port.SceneLoads, Is.Zero);
            Assert.That(_director.GetContentLoadRecord("Group"), Is.Null);
        });

        [UnityTest]
        public IEnumerator SceneAlreadyInSceneManager_IsReused_WithoutTheLoader()
            => UniTask.ToCoroutine(async () =>
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    Assert.Ignore("An open scene has unsaved changes; the test does not open another scene.");

            // test runner の untitled の Scene があると Additive の新規 Scene を作れないので、Single で作って保存し、それを開いたままにする。
            // base の UICommon は Single で消えるので、この Scene に作り直して使う。
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var folder = "Assets/OsmS31Test" + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            GameObject? uiObject = null;
            try
            {
                var opened = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Assert.That(EditorSceneManager.SaveScene(opened, folder + "/S31Reused.unity"), Is.True);
                Assert.That(SceneManager.GetSceneByName("S31Reused").isLoaded, Is.True);
                uiObject = new GameObject("UICommon_S31");
                var ui = uiObject.AddComponent<Runtime.UISystem.UICommon>();
                var provider = new Provider();
                provider.Representations["S31Reused"] = new[] { "Full" };
                var resource = Resource("S31Reused");
                Map = SceneTestHelper.CreateSceneResourceMap(resource);
                CreatedSOs.Add(Map);
                _director = new SceneDirector(Factory, ui, Map, new NoLoadingDisplay(), _assets, "Whitebox", true, Loader(provider), "Full");

                await _director.AddScene("S31Reused", null, CancellationToken.None);

                Assert.That(provider.Calls, Is.Zero, "SceneManager にある Scene は読まずに再利用する");
                Assert.That(_port.SceneLoads, Is.Zero);
                Assert.That(_director.GetContentLoadRecord("S31Reused"), Is.Null);
            }
            finally
            {
                _director?.Dispose();
                _director = null;
                if (uiObject != null) UnityEngine.Object.DestroyImmediate(uiObject);
                if (setup.Length != 0 && setup.All(x => System.IO.File.Exists(x.path)) && setup.Any(x => x.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        });

        // ─── 失敗経路 ───

        [UnityTest]
        public IEnumerator LoaderRejection_FailsAddScene_WithoutDirectEntry_AndAnotherSceneStillLoads(
            [Values("mismatch", "notFound")] string rejection)
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            provider.Representations["Good"] = new[] { "Full" };
            if (rejection == "mismatch")
            {
                provider.Representations["Bad"] = new[] { "Full" };
                provider.Binding = candidate => candidate.LogicalKey != "Bad";
            }

            _director = CreateDirector(provider, "Full", Resource("Bad"), Resource("Good"));

            ContentResolutionException? failure = null;
            try { await _director.AddScene("Bad", null, CancellationToken.None); }
            catch (ContentResolutionException exception) { failure = exception; }

            Assert.That(failure, Is.Not.Null, "AddScene は失敗する");
            if (rejection == "mismatch") Assert.That(failure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingMismatch));
            else Assert.That(failure!.Result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.NoCandidate));
            Assert.That(_port.SceneLoads, Is.Zero, "content 入口を直接呼んで読み直さない");
            Assert.That(_director.ContainsScene("Bad"), Is.False, "既存の load の失敗と同じ後始末");
            Assert.That(_director.GetContentLoadRecord("Bad"), Is.Null);

            await _director.AddScene("Good", null, CancellationToken.None);
            Assert.That(_director.GetContentLoadRecord("Good"), Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator FailureAfterLoaderSuccess_RemovesTheRecordWithThePair([Values("exception", "canceled")] string failure)
            => UniTask.ToCoroutine(async () =>
        {
            var provider = new Provider();
            provider.Representations["Entry"] = new[] { "Full" };
            _director = CreateDirector(provider, "Full", Resource("Entry"));
            Factory.OnCreated = scene =>
            {
                scene.LoadedAction = _ => failure == "exception"
                    ? throw new InvalidOperationException("OnLoaded failure")
                    : throw new OperationCanceledException();
            };

            try { await _director.AddScene("Entry", null, CancellationToken.None); }
            catch (InvalidOperationException) when (failure == "exception") { }

            Assert.That(_port.SceneLoads, Is.EqualTo(1), "loader は成功している");
            Assert.That(_director.ContainsScene("Entry"), Is.False, "後始末が組を外した");
            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Null, "組と一緒に記録も消える");

            Factory.OnCreated = null;
            await _director.AddScene("Entry", null, CancellationToken.None);
            Assert.That(_director.GetContentLoadRecord("Entry"), Is.Not.Null, "読み直した Scene には新しい記録が付く");
        });

        // ─── Editor の Play の複製（snapshot）が宣言する identity ───

        [UnityTest]
        public IEnumerator DeclaredSnapshotScene_IsReadThroughTheLoader_AndUnloadedThroughTheRegistry()
            => UniTask.ToCoroutine(async () =>
        {
            var snapshot = Snapshot();
            snapshot.Io.AddLoaded("Snap", SnapCopy);
            var director = CreateRecordingDirector(snapshot, Resource("Snap"));

            await director.AddScene("Snap", null, CancellationToken.None);

            var record = director.GetContentLoadRecord("Snap");
            Assert.That(record, Is.Not.Null);
            Assert.That(record!.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));
            Assert.That(snapshot.Io.Loads, Is.Zero, "load 済みの複製を採る");
            Assert.That(_port.SceneLoads, Is.Zero);

            await director.UnloadScene("Snap");
            Assert.That(director.Unloads, Is.EqualTo(new[] { ("Snap", true) }), "台帳経由で unload する");
            Assert.That(snapshot.Io.Unloads, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator DeclaredSnapshotScene_CanceledInPreLoad_IsCleanedUpThroughTheRegistryOnly()
            => UniTask.ToCoroutine(async () =>
        {
            var snapshot = Snapshot();
            var unrequested = snapshot.Io.AddLoaded("Snap", SnapCopy);
            var director = CreateRecordingDirector(snapshot, Resource("Snap"));
            using var cts = new CancellationTokenSource();
            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    cts.Cancel();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            var canceled = false;
            try { await director.AddScene("Snap", null, cts.Token); }
            catch (OperationCanceledException) { canceled = true; }

            Assert.That(canceled, Is.True);
            Assert.That(director.Unloads, Is.EqualTo(new[] { ("Snap", true) }), "名前の unload に落とさず、台帳経由（台帳に無いので何もしない）");
            Assert.That(unrequested.IsLoaded, Is.True, "load 済みのまま lifecycle の外に居る複製を離さない");
            Assert.That(snapshot.Io.Unloads + snapshot.Io.Loads, Is.Zero);
            Assert.That(director.ContainsScene("Snap"), Is.False);
        });

        [UnityTest]
        public IEnumerator DeclaredSnapshotScene_FailedInPreLoad_IsCleanedUpThroughTheRegistryOnly()
            => UniTask.ToCoroutine(async () =>
        {
            var snapshot = Snapshot();
            var unrequested = snapshot.Io.AddLoaded("Snap", SnapCopy);
            var director = CreateRecordingDirector(snapshot, Resource("Snap"));
            Factory.OnCreated = scene => scene.PreLoadAction = _ => throw new InvalidOperationException("PreLoad failure");

            var failed = false;
            try { await director.AddScene("Snap", null, CancellationToken.None); }
            catch (InvalidOperationException) { failed = true; }

            Assert.That(failed, Is.True);
            Assert.That(director.Unloads.All(x => x.Item1 == "Snap" && x.Item2), Is.True, "宣言にある identity は台帳経由だけ");
            Assert.That(unrequested.IsLoaded, Is.True);
            Assert.That(snapshot.Io.Unloads, Is.Zero);
            Assert.That(director.ContainsScene("Snap"), Is.False);
        });

        [UnityTest]
        public IEnumerator UndeclaredScene_KeepsTheExistingCleanupFlag()
            => UniTask.ToCoroutine(async () =>
        {
            var snapshot = Snapshot();
            var director = CreateRecordingDirector(snapshot, Resource("Other"));
            Factory.OnCreated = scene => scene.PreLoadAction = _ => throw new InvalidOperationException("PreLoad failure");

            try { await director.AddScene("Other", null, CancellationToken.None); }
            catch (InvalidOperationException) { }

            Assert.That(director.Unloads.All(x => x.Item1 == "Other" && !x.Item2), Is.True, "宣言に無い identity の後始末は今と同じ");
        });

        [Test]
        public void LoaderWithoutContentDirectoryMode_IsRejected()
        {
            var map = SceneTestHelper.CreateSceneResourceMap(Resource("Entry"));
            CreatedSOs.Add(map);
            Assert.Throws<ArgumentException>(() => new SceneDirector(
                Factory, UICommon, map, new NoLoadingDisplay(), _assets, "Whitebox", false, Loader(new Provider()), "Full"));
        }

        // ─── fixture ───

        private const string SnapCopy = "Assets/OsmPlaySnapshot/1_2_ab/Snap.unity";

        private (EditorSnapshotProvider Provider, FakeSceneIo Io) Snapshot()
        {
            var io = new FakeSceneIo();
            var source = new EditorSnapshotSceneSource(
                new[] { new EditorSnapshotDeclaredScene("Snap", SnapCopy, "Assets/Scenes/Snap.unity", false, "abc") }, io);
            _assets.InstallAdoptedSceneSource(source);
            return (new EditorSnapshotProvider(source, "Full"), io);
        }

        private RecordingDirector CreateRecordingDirector((EditorSnapshotProvider Provider, FakeSceneIo Io) snapshot, params SceneResource[] resources)
        {
            Map = SceneTestHelper.CreateSceneResourceMap(resources);
            CreatedSOs.Add(Map);
            var loader = new ContentRequestLoader(_assets, new ResolutionPolicy(new IContentProvider[] { snapshot.Provider, new Provider() }, new AcceptAllRule()));
            var director = new RecordingDirector(Factory, UICommon, Map, _assets, loader);
            _director = director;
            return director;
        }

        /// <summary>Unity の Scene の unload の判断（台帳経由か、名前か）を記録する SceneDirector。</summary>
        private sealed class RecordingDirector : SceneDirector
        {
            internal RecordingDirector(ISceneFactory factory, Runtime.UISystem.UICommon ui, SceneResourceMap map, IAssetManagement assets, ContentRequestLoader loader)
                : base(factory, ui, map, new NoLoadingDisplay(), assets, "Full", true, loader, "Full")
            {
            }

            internal List<(string, bool)> Unloads { get; } = new();

            protected override UniTask PerformUnitySceneUnload(string sceneIdentify, bool backendSceneLoaded)
            {
                Unloads.Add((sceneIdentify, backendSceneLoaded));
                return base.PerformUnitySceneUnload(sceneIdentify, backendSceneLoaded);
            }
        }

        private SceneDirector CreateDirector(Provider provider, string variant, params SceneResource[] resources)
        {
            Map = SceneTestHelper.CreateSceneResourceMap(resources);
            CreatedSOs.Add(Map);
            return new SceneDirector(Factory, UICommon, Map, new NoLoadingDisplay(), _assets, variant, true, Loader(provider), "Full");
        }

        private ContentRequestLoader Loader(Provider provider)
            => new(_assets, new ResolutionPolicy(new IContentProvider[] { provider }, new AcceptAllRule()));

        private SceneResource Resource(string identity)
        {
            var resource = SceneTestHelper.CreateSceneResource(identity);
            SetDescription(resource, new SceneAssetDescription(identity, LoadType.OnDemand, new List<AssetPayload>
            {
                new(string.Empty, new AssetReference("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")),
            }));
            CreatedSOs.Add(resource);
            return resource;
        }

        private static void SetDescription(SceneResource resource, SceneAssetDescription description)
        {
            var field = typeof(SceneResource).GetField("_sceneAssetDescription", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field!.SetValue(resource, description);
        }

        /// <summary>
        /// 登録した directory の供給元を模す。key ごとの表現の一覧から、要求の表現があればそれ、無ければ Full を実効の表現として申告する
        /// （directory の索引と同じ）。照合の既定は真。
        /// </summary>
        private sealed class Provider : IContentProvider, IContentSourceBinding
        {
            internal Dictionary<string, string[]> Representations { get; } = new(StringComparer.Ordinal);
            internal Func<ContentCandidate, bool> Binding { get; set; } = _ => true;
            internal int Calls { get; private set; }
            internal ContentRequest? LastRequest { get; private set; }

            public string Id => ProviderId;

            public ContentSourceKind SourceKind => ContentSourceKind.ContentDirectory;

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
            {
                Calls++;
                LastRequest = request;
                if (!Representations.TryGetValue(request.LogicalKey, out var available))
                    return new(new ContentAvailability(ContentAvailabilityStatus.NotFound));
                var wanted = request.Representation.Length == 0 ? "Full" : request.Representation;
                var effective = Array.IndexOf(available, wanted) >= 0 ? wanted : Array.IndexOf(available, "Full") >= 0 ? "Full" : null;
                if (effective == null)
                    return new(new ContentAvailability(ContentAvailabilityStatus.NotFound));
                var candidate = new ContentCandidate(ProviderId, ContentSourceKind.ContentDirectory, request.LogicalKey, request.Kind,
                    effective, Identity, null, ContentAcceptanceBasis.VerifiedDigest, ContentLoadPath.ContentDirectory);
                return new(new ContentAvailability(ContentAvailabilityStatus.Available, candidate));
            }

            public bool IsLoadedBy(ContentCandidate candidate, IAssetManagement assetManagement) => Binding(candidate);
        }

        private sealed class AcceptAllRule : IContentCompatibilityRule
        {
            public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason)
            {
                reason = "accepted";
                return true;
            }
        }

        private sealed class DirectoryScene : IBackendScene, IContentDirectoryToken
        {
            public bool IsLoaded { get; private set; } = true;
            public string Name => "directory-scene";
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
            internal void Unload() => IsLoaded = false;
        }

        /// <summary>完了済みの結果を返し、Scene の load・unload の回数を数える Content Directory port。</summary>
        private sealed class DirectoryPort : IContentDirectoryBackend
        {
            internal int SceneLoads;
            internal int SceneUnloads;
            internal string? LastSceneRepresentation;
            public string BuildIdentity => "build";
            public string Target => "StandaloneWindows64-Player";
            public string CachePrefix => "directory:s31-scene-test:";
            public AssetKey GetObjectKey(string logicalKey, string representation)
                => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
                => throw new NotSupportedException();
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
            public void Release(IBackendAsset asset) { }
            public void ConfigureCacheEviction(Action evictRevisionEntries) { }
            public UniTask StopAndDrainAsync() => UniTask.CompletedTask;
            public void EnsureAccepting() { }
            public UniTask CloseAsync() => UniTask.CompletedTask;
            public void CompletePlayStop() { }
            public void BeginSynchronousShutdown() { }
        }

        private sealed class NoLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType type, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask Hide(CancellationToken ct) => UniTask.CompletedTask;
        }
    }
}
