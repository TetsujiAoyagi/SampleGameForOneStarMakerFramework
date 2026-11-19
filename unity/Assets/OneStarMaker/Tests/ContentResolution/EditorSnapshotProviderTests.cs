#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.ContentResolution;

namespace OneStarMaker.Tests.ContentResolution
{
    /// <summary>
    /// Editor の Play の複製の供給元を、実物の resolver と互換の規則、fake の directory の供給元と組んで確かめる（AC1）。
    /// 宣言にある identity の要求は、どの状態でも次の供給元（directory）へ進ませない。
    /// </summary>
    [TestFixture]
    public sealed class EditorSnapshotProviderTests
    {
        private const string Full = "Full";
        private const string TitleCopy = "Assets/OsmPlaySnapshot/1_2_ab/Title.unity";
        private static readonly ContentIdentity DirectoryIdentity = new("set", "r1", "StandaloneWindows64-Player", "digest");

        private readonly ContentResolver _resolver = new();

        [Test]
        public void DeclaredScene_IsAdoptedFromTheSnapshot_AndTheDirectoryIsNotProbed()
        {
            var directory = new FakeDirectoryProvider();
            var result = Resolve(new ContentRequest("Title", ContentKind.Scene, "spring"), directory);

            Assert.That(result.IsResolved, Is.True);
            var candidate = result.Candidate!;
            Assert.That(candidate.ProviderId, Is.EqualTo(EditorSnapshotProvider.DefaultId));
            Assert.That(candidate.SourceKind, Is.EqualTo(ContentSourceKind.EditorSnapshot));
            Assert.That(candidate.AcceptanceBasis, Is.EqualTo(ContentAcceptanceBasis.OpenSceneSnapshot));
            Assert.That(candidate.LoadPath, Is.EqualTo(ContentLoadPath.AlreadyLoadedScene));
            Assert.That(candidate.Representation, Is.EqualTo("spring"), "要求の表現をそのまま申告する");
            Assert.That(candidate.Identity.IsEmpty, Is.True, "複製は検証した物ではないので identity を持たない");
            Assert.That(candidate.Facts[EditorSnapshotProvider.SourcePathFact], Is.EqualTo("Assets/SampleGame/Scenes/Title.unity"));
            Assert.That(candidate.Facts[EditorSnapshotProvider.CopyPathFact], Is.EqualTo(TitleCopy));
            Assert.That(candidate.Facts[EditorSnapshotProvider.WasDirtyFact], Is.EqualTo("true"));
            Assert.That(candidate.Facts[EditorSnapshotProvider.Sha256Fact], Is.EqualTo("abc123"));
            Assert.That(directory.Calls, Is.Zero, "先頭の供給元が採られ、directory は probe されない");
        }

        [Test]
        public void UnspecifiedRepresentation_IsAnsweredWithFull_AndAdopted()
        {
            var directory = new FakeDirectoryProvider();
            var result = Resolve(new ContentRequest("Title", ContentKind.Scene, string.Empty), directory);

            Assert.That(result.IsResolved, Is.True, "空の表現の候補は作れないので、指定の無い要求には Full を申告する");
            Assert.That(result.Candidate!.Representation, Is.EqualTo(Full));
            Assert.That(result.Candidate.ProviderId, Is.EqualTo(EditorSnapshotProvider.DefaultId));
            Assert.That(directory.Calls, Is.Zero);
        }

