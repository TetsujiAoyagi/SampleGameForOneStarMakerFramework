#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Runtime.BuildContent.Distribution;
using SampleGame.DependOnAll.Editor.Build;
using UnityEngine;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// batch 入口の終了コード、結果 file の確定、その実行の published directory の所有。
    /// 段（build・publish・結果の確定）を偽にして、Unity の build を起動しない。
    /// </summary>
    public sealed class SampleGameContentBatchTests
    {
        private string _root = "";
        private string _resultPath = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "osm-bs5b1-batch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            _resultPath = Path.Combine(_root, "batch", SampleGameContentBatch.ResultFileName);
            // 前回の成功結果が残っている状態から始める。どの経路でも今回の結果と読める形で残ってはいけない。
            Directory.CreateDirectory(Path.GetDirectoryName(_resultPath)!);
            File.WriteAllText(_resultPath, "{\"exitCode\":0,\"identity\":\"previous\"}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void BuildModes_BatchIsFull_MenuIsIncremental()
        {
            Assert.That(SampleGameContentBatch.BuildMode, Is.EqualTo(BuildContentMode.Full));
            Assert.That(SampleGameContentBuild.MenuBuildMode, Is.EqualTo(BuildContentMode.Incremental));
        }

        [Test]
        public void Success_WritesResultFieldsAndReturnsZero()
        {
            var fake = new FakeStages(_root);
            var code = SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root);
            Assert.That(code, Is.EqualTo(SampleGameContentBatch.ExitSuccess));
            Assert.That(fake.BuiltDeclaration, Is.EqualTo("spring-full"));
            var result = JsonUtility.FromJson<SampleGameContentBatch.SuccessFile>(File.ReadAllText(_resultPath));
            Assert.That(result.exitCode, Is.EqualTo(0));
            Assert.That(result.identity, Is.EqualTo(fake.Identity));
            Assert.That(result.publishPath, Is.EqualTo(Path.Combine(_root, "transport", fake.Identity)));
            Assert.That(result.declaration, Is.EqualTo("spring-full"));
            Assert.That(result.buildMode, Is.EqualTo("Full"));
            Assert.That(result.manifestSha256, Is.EqualTo(Sha256(Path.Combine(result.publishPath, "transport.json"))));
            Assert.That(Directory.Exists(result.publishPath), Is.True);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_resultPath)!), Has.Length.EqualTo(1), "一時 file が残っている");
            Assert.That(fake.Errors, Is.Empty);
        }

        private static IEnumerable<TestCaseData> InvalidArgumentCases()
        {
            // 単一の配列引数は TestCase だと引数列に展開されるので、TestCaseData で 1 引数として渡す。
            yield return new TestCaseData((object)new string[0]).SetName("Missing");
            yield return new TestCaseData((object)new[] { "-osmPurpose" }).SetName("NoValue");
            yield return new TestCaseData((object)new[] { "-osmPurpose", "-logFile", "x" }).SetName("ValueIsOption");
            yield return new TestCaseData((object)new[] { "-osmPurpose", "no-such-purpose" }).SetName("Unknown");
        }

        [TestCaseSource(nameof(InvalidArgumentCases))]
        public void InvalidArguments_ReturnTwoWithoutBuilding(string[] args)
        {
            var fake = new FakeStages(_root);
            var code = SampleGameContentBatch.Run(args, fake.Create(), _root);
            Assert.That(code, Is.EqualTo(SampleGameContentBatch.ExitInvalidArguments));
            Assert.That(fake.BuildCalls, Is.Zero);
            AssertFailureFile(SampleGameContentBatch.ExitInvalidArguments, fake);
            Assert.That(Directory.Exists(Path.Combine(_root, "transport")), Is.False);
        }

        [Test]
        public void SelectionException_ReturnsThree()
        {
            var fake = new FakeStages(_root) { ThrowSelection = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitSelectionFailed));
            AssertFailureFile(SampleGameContentBatch.ExitSelectionFailed, fake);
        }

        [TestCase(BuildContentIssueCode.MissingClosure, SampleGameContentBatch.ExitSelectionFailed)]
        [TestCase(BuildContentIssueCode.BuildFailure, SampleGameContentBatch.ExitBuildFailed)]
        public void FailedBuildResult_MapsIssueToExitCode(BuildContentIssueCode issue, int expected)
        {
            var fake = new FakeStages(_root) { FailIssue = issue };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root), Is.EqualTo(expected));
            Assert.That(fake.PublishCalls, Is.Zero);
            AssertFailureFile(expected, fake);
        }

        [Test]
        public void FailedBuildResultWithoutIssues_ReturnsFour()
        {
            // issue が無くても成果物が無ければ失敗。選択の失敗と言える根拠が無いので build の失敗に分類する。
            var fake = new FakeStages(_root) { FailWithoutIssue = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitBuildFailed));
            Assert.That(fake.PublishCalls, Is.Zero);
            AssertFailureFile(SampleGameContentBatch.ExitBuildFailed, fake);
        }

        [Test]
        public void PublishFailure_ReturnsFiveAndLeavesNoPublishedDirectory()
        {
            var fake = new FakeStages(_root) { ThrowPublish = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitPublishFailed));
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.False);
            AssertFailureFile(SampleGameContentBatch.ExitPublishFailed, fake);
        }

        [Test]
        public void CommitFailureAfterPublish_ReturnsOneAndRemovesOnlyThisRunsPublishedDirectory()
        {
            var other = Path.Combine(_root, "transport", "other-run");
            Directory.CreateDirectory(other);
            var fake = new FakeStages(_root) { FailFirstCommit = true };
            var code = SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root);
            Assert.That(code, Is.EqualTo(SampleGameContentBatch.ExitUnexpected));
            Assert.That(fake.PublishCalls, Is.EqualTo(1));
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.False);
            Assert.That(Directory.Exists(other), Is.True);
            // 前回の成功結果は開始時に消え、残るのは今回の失敗の結果だけ。
            AssertFailureFile(SampleGameContentBatch.ExitUnexpected, fake);
        }

        [Test]
        public void CommitAlwaysFailing_StillReturnsOneAndLeavesNoPreviousResult()
        {
            var fake = new FakeStages(_root) { FailAllCommits = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitUnexpected));
            Assert.That(File.Exists(_resultPath), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.False);
            Assert.That(fake.Errors, Has.Some.Contains("exitCode=" + SampleGameContentBatch.ExitUnexpected + " "));
            // 失敗の結果 file も書けなかったことが log に残る。
            Assert.That(fake.Errors, Has.Some.Contains("fixture commit failure"));
        }

        // ─── 取得元の head（AC7） ───

        private string HeadPath => Path.Combine(_root, "transport", ".origin", "spring-full", VerifyPlayLauncher.Target + ".json");

        [Test]
        public void Success_PointsTheOriginHeadAtThisPublish()
        {
            var fake = new FakeStages(_root) { RealCommit = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root), Is.EqualTo(SampleGameContentBatch.ExitSuccess));
            var result = JsonUtility.FromJson<SampleGameContentBatch.SuccessFile>(File.ReadAllText(_resultPath));
            var head = ContentOriginHead.Read(File.ReadAllBytes(HeadPath), "spring-full", VerifyPlayLauncher.Target);
            Assert.That(head.State, Is.EqualTo(ContentOriginHeadState.Present), head.Reason);
            Assert.That(head.Head!.revision, Is.EqualTo(fake.Identity));
            Assert.That(head.Head.manifestSha256, Is.EqualTo(result.manifestSha256));
            Assert.That(head.Head.path, Is.EqualTo(fake.Identity));
            Assert.That(result.originHeadFailure, Is.Empty);
            Assert.That(fake.Errors, Is.Empty);
        }

        [Test]
        public void HeadFailure_KeepsThePublishAndReplacesTheResultWithTheFailureField()
        {
            var fake = new FakeStages(_root) { FailHead = true, RealCommit = true };
            var code = SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root);
            Assert.That(code, Is.EqualTo(SampleGameContentBatch.ExitOriginHeadFailed));
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.True, "正しく作れた公開物を消さない");
            var result = JsonUtility.FromJson<SampleGameContentBatch.SuccessFile>(File.ReadAllText(_resultPath));
            Assert.That(result.exitCode, Is.EqualTo(SampleGameContentBatch.ExitOriginHeadFailed));
            Assert.That(result.originHeadFailure, Does.Contain("fixture head failure"));
            Assert.That(result.identity, Is.EqualTo(fake.Identity));
            Assert.That(result.manifestSha256, Is.Not.Empty);
            Assert.That(File.Exists(HeadPath), Is.False);
            Assert.That(fake.Errors, Has.Some.Contains("exitCode=" + SampleGameContentBatch.ExitOriginHeadFailed + " "));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(_resultPath)!), Has.Length.EqualTo(1), "一時 file が残っている");
        }

        [Test]
        public void CommitFailure_DoesNotWriteTheHead()
        {
            var fake = new FakeStages(_root) { FailFirstCommit = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root), Is.EqualTo(SampleGameContentBatch.ExitUnexpected));
            Assert.That(fake.HeadCalls, Is.Zero);
            Assert.That(File.Exists(HeadPath), Is.False);
        }

        [Test]
        public void CleanupFailureAfterTheHeadWasReplaced_IsOnlyADiagnostic()
        {
            var fake = new FakeStages(_root) { HeadDiagnostic = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root), Is.EqualTo(SampleGameContentBatch.ExitSuccess));
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.True);
            Assert.That(File.Exists(HeadPath), Is.True);
        }

        [Test]
        public void HeadLogLine_CarriesThePathRevisionAndDigest()
        {
            var head = new ContentOriginHeadFile { revision = "r1", manifestSha256 = new string('a', 64) };
            Assert.That(SampleGameContentBatch.HeadLogLine(new ContentOriginHeadWriteResult("C:\\o\\h.json", head, Array.Empty<string>())),
                Is.EqualTo("origin head written path=C:\\o\\h.json revision=r1 manifestSha256=" + new string('a', 64)));
        }

        private void AssertFailureFile(int expected, FakeStages fake)
        {
            Assert.That(fake.Errors, Has.Some.Contains("exitCode=" + expected + " "));
            var json = File.ReadAllText(_resultPath);
            var failure = JsonUtility.FromJson<SampleGameContentBatch.FailureFile>(json);
            Assert.That(failure.exitCode, Is.EqualTo(expected));
            Assert.That(failure.reason, Is.Not.Empty);
            // 失敗の結果 file は exitCode と理由だけで、成功の欄を持たない。
            Assert.That(json, Does.Not.Contain("\"identity\""));
            Assert.That(json, Does.Not.Contain("publishPath"));
        }

        private static string[] Args(string purpose) =>
            new[] { "Unity.exe", "-batchmode", "-executeMethod", "x", SampleGameContentBatch.PurposeArgument, purpose, "-logFile", "log" };

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = System.Security.Cryptography.SHA256.Create();
            return string.Concat(Array.ConvertAll(hash.ComputeHash(stream), x => x.ToString("x2")));
        }

        private sealed class FakeStages
        {
            private readonly string _root;
            private int _commits;
            public FakeStages(string root) => _root = root;
            public string Identity { get; } = "20261102T000000000Z-" + Guid.NewGuid().ToString("N");
            public bool ThrowSelection { get; set; }
            public BuildContentIssueCode? FailIssue { get; set; }
            public bool ThrowPublish { get; set; }
            public bool FailFirstCommit { get; set; }
            public bool FailAllCommits { get; set; }
            public bool FailWithoutIssue { get; set; }
            public bool FailHead { get; set; }
            public bool HeadDiagnostic { get; set; }
            /// <summary>結果 file の確定に本物の段（置き換えられる確定）を使う。</summary>
            public bool RealCommit { get; set; }
            public int BuildCalls { get; private set; }
            public int PublishCalls { get; private set; }
            public int HeadCalls { get; private set; }
            public string? BuiltDeclaration { get; private set; }
            public List<string> Errors { get; } = new List<string>();

            public SampleGameContentBatch.Stages Create() => new SampleGameContentBatch.Stages(Build, Publish, Commit, Errors.Add, WriteHead);

            // head は本物の書き手で書く（取得元の root は公開先の親）。失敗と、置き換えの後の掃除の失敗の診断は注入する。
            private ContentOriginHeadWriteResult WriteHead(string published)
            {
                HeadCalls++;
                if (FailHead) throw new IOException("fixture head failure");
                var written = ContentOriginHeadWriter.Write(published);
                return HeadDiagnostic
                    ? new ContentOriginHeadWriteResult(written.HeadPath, written.Head, new[] { "fixture cleanup failure" })
                    : written;
            }

            private BuildContentResult Build(BuildPurposeDeclaration declaration)
            {
                BuildCalls++;
                BuiltDeclaration = declaration.Name;
                if (ThrowSelection) throw new PurposeSelectionException("fixture selection failure");
                var reports = Path.Combine(_root, "reports", Identity);
                if (FailIssue is { } issue)
                    return new BuildContentResult(Identity, Path.Combine(reports, "preflight.json"), Path.Combine(reports, "outcome.json"),
                        null, null, null, "failed", new[] { new BuildContentIssue(issue, "key", "fixture") }, declaration, BuildContentMode.Full);
                if (FailWithoutIssue)
                    return new BuildContentResult(Identity, Path.Combine(reports, "preflight.json"), Path.Combine(reports, "outcome.json"),
                        null, null, null, "failed", Array.Empty<BuildContentIssue>(), declaration, BuildContentMode.Full);
                var content = Path.Combine(_root, "content", Identity);
                Directory.CreateDirectory(content);
                return new BuildContentResult(Identity, Path.Combine(reports, "preflight.json"), Path.Combine(reports, "outcome.json"),
                    content, Path.Combine(content, "manifest"), content, "ok", Array.Empty<BuildContentIssue>(), declaration,
                    BuildContentMode.Full);
            }

            private string Publish(BuildContentResult result, string publishRoot)
            {
                PublishCalls++;
                // 実 publisher と同じく、失敗では publish 先を作らない。
                if (ThrowPublish) throw new IOException("fixture publish failure");
                Directory.CreateDirectory(Path.Combine(publishRoot, "content"));
                // head の書き手が manifest の identity を読むので、形の正しい manifest を書く。
                File.WriteAllText(Path.Combine(publishRoot, "transport.json"), JsonUtility.ToJson(new ContentTransportManifest
                {
                    version = 1, product = "OneStarMaker", contentSet = "spring-full", revision = result.Identity, target = VerifyPlayLauncher.Target,
                    unityVersion = Application.unityVersion, rootSchemaVersion = 2, playerConfigSchemaVersion = 1,
                    files = new[] { new ContentTransportFile { path = "payload.bin", size = 0, sha256 = new string('0', 64) } },
                }));
                return publishRoot;
            }

            private void Commit(string path, string json)
            {
                _commits++;
                if (FailAllCommits || (FailFirstCommit && _commits == 1)) throw new IOException("fixture commit failure");
                if (RealCommit) SampleGameContentBatch.CommitResult(path, json);
                else File.WriteAllText(path, json);
            }
        }
    }
}
