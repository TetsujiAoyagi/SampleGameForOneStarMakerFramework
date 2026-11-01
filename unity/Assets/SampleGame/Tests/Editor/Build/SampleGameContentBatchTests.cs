#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
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
            AssertFailureFile(SampleGameContentBatch.ExitInvalidArguments);
            Assert.That(Directory.Exists(Path.Combine(_root, "transport")), Is.False);
        }

        [Test]
        public void SelectionException_ReturnsThree()
        {
            var fake = new FakeStages(_root) { ThrowSelection = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitSelectionFailed));
            AssertFailureFile(SampleGameContentBatch.ExitSelectionFailed);
        }

        [TestCase(BuildContentIssueCode.MissingClosure, SampleGameContentBatch.ExitSelectionFailed)]
        [TestCase(BuildContentIssueCode.BuildFailure, SampleGameContentBatch.ExitBuildFailed)]
        public void FailedBuildResult_MapsIssueToExitCode(BuildContentIssueCode issue, int expected)
        {
            var fake = new FakeStages(_root) { FailIssue = issue };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root), Is.EqualTo(expected));
            Assert.That(fake.PublishCalls, Is.Zero);
            AssertFailureFile(expected);
        }

        [Test]
        public void PublishFailure_ReturnsFiveAndLeavesNoPublishedDirectory()
        {
            var fake = new FakeStages(_root) { ThrowPublish = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitPublishFailed));
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.False);
            AssertFailureFile(SampleGameContentBatch.ExitPublishFailed);
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
            AssertFailureFile(SampleGameContentBatch.ExitUnexpected);
        }

        [Test]
        public void CommitAlwaysFailing_StillReturnsOneAndLeavesNoPreviousResult()
        {
            var fake = new FakeStages(_root) { FailAllCommits = true };
            Assert.That(SampleGameContentBatch.Run(Args("spring-full"), fake.Create(), _root),
                Is.EqualTo(SampleGameContentBatch.ExitUnexpected));
            Assert.That(File.Exists(_resultPath), Is.False);
            Assert.That(Directory.Exists(Path.Combine(_root, "transport", fake.Identity)), Is.False);
        }

        private void AssertFailureFile(int expected)
        {
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
            public int BuildCalls { get; private set; }
            public int PublishCalls { get; private set; }
            public string? BuiltDeclaration { get; private set; }

            public SampleGameContentBatch.Stages Create() => new SampleGameContentBatch.Stages(Build, Publish, Commit);

            private BuildContentResult Build(BuildPurposeDeclaration declaration)
            {
                BuildCalls++;
                BuiltDeclaration = declaration.Name;
                if (ThrowSelection) throw new PurposeSelectionException("fixture selection failure");
                var reports = Path.Combine(_root, "reports", Identity);
                if (FailIssue is { } issue)
                    return new BuildContentResult(Identity, Path.Combine(reports, "preflight.json"), Path.Combine(reports, "outcome.json"),
                        null, null, null, "failed", new[] { new BuildContentIssue(issue, "key", "fixture") }, declaration, BuildContentMode.Full);
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
                File.WriteAllText(Path.Combine(publishRoot, "transport.json"), "{\"revision\":\"" + result.Identity + "\"}");
                return publishRoot;
            }

            private void Commit(string path, string json)
            {
                _commits++;
                if (FailAllCommits || (FailFirstCommit && _commits == 1)) throw new IOException("fixture commit failure");
                File.WriteAllText(path, json);
            }
        }
    }
}
