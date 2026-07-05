#nullable enable

using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Tests.UISystem.TestDoubles;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// <see cref="InterruptPolicy.Rewind"/> の割り込み仕様を検証する。
    /// </summary>
    [TestFixture]
    public class BehaviorRunnerRewindTests
    {
        [UnityTest]
        public IEnumerator Rewind_CallsRewindAsyncOnRewindableBehavior()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Rewind);

                var previous = new ManualBehavior();
                var next = new ManualBehavior();

                previous.OnStarted = ctx =>
                {
                    ctx.VisualState.Set(VisualStateStore.EstimatedDurationKey, 1f);
                };

                var previousRun = runner.Run(previous, new TransitionPayload(0, 1), CancellationToken.None);
                var nextRun = runner.Run(next, new TransitionPayload(2, 3), CancellationToken.None);

                Assert.That(previous.RewindCount, Is.EqualTo(1));
                Assert.That(previous.SnapCount, Is.EqualTo(0));
                Assert.That(previous.LastRewindProgress, Is.GreaterThanOrEqualTo(0f));
                Assert.That(previous.LastRewindProgress, Is.LessThanOrEqualTo(1f));

                next.Complete();
                await nextRun;

                try
                {
                    await previousRun;
                }
                catch (OperationCanceledException)
                {
                }

                runner.Dispose();
            });

        [UnityTest]
        public IEnumerator Rewind_FallsBackToSnapWhenBehaviorIsNotRewindable()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Rewind);

                var previous = new SnapOnlyManualBehavior();
                var next = new ManualBehavior();

                var previousRun = runner.Run(previous, new TransitionPayload(0, 1), CancellationToken.None);
                var nextRun = runner.Run(next, new TransitionPayload(2, 3), CancellationToken.None);

                Assert.That(previous.SnapCount, Is.EqualTo(1));

                next.Complete();
                await nextRun;

                try
                {
                    await previousRun;
                }
                catch (OperationCanceledException)
                {
                }

                runner.Dispose();
            });
    }
}