        [Test]
        public void UndeclaredScene_IsNotFound_AndTheDirectoryCandidateIsAdopted()
        {
            var directory = new FakeDirectoryProvider();
            var result = Resolve(new ContentRequest("Field", ContentKind.Scene, Full), directory);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Candidate!.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory));
            Assert.That(result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.NotFound));
            Assert.That(directory.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ObjectRequest_IsNotFound_EvenWithADeclaredName()
        {
            var directory = new FakeDirectoryProvider();
            var result = Resolve(new ContentRequest("Title", ContentKind.Object, Full), directory);

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Candidate!.SourceKind, Is.EqualTo(ContentSourceKind.ContentDirectory), "Object の snapshot は扱わない");
            Assert.That(result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.NotFound));
        }

        [Test]
        public void PinnedRequestForADeclaredScene_StopsTheSearch_EvenWhenTheDirectoryMatchesThePin()
        {
            var directory = new FakeDirectoryProvider();
            var result = Resolve(new ContentRequest("Title", ContentKind.Scene, Full, DirectoryIdentity), directory);

            Assert.That(result.IsResolved, Is.False, "pin に合う directory の版を採らない");
            Assert.That(result.FailureCode, Is.EqualTo(ContentResolutionFailureCode.IntegrityRejected));
            Assert.That(result.Probes.Count, Is.EqualTo(1));
            Assert.That(result.Probes[0].Outcome, Is.EqualTo(ProbeOutcome.Rejected));
            Assert.That(result.Probes[0].Reason, Does.Contain("Title"));
            Assert.That(directory.Calls, Is.Zero);
        }

        [Test]
        public void DeclaredScene_IsNeverUnavailableOrNotFound()
        {
            var provider = Provider();
            foreach (var representation in new[] { string.Empty, Full, "spring", "other" })
            {
                var answer = provider.FindAsync(new ContentRequest("Title", ContentKind.Scene, representation), CancellationToken.None).Result;
                Assert.That(answer.Status, Is.EqualTo(ContentAvailabilityStatus.Available), "representation=" + representation);
            }

            var pinned = provider.FindAsync(new ContentRequest("Title", ContentKind.Scene, Full, DirectoryIdentity), CancellationToken.None).Result;
            Assert.That(pinned.Status, Is.EqualTo(ContentAvailabilityStatus.Rejected));
            Assert.That(pinned.ExpectedIdentity.HasValue, Is.False, "identity の無い Rejected で探索を止める");
        }

        [Test]
        public void Declares_AnswersOnlyForDeclaredIdentities()
        {
            var provider = Provider();
            Assert.That(provider.Declares("Title"), Is.True);
            Assert.That(provider.Declares("Field"), Is.False);
            Assert.That(provider.Declares(string.Empty), Is.False);
        }

        private ContentResolutionResult Resolve(ContentRequest request, FakeDirectoryProvider directory)
        {
            var envelope = new ContentCompatibilityEnvelope("set", DirectoryIdentity.Target, "6000.6.9f1", 1);
            var policy = new ResolutionPolicy(new IContentProvider[] { Provider(), directory }, new ContentCompatibilityRule(envelope));
            return _resolver.ResolveAsync(request, policy, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        private static EditorSnapshotProvider Provider()
        {
            var declaration = new[]
            {
                new EditorSnapshotDeclaredScene("Title", TitleCopy, "Assets/SampleGame/Scenes/Title.unity", wasDirty: true, sha256: "abc123"),
            };
            return new EditorSnapshotProvider(new EditorSnapshotSceneSource(declaration, new NoSceneIo()), Full);
        }

        /// <summary>候補を探すだけの test なので Unity の Scene を扱わない I/O。</summary>
        private sealed class NoSceneIo : IEditorSnapshotSceneIo
        {
            public IReadOnlyList<EditorSnapshotLoadedScene> CaptureLoadedScenes() => Array.Empty<EditorSnapshotLoadedScene>();
            public Runtime.AssetManagement.Internal.IBackendScene? FindLoaded(string copyPath) => null;
            public Cysharp.Threading.Tasks.UniTask<bool> LoadAsync(string copyPath) => throw new NotSupportedException();
            public Cysharp.Threading.Tasks.UniTask<bool> UnloadAsync(Runtime.AssetManagement.Internal.IBackendScene scene) => throw new NotSupportedException();
        }

        /// <summary>規則を通る directory の候補を、どの要求にも返す fake。probe の回数を数える。</summary>
        private sealed class FakeDirectoryProvider : IContentProvider
        {
            internal int Calls { get; private set; }

            public string Id => "fake-directory";

            public ContentSourceKind SourceKind => ContentSourceKind.ContentDirectory;

            public ValueTask<ContentAvailability> FindAsync(ContentRequest request, CancellationToken cancellationToken)
            {
                Calls++;
                var candidate = new ContentCandidate(Id, SourceKind, request.LogicalKey, request.Kind,
                    request.Representation.Length == 0 ? Full : request.Representation, DirectoryIdentity, null,
                    ContentAcceptanceBasis.VerifiedDigest, ContentLoadPath.ContentDirectory,
                    new Dictionary<string, string>
                    {
                        [ContentCompatibilityFactKeys.EngineVersion] = "6000.6.9f1",
                        [ContentCompatibilityFactKeys.RootSchemaVersion] = "1",
                    });
                return new ValueTask<ContentAvailability>(new ContentAvailability(ContentAvailabilityStatus.Available, candidate));
            }
        }
    }
}
