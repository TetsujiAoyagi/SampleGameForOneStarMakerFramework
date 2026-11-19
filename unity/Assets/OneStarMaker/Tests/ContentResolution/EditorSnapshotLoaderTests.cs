#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Tests.AssetManagement;
using UnityEngine;

namespace OneStarMaker.Tests.ContentResolution
{
    /// <summary>
    /// 既ロードの経路の候補を、loader・実物の AssetManagement・Scene の実体を差し替えた預け元で読む（AC2）。
    /// 預け元の Unity の I/O だけを fake にし、採る・読み直す・照合・unload の判断は製品の型が行う。
    /// </summary>
    [TestFixture]
    public sealed class EditorSnapshotLoaderTests
    {
        private const string TitleCopy = "Assets/OsmPlaySnapshot/1_2_ab/Title.unity";
        private const string TitleSource = "Assets/SampleGame/Scenes/Title.unity";

        private Runtime.AssetManagement.AssetManagement _assets = null!;
        private CountingDirectoryPort _port = null!;
        private FakeSceneIo _io = null!;
        private EditorSnapshotSceneSource _source = null!;
        private EditorSnapshotProvider _provider = null!;

        [SetUp]
        public void SetUp()
        {
            _assets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            _port = new CountingDirectoryPort();
            _assets.InstallContentDirectory(_port);
            _io = new FakeSceneIo();
            _source = new EditorSnapshotSceneSource(
                new[] { new EditorSnapshotDeclaredScene("Title", TitleCopy, TitleSource, wasDirty: false, sha256: "abc") }, _io);
            _provider = new EditorSnapshotProvider(_source, "Full");
            _assets.InstallAdoptedSceneSource(_source);
        }

        [Test]
        public void LoadedCopy_IsTakenWithoutReloading_AndReleasedThroughExistingApi()
        {
            _io.AddLoaded("Title", TitleCopy);

            var outcome = Loader().LoadSceneAsync(Request()).GetAwaiter().GetResult();

            Assert.That(outcome.Handle.IsLoaded, Is.True);
            Assert.That(outcome.Record.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));
            Assert.That(outcome.Record.Candidate.LoadPath, Is.EqualTo(ContentLoadPath.AlreadyLoadedScene));
            Assert.That(outcome.Record.SourceBindingVerified, Is.True);
            Assert.That(outcome.Record.OwnerLabel, Is.EqualTo("Scene(Title)"));
            Assert.That(_io.Loads, Is.Zero, "load 済みの複製は読み直さない");
            Assert.That(_port.SceneLoads, Is.Zero, "directory の content 入口は呼ばない");

            _assets.UnloadSceneAsync("Title").GetAwaiter().GetResult();
            _assets.ReleaseScene("Title");
            Assert.That(_io.Unloads, Is.EqualTo(1), "採った複製も離す時は unload する");
            Assert.That(_io.IsLoaded(TitleCopy), Is.False);
        }

        [Test]
        public void CopyNotLoaded_IsReadOnceFromTheCopyPath()
        {
            var outcome = Loader().LoadSceneAsync(Request()).GetAwaiter().GetResult();

            Assert.That(_io.Loads, Is.EqualTo(1));
            Assert.That(_io.LastLoadPath, Is.EqualTo(TitleCopy));
            Assert.That(outcome.Handle.IsLoaded, Is.True);
            Assert.That(outcome.Record.Candidate.Facts[EditorSnapshotProvider.CopyPathFact], Is.EqualTo(TitleCopy));
        }

        [Test]
        public void SceneMatch_RejectsNameAndPathDifferences()
        {
            Assert.That(EditorSnapshotSceneMatch.Verify("Title", TitleCopy, Loaded(("Title", TitleCopy))), Is.Null);
            Assert.That(EditorSnapshotSceneMatch.Verify("Title", TitleCopy, Loaded(("Other", TitleCopy))), Is.Not.Null, "名前が違う");
            Assert.That(EditorSnapshotSceneMatch.Verify("Title", TitleCopy, Loaded(("Title", TitleSource))), Does.Contain("not loaded"), "path が違う");
            Assert.That(EditorSnapshotSceneMatch.Verify("Title", TitleCopy, Loaded(("Title", TitleCopy), ("Title", TitleCopy))), Does.Contain("more than once"), "同名が 2 つ");
            Assert.That(EditorSnapshotSceneMatch.Verify("Title", TitleCopy, Loaded(("Title", TitleCopy), ("Title", TitleSource))), Does.Contain(TitleSource), "同名が別の path で load 済み");
        }

        [Test]
        public void MismatchAfterLoad_Throws_UnloadsTheReadScene_AndDoesNotRegisterIt()
        {
            // Unity が別の名前で Scene を出した（複製の file が別物に差し替わった等）。
            _io.NameOnLoad = "Impostor";

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await Loader().LoadSceneAsync(Request()));

