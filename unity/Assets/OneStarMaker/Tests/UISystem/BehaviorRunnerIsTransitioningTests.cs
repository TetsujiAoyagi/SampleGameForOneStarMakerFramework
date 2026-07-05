#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem.Behaviors;
using OneStarMaker.Tests.UISystem.TestDoubles;
using R3;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// <see cref="BehaviorRunner.IsTransitioning"/> の遷移フラグ挙動を検証する。
    /// </summary>
    [TestFixture]
    public class BehaviorRunnerIsTransitioningTests
    {
        [UnityTest]
        public IEnumerator IsTransitioning_IsTrueWhileRunningAndFalseAfterComplete()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);
                var behavior = new ManualBehavior();

                Assert.That(runner.IsTransitioning.CurrentValue, Is.False);

                var run = runner.Run(behavior, new TransitionPayload(0, 1), CancellationToken.None);
                Assert.That(runner.IsTransitioning.CurrentValue, Is.True);

                behavior.Complete();
                await run;

                Assert.That(runner.IsTransitioning.CurrentValue, Is.False);
                runner.Dispose();
            });

        [UnityTest]
        public IEnumerator IsTransitioning_DoesNotBecomeFalseDuringInterruptChain()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);
                var transitions = new List<bool>();
                using var subscription = runner.IsTransitioning.Subscribe(value => transitions.Add(value));

                var behaviorA = new ManualBehavior();
                var behaviorB = new ManualBehavior();
                var behaviorC = new ManualBehavior();

                var runA = runner.Run(behaviorA, new TransitionPayload(0, 1), CancellationToken.None);
                var runB = runner.Run(behaviorB, new TransitionPayload(1, 2), CancellationToken.None);
                var runC = runner.Run(behaviorC, new TransitionPayload(2, 3), CancellationToken.None);

                Assert.That(runner.IsTransitioning.CurrentValue, Is.True);

                behaviorC.Complete();
                await runC;

                try
                {
                    await runB;
                }
                catch (OperationCanceledException)
                {
                }

                try
                {
                    await runA;
                }
                catch (OperationCanceledException)
                {
                }

                AssertNoFalseBetweenFirstTrueAndLastFalse(transitions);
                Assert.That(runner.IsTransitioning.CurrentValue, Is.False);

                runner.Dispose();
            });

        [UnityTest]
        public IEnumerator IsTransitioning_EmitsFalseOnDispose()
            => UniTask.ToCoroutine(async () =>
            {
                await UniTask.CompletedTask;

                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);
                var transitions = new List<bool>();
                using var subscription = runner.IsTransitioning.Subscribe(value => transitions.Add(value));

                runner.Run(new ManualBehavior(), new TransitionPayload(0, 1), CancellationToken.None).Forget();
                runner.Dispose();

                Assert.That(transitions[^1], Is.False);
            });

        private static void AssertNoFalseBetweenFirstTrueAndLastFalse(IReadOnlyList<bool> transitions)
        {
            var firstTrueIndex = -1;
            for (var i = 0; i < transitions.Count; i++)
            {
                if (transitions[i])
                {
                    firstTrueIndex = i;
                    break;
                }
            }

            Assert.That(firstTrueIndex, Is.GreaterThanOrEqualTo(0));

            for (var i = firstTrueIndex; i < transitions.Count - 1; i++)
            {
                Assert.That(transitions[i], Is.True, $"index {i} で false が観測されました（割り込み連鎖中）");
            }

            Assert.That(transitions[^1], Is.False);
        }
    }
}
