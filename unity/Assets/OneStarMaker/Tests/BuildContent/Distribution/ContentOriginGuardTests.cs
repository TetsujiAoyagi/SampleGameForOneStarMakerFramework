#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>
    /// 振る舞いの悪い取得元の受け止め（AC1・AC3）と、installer が取消を見る最後の点の前後（AC1）。順は signal で作り、待ちに時間を使わない。
    /// 振る舞いの悪い行は、memory の取得元と、関数を注入した <see cref="IndexedContentOrigin"/> の両方で走らせる（guard を型で外す実装を検出する）。
    /// </summary>
    public sealed class ContentOriginGuardTests
    {
        public enum Misbehaving { Memory, Indexed }

        private const string Payload = "payload-bytes-for-guard"; // 7 byte ずつ読むので、EOF は 4 回目の読み
        private OriginFixture _fixture = null!;
        private string _root = "";

        [SetUp]
        public void SetUp()
        {
            _fixture = new OriginFixture();
            _root = _fixture.OriginRoot(1);
            OriginFixture.Publish(_root, "r1", Payload);
            OriginFixture.WriteHead(_root, "r1");
        }

        [TearDown] public void TearDown() => _fixture.Dispose();

        /// <summary>content の open だけを差し替えた取得元。<paramref name="open"/> が null を返せば directory の bytes を返す。</summary>
        private IContentOrigin Origin(Misbehaving kind, Func<string, CancellationToken, Task<Stream>?> open)
        {
            if (kind == Misbehaving.Memory)
                return new MemoryOrigin("memory", MemoryOrigin.FromDirectory("x", _root).Files()) { OpenOverride = open };
            var root = _root;
            return new IndexedContentOrigin("indexed",
                (relative, token) => Task.FromResult(File.Exists(Path.Combine(root, relative))
                    ? ContentOriginOpen.Opened(File.OpenRead(Path.Combine(root, relative))) : ContentOriginOpen.Missing(relative)),
                location => new OverrideArtifacts(root, location, open));
        }

        private Task<ContentAcquisitionResult> Acquire(IContentOrigin origin, CancellationToken token, ContentAcquisitionHooks? hooks = null) =>
            ContentAcquisition.AcquireAsync(_fixture.Request(), new[] { OriginFixture.Accept(origin) }, _fixture.Store(), token, hooks);

        private static Task<Stream>? Content(string path, Func<ControlledStream, Task<Stream>?> make) =>
            path.EndsWith("/content/payload.bin", StringComparison.Ordinal) ? make(new ControlledStream(Encoding.UTF8.GetBytes(Payload))) : null;

        private void AssertNothingLeft(string[] stagingBefore)
        {
            Assert.That(_fixture.Installed(), Is.Empty);
            Assert.That(_fixture.Staging().Except(stagingBefore), Is.Empty);
            Assert.DoesNotThrow(() => _fixture.Store().Inspect(), "cache の transaction の lock を取り直せる");
        }

        [Test]
        public async Task CancelWhileTheOriginIgnoresCancellationDuringARead_ReturnsCanceledAndNeverInstalls([Values] Misbehaving kind)
        {
            var started = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            var origin = Origin(kind, (path, _) => Content(path, stream =>
            {
                stream.BeforeRead = index => { if (index != 1) return null; started.TrySetResult(true); return release.Task; };
                return Task.FromResult<Stream>(stream);
            }));
            var staging = _fixture.Staging();
            using var cts = new CancellationTokenSource();

            var acquisition = Acquire(origin, cts.Token);
            await started.Task;
            cts.Cancel();
            var result = await acquisition;

            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Canceled));
            AssertNothingLeft(staging);
            release.SetResult(true); // 取消の後に読みが終わっても、install は現れない
            AssertNothingLeft(staging);
        }

        [Test]
        public async Task CancelWhileTheLastEndOfFileIsPending_IsCanceled([Values] Misbehaving kind)
        {
            var started = new TaskCompletionSource<bool>();
            var origin = Origin(kind, (path, _) => Content(path, stream =>
            {
                stream.BeforeRead = index => { if (index != 4) return null; started.TrySetResult(true); return new TaskCompletionSource<bool>().Task; };
                return Task.FromResult<Stream>(stream);
            }));
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            await started.Task;
            cts.Cancel();
            Assert.That((await acquisition).State, Is.EqualTo(ContentAcquisitionState.Canceled));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task CancelAfterTheLastEndOfFileWasHandedToTheInstaller_IsAcquired()
        {
            // guard が最後の EOF を installer へ返す直前の通知の中で取消す。installer は EOF の後に token を見ないので公開まで進む。
            using var cts = new CancellationTokenSource();
            var hooks = new ContentAcquisitionHooks { BeforeEndOfFileReturned = path => { if (path == "content/payload.bin") cts.Cancel(); } };
            var result = await Acquire(ContentOrigins.Directory("dir", _root), cts.Token, hooks);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), "結果と一覧が食い違わない");
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }));
        }

        [Test]
        public async Task OriginThrowsItsOwnOperationCanceled_IsAnOriginFailureNotACancel([Values] Misbehaving kind)
        {
            var origin = Origin(kind, (path, _) => Content(path, _ => throw new OperationCanceledException("origin gave up")));
            var result = await Acquire(origin, CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.NotFound), result.Reason);
            Assert.That(result.Attempts.Last().Outcome, Is.EqualTo("Unreachable"));
        }

        [Test]
        public async Task ReadThrowsMidway_IsUnreachableAndTheNextOriginDelivers([Values] Misbehaving kind)
        {
            var second = _fixture.OriginRoot(2);
            OriginFixture.Publish(second, "r1", Payload);
            var origin = Origin(kind, (path, _) => Content(path, stream => { stream.ThrowAtRead = 2; return Task.FromResult<Stream>(stream); }));
            var result = await ContentAcquisition.AcquireAsync(_fixture.Request(),
                new[] { OriginFixture.Accept(origin), OriginFixture.Accept(ContentOrigins.Directory("dir2", second)) }, _fixture.Store(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.BytesOriginId, Is.EqualTo("dir2"));
            Assert.That(result.Attempts.Select(x => x.Outcome), Is.EqualTo(new[] { "Target", "Unreachable", "Installed" }));
        }

        [Test]
        public async Task StreamReturnedAfterCancel_IsDisposed_AndALateExceptionIsObserved([Values] Misbehaving kind)
        {
            var opened = new TaskCompletionSource<bool>();
            var late = new TaskCompletionSource<Stream>();
            var origin = Origin(kind, (path, _) => Content(path, _ => { opened.TrySetResult(true); return late.Task; }));
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            await opened.Task;
            cts.Cancel();
            Assert.That((await acquisition).State, Is.EqualTo(ContentAcquisitionState.Canceled));
            var stream = new ControlledStream(Encoding.UTF8.GetBytes(Payload));
            late.SetResult(stream);
            Assert.That(stream.Disposed, Is.True);
        }

        [Test]
        public async Task ExceptionAfterCancel_DoesNotEscape([Values] Misbehaving kind)
        {
            var opened = new TaskCompletionSource<bool>();
            var late = new TaskCompletionSource<Stream>();
            var origin = Origin(kind, (path, _) => Content(path, _ => { opened.TrySetResult(true); return late.Task; }));
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            await opened.Task;
            cts.Cancel();
            Assert.That((await acquisition).State, Is.EqualTo(ContentAcquisitionState.Canceled));
            Assert.DoesNotThrow(() => late.SetException(new IOException("late failure")));
        }

        [Test]
        public async Task QueryThatNeverAnswers_IsAbandonedOnCancel()
        {
            var origin = new MemoryOrigin("memory", new Dictionary<string, byte[]>())
            {
                FindOverride = (_, _) => new TaskCompletionSource<ContentOriginAnswer>().Task,
            };
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            cts.Cancel();
            Assert.That((await acquisition).State, Is.EqualTo(ContentAcquisitionState.Canceled));
        }

        [TestCase("throws")]
        [TestCase("other-set")]
        [TestCase("no-digest")]
        public async Task BadQueryAnswers_AreSkippedAsUnanswered(string behavior)
        {
            var origin = new MemoryOrigin("memory", new Dictionary<string, byte[]>())
            {
                FindOverride = (query, _) => behavior switch
                {
                    "throws" => throw new InvalidOperationException("fixture query failure"),
                    "other-set" => Task.FromResult(ContentOriginAnswer.Found(new ContentOriginEntry("other", query.Target, "r1", new string('a', 64), "r1"))),
                    _ => Task.FromResult(ContentOriginAnswer.Found(new ContentOriginEntry(query.ContentSet, query.Target, "r1", null, "r1"))),
                },
            };
            var result = await Acquire(origin, CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.NotFound));
            Assert.That(result.Attempts.Single().Outcome,
                Is.EqualTo(behavior == "throws" ? "Threw" : behavior == "other-set" ? "MismatchedEntry" : "MissingDigest"));
        }

        [Test]
        public async Task StagingAnomalyAfterCorrectBytes_IsTheCacheSide([Values] Misbehaving kind)
        {
            // XR-1: 正しい bytes を渡した後に staging の側へ余分な file を置く。installer の IntegrityMismatch は取得元の証拠にならない。
            var second = _fixture.OriginRoot(2);
            OriginFixture.Publish(second, "r1", Payload);
            var cache = _fixture.CacheRoot;
            var origin = Origin(kind, (path, _) => Content(path, stream =>
            {
                foreach (var staging in Directory.GetDirectories(Path.Combine(cache, "staging")))
                    File.WriteAllText(Path.Combine(staging, "content", "extra.bin"), "x");
                return Task.FromResult<Stream>(stream);
            }));
            var result = await ContentAcquisition.AcquireAsync(_fixture.Request(),
                new[] { OriginFixture.Accept(origin), OriginFixture.Accept(ContentOrigins.Directory("dir2", second)) }, _fixture.Store(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.ArrivalFailure), result.Reason);
            Assert.That(result.Attempts.Select(x => x.Outcome), Is.EqualTo(new[] { "Target", "ArrivalFailure" }));
            Assert.That(_fixture.Installed(), Is.Empty);
        }

        [Test]
        public async Task ManifestOverTheLimit_IsAnIntegrityFailure()
        {
            var large = new byte[4 * 1024 * 1024 + 1];
            var head = JsonUtility.ToJson(new ContentOriginHeadFile
            {
                contentSet = OriginFixture.Set, target = OriginFixture.Target, revision = "r1", manifestSha256 = ContentDeliveryFiles.Sha256(large), path = "r1",
            });
            var origin = new MemoryOrigin("memory", new Dictionary<string, byte[]>
            {
                [ContentOriginHead.RelativePath(OriginFixture.Set, OriginFixture.Target)] = Encoding.UTF8.GetBytes(head),
                ["r1/transport.json"] = large,
            });
            var result = await Acquire(origin, CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.IntegrityFailure), result.Reason);
            Assert.That(result.Attempts.Last().Outcome, Is.EqualTo("TooLarge"));
        }

        // ─── 後片付けの例外（外部実装レビュー P1-1）───

        [Test]
        public async Task ArtifactSourceDisposeThrowsAfterAnInstall_StaysAcquiredAndIsDiagnosed()
        {
            var origin = new MemoryOrigin("memory", MemoryOrigin.FromDirectory("x", _root).Files())
            {
                DisposeThrows = () => new IOException("fixture source dispose failure"),
            };
            var result = await Acquire(origin, CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.BytesOriginId, Is.EqualTo("memory"));
            Assert.That(_fixture.Installed(), Is.EqualTo(new[] { "set/r1" }), "結果と一覧が食い違わない");
            Assert.That(result.Diagnostics, Has.Some.Contain("fixture source dispose failure"));
            Assert.DoesNotThrow(() => _fixture.Store().Inspect(), "cache の transaction の lock を取り直せる");
        }

        [Test]
        public async Task StreamDisposeThrowsWhileAReadFailure_KeepsTheFailureAndTheNextOriginDelivers([Values] Misbehaving kind)
        {
            var second = _fixture.OriginRoot(2);
            OriginFixture.Publish(second, "r1", Payload);
            var origin = Origin(kind, (path, _) => Content(path, stream =>
            {
                stream.ThrowAtRead = 2;
                stream.ThrowOnDispose = new IOException("fixture stream dispose failure");
                return Task.FromResult<Stream>(stream);
            }));
            var result = await ContentAcquisition.AcquireAsync(_fixture.Request(),
                new[] { OriginFixture.Accept(origin), OriginFixture.Accept(ContentOrigins.Directory("dir2", second)) }, _fixture.Store(), CancellationToken.None);
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Acquired), result.Reason);
            Assert.That(result.BytesOriginId, Is.EqualTo("dir2"));
            Assert.That(result.Attempts.Select(x => x.Outcome), Is.EqualTo(new[] { "Target", "Unreachable", "Installed" }), "元の読みの失敗が上書きされない");
            Assert.That(result.Diagnostics, Has.Some.Contain("fixture stream dispose failure"));
        }

        [Test]
        public async Task SourceAndStreamDisposeThrowWhileCanceling_StaysCanceled()
        {
            var started = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            var origin = new MemoryOrigin("memory", MemoryOrigin.FromDirectory("x", _root).Files())
            {
                DisposeThrows = () => new IOException("fixture source dispose failure"),
            };
            origin.OpenOverride = (path, _) => Content(path, stream =>
            {
                stream.BeforeRead = index => { if (index != 1) return null; started.TrySetResult(true); return release.Task; };
                stream.ThrowOnDispose = new IOException("fixture stream dispose failure");
                return Task.FromResult<Stream>(stream);
            });
            var staging = _fixture.Staging();
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            await started.Task;
            cts.Cancel();
            var result = await acquisition;
            Assert.That(result.State, Is.EqualTo(ContentAcquisitionState.Canceled), result.Reason);
            Assert.That(result.Diagnostics, Has.Some.Contain("fixture source dispose failure"));
            AssertNothingLeft(staging);
            // 打ち切った読みが後で終わると、その後で stream を閉じる。その Dispose の例外も外へ出ない。
            Assert.DoesNotThrow(() => release.SetResult(true));
            AssertNothingLeft(staging);
        }

        [Test]
        public async Task LateStreamWhoseDisposeThrows_AfterCancel_DoesNotEscape([Values] Misbehaving kind)
        {
            var opened = new TaskCompletionSource<bool>();
            var late = new TaskCompletionSource<Stream>();
            var origin = Origin(kind, (path, _) => Content(path, _ => { opened.TrySetResult(true); return late.Task; }));
            using var cts = new CancellationTokenSource();
            var acquisition = Acquire(origin, cts.Token);
            await opened.Task;
            cts.Cancel();
            Assert.That((await acquisition).State, Is.EqualTo(ContentAcquisitionState.Canceled));
            var stream = new ControlledStream(Encoding.UTF8.GetBytes(Payload)) { ThrowOnDispose = new IOException("fixture late dispose failure") };
            Assert.DoesNotThrow(() => late.SetResult(stream));
            Assert.That(stream.Disposed, Is.True);
        }

        // ─── guard の単体 ───

        private ContentOriginGuard Guard(IContentOrigin origin)
        {
            var bytes = File.ReadAllBytes(Path.Combine(_root, "r1", "transport.json"));
            var guard = new ContentOriginGuard(origin, new ContentOriginEntry(OriginFixture.Set, OriginFixture.Target, "r1", null, "r1"),
                ContentDeliveryFiles.Sha256(bytes), null);
            var payload = Encoding.UTF8.GetBytes(Payload);
            guard.SetExpectedFiles(new[] { new ContentTransportFile { path = "payload.bin", size = payload.Length, sha256 = ContentDeliveryFiles.Sha256(payload) } });
            return guard;
        }

        [Test]
        public async Task ReadFinishingAfterCancel_DoesNotWriteTheInstallersBuffer()
        {
            var release = new TaskCompletionSource<bool>();
            var origin = Origin(Misbehaving.Memory, (path, _) => Content(path, stream =>
            {
                stream.BeforeRead = index => index == 0 ? release.Task : null;
                return Task.FromResult<Stream>(stream);
            }));
            using var guard = Guard(origin);
            using var cts = new CancellationTokenSource();
            var stream = await guard.OpenReadAsync("content/payload.bin", cts.Token);
            var buffer = new byte[16];
            var read = stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
            cts.Cancel();
            // 同期の待ちで main thread の継続を塞がないよう、await で受ける。
            var canceled = false;
            try { await read; }
            catch (OperationCanceledException) { canceled = true; }
            Assert.That(canceled, Is.True);
            release.SetResult(true);
            Assert.That(buffer.All(x => x == 0), Is.True);
            stream.Dispose();
        }

        [Test]
        public async Task PartialReadThenStop_IsNotAMismatch_ButALongerFileIs()
        {
            using (var guard = Guard(ContentOrigins.Directory("dir", _root)))
            {
                using var stream = await guard.OpenReadAsync("content/payload.bin", CancellationToken.None);
                Assert.That(await stream.ReadAsync(new byte[4], 0, 4, CancellationToken.None), Is.EqualTo(4));
                Assert.That(guard.DeliveredMismatch, Is.False, "途中で読むのを止めた時の部分の hash は数えない");
            }
            File.WriteAllText(Path.Combine(_root, "r1", "content", "payload.bin"), Payload + "!");
            using (var guard = Guard(ContentOrigins.Directory("dir", _root)))
            {
                using var stream = await guard.OpenReadAsync("content/payload.bin", CancellationToken.None);
                var buffer = new byte[64];
                while (await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None) > 0) { }
                Assert.That(guard.DeliveredMismatch, Is.True);
            }
        }

        [Test]
        public async Task OpenFailure_IsReportedAsTransportFailureWithSourceEvidence()
        {
            var origin = Origin(Misbehaving.Memory, (path, _) => Content(path, _ => throw new UnauthorizedAccessException("denied")));
            using var guard = Guard(origin);
            ContentDeliveryException? error = null;
            try { await guard.OpenReadAsync("content/payload.bin", CancellationToken.None); }
            catch (ContentDeliveryException ex) { error = ex; }
            Assert.That(error?.Code, Is.EqualTo(ContentDeliveryFailureCode.TransportFailure));
            Assert.That(guard.SourceFailureObserved, Is.True);
        }

        private sealed class OverrideArtifacts : IContentArtifactSource
        {
            private readonly LocalContentArtifactSource _inner;
            private readonly string _location;
            private readonly Func<string, CancellationToken, Task<Stream>?> _open;

            internal OverrideArtifacts(string root, string location, Func<string, CancellationToken, Task<Stream>?> open)
            {
                _inner = new LocalContentArtifactSource(Path.Combine(root, location));
                _location = location;
                _open = open;
            }

            public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken) =>
                _open(_location + "/" + relativePath, cancellationToken) ?? _inner.OpenReadAsync(relativePath, cancellationToken);

            public void Dispose() => _inner.Dispose();
        }
    }
}
