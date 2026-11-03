#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>
    /// 検証 Play の段の順序と停止、1 つずつ、取り消し。段は偽物で、install の未完了は TaskCompletionSource で保つ（待ち時間は使わない）。
    /// </summary>
    public sealed class VerifyPlaySequenceTests
    {
        private static readonly PublishedContent Published = new("published", "abc", "rev", "set", "spring-full", "Full",
            Array.Empty<KeyValuePair<string, string>>());

        private sealed class FakeStages : IVerifyPlayStages
        {
            internal readonly List<string> Calls = new();
            internal string? StartRejection;
            internal string? PlayRejection;
            internal VerifyPlayStage? Throw;
            internal TaskCompletionSource<InstalledContent>? PendingInstall;

            private void Record(string name, VerifyPlayStage stage)
            {
                Calls.Add(name);
                if (Throw == stage) throw new InvalidOperationException("injected " + name);
            }

            public string? CheckBeforeStart() { Record("start", VerifyPlayStage.Start); return StartRejection; }
            public PublishedContent Produce() { Record("produce", VerifyPlayStage.Produce); return Published; }

            public Task<InstalledContent> InstallAsync(PublishedContent published, CancellationToken token)
            {
                Record("install", VerifyPlayStage.Install);
                return PendingInstall?.Task ?? Task.FromResult(new InstalledContent("root", "abc", "rev"));
            }

            public string? CheckBeforePlay() { Record("before-play", VerifyPlayStage.BeforePlay); return PlayRejection; }
            public void Bridge(PublishedContent published, InstalledContent installed) => Record("bridge", VerifyPlayStage.Bridge);
            public void EnterPlaymode() => Record("enter", VerifyPlayStage.EnterPlaymode);
            public void ResetBridge() => Calls.Add("reset");
        }

        [Test]
        public async Task Success_RunsStagesInOrder()
        {
            var stages = new FakeStages();
            var sequence = new VerifyPlaySequence();
            var outcome = await sequence.RunAsync(stages, CancellationToken.None);
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.PlayRequested));
            Assert.That(stages.Calls, Is.EqualTo(new[] { "start", "produce", "install", "before-play", "bridge", "enter" }));
            Assert.That(sequence.IsRunning, Is.False);
        }

        [Test]
        public async Task StartRejection_DoesNotBuild()
        {
            var stages = new FakeStages { StartRejection = "scene open" };
            var outcome = await new VerifyPlaySequence().RunAsync(stages, CancellationToken.None);
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Rejected));
            Assert.That(outcome.Message, Is.EqualTo("scene open"));
            Assert.That(stages.Calls, Is.EqualTo(new[] { "start" }), "build の段の呼び出しは 0 回");
        }

        [Test]
        public async Task BeforePlayRejection_StopsBeforeBridge_WithoutReset()
        {
            // install の待ちの間に profile が選ばれた、または外の値が置かれた。成果物は残り、環境は変えない。
            var stages = new FakeStages { PlayRejection = "profile selected" };
            var outcome = await new VerifyPlaySequence().RunAsync(stages, CancellationToken.None);
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Rejected));
            Assert.That(outcome.Stage, Is.EqualTo(VerifyPlayStage.BeforePlay));
            Assert.That(stages.Calls, Is.EqualTo(new[] { "start", "produce", "install", "before-play" }));
        }

        // 段は internal の enum なので、public な test method には名前で渡す。
        [TestCase("Start", new[] { "start" })]
        [TestCase("Produce", new[] { "start", "produce" })]
        [TestCase("Install", new[] { "start", "produce", "install" })]
        [TestCase("BeforePlay", new[] { "start", "produce", "install", "before-play" })]
        [TestCase("Bridge", new[] { "start", "produce", "install", "before-play", "bridge", "reset" })]
        [TestCase("EnterPlaymode", new[] { "start", "produce", "install", "before-play", "bridge", "enter", "reset" })]
        public async Task StageFailure_StopsLaterStages_AndResetsOnlyAfterBridge(string failingStage, string[] expected)
        {
            var failing = (VerifyPlayStage)Enum.Parse(typeof(VerifyPlayStage), failingStage);
            var stages = new FakeStages { Throw = failing };
            var outcome = await new VerifyPlaySequence().RunAsync(stages, CancellationToken.None);
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Failed));
            Assert.That(outcome.Stage, Is.EqualTo(failing));
            Assert.That(stages.Calls, Is.EqualTo(expected));
        }

        [Test]
        public async Task SecondRequestWhileInstalling_IsRejectedBeforeBuild_FirstReachesBridge()
        {
            var sequence = new VerifyPlaySequence();
            var first = new FakeStages { PendingInstall = new TaskCompletionSource<InstalledContent>() };
            var firstRun = sequence.RunAsync(first, CancellationToken.None);
            Assert.That(sequence.IsRunning, Is.True);

            var second = new FakeStages();
            var secondOutcome = await sequence.RunAsync(second, CancellationToken.None);
            Assert.That(secondOutcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Rejected));
            Assert.That(second.Calls, Is.Empty, "2 件目は build・publish・install に入らない");

            first.PendingInstall.SetResult(new InstalledContent("root", "abc", "rev"));
            var firstOutcome = await firstRun;
            Assert.That(firstOutcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.PlayRequested));
            Assert.That(first.Calls, Does.Contain("bridge"));
            Assert.That(sequence.IsRunning, Is.False);
        }

        [Test]
        public async Task CancelWhileInstalling_LateContinuationDoesNotBridge()
        {
            var sequence = new VerifyPlaySequence();
            using var cancellation = new CancellationTokenSource();
            var stages = new FakeStages { PendingInstall = new TaskCompletionSource<InstalledContent>() };
            var run = sequence.RunAsync(stages, cancellation.Token);
            cancellation.Cancel();
            // install が取り消しを無視して完了しても、bridge の直前で止まる。
            stages.PendingInstall.SetResult(new InstalledContent("root", "abc", "rev"));
            var outcome = await run;
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Cancelled));
            Assert.That(stages.Calls, Has.No.Member("bridge").And.No.Member("enter"));
            Assert.That(sequence.IsRunning, Is.False);
        }

        [Test]
        public async Task CancelWhileInstalling_InstallObservesToken()
        {
            var sequence = new VerifyPlaySequence();
            using var cancellation = new CancellationTokenSource();
            var stages = new FakeStages { PendingInstall = new TaskCompletionSource<InstalledContent>() };
            var run = sequence.RunAsync(stages, cancellation.Token);
            cancellation.Cancel();
            stages.PendingInstall.SetCanceled();
            var outcome = await run;
            Assert.That(outcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Cancelled));
            Assert.That(stages.Calls, Has.No.Member("bridge"));
        }

        [Test]
        public async Task CancelBothRequests_NeitherRequestsPlay()
        {
            var sequence = new VerifyPlaySequence();
            using var cancellation = new CancellationTokenSource();
            var first = new FakeStages { PendingInstall = new TaskCompletionSource<InstalledContent>() };
            var firstRun = sequence.RunAsync(first, cancellation.Token);
            var second = new FakeStages();
            var secondOutcome = await sequence.RunAsync(second, cancellation.Token);
            cancellation.Cancel();
            first.PendingInstall.SetResult(new InstalledContent("root", "abc", "rev"));
            Assert.That((await firstRun).Kind, Is.EqualTo(VerifyPlayOutcomeKind.Cancelled));
            Assert.That(secondOutcome.Kind, Is.EqualTo(VerifyPlayOutcomeKind.Rejected));
            Assert.That(first.Calls, Has.No.Member("enter"));
            Assert.That(second.Calls, Is.Empty);
        }
    }
}
