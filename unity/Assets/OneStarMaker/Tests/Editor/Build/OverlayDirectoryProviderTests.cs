#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.AssetManagement.Cache;
using OneStarMaker.Runtime.AssetManagement.Internal;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.ContentResolution;
using UnityEngine;
using Fixture = OneStarMaker.Tests.Editor.Build.OverlayRegistrationFixture;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 上書きを重ねた session の要求時解決（供給元 2 つ・route・loader）。session は層 2 つ（base は実際に install して検証）、native は fake、
    /// AssetManagement は実物。層の解決が (key, kind, 実効の表現) の単位で層を選び、record が読んだ層を由来・provider の id・identity で言うこと、
    /// id による影の base の entry を loader でも content 入口でも base から引かないこと、照合が層まで比べること、
    /// close の退避 1 回で両方の層の cache が消えることを見る。
    /// </summary>
    public sealed class OverlayDirectoryProviderTests
    {
        private const string GuidA = "1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a";
        private const string GuidB = "1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b";
        private const string GuidC = "1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c1c";
        private const string GuidD = "1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d1d";
        private const string SceneA = "2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a2a";
        private const string SceneB = "2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b2b";
        private const string SceneC = "2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c2c";

        private sealed class Context
        {
            internal Context(Fixture fx, ContentDirectorySession session, AssetManagement assets, RegisteredDirectoryRoute route)
            { Fx = fx; Session = session; Assets = assets; Route = route; }
            internal Fixture Fx { get; }
            internal ContentDirectorySession Session { get; }
            internal AssetManagement Assets { get; }
            internal RegisteredDirectoryRoute Route { get; }
        }

        private static Task With(IReadOnlyList<BuildContentEntry> baseEntries, IReadOnlyList<BuildContentEntry> overlayEntries,
            Func<Context, Task> body, IAssetResidentCache? cache = null)
            => Fixture.WithAsync(async fx =>
            {
                fx.Arrange(baseEntries, overlayEntries);
                fx.BaseNative.TextureFactory = fx.NewTexture;
                fx.OverlayNative.TextureFactory = fx.NewTexture;
                var session = fx.Register();
                var assets = cache == null ? new AssetManagement() : new AssetManagement(new AddressableBackend(), cache);
                assets.InstallContentDirectory(session);
                Assert.That(RegisteredDirectoryRoute.TryCreate(session, assets, out var route, out var reason), Is.True, reason);
                try { await body(new Context(fx, session, assets, route!)); }
                finally
                {
                    if (assets.IsInstalledContentDirectory(session) && session.IsAccepting) await assets.CloseContentDirectoryAsync();
                }
            });

        private static Task<ContentResolutionResult> Resolve(Context c, ContentRequest request)
            => new ContentResolver().ResolveAsync(request, c.Route.Policy, CancellationToken.None).AsTask();

        private static ContentIdentity BaseIdentity(Fixture fx)
            => new(Fixture.Set, Fixture.Revision, Fixture.Target, fx.Digest);

        private static readonly ContentIdentity OverlayIdentity = new(Fixture.OverlaySet, Fixture.OverlayIdentity, Fixture.Target, "");

        private static string Probes(ContentResolutionResult result)
            => string.Join(",", result.Probes.Select(x => x.ProviderId + ":" + x.Outcome));

        // ─── route ───

        [Test]
        public Task Route_HasOverlayThenBaseProviders_AndProviderKeepsTheBaseMeaning()
            => With(new[] { Fixture.ObjectEntry("Prop", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidB) }, async c =>
        {
            Assert.That(c.Route.Policy.Providers.Select(x => x.Id),
                Is.EqualTo(new[] { RegisteredDirectoryProvider.OverlayId, RegisteredDirectoryProvider.DefaultId }));
            Assert.That(c.Route.Provider.Id, Is.EqualTo(RegisteredDirectoryProvider.DefaultId));
            Assert.That(c.Route.Provider.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
            Assert.That(c.Route.Provider.Identity, Is.EqualTo(BaseIdentity(c.Fx)));
            Assert.That(c.Route.OverlayProvider!.Id, Is.EqualTo(RegisteredDirectoryProvider.OverlayId));
            Assert.That(c.Route.OverlayProvider.SourceKind, Is.EqualTo(ContentSourceKind.ContentOverlay));
            Assert.That(c.Route.OverlayProvider.Identity, Is.EqualTo(OverlayIdentity));
            Assert.That(c.Route.Envelope.ContentSet, Is.EqualTo(Fixture.Set), "基準は base の検証済みの値");
            await Task.CompletedTask;
        });

        // ─── 層の解決と record ───

        [Test]
        public Task OverlayAndBaseEntries_AreLoadedFromTheSelectedLayer_WithMatchingRecords()
            => With(
                new[] { Fixture.ObjectEntry("Prop", "Full", GuidA), Fixture.ObjectEntry("BaseOnly", "Full", GuidB), Fixture.SceneEntry("Stay", "Full", SceneA) },
                new[] { Fixture.ObjectEntry("Prop", "Full", GuidC), Fixture.SceneEntry("Added", "Full", SceneB) },
                async c =>
        {
            var prop = await c.Route.Loader.LoadAssetAsync<Texture2D>(new ContentRequest("Prop", ContentKind.Object, "Full"), AssetOwner.Manual);
            var candidate = prop.Record.Candidate;
            Assert.That(candidate.SourceKind, Is.EqualTo(ContentSourceKind.ContentOverlay));
            Assert.That(candidate.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId));
            Assert.That(candidate.AcceptanceBasis, Is.EqualTo(ContentAcceptanceBasis.RecordedBaseMatch));
            Assert.That(candidate.LoadPath, Is.EqualTo(ContentLoadPath.ContentDirectory));
            Assert.That(candidate.Identity, Is.EqualTo(OverlayIdentity), "上書きの contentSet・build identity・target・空の digest");
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.OverlayBaseContentSet], Is.EqualTo(Fixture.Set));
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.OverlayBaseRevision], Is.EqualTo(Fixture.Revision));
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.OverlayBaseTarget], Is.EqualTo(Fixture.Target));
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.OverlayBaseManifestSha256], Is.EqualTo(c.Fx.Digest));
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.EngineVersion], Is.EqualTo(Fixture.UnityVersion), "上書きの root の記録");
            Assert.That(candidate.Facts[ContentCompatibilityFactKeys.RootSchemaVersion], Is.EqualTo("2"), "上書きの root の schema");
            Assert.That(prop.Record.SourceBindingVerified, Is.True);
            Assert.That(prop.Record.Probes.Select(x => x.Outcome), Is.EqualTo(new[] { ProbeOutcome.Adopted }));
            Assert.That(c.Fx.OverlayNative.LastEntry!.FileGuid, Is.EqualTo(GuidC), "上書きの native で上書きの entry を読む");
            Assert.That(c.Fx.BaseNative.LoadCount, Is.Zero);
            c.Assets.Release(prop.Handle);

            var baseOnly = await c.Route.Loader.LoadAssetAsync<Texture2D>(new ContentRequest("BaseOnly", ContentKind.Object, "Full"), AssetOwner.Manual);
            Assert.That(baseOnly.Record.Candidate.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
            Assert.That(baseOnly.Record.Candidate.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.DefaultId));
            Assert.That(baseOnly.Record.Candidate.AcceptanceBasis, Is.EqualTo(ContentAcceptanceBasis.VerifiedDigest));
            Assert.That(baseOnly.Record.Candidate.Identity, Is.EqualTo(BaseIdentity(c.Fx)), "S3-1 と同じ値");
            Assert.That(baseOnly.Record.SourceBindingVerified, Is.True);
            Assert.That(baseOnly.Record.Probes.Select(x => x.Outcome), Is.EqualTo(new[] { ProbeOutcome.NotFound, ProbeOutcome.Adopted }),
                "上書きの NotFound → base の採用の経緯が残る");
            Assert.That(c.Fx.BaseNative.LastEntry!.FileGuid, Is.EqualTo(GuidB));
            c.Assets.Release(baseOnly.Handle);

            var stay = await c.Route.Loader.LoadSceneAsync(new ContentRequest("Stay", ContentKind.Scene, "Full"));
            Assert.That(stay.Record.Candidate.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.DefaultId));
            await c.Assets.UnloadSceneAsync("Stay");
            c.Assets.ReleaseScene("Stay");

            var added = await c.Route.Loader.LoadSceneAsync(new ContentRequest("Added", ContentKind.Scene, "Full"));
            Assert.That(added.Record.Candidate.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId), "上書きにだけある key");
            Assert.That(added.Record.Candidate.Identity, Is.EqualTo(OverlayIdentity));
            await c.Assets.UnloadSceneAsync("Added");
            c.Assets.ReleaseScene("Added");
        });

        [Test]
        public Task Representation_OverlayHasFullOnly_BaseHasBoth()
            => With(
                new[] { Fixture.SceneEntry("K", "Whitebox", SceneA), Fixture.SceneEntry("K", "Full", SceneB) },
                new[] { Fixture.SceneEntry("K", "Full", SceneC) },
                async c =>
        {
            var whitebox = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Whitebox"));
            Assert.That(whitebox.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.DefaultId), "編集と関係の無い表現は base から");
            Assert.That(whitebox.Candidate.Representation, Is.EqualTo("Whitebox"));
            var full = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Full"));
            Assert.That(full.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId));
        });

        [Test]
        public Task Representation_OverlayHasWhiteboxOnly_BaseHasFull()
            => With(
                new[] { Fixture.SceneEntry("K", "Full", SceneA) },
                new[] { Fixture.SceneEntry("K", "Whitebox", SceneB) },
                async c =>
        {
            var full = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Full"));
            Assert.That(full.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.DefaultId));
            var whitebox = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Whitebox"));
            Assert.That(whitebox.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId));
            Assert.That(whitebox.Candidate.Representation, Is.EqualTo("Whitebox"));
        });

        [Test]
        public Task Fallback_AcrossLayers_TakesTheOverlayFull_WhenNeitherHasTheRequestedRepresentation()
            => With(
                new[] { Fixture.ObjectEntry("K", "Full", GuidA) },
                new[] { Fixture.ObjectEntry("K", "Full", GuidB) },
                async c =>
        {
            var outcome = await c.Route.Loader.LoadAssetAsync<Texture2D>(
                new ContentRequest("K", ContentKind.Object, "Whitebox", fallbackRepresentation: "Full"), AssetOwner.Manual);
            Assert.That(outcome.Record.Candidate.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId), "base の Full が採られれば不合格");
            Assert.That(outcome.Record.Candidate.Representation, Is.EqualTo("Full"));
            Assert.That(c.Fx.OverlayNative.LastEntry!.Representation, Is.EqualTo("Full"), "content 入口に渡す表現");
            Assert.That(c.Fx.BaseNative.LoadCount, Is.Zero);
            c.Assets.Release(outcome.Handle);

            var noFallback = await Resolve(c, new ContentRequest("K", ContentKind.Object, "Whitebox"));
            Assert.That(noFallback.IsResolved, Is.False, "fallback を指定しない要求は採られない");
        });

        [Test]
        public Task Fallback_AcrossLayers_TakesTheBaseFull_WhenTheOverlayHasNoFull()
            => With(
                new[] { Fixture.ObjectEntry("K", "Full", GuidA) },
                new[] { Fixture.ObjectEntry("Other", "Full", GuidB) },
                async c =>
        {
            var result = await Resolve(c, new ContentRequest("K", ContentKind.Object, "Whitebox", fallbackRepresentation: "Full"));
            Assert.That(result.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.DefaultId));
            Assert.That(result.Candidate.Representation, Is.EqualTo("Full"));
        });

        // ─── id による影 ───

        [Test]
        public Task Shadow_Object_BaseEntryWithTheOverlayNativeId_IsNotTakenFromTheBase()
            => With(
                new[] { Fixture.ObjectEntry("K", "Whitebox", GuidA) },
                new[] { Fixture.ObjectEntry("K", "Full", GuidA) },
                async c =>
        {
            var plain = await Resolve(c, new ContentRequest("K", ContentKind.Object, "Whitebox"));
            Assert.That(plain.IsResolved, Is.False);
            Assert.That(Probes(plain), Is.EqualTo(RegisteredDirectoryProvider.OverlayId + ":RepresentationMismatch," + RegisteredDirectoryProvider.DefaultId + ":NotFound"));
            Assert.That(plain.Probes[1].Reason, Does.Contain("shadowed"), "base の NotFound の理由は id による影");

            var pinned = await Resolve(c, new ContentRequest("K", ContentKind.Object, "Whitebox", BaseIdentity(c.Fx)));
            Assert.That(pinned.IsResolved, Is.False, "base の expected identity で pin しても採られない");

            var fallback = await Resolve(c, new ContentRequest("K", ContentKind.Object, "Whitebox", fallbackRepresentation: "Full"));
            Assert.That(fallback.Candidate!.ProviderId, Is.EqualTo(RegisteredDirectoryProvider.OverlayId));
            Assert.That(fallback.Candidate.Representation, Is.EqualTo("Full"));

            var direct = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                await c.Assets.LoadContentAssetAsync<Texture2D>("K", "Whitebox", AssetOwner.Manual));
            Assert.That(direct!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryMissing));
            Assert.That(c.Fx.BaseNative.LoadCount + c.Fx.OverlayNative.LoadCount, Is.Zero, "native の load は 0 回");
        });

        [Test]
        public Task Shadow_Scene_BaseEntryWithTheOverlaySceneId_IsNotTakenFromTheBase()
            => With(
                new[] { Fixture.SceneEntry("K", "Whitebox", SceneA) },
                new[] { Fixture.SceneEntry("K", "Full", SceneA) },
                async c =>
        {
            var plain = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Whitebox"));
            Assert.That(plain.IsResolved, Is.False);
            Assert.That(plain.Probes[1].Reason, Does.Contain("shadowed"));
            var pinned = await Resolve(c, new ContentRequest("K", ContentKind.Scene, "Whitebox", BaseIdentity(c.Fx)));
            Assert.That(pinned.IsResolved, Is.False);

            var direct = Assert.ThrowsAsync<ContentDirectoryException>(async () =>
                await c.Assets.LoadContentSceneAsync("K", "Whitebox"));
            Assert.That(direct!.Code, Is.EqualTo(ContentDirectoryFailureCode.EntryMissing));
            Assert.That(c.Fx.BaseNative.LoadCount + c.Fx.OverlayNative.LoadCount, Is.Zero);
        });

        // ─── 失敗経路 ───

        [Test]
        public Task OverlayEntry_PinnedToTheBaseIdentity_IsNotAdopted()
            => With(new[] { Fixture.ObjectEntry("Prop", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidB) }, async c =>
        {
            var result = await Resolve(c, new ContentRequest("Prop", ContentKind.Object, "Full", BaseIdentity(c.Fx)));
            Assert.That(result.IsResolved, Is.False);
            Assert.That(result.Probes.Select(x => x.Outcome), Is.EqualTo(new[] { ProbeOutcome.PinMismatch, ProbeOutcome.NotFound }));
            Assert.That(result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.IntegrityRejected), "pin が未充足の失敗");
        });

        [Test]
        public Task Binding_ComparesTheLayer()
            => With(new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidB) }, async c =>
        {
            var overlayCandidate = (await c.Route.OverlayProvider!.FindAsync(new ContentRequest("Prop", ContentKind.Object, "Full"), CancellationToken.None)).Candidate!;
            var baseCandidate = (await c.Route.Provider.FindAsync(new ContentRequest("BaseOnly", ContentKind.Object, "Full"), CancellationToken.None)).Candidate!;
            Assert.That(c.Route.OverlayProvider.IsLoadedBy(overlayCandidate, c.Assets), Is.True);
            Assert.That(c.Route.Provider.IsLoadedBy(baseCandidate, c.Assets), Is.True);
            Assert.That(c.Route.Provider.IsLoadedBy(overlayCandidate, c.Assets), Is.False, "上書きの候補を base の照合に渡すと偽");
            Assert.That(c.Route.OverlayProvider.IsLoadedBy(baseCandidate, c.Assets), Is.False, "base の候補を上書きの照合に渡すと偽");
        });

        [Test]
        public Task OverlayCandidate_EachRuleRowBroken_IsRejectedWithItsValue()
            => With(new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidB) }, async c =>
        {
            var request = new ContentRequest("Prop", ContentKind.Object, "Full");
            var candidate = (await c.Route.OverlayProvider!.FindAsync(request, CancellationToken.None)).Candidate!;
            var rule = new ContentCompatibilityRule(c.Route.Envelope);
            Assert.That(rule.Evaluate(request, candidate, out var reason), Is.EqualTo(ContentCompatibilityMismatch.Compatible));
            Assert.That(reason, Is.EqualTo("compat:Compatible:ContentOverlay"));

            void Expect(ContentCompatibilityMismatch expected, Action<Dictionary<string, string>> change, ContentIdentity? identity = null, ContentCompatibilityRule? with = null)
            {
                var facts = new Dictionary<string, string>(candidate.Facts.ToDictionary(x => x.Key, x => x.Value), StringComparer.Ordinal);
                change(facts);
                var broken = new ContentCandidate(candidate.ProviderId, candidate.SourceKind, candidate.LogicalKey, candidate.Kind, candidate.Representation,
                    identity ?? candidate.Identity, null, candidate.AcceptanceBasis, candidate.LoadPath, facts);
                Assert.That((with ?? rule).Evaluate(request, broken, out var why), Is.EqualTo(expected), why);
            }

            foreach (var key in new[] { ContentCompatibilityFactKeys.OverlayBaseContentSet, ContentCompatibilityFactKeys.OverlayBaseRevision,
                         ContentCompatibilityFactKeys.OverlayBaseTarget, ContentCompatibilityFactKeys.OverlayBaseManifestSha256 })
                Expect(ContentCompatibilityMismatch.OverlayBaseFactMissing, f => f.Remove(key));
            Expect(ContentCompatibilityMismatch.ContentSetMismatch, f => f[ContentCompatibilityFactKeys.OverlayBaseContentSet] = "other");
            var fixedRevision = new ContentCompatibilityRule(new ContentCompatibilityEnvelope(c.Route.Envelope.ContentSet, c.Route.Envelope.Target,
                c.Route.Envelope.EngineVersion, c.Route.Envelope.RootSchemaVersion, revision: Fixture.Revision));
            Expect(ContentCompatibilityMismatch.Compatible, _ => { }, with: fixedRevision);
            Expect(ContentCompatibilityMismatch.RevisionMismatch, f => f[ContentCompatibilityFactKeys.OverlayBaseRevision] = "other", with: fixedRevision);
            Expect(ContentCompatibilityMismatch.TargetMismatch, f => f[ContentCompatibilityFactKeys.OverlayBaseTarget] = "Android-Player");
            Expect(ContentCompatibilityMismatch.TargetMismatch, _ => { }, new ContentIdentity(Fixture.OverlaySet, Fixture.OverlayIdentity, "Android-Player", ""));
            Expect(ContentCompatibilityMismatch.EngineVersionMissing, f => f.Remove(ContentCompatibilityFactKeys.EngineVersion));
            Expect(ContentCompatibilityMismatch.EngineVersionMismatch, f => f[ContentCompatibilityFactKeys.EngineVersion] = "6000.6.0f1");
            Expect(ContentCompatibilityMismatch.SchemaVersionMissing, f => f.Remove(ContentCompatibilityFactKeys.RootSchemaVersion));
            Expect(ContentCompatibilityMismatch.SchemaVersionMalformed, f => f[ContentCompatibilityFactKeys.RootSchemaVersion] = "02");
            Expect(ContentCompatibilityMismatch.SchemaVersionMismatch, f => f[ContentCompatibilityFactKeys.RootSchemaVersion] = "1");
            var rejecting = new ContentCompatibilityRule(c.Route.Envelope, new IContentCompatibilityRule[] { new RejectingPredicate() });
            Expect(ContentCompatibilityMismatch.HostPredicateRejected, _ => { }, with: rejecting);
        });

        [Test]
        public Task ProviderWithoutBinding_ClaimingTheOverlaySource_IsRejectedBeforeLoad()
            => With(new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidB) }, async c =>
        {
            var request = new ContentRequest("Prop", ContentKind.Object, "Full");
            var real = (await c.Route.OverlayProvider!.FindAsync(request, CancellationToken.None)).Candidate!;
            var impostor = new UnboundProvider(new ContentCandidate("impostor", ContentSourceKind.ContentOverlay, real.LogicalKey, real.Kind,
                real.Representation, real.Identity, null, ContentAcceptanceBasis.RecordedBaseMatch, ContentLoadPath.ContentDirectory, real.Facts));
            var policy = new ResolutionPolicy(new IContentProvider[] { impostor }, new ContentCompatibilityRule(c.Route.Envelope));
            var loader = new ContentRequestLoader(c.Assets, policy);
            var failure = Assert.ThrowsAsync<ContentResolutionException>(async () => await loader.LoadAssetAsync<Texture2D>(request, AssetOwner.Manual));
            Assert.That(failure!.Rejection, Is.EqualTo(ContentLoadRejection.SourceBindingUnavailable));
            Assert.That(failure.Result.Probes[0].Reason, Is.EqualTo("compat:Compatible:ContentOverlay"), "実物の規則は採ると答えた");
            Assert.That(c.Fx.BaseNative.LoadCount + c.Fx.OverlayNative.LoadCount, Is.Zero, "content 入口を呼ばない");
        });

        // ─── cache ───

        [Test]
        public Task CloseEviction_OnePrefix_ClearsBothLayers()
        {
            var cache = new RecordingCache();
            return With(new[] { Fixture.ObjectEntry("BaseOnly", "Full", GuidA) }, new[] { Fixture.ObjectEntry("Prop", "Full", GuidD) }, async c =>
            {
                var prop = await c.Route.Loader.LoadAssetAsync<Texture2D>(new ContentRequest("Prop", ContentKind.Object, "Full"), AssetOwner.Manual);
                var baseOnly = await c.Route.Loader.LoadAssetAsync<Texture2D>(new ContentRequest("BaseOnly", ContentKind.Object, "Full"), AssetOwner.Manual);
                c.Assets.Release(prop.Handle);
                c.Assets.Release(baseOnly.Handle);
                Assert.That(cache.Stored.Count, Is.EqualTo(2), "両方の層の物が cache に入る");
                Assert.That(cache.Stored.Keys.All(x => x.StartsWith(c.Session.CachePrefix, StringComparison.Ordinal)), Is.True, "session に共通の前置き");
                Assert.That(cache.Stored.Keys.Distinct().Count(), Is.EqualTo(2), "層を区別する key");
                Assert.That(c.Fx.Log.Count(x => x.EndsWith(" release")), Is.Zero, "cache に入った物はまだ解放されない");

                await c.Assets.CloseContentDirectoryAsync();
                Assert.That(cache.Evictions, Is.EqualTo(new[] { c.Session.CachePrefix }), "退避は前置き 1 つで 1 回");
                Assert.That(cache.Stored, Is.Empty);
                Assert.That(c.Fx.Log.Where(x => x.EndsWith(" release") || x.Contains("unregister")),
                    Is.EquivalentTo(new[] { "overlay release", "base release", "overlay unregister", "base unregister" }));
                Assert.That(c.Fx.Log.Where(x => x.Contains("unregister")), Is.EqualTo(new[] { "overlay unregister", "base unregister" }));
            }, cache);
        }

        private sealed class RecordingCache : IAssetResidentCache
        {
            internal readonly Dictionary<string, (IBackendAsset Asset, Action<IBackendAsset>? Release)> Stored = new(StringComparer.Ordinal);
            internal readonly List<string> Evictions = new();
            public bool TryTake(string key, out IBackendAsset asset)
            {
                if (Stored.TryGetValue(key, out var value)) { Stored.Remove(key); asset = value.Asset; return true; }
                asset = null!;
                return false;
            }
            public void Store(string key, AssetType type, IBackendAsset asset, Action<IBackendAsset>? release = null) => Stored[key] = (asset, release);
            public void EvictByPrefix(string prefix)
            {
                Evictions.Add(prefix);
                foreach (var key in Stored.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                {
                    var value = Stored[key];
                    Stored.Remove(key);
                    value.Release?.Invoke(value.Asset);
                }
            }
            public void Clear() => Stored.Clear();
            public CacheStatsSnapshot GetSnapshot() => default;
        }

        private sealed class RejectingPredicate : IContentCompatibilityRule
        {
            public bool Accepts(ContentRequest request, ContentCandidate candidate, out string reason) { reason = "host says no"; return false; }
        }

        /// <summary>照合（IContentSourceBinding）を答えない供給元。</summary>
        private sealed class UnboundProvider : IContentProvider
        {
            private readonly ContentCandidate _candidate;
            internal UnboundProvider(ContentCandidate candidate) => _candidate = candidate;
            public string Id => _candidate.ProviderId;
            public ContentSourceKind SourceKind => _candidate.SourceKind;
            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
                => new(new ContentAvailability(ContentAvailabilityStatus.Available, _candidate));
        }
    }
}