            Assert.That(exception!.Message, Does.Contain("Title"));
            Assert.That(exception.Message, Does.Contain("not loaded from " + TitleCopy));
            AssertNotRegisteredAndDirectoryUntouched();
            Assert.That(_io.Unloads, Is.EqualTo(1), "預け元が読んだ Scene は unload する");
        }

        [Test]
        public void LoadNotAccepted_Throws_WithTheReason()
        {
            _io.AcceptLoad = false;

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await Loader().LoadSceneAsync(Request()));

            Assert.That(exception!.Message, Does.Contain("Title"));
            Assert.That(exception.Message, Does.Contain("may be missing"));
            AssertNotRegisteredAndDirectoryUntouched();
        }

        [Test]
        public void LoadedButNoScene_Throws_WithTheReason()
        {
            _io.ProduceScene = false;

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await Loader().LoadSceneAsync(Request()));

            Assert.That(exception!.Message, Does.Contain("Title"));
            Assert.That(exception.Message, Does.Contain("did not load"));
            AssertNotRegisteredAndDirectoryUntouched();
        }

        [Test]
        public void SameNameFromAnotherPath_IsNotLoaded_AndThrows()
        {
            _io.AddLoaded("Title", TitleSource);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(async () => await Loader().LoadSceneAsync(Request()));

            Assert.That(exception!.Message, Does.Contain("Title"));
            Assert.That(exception.Message, Does.Contain(TitleSource));
            Assert.That(_io.Loads, Is.Zero, "読むと同名が 2 つになるので読まない");
            AssertNotRegisteredAndDirectoryUntouched();
            Assert.That(_io.IsLoaded(TitleSource), Is.True, "他の由来の Scene には触らない");
        }

        [Test]
        public void AlreadyLoadedCandidate_FromAProviderWithoutTheLoadPort_IsUnsupported()
        {
            var candidate = new ContentCandidate("plain", ContentSourceKind.EditorSnapshot, "Title", ContentKind.Scene, "Full", default, null,
                ContentAcceptanceBasis.OpenSceneSnapshot, ContentLoadPath.AlreadyLoadedScene);
            var loader = new ContentRequestLoader(_assets, new ResolutionPolicy(new IContentProvider[] { new PlainProvider(candidate) }, Rule()));

            var exception = Assert.ThrowsAsync<ContentResolutionException>(async () => await loader.LoadSceneAsync(Request()));

            Assert.That(exception!.Rejection, Is.EqualTo(ContentLoadRejection.LoadPathUnsupported));
            Assert.That(_io.Loads + _port.SceneLoads, Is.Zero);
        }

        [Test]
        public void SourceNotInstalledInTheAssetManager_IsABindingMismatch()
        {
            var other = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            other.InstallContentDirectory(new CountingDirectoryPort());
            var loader = new ContentRequestLoader(other, new ResolutionPolicy(new IContentProvider[] { _provider }, Rule()));

            var exception = Assert.ThrowsAsync<ContentResolutionException>(async () => await loader.LoadSceneAsync(Request()));

            Assert.That(exception!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingMismatch));
            Assert.That(_io.Loads, Is.Zero);
        }

        [Test]
        public void DeclaresAlreadyLoadedScene_FollowsTheSnapshotProvider()
        {
            var loader = Loader();
            Assert.That(loader.DeclaresAlreadyLoadedScene("Title"), Is.True);
            Assert.That(loader.DeclaresAlreadyLoadedScene("Field"), Is.False);
        }

        private void AssertNotRegisteredAndDirectoryUntouched()
        {
            Assert.That(_port.SceneLoads, Is.Zero, "directory の content 入口で読み直さない");
            var unloadsBefore = _io.Unloads;
            _assets.UnloadSceneAsync("Title").GetAwaiter().GetResult();
            Assert.That(_io.Unloads, Is.EqualTo(unloadsBefore), "台帳に入っていないので、台帳の unload は何もしない");
            Assert.That(_io.LoadedCount("Title", TitleCopy), Is.Zero, "複製を load 済みのまま残さない");
        }

        private ContentRequestLoader Loader() => new(_assets, new ResolutionPolicy(new IContentProvider[] { _provider }, Rule()));

        private static ContentCompatibilityRule Rule()
            => new(new ContentCompatibilityEnvelope("set", "StandaloneWindows64-Player", "6000.6.9f1", 1));

        private static ContentRequest Request() => new("Title", ContentKind.Scene, "Full");

        private static IReadOnlyList<EditorSnapshotLoadedScene> Loaded(params (string Name, string Path)[] scenes)
            => scenes.Select(x => new EditorSnapshotLoadedScene(x.Name, x.Path)).ToArray();

        /// <summary>既ロードの候補を返すが、load の口を持たない供給元。</summary>
        private sealed class PlainProvider : IContentProvider
        {
            private readonly ContentCandidate _candidate;

            internal PlainProvider(ContentCandidate candidate) => _candidate = candidate;

            public string Id => "plain";

            public ContentSourceKind SourceKind => ContentSourceKind.EditorSnapshot;

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
                => new(new ContentAvailability(ContentAvailabilityStatus.Available, _candidate));
        }
    }

    /// <summary>Unity の Scene の代わり。unload されると load 済みでないと答える。</summary>
    internal sealed class FakeSnapshotScene : IBackendScene
    {
        internal FakeSnapshotScene(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }

        internal string Path { get; }

        public bool IsLoaded { get; internal set; } = true;

        public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
    }

    /// <summary>
    /// 預け元の Unity の I/O の fake。load は path の file 名を Scene 名にして Scene を足す（<see cref="NameOnLoad"/> で差し替えられる）。
    /// </summary>
    internal sealed class FakeSceneIo : IEditorSnapshotSceneIo
    {
        private readonly List<FakeSnapshotScene> _scenes = new();

        internal int Loads { get; private set; }

        internal int Unloads { get; private set; }

        internal string? LastLoadPath { get; private set; }

        /// <summary>偽なら Unity が load を受け付けない（file が無い等）。</summary>
        internal bool AcceptLoad { get; set; } = true;

        /// <summary>偽なら load は完了するが Scene が現れない。</summary>
        internal bool ProduceScene { get; set; } = true;

        /// <summary>load で現れる Scene の名前（null なら file 名）。</summary>
        internal string? NameOnLoad { get; set; }

        /// <summary>偽なら unload を受け付けない（Scene は load 済みのまま）。</summary>
        internal bool AcceptUnload { get; set; } = true;

        internal FakeSnapshotScene AddLoaded(string name, string path)
        {
            var scene = new FakeSnapshotScene(name, path);
            _scenes.Add(scene);
            return scene;
        }

        internal bool IsLoaded(string path) => _scenes.Any(x => x.IsLoaded && x.Path == path);

        internal int LoadedCount(string name, string path) => _scenes.Count(x => x.IsLoaded && x.Name == name && x.Path == path);

        public IReadOnlyList<EditorSnapshotLoadedScene> CaptureLoadedScenes()
            => _scenes.Where(x => x.IsLoaded).Select(x => new EditorSnapshotLoadedScene(x.Name, x.Path)).ToArray();

        public IBackendScene? FindLoaded(string copyPath) => _scenes.FirstOrDefault(x => x.IsLoaded && x.Path == copyPath);

        public UniTask<bool> LoadAsync(string copyPath)
        {
            Loads++;
            LastLoadPath = copyPath;
            if (!AcceptLoad) return UniTask.FromResult(false);
            if (ProduceScene) AddLoaded(NameOnLoad ?? System.IO.Path.GetFileNameWithoutExtension(copyPath), copyPath);
            return UniTask.FromResult(true);
        }

        public UniTask<bool> UnloadAsync(IBackendScene scene)
        {
            Unloads++;
            if (!AcceptUnload) return UniTask.FromResult(false);
            ((FakeSnapshotScene)scene).IsLoaded = false;
            return UniTask.FromResult(true);
        }
    }

    /// <summary>Scene の load の回数を数え、<see cref="SceneGate"/> があれば完了をそこで止める Content Directory port。</summary>
    internal sealed class CountingDirectoryPort : IContentDirectoryBackend
    {
        internal int SceneLoads;
        internal int SceneUnloads;
        internal bool Accepting = true;
        internal UniTaskCompletionSource? SceneGate;

        public string BuildIdentity => "build";
        public string Target => "StandaloneWindows64-Player";
        public string CachePrefix => "directory:snapshot-test:";
        public AssetKey GetObjectKey(string logicalKey, string representation)
            => AssetKey.FromContentDirectory(CachePrefix + logicalKey + ":" + representation, AssetType.Other);
        public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object
            => throw new NotSupportedException();
        public async UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct)
        {
            SceneLoads++;
            if (SceneGate != null) await SceneGate.Task;
            return new DirectorySceneToken(sceneIdentity);
        }
        public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct)
            => throw new NotSupportedException();
        public UniTask UnloadSceneAsync(IBackendScene scene)
        {
            SceneUnloads++;
            ((DirectorySceneToken)scene).IsLoaded = false;
            return UniTask.CompletedTask;
        }
        public void ReleaseSceneAfterUnityShutdown(IBackendScene scene) { }
        public void ReleaseSceneTokenAfterPlayStop(IBackendScene scene) { }
        public void Release(IBackendAsset asset) { }
        public void ConfigureCacheEviction(Action evictRevisionEntries) { }
        public UniTask StopAndDrainAsync()
        {
            Accepting = false;
            return UniTask.CompletedTask;
        }
        public void EnsureAccepting()
        {
            if (!Accepting) throw new Runtime.BuildContent.ContentDirectoryException(Runtime.BuildContent.ContentDirectoryFailureCode.OperationFailed, "The content directory is no longer accepting loads.");
        }
        public UniTask CloseAsync() => UniTask.CompletedTask;
        public void CompletePlayStop() { }
        public void BeginSynchronousShutdown() { }

        internal sealed class DirectorySceneToken : IBackendScene, IContentDirectoryToken
        {
            internal DirectorySceneToken(string name) => Name = name;
            public bool IsLoaded { get; internal set; } = true;
            public string Name { get; }
            public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
        }
    }
}
