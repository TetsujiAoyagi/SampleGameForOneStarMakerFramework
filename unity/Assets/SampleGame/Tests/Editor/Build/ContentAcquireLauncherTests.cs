#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 取得の操作（AC6・S7）。Editor の状態・設定・時計・記録の保存・取得の実行の 5 つの口を差し替える。取得の本体は製品の調停を
    /// temp の取得元と cache で走らせ、Play が使う revision は入口と同じ選択で確かめる。
    /// </summary>
    public sealed class ContentAcquireLauncherTests
    {
        private string _root = "";
        private string _origin = "";
        private string _cache = "";
        private string _record = "";

        [SetUp]
        public void SetUp()
        {
            Assert.That(ContentAcquireLauncher.IsRunning, Is.False, "前の test の取得が残っている");
            _root = Path.Combine(Path.GetTempPath(), "osm-acq", Guid.NewGuid().ToString("N").Substring(0, 8));
            _origin = Path.Combine(_root, "transport");
            _cache = Path.Combine(_root, "cache");
            _record = Path.Combine(_root, "selection.json");
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            // 各 test は自分の取得の終わりを await する。失敗で残った取得は取り消すだけ（同期の待ちで main thread を塞がない）。
            ContentAcquireLauncher.Cancel();
            if (Directory.Exists(_root) && !ContentAcquireLauncher.IsRunning) Directory.Delete(_root, true);
        }

        private ContentAcquirePorts Ports(string? busy = null, Action<ContentBaseSelectionEntry>? save = null,
            Func<ContentAcquisitionRequest, IReadOnlyList<ContentAcquisitionOrigin>, ContentCacheStore, CancellationToken, Task<ContentAcquisitionResult>>? acquire = null) =>
            new(() => busy,
                () => new ContentAcquireSettings(_cache, 1024 * 1024, "spring-full", ContentOriginSettings.Origins(_origin), _record, Array.Empty<string>()),
                () => DateTime.UtcNow,
                save ?? (entry => ContentBaseSelection.Write(_record, entry)),
                acquire ?? ContentAcquisition.AcquireAsync);

        private string Publish(string revision, string declaration = "spring-full")
        {
            var published = Path.Combine(_origin, revision);
            Directory.CreateDirectory(Path.Combine(published, "content"));
            var payload = Encoding.UTF8.GetBytes("acquire payload " + revision);
            File.WriteAllBytes(Path.Combine(published, "content", "payload.bin"), payload);
            using var sha = SHA256.Create();
            var manifest = new ContentTransportManifest
            {
                version = 1, product = "OneStarMaker", contentSet = "spring-full", revision = revision, target = VerifyPlayLauncher.Target,
                unityVersion = Application.unityVersion, rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                files = new[] { new ContentTransportFile { path = "payload.bin", size = payload.Length, sha256 = Hex(sha.ComputeHash(payload)) } },
                declaration = new ContentManifestDeclaration
                {
                    name = declaration, representation = "Full",
                    runtimeDefaults = new[] { new ContentManifestDeclarationValue { key = "world:cellCompanionSet", value = "Full" } },
                },
            };
            File.WriteAllText(Path.Combine(published, "transport.json"), JsonUtility.ToJson(manifest));
            return ContentOriginHeadWriter.Write(published).Head.manifestSha256;
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private static ContentAcquisitionResult Result(ContentAcquisitionState state) =>
            new(state, "fixture", null, null, null, null, null, null, Array.Empty<ContentAcquisitionAttempt>(), Array.Empty<string>(), Array.Empty<string>());

        [Test]
        public async Task Acquire_RecordsTheBase_AndPlayUsesIt()
        {
            var digest = Publish("rev-1");
            var operation = ContentAcquireLauncher.Start(Ports());
            await ContentAcquireLauncher.Running;

            var last = ContentAcquireLauncher.LastResult!;
            Assert.That(last.operation, Is.EqualTo(operation));
            Assert.That(ContentAcquireLauncher.LastOperation, Is.EqualTo(operation));
            Assert.That(last.state, Is.EqualTo("Acquired"), last.reason);
            Assert.That(last.revision, Is.EqualTo("rev-1"));
            Assert.That(last.manifestSha256, Is.EqualTo(digest));
            Assert.That(last.targetOrigin, Is.EqualTo(ContentOriginSettings.LocalPublishOriginId));
            Assert.That(last.bytesOrigin, Is.EqualTo(ContentOriginSettings.LocalPublishOriginId));
            Assert.That(last.digestAuthority, Is.EqualTo("Declared"));
            Assert.That(last.selectionSaved, Is.True);
            Assert.That(last.playRevision, Is.EqualTo("rev-1"));
            Assert.That(last.warnings, Is.Empty);
            var saved = ContentBaseSelection.Read(_record, _cache, "spring-full", VerifyPlayLauncher.Target).Entry!;
            Assert.That((saved.revision, saved.manifestSha256, saved.digestAuthority), Is.EqualTo(("rev-1", digest, "Declared")));
            StringAssert.Contains("\"operation\": " + operation, ContentAcquireLauncher.LastResultJson);
            StringAssert.Contains("Play (Built) uses rev-1", ContentAcquireLauncher.Describe(last));
        }

        [Test]
        public void EditorBusy_IsRejectedWithoutTouchingTheCache()
        {
            Publish("rev-1");
            var operation = ContentAcquireLauncher.Start(Ports(busy: "Stop Play first."));
            Assert.That(ContentAcquireLauncher.IsRunning, Is.False);
            Assert.That(ContentAcquireLauncher.LastResult!.operation, Is.EqualTo(operation));
            Assert.That(ContentAcquireLauncher.LastResult.state, Is.EqualTo("Rejected"));
            Assert.That(Directory.Exists(_cache), Is.False);
        }

        [Test]
        public async Task SecondStartWhileRunning_IsRejected_AndTheFirstFinishesWithItsOwnNumber()
        {
            var gate = new TaskCompletionSource<ContentAcquisitionResult>();
            var first = ContentAcquireLauncher.Start(Ports(acquire: (_, _, _, _) => gate.Task));
            var second = ContentAcquireLauncher.Start(Ports());
            Assert.That(second, Is.EqualTo(first + 1));
            Assert.That(ContentAcquireLauncher.LastResult!.operation, Is.EqualTo(second));
            Assert.That(ContentAcquireLauncher.LastResult.state, Is.EqualTo("Rejected"));
            gate.SetResult(Result(ContentAcquisitionState.NotFound));
            await ContentAcquireLauncher.Running;
            Assert.That(ContentAcquireLauncher.LastResult!.operation, Is.EqualTo(first));
            Assert.That(ContentAcquireLauncher.LastResult.state, Is.EqualTo("NotFound"));
        }

        [Test]
        public async Task Cancel_EndsTheRunningAcquisition()
        {
            ContentAcquireLauncher.Start(Ports(acquire: async (_, _, _, token) =>
            {
                var canceled = new TaskCompletionSource<bool>();
                using (token.Register(() => canceled.TrySetResult(true))) await canceled.Task;
                return Result(ContentAcquisitionState.Canceled);
            }));
            Assert.That(ContentAcquireLauncher.IsRunning, Is.True);
            ContentAcquireLauncher.Cancel();
            await ContentAcquireLauncher.Running;
            Assert.That(ContentAcquireLauncher.IsRunning, Is.False);
            Assert.That(ContentAcquireLauncher.LastResult!.state, Is.EqualTo("Canceled"));
        }

        [Test]
        public async Task RecordSaveFailure_KeepsTheAcquisitionAndWarns()
        {
            Publish("rev-1");
            ContentAcquireLauncher.Start(Ports(save: _ => throw new IOException("fixture save failure")));
            await ContentAcquireLauncher.Running;
            var last = ContentAcquireLauncher.LastResult!;
            Assert.That(last.state, Is.EqualTo("Acquired"));
            Assert.That(last.selectionSaved, Is.False);
            Assert.That(last.warnings, Has.Some.Contains("could not be recorded"));
        }

        [Test]
        public async Task AcquiredContentThatPlayCannotUse_IsSaidSo()
        {
            Publish("rev-1", declaration: "spring-whitebox");
            ContentAcquireLauncher.Start(Ports());
            await ContentAcquireLauncher.Running;
            var last = ContentAcquireLauncher.LastResult!;
            Assert.That(last.state, Is.EqualTo("Acquired"), last.reason);
            Assert.That(last.playRevision, Is.Empty);
            Assert.That(last.warnings, Has.Some.Contains("Play will not use the acquired rev-1"));
        }

        [Test]
        public async Task NoOrigin_EndsNotFoundAndLeavesTheRecordAlone()
        {
            ContentAcquireLauncher.Start(Ports());
            await ContentAcquireLauncher.Running;
            Assert.That(ContentAcquireLauncher.LastResult!.state, Is.EqualTo("NotFound"));
            Assert.That(File.Exists(_record), Is.False);
        }
    }
}
