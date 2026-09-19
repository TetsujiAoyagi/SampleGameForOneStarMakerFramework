#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SampleGame.DependOnAll;

namespace OneStarMaker.Tests.BuildContent
{
    public sealed class PlayerContentSmokeSequenceTests
    {
        [Test]
        public async Task Success_RecordsAllFixtureStages()
        {
            var result = await Run();
            Assert.That(result.Failure, Is.Null);
            Assert.That(result.CompletedStages, Is.EqualTo(Enum.GetValues(typeof(PlayerContentSmoke.Stage)).Cast<PlayerContentSmoke.Stage>()));
        }

        [Test]
        public async Task VerifyFailure_StillDestroysAndReleases()
        {
            var primary = new InvalidOperationException("verify");
            var result = await Run(verify: () => UniTask.FromException(primary));
            Assert.That(result.Failure, Is.SameAs(primary));
            Assert.That(result.CompletedStages, Is.EqualTo(new[] { PlayerContentSmoke.Stage.Loaded,
                PlayerContentSmoke.Stage.Instantiated, PlayerContentSmoke.Stage.Destroyed, PlayerContentSmoke.Stage.HandleReleased }));
        }

        [Test]
        public async Task HoldFailure_AfterBehaviorVerification_StillDestroysAndReleases()
        {
            var failure = new OperationCanceledException("hold");
            var result = await Run(hold: () => UniTask.FromException(failure));

            Assert.That(result.Failure, Is.SameAs(failure));
            Assert.That(result.CompletedStages, Is.EqualTo(new[]
            {
                PlayerContentSmoke.Stage.Loaded,
                PlayerContentSmoke.Stage.Instantiated,
                PlayerContentSmoke.Stage.BehaviorVerified,
                PlayerContentSmoke.Stage.Destroyed,
                PlayerContentSmoke.Stage.HandleReleased
            }));
        }

        [Test]
        public async Task Hold_RunsBetweenBehaviorVerificationAndDestroy()
        {
            var calls = new List<string>();
            var result = await Run(
                verify: () => Record(calls, "verify"),
                hold: () => Record(calls, "hold"),
                destroy: () => Record(calls, "destroy"));

            Assert.That(result.Failure, Is.Null);
            Assert.That(calls, Is.EqualTo(new[] { "verify", "hold", "destroy" }));
        }

        [Test]
        public async Task PrimaryAndCleanupFailures_ArePreservedInOrder()
        {
            var primary = new InvalidOperationException("verify");
            var destroy = new InvalidOperationException("destroy");
            var release = new InvalidOperationException("release");
            var result = await Run(verify: () => UniTask.FromException(primary), destroy: () => UniTask.FromException(destroy),
                release: () => UniTask.FromException(release));
            var aggregate = result.Failure as AggregateException;
            Assert.That(aggregate, Is.Not.Null);
            Assert.That(Flatten(aggregate!).First(), Is.SameAs(primary));
            Assert.That(Flatten(aggregate!).Any(x => ReferenceEquals(x, destroy)), Is.True);
            Assert.That(Flatten(aggregate!).Any(x => ReferenceEquals(x, release)), Is.True);
            Assert.That(result.CompletedStages, Is.EqualTo(new[] { PlayerContentSmoke.Stage.Loaded, PlayerContentSmoke.Stage.Instantiated }));
        }

        private static UniTask<PlayerContentSmoke.Result> Run(
            Func<UniTask>? verify = null,
            Func<UniTask>? hold = null,
            Func<UniTask>? destroy = null,
            Func<UniTask>? release = null) => PlayerContentSmoke.RunSequenceAsync(
                () => UniTask.CompletedTask,
                () => UniTask.CompletedTask,
                verify ?? (() => UniTask.CompletedTask),
                hold ?? (() => UniTask.CompletedTask),
                destroy ?? (() => UniTask.CompletedTask),
                release ?? (() => UniTask.CompletedTask));

        private static UniTask Record(ICollection<string> calls, string value)
        {
            calls.Add(value);
            return UniTask.CompletedTask;
        }

        private static IReadOnlyList<Exception> Flatten(Exception exception)
        {
            var result = new List<Exception>();
            void Add(Exception value)
            {
                if (value is AggregateException aggregate)
                    foreach (var inner in aggregate.InnerExceptions) Add(inner);
                else result.Add(value);
            }
            Add(exception);
            return result;
        }
    }
}
