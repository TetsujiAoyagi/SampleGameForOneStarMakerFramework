#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.ContentResolution;
using UnityEditor;
using Unity.Loading;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 登録した 1 つの directory の供給元（<see cref="RegisteredDirectoryProvider"/>）と、その組み立て（<see cref="RegisteredDirectoryRoute"/>）。
    /// revision は temp の cache に製品の installer で実際に install し、製品の検証を通して登録する。native の directory だけを fake にする。
    /// 候補の identity と事実が、install した manifest と install の要求に渡した digest から来ること、照合が今の登録だけに真を返すこと、
    /// 互換の基準が登録の値と定数から作られ、事実を変えた候補が落ちることを見る。
    /// </summary>
    public sealed class RegisteredDirectoryProviderTests
    {
        private const string Set = "s31set";
        private const string Revision = "s31rev";
        private const string Target = "StandaloneWindows64-Player";
        private const string ManifestUnityVersion = "6000.6.0f1";
        private const int ManifestRootSchemaVersion = 2;
        private const string ScenePath = "Assets/OneStarMaker/Scenes/UISystem/UIScene.unity";
        private const string ObjectPath = "Assets/TutorialInfo/Icons/URP.png";

        private string _cacheRoot = "";
        private string _digest = "";
        private BuildContentRoot? _root;
        private FakeNativeDirectory? _native;
        private ContentDirectorySession? _session;
        private AssetManagement? _assets;

        /// <summary>
        /// 各 test の前に install・検証・登録し、後で close して消す。Unity の同期 context の上で install の await を
        /// 同期で待つと止まりうるので、NUnit の SetUp ではなく test の中で await する。
        /// </summary>
        private async Task WithFixtureAsync(Func<Task> body)
        {
            await ArrangeAsync();
            try { await body(); }
            finally { await CleanupAsync(); }
        }

        private async Task ArrangeAsync()
        {
            _cacheRoot = Path.Combine(Path.GetTempPath(), "osm-s31-provider", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cacheRoot);
            var installed = await InstallAsync(_cacheRoot);
            _digest = installed.ManifestSha256;
            var verified = InstalledRevisionVerifier.Verify(installed.RevisionRoot, _digest, Set, Revision, Target);
            _root = CreateRoot(Revision);
            _native = new FakeNativeDirectory(_root);
            _session = ContentDirectorySession.RegisterVerified(verified, _native);
            _assets = new AssetManagement();
            _assets.InstallContentDirectory(_session);
        }

        private async Task CleanupAsync()
        {
            if (_session != null && _session.IsAccepting)
            {
                if (_assets != null && _assets.IsInstalledContentDirectory(_session)) await _assets.CloseContentDirectoryAsync();
                else await _session.CloseAsync();
            }

            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (Directory.Exists(_cacheRoot)) Directory.Delete(_cacheRoot, true);
            _session = null;
            _assets = null;
        }

        // ─── AC2: 候補の identity・根拠・経路・事実・実効の表現 ───

        [TestCase("Entry", ContentKind.Scene, "Whitebox", "Whitebox")]
        [TestCase("Entry", ContentKind.Scene, "", "Full")]
        [TestCase("Next", ContentKind.Scene, "Whitebox", "Full")]
        [TestCase("Legacy", ContentKind.Scene, "Full", "Full")]
        [TestCase("Legacy", ContentKind.Scene, "Whitebox", "Full")]
        [TestCase("Prop", ContentKind.Object, "", "Full")]
        [TestCase("Prop", ContentKind.Object, "Whitebox", "Full")]
        public Task Available_CarriesInstalledIdentity_ManifestFacts_AndEffectiveRepresentation(
            string key, ContentKind kind, string requested, string expectedRepresentation)
            => WithFixtureAsync(async () =>
        {
            var provider = new RegisteredDirectoryProvider(_session!);

            var answer = await provider.FindAsync(new ContentRequest(key, kind, requested), CancellationToken.None);

            Assert.That(answer.Status, Is.EqualTo(ContentAvailabilityStatus.Available));
            var candidate = answer.Candidate!;
            Assert.That(candidate.Representation, Is.EqualTo(expectedRepresentation), "索引が返した entry の実効の表現");
            Assert.That(candidate.Identity.ContentSet, Is.EqualTo(Set));
            Assert.That(candidate.Identity.Revision, Is.EqualTo(Revision));
            Assert.That(candidate.Identity.Target, Is.EqualTo(Target));
            Assert.That(candidate.Identity.Digest, Is.EqualTo(_digest), "install の要求に渡した digest（ContentDeliveryFiles.Sha256 の形式）");
            Assert.That(candidate.AcceptanceBasis, Is.EqualTo(ContentAcceptanceBasis.VerifiedDigest));
            Assert.That(candidate.LoadPath, Is.EqualTo(ContentLoadPath.ContentDirectory));
            Assert.That(candidate.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
            Assert.That(candidate.ProviderId, Is.EqualTo(provider.Id));
            Assert.That(candidate.LogicalKey, Is.EqualTo(key));
            Assert.That(candidate.Kind, Is.EqualTo(kind));
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.EngineVersion], Is.EqualTo(ManifestUnityVersion), "manifest の unityVersion");
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.RootSchemaVersion], Is.EqualTo(ManifestRootSchemaVersion.ToString()), "manifest の rootSchemaVersion");
            Assert.That(_native!.LoadCount, Is.Zero, "供給元は native の load をしない");
        });

        [TestCase("Missing", ContentKind.Scene)]
        [TestCase("Prop", ContentKind.Scene)]
        [TestCase("Entry", ContentKind.Object)]
        public Task MissingKeyOrKind_IsNotFound(string key, ContentKind kind)
            => WithFixtureAsync(async () =>
        {
            var answer = await new RegisteredDirectoryProvider(_session!).FindAsync(new ContentRequest(key, kind, "Full"), CancellationToken.None);

            Assert.That(answer.Status, Is.EqualTo(ContentAvailabilityStatus.NotFound));
            Assert.That(answer.Candidate, Is.Null);
        });

        // ─── AC2: 照合 ───

        [Test]
        public Task Binding_IsTrueOnlyWhileThisSessionIsInstalledAndAccepting()
            => WithFixtureAsync(async () =>
        {
            var provider = new RegisteredDirectoryProvider(_session!);
            var candidate = (await provider.FindAsync(new ContentRequest("Entry", ContentKind.Scene, "Full"), CancellationToken.None)).Candidate!;

            Assert.That(provider.IsLoadedBy(candidate, _assets!), Is.True);

            await _assets!.CloseContentDirectoryAsync();

            var afterClose = await provider.FindAsync(new ContentRequest("Entry", ContentKind.Scene, "Full"), CancellationToken.None);
            Assert.That(afterClose.Status, Is.EqualTo(ContentAvailabilityStatus.Unavailable));
            Assert.That(provider.IsLoadedBy(candidate, _assets), Is.False);
        });

        [Test]
        public Task Binding_IsFalseForOtherAssetManagements_AndWrappers()
            => WithFixtureAsync(async () =>
        {
            var provider = new RegisteredDirectoryProvider(_session!);
            var candidate = (await provider.FindAsync(new ContentRequest("Entry", ContentKind.Scene, "Full"), CancellationToken.None)).Candidate!;

            var empty = new AssetManagement();
            Assert.That(provider.IsLoadedBy(candidate, empty), Is.False, "何も登録していない AssetManagement");

            var other = new AssetManagement();
            other.InstallContentDirectory(new OtherPort());
            Assert.That(provider.IsLoadedBy(candidate, other), Is.False, "別の directory を登録している AssetManagement");

            Assert.That(provider.IsLoadedBy(candidate, new WrappedAssets(_assets!)), Is.False, "AssetManagement を包んだ別の IAssetManagement");
        });

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public Task Binding_IsFalseWhenOneIdentityFieldDiffers(int field)
            => WithFixtureAsync(async () =>
        {
            var provider = new RegisteredDirectoryProvider(_session!);
            var original = (await provider.FindAsync(new ContentRequest("Entry", ContentKind.Scene, "Full"), CancellationToken.None)).Candidate!;
            var id = original.Identity;
            var changed = field switch
            {
                0 => new ContentIdentity(id.ContentSet + "x", id.Revision, id.Target, id.Digest),
                1 => new ContentIdentity(id.ContentSet, id.Revision + "x", id.Target, id.Digest),
                2 => new ContentIdentity(id.ContentSet, id.Revision, id.Target + "x", id.Digest),
                _ => new ContentIdentity(id.ContentSet, id.Revision, id.Target, new string('0', 64)),
            };

            Assert.That(provider.IsLoadedBy(WithIdentity(original, changed), _assets!), Is.False);
        });

        // ─── AC2: 検証済みでない登録と、close を妨げないこと ───

        [Test]
        public async Task UnverifiedRegistration_IsNotMadeIntoAProvider()
        {
            var path = Path.Combine(Path.GetTempPath(), "osm-s31-unverified-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            var root = CreateRoot("plain");
            var native = new FakeNativeDirectory(root);
            try
            {
                var session = ContentDirectorySession.Register(path, "plain", Target, native);
                Assert.That(session.VerifiedRevision, Is.Null, "path の登録は検証済みの値を持たない");
                var assets = new AssetManagement();
                assets.InstallContentDirectory(session);

                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.False, "作らない");
                Assert.That(route, Is.Null);
                Assert.That(reason, Is.Not.Empty, "作らない理由を返す");
                Assert.Throws<ArgumentException>(() => new RegisteredDirectoryProvider(session));

                await assets.CloseContentDirectoryAsync();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                Directory.Delete(path, true);
            }
        }

        [Test]
        public Task Provider_DoesNotHoldTheSessionOpen()
            => WithFixtureAsync(async () =>
        {
            var provider = new RegisteredDirectoryProvider(_session!);
            await provider.FindAsync(new ContentRequest("Entry", ContentKind.Scene, "Whitebox"), CancellationToken.None);
            await provider.FindAsync(new ContentRequest("Prop", ContentKind.Object, "Full"), CancellationToken.None);

            await _assets!.CloseContentDirectoryAsync();

            Assert.That(_native!.UnregisterCount, Is.EqualTo(1), "供給元が生きていても close は完了する");
            Assert.That(_session!.IsAccepting, Is.False);
            GC.KeepAlive(provider);
        });

        // ─── AC4: 互換の基準 ───

        [Test]
        public Task Route_EnvelopeComesFromTheVerifiedSessionAndConstants_AndAcceptsTheProviderCandidate()
            => WithFixtureAsync(async () =>
        {
            Assert.That(RegisteredDirectoryRoute.TryCreate(_session!, _assets!, out var route, out var reason), Is.True, reason);

            Assert.That(route!.Envelope.ContentSet, Is.EqualTo(Set));
            Assert.That(route.Envelope.Target, Is.EqualTo(Target));
            Assert.That(route.Envelope.EngineVersion, Is.EqualTo(ContentManifestValidation.SupportedUnityVersion));
            Assert.That(route.Envelope.RootSchemaVersion, Is.EqualTo(ContentManifestValidation.SupportedRootSchemaVersion));
            Assert.That(route.Envelope.Revision, Is.Null, "revision は固定しない");
            Assert.That(route.Policy.Providers, Is.EqualTo(new IContentProvider[] { route.Provider }));
            Assert.That(route.FallbackRepresentation, Is.EqualTo("Full"));

            var request = new ContentRequest("Entry", ContentKind.Scene, "Whitebox");
            var candidate = (await route.Provider.FindAsync(request, CancellationToken.None)).Candidate!;
            var rule = new ContentCompatibilityRule(route.Envelope);
            Assert.That(rule.Evaluate(request, candidate, out var why), Is.EqualTo(ContentCompatibilityMismatch.Compatible), why);
        });

        [TestCase("engine")]
        [TestCase("schema")]
        [TestCase("contentSet")]
        public Task Route_RejectsCandidatesWhoseFactsOrSetDiffer(string change)
            => WithFixtureAsync(async () =>
        {
            Assert.That(RegisteredDirectoryRoute.TryCreate(_session!, _assets!, out var route, out _), Is.True);
            var request = new ContentRequest("Entry", ContentKind.Scene, "Full");
            var original = (await route!.Provider.FindAsync(request, CancellationToken.None)).Candidate!;
            var facts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ContentCompatibilityFactKeys.EngineVersion] = original.Facts[ContentCompatibilityFactKeys.EngineVersion],
                [ContentCompatibilityFactKeys.RootSchemaVersion] = original.Facts[ContentCompatibilityFactKeys.RootSchemaVersion],
            };
            var identity = original.Identity;
            switch (change)
            {
                case "engine":
                    // 1 文字だけ変える。
                    var engine = facts[ContentCompatibilityFactKeys.EngineVersion];
                    facts[ContentCompatibilityFactKeys.EngineVersion] = engine.Substring(0, engine.Length - 1) + (engine[engine.Length - 1] == '1' ? '2' : '1');
                    break;
                case "schema":
                    facts[ContentCompatibilityFactKeys.RootSchemaVersion] = (ContentManifestValidation.SupportedRootSchemaVersion + 1).ToString();
                    break;
                default:
                    identity = new ContentIdentity("other-set", identity.Revision, identity.Target, identity.Digest);
                    break;
            }

            var altered = new ContentCandidate(original.ProviderId, original.SourceKind, original.LogicalKey, original.Kind,
                original.Representation, identity, null, original.AcceptanceBasis, original.LoadPath, facts);
            var policy = new ResolutionPolicy(new IContentProvider[] { new FixedProvider(original.ProviderId, altered) }, route.Policy.Rule);

            var result = await new ContentResolver().ResolveAsync(request, policy, CancellationToken.None);

            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.RuleRejected));
        });

        [Test]
        public Task Route_FailuresForAVerifiedSession_AreExceptions()
            => WithFixtureAsync(async () =>
        {
            // 検証済みの session を、その session を登録していない AssetManagement と組む。
            var other = new AssetManagement();
            Assert.Throws<InvalidOperationException>(() => RegisteredDirectoryRoute.TryCreate(_session!, other, out _, out _));

            // 受付を止めた session。
            await _assets!.CloseContentDirectoryAsync();
            Assert.Throws<InvalidOperationException>(() => RegisteredDirectoryRoute.TryCreate(_session!, _assets, out _, out _));
        });

        // ─── fixture ───

        private static async Task<ContentInstallResult> InstallAsync(string cacheRoot)
        {
            var payload = Encoding.UTF8.GetBytes("s31-payload");
            var manifest = new ContentTransportManifest
            {
                version = 1,
                product = "OneStarMaker",
                contentSet = Set,
                revision = Revision,
                target = Target,
                unityVersion = ManifestUnityVersion,
                rootSchemaVersion = ManifestRootSchemaVersion,
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
            var request = new ContentInstallRequest(digest, Set, Revision, Target, ManifestUnityVersion, ManifestRootSchemaVersion, 1024 * 1024);
            var result = await new ContentInstaller(new ContentCacheStore(cacheRoot)).InstallAsync(request, source, CancellationToken.None);
            Assert.That(result.ManifestSha256, Is.EqualTo(digest));
            return result;
        }

        private static BuildContentRoot CreateRoot(string identity)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(ObjectPath);
            Assert.That(asset, Is.Not.Null, ObjectPath);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var scene = LoadableSceneIdEditorUtility.CreateLoadableSceneId(ScenePath);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(identity, Target, new[]
            {
                new BuildContentEntry("Entry", "entry-full", "Full", scene),
                new BuildContentEntry("Entry", "entry-whitebox", "Whitebox", scene),
                new BuildContentEntry("Next", "next-full", "Full", scene),
                // 旧 build の未タグ entry。Full と読む。
                new BuildContentEntry("Legacy", "legacy-untagged", "", scene),
                new BuildContentEntry("Prop", "prop-full", "Full",
                    new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId),
            });
            return root;
        }

        private static ContentCandidate WithIdentity(ContentCandidate candidate, ContentIdentity identity)
            => new(candidate.ProviderId, candidate.SourceKind, candidate.LogicalKey, candidate.Kind, candidate.Representation,
                identity, null, candidate.AcceptanceBasis, candidate.LoadPath, candidate.Facts);

        private sealed class FixedProvider : IContentProvider
        {
            private readonly ContentCandidate _candidate;

            internal FixedProvider(string id, ContentCandidate candidate)
            {
                Id = id;
                _candidate = candidate;
            }

            public string Id { get; }

            public ContentSourceKind SourceKind => ContentSourceKind.ContentDirectory;

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
                => new(new ContentAvailability(ContentAvailabilityStatus.Available, _candidate));
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

        /// <summary>native の登録だけを模す。load は数えるだけで、この file の test は呼ばない。</summary>
        private sealed class FakeNativeDirectory : IContentNativeDirectory
        {
            private readonly BuildContentRoot _root;
            internal FakeNativeDirectory(BuildContentRoot root) => _root = root;
            internal int LoadCount;
            internal int UnregisterCount;
            public bool Register(string path) => true;
            public BuildContentRoot[] GetRoots() => new[] { _root };
            public void Unregister() => UnregisterCount++;
            public UniTask<IBackendAsset> LoadObjectAsync<T>(BuildContentEntry entry) where T : UnityEngine.Object
            { LoadCount++; throw new NotSupportedException(); }
            public UniTask<IBackendScene> LoadSceneAsync(BuildContentEntry entry, SceneLoadOptions options)
            { LoadCount++; throw new NotSupportedException(); }
            public UniTask<IBackendInstance> InstantiateAsync(BuildContentEntry entry, Transform? parent, bool worldSpace)
            { LoadCount++; throw new NotSupportedException(); }
            public void Release(IBackendAsset asset) { }
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
        }

        /// <summary>この session とは別の directory の port。</summary>
        private sealed class OtherPort : IContentDirectoryBackend
        {
            public string BuildIdentity => "other";
            public string Target => RegisteredDirectoryProviderTests.Target;
            public string CachePrefix => "directory:s31-other:";
            public AssetKey GetObjectKey(string logicalKey, string representation) => throw new NotSupportedException();
            public UniTask<IBackendAsset> LoadAssetAsync<T>(string logicalKey, string representation, CancellationToken ct) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IBackendScene> LoadSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options, CancellationToken ct) => throw new NotSupportedException();
            public UniTask<IBackendInstance> InstantiateAsync(string logicalKey, string representation, Transform? parent, bool worldSpace, CancellationToken ct) => throw new NotSupportedException();
            public UniTask UnloadSceneAsync(IBackendScene scene) => UniTask.CompletedTask;
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

        /// <summary>実物の AssetManagement に全てを委ねる wrapper。照合はこれを実物と認めない。</summary>
        private sealed class WrappedAssets : IAssetManagement
        {
            private readonly IAssetManagement _inner;
            internal WrappedAssets(IAssetManagement inner) => _inner = inner;
            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(AssetKey key, AssetOwner owner, CancellationToken ct = default) where T : UnityEngine.Object
                => _inner.LoadAssetAsync<T>(key, owner, ct);
            public UniTask<IAssetHandle<T>> LoadContentAssetAsync<T>(string logicalKey, string representation, AssetOwner owner, CancellationToken ct = default) where T : UnityEngine.Object
                => _inner.LoadContentAssetAsync<T>(logicalKey, representation, owner, ct);
            public UniTask<ISceneHandle> LoadContentSceneAsync(string sceneIdentity, string representation, SceneLoadOptions options = default, CancellationToken ct = default)
                => _inner.LoadContentSceneAsync(sceneIdentity, representation, options, ct);
            public UniTask<GameObject> InstantiateContentAsync(string logicalKey, string representation, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default)
                => _inner.InstantiateContentAsync(logicalKey, representation, parent, worldSpace, ct);
            public IAssetHandle<T> LoadAppAssetSync<T>(AssetKey key) where T : UnityEngine.Object => _inner.LoadAppAssetSync<T>(key);
            public UniTask<ISceneHandle> LoadSceneAsync(string sceneIdentity, SceneAssetDescription desc, string variant = "", SceneLoadOptions options = default, CancellationToken ct = default)
                => _inner.LoadSceneAsync(sceneIdentity, desc, variant, options, ct);
            public UniTask UnloadSceneAsync(string sceneIdentity, CancellationToken ct = default) => _inner.UnloadSceneAsync(sceneIdentity, ct);
            public UniTask<GameObject> InstantiateAsync(AssetKey key, Transform? parent = null, bool worldSpace = false, CancellationToken ct = default)
                => _inner.InstantiateAsync(key, parent, worldSpace, ct);
            public void Release(IAssetHandle handle) => _inner.Release(handle);
            public void ReleaseScene(string sceneIdentity) => _inner.ReleaseScene(sceneIdentity);
            public void ReleaseAll() => _inner.ReleaseAll();
        }
    }
}
