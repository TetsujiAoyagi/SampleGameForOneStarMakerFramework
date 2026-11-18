#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using Unity.Loading;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// base に上書きを重ねた登録の単体 test の共通の組み立て。base は temp の cache に製品の installer で実際に install し、製品の検証を通す。
    /// native の directory は層ごとに fake（登録・解除・解放を共通の log に書く）で、root は記録を差し替えられる合成の物。
    /// </summary>
    internal sealed class OverlayRegistrationFixture
    {
        internal const string Set = "s32set";
        internal const string Revision = "s32rev";
        internal const string Target = "StandaloneWindows64-Player";
        internal const string UnityVersion = "6000.6.9f1";
        internal const int RootSchemaVersion = 2;
        internal const string OverlaySet = "s32overlay";
        internal const string OverlayIdentity = "s32overlay-build";

        private readonly List<UnityEngine.Object> _objects = new();

        private OverlayRegistrationFixture(string cacheRoot, string overlayPath, VerifiedInstalledRevision verified)
        {
            CacheRoot = cacheRoot;
            OverlayPath = overlayPath;
            Verified = verified;
            BaseNative = new LayerNative("base", Log);
            OverlayNative = new LayerNative("overlay", Log);
        }

        internal string CacheRoot { get; }
        internal string OverlayPath { get; }
        internal VerifiedInstalledRevision Verified { get; }
        internal string Digest => Verified.ManifestSha256;
        internal List<string> Log { get; } = new();
        internal LayerNative BaseNative { get; }
        internal LayerNative OverlayNative { get; }

        internal static async Task<OverlayRegistrationFixture> CreateAsync()
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "osm-s32-overlay", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cacheRoot);
            var overlayPath = Path.Combine(cacheRoot, "overlay-content");
            Directory.CreateDirectory(overlayPath);
            var installed = await InstallAsync(Path.Combine(cacheRoot, "cache"));
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, installed.ManifestSha256, Set, Revision, Target);
            return new OverlayRegistrationFixture(cacheRoot, overlayPath, verified);
        }

        /// <summary>base の検証済みの値と一致する記録。引数で 1 欄ずつ変えられる。</summary>
        internal BuildContentOverlayBase Record(string? contentSet = null, string? revision = null, string? target = null,
            string? digest = null, string? declarationName = null)
            => new(contentSet ?? Set, revision ?? Revision, target ?? Target, digest ?? Digest, declarationName, OverlaySet, UnityVersion);

        internal BuildContentRoot Root(string identity, IReadOnlyList<BuildContentEntry> entries,
            BuildContentOverlayBase? overlayBase = null, string? declarationName = null)
        {
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            var declaration = declarationName == null ? null
                : new BuildContentDeclaration(declarationName, "Full", Array.Empty<KeyValuePair<string, string>>());
            root.Initialize(identity, Target, entries, declaration, overlayBase);
            _objects.Add(root);
            return root;
        }

        /// <summary>base の root と、記録が一致する上書きの root を natives に置く（entry は呼び手が決める）。</summary>
        internal void Arrange(IReadOnlyList<BuildContentEntry> baseEntries, IReadOnlyList<BuildContentEntry> overlayEntries,
            BuildContentOverlayBase? record = null, string? baseDeclarationName = null)
        {
            BaseNative.Root = Root(Revision, baseEntries, declarationName: baseDeclarationName);
            OverlayNative.Root = Root(OverlayIdentity, overlayEntries, record ?? Record(declarationName: baseDeclarationName),
                declarationName: baseDeclarationName);
        }

        internal ContentDirectorySession Register()
            => ContentDirectorySession.RegisterVerifiedWithOverlay(Verified, BaseNative, OverlayPath, OverlayIdentity, OverlayNative);

        internal Texture2D NewTexture()
        {
            var texture = new Texture2D(1, 1);
            _objects.Add(texture);
            return texture;
        }

        /// <summary>登録の予約が gate に残っているか（base の revision の削除の許可が拒まれるか）。</summary>
        internal bool ReservationHeld()
        {
            if (!ContentRevisionGate.TryAcquireDelete(Revision, Target, Verified.ContentPath, out var lease, out _)) return true;
            lease!.Dispose();
            if (!ContentRevisionGate.TryAcquireDelete(OverlayIdentity, Target, OverlayPath, out lease, out _)) return true;
            lease!.Dispose();
            return false;
        }

        /// <summary>保留中の巻き戻しの再試行を、失敗しない natives での登録と close で片付ける（後の test へ持ち越さない）。</summary>
        internal async Task FlushPendingAsync()
        {
            if (!ContentDirectoryLayers.HasPendingRollback) return;
            BaseNative.FailUnregisterCount = 0;
            OverlayNative.FailUnregisterCount = 0;
            var cleanupBase = new LayerNative("cleanup-base", new List<string>()) { Root = Root(Revision, Array.Empty<BuildContentEntry>()) };
            var session = ContentDirectorySession.RegisterVerified(Verified, cleanupBase);
            await session.CloseAsync();
        }

        internal async Task CleanupAsync()
        {
            await FlushPendingAsync();
            foreach (var value in _objects)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            if (Directory.Exists(CacheRoot)) Directory.Delete(CacheRoot, true);
        }

        internal static async Task WithAsync(Func<OverlayRegistrationFixture, Task> body)
        {
            // Unity の同期 context の上で install の await を同期で待つと止まりうるので、SetUp ではなく test の中で await する。
            var fixture = await CreateAsync();
            try { await body(fixture); }
            finally { await fixture.CleanupAsync(); }
        }

        // ─── entry ───

        internal static BuildContentEntry SceneEntry(string key, string representation, string sceneGuid)
            => new(key, key + "-" + representation + "-" + sceneGuid.Substring(0, 6), representation, SceneId(sceneGuid));

        internal static BuildContentEntry ObjectEntry(string key, string representation, string objectGuid, long localId = 1001)
            => new(key, key + "-" + representation + "-" + objectGuid.Substring(0, 6) + "-" + localId, representation,
                new Loadable<UnityEngine.Object>(ObjectId(objectGuid, localId)), objectGuid, localId, AssetType.Texture);

        // LoadableSceneId / LoadableObjectId は Editor の utility で作ると実在の asset の値になる。層の解決と id による影の単体では、
        // 実在の asset を選ばずに任意の id の組（同じ・違う）を作りたいので、構造体の field を設定して作る（native は fake なので読まない）。
        private static LoadableSceneId SceneId(string guid)
        {
            object boxed = default(LoadableSceneId);
            typeof(LoadableSceneId).GetField("m_SceneGUID", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(boxed, new GUID(guid));
            return (LoadableSceneId)boxed;
        }

        private static LoadableObjectId ObjectId(string guid, long localId)
        {
            object boxed = default(LoadableObjectId);
            var type = typeof(LoadableObjectId);
            type.GetField("m_GUID", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(boxed, new GUID(guid));
            type.GetField("m_LocalIdentifierInFile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(boxed, localId);
            return (LoadableObjectId)boxed;
        }

        private static async Task<ContentInstallResult> InstallAsync(string cacheRoot)
        {
            Directory.CreateDirectory(cacheRoot);
            var payload = Encoding.UTF8.GetBytes("s32-payload");
            var manifest = new ContentTransportManifest
            {
                version = 1,
                product = "OneStarMaker",
                contentSet = Set,
                revision = Revision,
                target = Target,
                unityVersion = UnityVersion,
                rootSchemaVersion = RootSchemaVersion,
                playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "payload.bin", size = payload.Length, sha256 = ContentDeliveryFiles.Sha256(payload) } },
                sourceFiles = Array.Empty<ContentSourceFile>(),
            };
            var manifestBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(manifest));
            var digest = ContentDeliveryFiles.Sha256(manifestBytes);
            using var source = new MemorySource(new Dictionary<string, byte[]>
            {
                { "transport.json", manifestBytes },
                { "content/payload.bin", payload },
            });
            var request = new ContentInstallRequest(digest, Set, Revision, Target, UnityVersion, RootSchemaVersion, 1024 * 1024);
            var result = await new ContentInstaller(new ContentCacheStore(cacheRoot)).InstallAsync(request, source, CancellationToken.None);
            Assert.That(result.ManifestSha256, Is.EqualTo(digest));
            return result;
        }

        private sealed class MemorySource : IContentArtifactSource
        {
            private readonly IReadOnlyDictionary<string, byte[]> _values;
            internal MemorySource(IReadOnlyDictionary<string, byte[]> values) => _values = values;
            public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(new MemoryStream(_values[path], false));
            }
            public void Dispose() { }
        }

        /// <summary>層 1 つの native。登録・解除・load・解放を共通の log に「名前 操作」で書く。</summary>
        internal sealed class LayerNative : IContentNativeDirectory
        {
            private readonly string _name;
            private readonly List<string> _log;

            internal LayerNative(string name, List<string> log) { _name = name; _log = log; }

            internal BuildContentRoot? Root { get; set; }
            internal bool InvalidHandle;
            internal int RootCount = 1;
            internal int FailUnregisterCount;
            internal int LoadCount;
            internal BuildContentEntry? LastEntry;
            internal Func<Texture2D>? TextureFactory;
            internal Action? OnUnregister;

            public bool Register(string path) { _log.Add(_name + " register"); return !InvalidHandle; }
            public BuildContentRoot[] GetRoots() => RootCount == 0 || Root == null ? Array.Empty<BuildContentRoot>() : new[] { Root };
            public void Unregister()
            {
                OnUnregister?.Invoke();
                if (FailUnregisterCount-- > 0) { _log.Add(_name + " unregister-failed"); throw new InvalidOperationException("fake unregister failure: " + _name); }
                _log.Add(_name + " unregister");
            }
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object
            {
                LoadCount++;
                LastEntry = entry;
                _log.Add(_name + " load " + entry.LogicalKey + "/" + entry.Representation);
                var texture = TextureFactory?.Invoke() ?? throw new NotSupportedException("no texture factory");
                return UniTask.FromResult<IBackendAsset>(new FakeAsset(texture));
            }
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options)
            {
                LoadCount++;
                LastEntry = entry;
                _log.Add(_name + " load-scene " + entry.LogicalKey + "/" + entry.Representation);
                return UniTask.FromResult<IBackendScene>(new FakeScene());
            }
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace)
                => throw new NotSupportedException();
            public void Release(IBackendAsset asset) => _log.Add(_name + " release");
            public UniTask UnloadSceneAsync(IBackendScene scene)
            {
                _log.Add(_name + " unload-scene");
                if (scene is FakeScene s) s.Loaded = false;
                return UniTask.CompletedTask;
            }

            private sealed class FakeAsset : IBackendAsset
            {
                internal FakeAsset(UnityEngine.Object value) => Value = value;
                private UnityEngine.Object Value { get; }
                public UnityEngine.Object? Asset => Value;
                public bool IsValid => Value != null;
            }

            private sealed class FakeScene : IBackendScene
            {
                internal bool Loaded = true;
                public bool IsLoaded => Loaded;
                public string Name => "overlay-test-scene";
                public GameObject[] GetRootGameObjects() => Array.Empty<GameObject>();
            }
        }
    }
}
