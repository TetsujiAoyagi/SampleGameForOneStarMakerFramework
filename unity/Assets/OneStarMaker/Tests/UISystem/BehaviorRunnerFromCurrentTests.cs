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
    /// <see cref="InterruptPolicy.FromCurrent"/> の割り込み仕様を検証する。
    /// </summary>
    [TestFixture]
    public class BehaviorRunnerFromCurrentTests
    {
        [UnityTest]
        public IEnumerator FromCurrent_UsesVisualStateCurrentValueAsOldValueWithoutSnap()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.FromCurrent);

                var first = new ManualBehavior();
                var second = new ManualBehavior();
                const int currentValue = 15;

                first.OnStarted = ctx =>
                {
                    ctx.VisualState.Set(VisualStateStore.CurrentTransitionKey, currentValue);
                };

                var firstRun = runner.Run(first, new TransitionPayload(10, 20), CancellationToken.None);
                var secondRun = runner.Run(second, new TransitionPayload(10, 99), CancellationToken.None);

                Assert.That(first.SnapCount, Is.EqualTo(0));
                Assert.That(first.WasCancelled, Is.True);
                Assert.That(second.LastPayload?.OldValue, Is.EqualTo(currentValue));
                Assert.That(second.LastPayload?.NewValue, Is.EqualTo(99));

                second.Complete();
                await secondRun;

                try
                {
                    await firstRun;
                }
                catch (OperationCanceledException)
                {
                }

                runner.Dispose();
            });
    }
}
