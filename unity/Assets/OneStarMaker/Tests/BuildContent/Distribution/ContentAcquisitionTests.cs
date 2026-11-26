#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>
    /// 取得の調停（HANDOFF §2.4 の段と止まる規則、AC1・AC2・AC3・AC5・AC8）。表の行は 3 種類の取得元（<see cref="OriginKind"/>）で同じ結果になる。
    /// 一覧の差の検査は、他に cache へ書く者がいない temp の cache で行う。
    /// </summary>
    public sealed class ContentAcquisitionTests
    {
        private OriginFixture _fixture = null!;

        [SetUp] public void SetUp() => _fixture = new OriginFixture();
        [TearDown] public void TearDown() => _fixture.Dispose();

        private Task<ContentAcquisitionResult> Acquire(params IContentOrigin[] origins) =>
            ContentAcquisition.AcquireAsync(_fixture.Request(), origins.Select(OriginFixture.Accept).ToArray(), _fixture.Store(), CancellationToken.None);

        private static string[] Outcomes(ContentAcquisitionResult result) => result.Attempts.Select(x => x.Stage + ":" + x.Outcome).ToArray();

        [Test]
        public async Task NotInstalled_IsAcquiredAndRecorded([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            var digest = OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            var origin = OriginFixture.Create(kind, "o1", root);

            var result = await Acquire(origin);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.Revision, Is.EqualTo("r1"));
            Assert.That(result.ManifestSha256, Is.EqualTo(digest));
            Assert.That(result.TargetOriginId, Is.EqualTo(origin.Id));
            Assert.That(result.BytesOriginId, Is.EqualTo(origin.Id));
            Assert.That(result.DigestAuthority, Is.EqualTo(ContentDigestAuthority.Declared));
            Assert.That(result.Install!.AlreadyInstalled, Is.False);
            Assert.That(Outcomes(result), Is.EqualTo(new[] { "Target:Target", "Fetch:Installed" }));
            Assert.That(result.Attempts.Last().ReadManifest, Is.True);
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }));
            Assert.That(result.RemovedInstalls, Is.Empty);
            // AC5: 取得の install は、今ある検証がそのまま受け付ける形である。
            var validated = _fixture.Store().ValidateInstalled(result.Install.RevisionRoot, digest, OriginFixture.Set, "r1", OriginFixture.Target);
            Assert.That(validated.ManifestSha256, Is.EqualTo(digest));
        }

        [Test]
        public async Task AlreadyInstalled_IsReverifiedWithoutOpeningTheArtifacts([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            Assert.That((await Acquire(OriginFixture.Create(kind, "o1", root))).State, Is.EqualTo(ContentAcquisitionState.Acquired));
            var receipt = File.ReadAllBytes(Path.Combine(_fixture.CacheRoot, "installed", "set", "r1", "receipt.json"));
            // revision の directory を消しても、到着点に在れば bytes の取得口を開かないので取得は通る（E3 と同じ形）。
            Directory.Delete(Path.Combine(root, "r1"), true);
            var counted = new WrappedOrigin("counted", OriginFixture.Create(kind, "o1", root));

            var result = await Acquire(counted);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.AlreadyInstalled), result.Reason);
            Assert.That(result.BytesOriginId, Is.Null);
            Assert.That(result.Install, Is.Not.Null);
            Assert.That(counted.OpenArtifactsCalls, Is.Zero);
            Assert.That(counted.OpenReadCalls, Is.Zero);
            Assert.That(File.ReadAllBytes(Path.Combine(_fixture.CacheRoot, "installed", "set", "r1", "receipt.json")), Is.EqualTo(receipt));
        }

        [Test]
        public async Task FirstOriginCorruptContent_TheSameTargetIsAcquiredFromTheNext([Values] OriginKind kind)
        {
            var first = _fixture.OriginRoot(1);
            var second = _fixture.OriginRoot(2);
            var digest = OriginFixture.Publish(first, "r1");
            OriginFixture.WriteHead(first, "r1");
            OriginFixture.Publish(second, "r1");
            File.WriteAllText(Path.Combine(first, "r1", "content", "payload.bin"), "tampered"); // 同じ大きさでない bytes

            var result = await Acquire(OriginFixture.Create(kind, "o1", first), OriginFixture.Create(kind, "o2", second));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.ManifestSha256, Is.EqualTo(digest));
            Assert.That(result.BytesOriginId, Does.EndWith("o2"));
            Assert.That(Outcomes(result), Is.EqualTo(new[] { "Target:Target", "Fetch:Integrity", "Fetch:Installed" }));
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }));
        }

        [Test]
        public async Task ManifestRewrittenWithAnotherUnityVersion_IsAnIntegrityFailureNotIncompatible([Values] OriginKind kind)
        {
            // 不合格例: head の R1 の transport.json が別の bytes（Unity の版の欄も違う）に書き換わっていて、「互換でない」と読む。
            var first = _fixture.OriginRoot(1);
            var second = _fixture.OriginRoot(2);
            OriginFixture.Publish(first, "r1");
            OriginFixture.WriteHead(first, "r1");
            OriginFixture.Publish(second, "r1");
            File.WriteAllBytes(Path.Combine(first, "r1", "transport.json"), OriginFixture.ManifestBytes("r1", Encoding.UTF8.GetBytes("payload"), "6000.0.0f1"));

            var result = await Acquire(OriginFixture.Create(kind, "o1", first), OriginFixture.Create(kind, "o2", second));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(Outcomes(result), Is.EqualTo(new[] { "Target:Target", "Fetch:DigestMismatch", "Fetch:Installed" }));
        }

        [Test]
        public async Task HeadRevisionUnreachable_DoesNotFallBackToAnOlderRevision([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r0");
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            Directory.Delete(Path.Combine(root, "r1"), true);

            var result = await Acquire(OriginFixture.Create(kind, "o1", root));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.NotFound), result.Reason);
            Assert.That(result.Revision, Is.EqualTo("r1"));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task EveryOriginDeliversWrongBytes_EndsAsIntegrityFailureWithoutInstalling([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1", "payload");
            OriginFixture.WriteHead(root, "r1");
            File.WriteAllText(Path.Combine(root, "r1", "content", "payload.bin"), "PAYLOAD"); // 同じ大きさで hash が違う
            var stagingBefore = _fixture.Staging();

            var result = await Acquire(OriginFixture.Create(kind, "o1", root));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.IntegrityFailure), result.Reason);
            Assert.That(_fixture.Installed(), Is.Empty);
            Assert.That(_fixture.Staging().Except(stagingBefore), Is.Empty);
            Assert.That(_fixture.Store().Inspect().Entries, Is.Empty, "直後に cache の lock を取り直せる");
        }

        [Test]
        public async Task HeadDeclaresAnotherDigestForAnInstalledRevision_IsRefused([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            Assert.That((await Acquire(OriginFixture.Create(kind, "o1", root))).State, Is.EqualTo(ContentAcquisitionState.Acquired));
            OriginFixture.DeclareDigest(root, new string('a', 64));
            var counted = new WrappedOrigin("counted", OriginFixture.Create(kind, "o1", root));

            var result = await Acquire(counted);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.ArrivalDigestConflict), result.Reason);
            Assert.That(counted.OpenArtifactsCalls, Is.Zero);
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }));
        }

        [Test]
        public async Task Pin_AcquiresThePinnedPairAndIgnoresTheHead([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            var r1 = OriginFixture.Publish(root, "r1");
            OriginFixture.Publish(root, "r2", "second");
            OriginFixture.WriteHead(root, "r2");
            OriginFixture.DeclareDigest(root, new string('b', 64)); // 申告の digest も pin と違う
            var request = _fixture.Request(new ContentPin("r1", r1));

            var result = await ContentAcquisition.AcquireAsync(request,
                new[] { new ContentAcquisitionOrigin(OriginFixture.Create(kind, "o1", root), ContentOriginTrust.PinnedOnly) }, _fixture.Store(), CancellationToken.None);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.Revision, Is.EqualTo("r1"));
            Assert.That(result.DigestAuthority, Is.EqualTo(ContentDigestAuthority.Pinned));
            Assert.That(result.TargetOriginId, Is.Null);
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }));
        }

        [Test]
        public async Task PinnedRevisionMissingEverywhere_EndsNotFound([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r2");
            OriginFixture.WriteHead(root, "r2");
            var request = _fixture.Request(new ContentPin("r1", new string('c', 64)));

            var result = await ContentAcquisition.AcquireAsync(request, new[] { OriginFixture.Accept(OriginFixture.Create(kind, "o1", root)) },
                _fixture.Store(), CancellationToken.None);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.NotFound), result.Reason);
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task OnlyPinnedOriginsWithoutAPin_EndNotFoundWithoutAnyQuery()
        {
            var origin = new MemoryOrigin("memory", new System.Collections.Generic.Dictionary<string, byte[]>());
            var result = await ContentAcquisition.AcquireAsync(_fixture.Request(),
                new[] { new ContentAcquisitionOrigin(origin, ContentOriginTrust.PinnedOnly) }, _fixture.Store(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.NotFound));
            Assert.That(origin.FindCalls, Is.Zero);
            Assert.That(Outcomes(result), Is.EqualTo(new[] { "Target:PinRequired" }));
        }

        [Test]
        public async Task CorruptKnownGood_IsTheCacheSideAndOtherOriginsAreNotTried([Values] OriginKind kind)
        {
            // 不合格例: 壊れた known-good.json の InvalidManifest を、全ての取得元で数えて「整合性の失敗」で終わる。
            var first = _fixture.OriginRoot(1);
            var second = _fixture.OriginRoot(2);
            OriginFixture.Publish(first, "r1");
            OriginFixture.WriteHead(first, "r1");
            OriginFixture.Publish(second, "r1");
            Directory.CreateDirectory(Path.Combine(_fixture.CacheRoot, "control"));
            File.WriteAllText(Path.Combine(_fixture.CacheRoot, "control", "known-good.json"), "{ broken");

            var result = await Acquire(OriginFixture.Create(kind, "o1", first), OriginFixture.Create(kind, "o2", second));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.ArrivalFailure), result.Reason);
            Assert.That(Outcomes(result), Is.EqualTo(new[] { "Target:Target", "Fetch:ArrivalFailure" }));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task CapacityRemovedAnOldInstallThenBytesFailed_TheRemovalIsReportedAndNothingIsAdded([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r0", new string('x', 600));
            OriginFixture.WriteHead(root, "r0");
            Assert.That((await Acquire(OriginFixture.Create(kind, "o1", root))).State, Is.EqualTo(ContentAcquisitionState.Acquired));
            OriginFixture.Publish(root, "r1", new string('y', 600));
            OriginFixture.WriteHead(root, "r1");
            File.WriteAllText(Path.Combine(root, "r1", "content", "payload.bin"), new string('z', 600));

            var result = await ContentAcquisition.AcquireAsync(_fixture.Request(budget: 2400),
                new[] { OriginFixture.Accept(OriginFixture.Create(kind, "o1", root)) }, _fixture.Store(), CancellationToken.None);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.IntegrityFailure), result.Reason);
            Assert.That(result.RemovedInstalls, Is.EqualTo(new[] { "set/r0" }));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task CorruptReceiptOfTheTarget_IsTheCacheSide([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            Assert.That((await Acquire(OriginFixture.Create(kind, "o1", root))).State, Is.EqualTo(ContentAcquisitionState.Acquired));
            File.WriteAllText(Path.Combine(_fixture.CacheRoot, "installed", "set", "r1", "receipt.json"), "{ broken");

            var result = await Acquire(OriginFixture.Create(kind, "o1", root));

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.ArrivalFailure), result.Reason);
            StringAssert.Contains("cache side", result.Reason);
        }

        [Test]
        public async Task CacheLockHeld_EndsAsArrivalFailureWithoutTouchingTheCache([Values] OriginKind kind)
        {
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            var store = _fixture.Store();
            ContentAcquisitionResult result;
            using (store.AcquireTransaction())
                result = await ContentAcquisition.AcquireAsync(_fixture.Request(), new[] { OriginFixture.Accept(OriginFixture.Create(kind, "o1", root)) },
                    store, CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.ArrivalFailure));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task AnotherOperationInstalledTheTargetMeanwhile_IsAlreadyInstalledWithNoBytesOrigin()
        {
            // 段 2 の後（取得口を開く時）に、別の操作が同じ revision を同じ digest で入れる。installer は再利用で成功を返す。
            var root = _fixture.OriginRoot(1);
            OriginFixture.Publish(root, "r1");
            OriginFixture.WriteHead(root, "r1");
            // 別の操作の install は別の cache root で先に作り、取得口を開く時に同じ volume の rename で到着点へ入れる
            // （同期の待ちで main thread の継続を塞がない）。
            var otherCache = Path.Combine(_fixture.Root, "other-cache");
            var prepared = await ContentAcquisition.AcquireAsync(_fixture.Request(), new[] { OriginFixture.Accept(MemoryOrigin.FromDirectory("other", root)) },
                new ContentCacheStore(otherCache), CancellationToken.None);
            Assert.That(prepared.State, Is.EqualTo(ContentAcquisitionState.Acquired));
            var origin = MemoryOrigin.FromDirectory("memory", root);
            origin.OnOpenArtifacts = () =>
            {
                origin.OnOpenArtifacts = null;
                Directory.CreateDirectory(Path.Combine(_fixture.CacheRoot, "installed", "set"));
                Directory.Move(Path.Combine(otherCache, "installed", "set", "r1"), Path.Combine(_fixture.CacheRoot, "installed", "set", "r1"));
            };

            var result = await Acquire(origin);

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.AlreadyInstalled), result.Reason);
            Assert.That(result.BytesOriginId, Is.Null);
            Assert.That(result.Attempts.Last().Outcome, Is.EqualTo("AlreadyInstalled"));
            Assert.That(result.Attempts.Last().ReadManifest, Is.True);
        }
    }
}
