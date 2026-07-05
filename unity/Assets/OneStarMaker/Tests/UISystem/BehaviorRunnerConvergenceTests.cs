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
    /// 割り込み連鎖後の収束と、外部キャンセル・Dispose 時の Snap を検証する。
    /// </summary>
    [TestFixture]
    public class BehaviorRunnerConvergenceTests
    {
        [UnityTest]
        public IEnumerator Convergence_RestartPolicy_SnapsToLastNewValueAfterInterruptChain()
            => UniTask.ToCoroutine(async () =>
            {
                await RunInterruptChainAndAssertConvergence(InterruptPolicy.Restart, 5, 50);
            });

        [UnityTest]
        public IEnumerator Convergence_FromCurrentPolicy_SnapsToLastNewValueAfterInterruptChain()
            => UniTask.ToCoroutine(async () =>
            {
                await RunInterruptChainAndAssertConvergence(InterruptPolicy.FromCurrent, 4, 40);
            });

        [UnityTest]
        public IEnumerator Convergence_RewindPolicy_SnapsToLastNewValueAfterInterruptChain()
            => UniTask.ToCoroutine(async () =>
            {
                await RunInterruptChainAndAssertConvergence(InterruptPolicy.Rewind, 3, 30);
            });

        [UnityTest]
        public IEnumerator Convergence_ExternalCancellation_SnapsActiveBehavior()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);
                var behavior = new ManualBehavior();
                using var cts = new CancellationTokenSource();

                var run = runner.Run(behavior, new TransitionPayload(0, 100), cts.Token);
                cts.Cancel();

                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(behavior.SnapCount, Is.EqualTo(1));
                Assert.That(behavior.LastResolvedNewValue, Is.EqualTo(100));

                runner.Dispose();
            });

        [UnityTest]
        public IEnumerator Convergence_Dispose_SnapsActiveBehavior()
            => UniTask.ToCoroutine(async () =>
            {
                await UniTask.CompletedTask;

                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);
                var behavior = new ManualBehavior();

                runner.Run(behavior, new TransitionPayload(0, 77), CancellationToken.None).Forget();
                runner.Dispose();

                Assert.That(behavior.SnapCount, Is.EqualTo(1));
            });

        private static async UniTask RunInterruptChainAndAssertConvergence(
            InterruptPolicy policy,
            int interruptCount,
            int finalNewValue)
        {
            var target = new Label();
            var runner = new BehaviorRunner(target, policy);

            ManualBehavior? lastBehavior = null;
            UniTask lastRun = default;
            var lastPayload = new TransitionPayload(0, finalNewValue);

            for (var i = 0; i < interruptCount; i++)
            {
                var behavior = new ManualBehavior();
                behavior.OnStarted = ctx =>
                {
                    ctx.VisualState.Set(VisualStateStore.CurrentTransitionKey, i);
                    ctx.VisualState.Set(VisualStateStore.EstimatedDurationKey, 1f);
                };

                var newValue = i == interruptCount - 1 ? finalNewValue : i + 1;
                lastPayload = new TransitionPayload(i, newValue);
                lastRun = runner.Run(behavior, lastPayload, CancellationToken.None);
                lastBehavior = behavior;
            }

            Assert.That(lastBehavior, Is.Not.Null);
            lastBehavior!.Complete();
            await lastRun;

            Assert.That(lastBehavior.LastPayload?.NewValue, Is.EqualTo(finalNewValue));
            Assert.That(lastBehavior.LastResolvedNewValue, Is.EqualTo(finalNewValue));

            runner.Dispose();
        }
    }
}
