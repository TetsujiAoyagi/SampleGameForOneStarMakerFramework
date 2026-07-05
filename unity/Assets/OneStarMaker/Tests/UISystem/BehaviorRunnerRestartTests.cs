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
    /// <see cref="InterruptPolicy.Restart"/> の割り込み仕様を検証する。
    /// </summary>
    [TestFixture]
    public class BehaviorRunnerRestartTests
    {
        [UnityTest]
        public IEnumerator Restart_InterruptsPrevious_SnapsOnceAndRunsNewWithOriginalPayload()
            => UniTask.ToCoroutine(async () =>
            {
                var target = new Label();
                var runner = new BehaviorRunner(target, InterruptPolicy.Restart);

                var first = new ManualBehavior();
                var second = new ManualBehavior();
                var firstPayload = new TransitionPayload(10, 20);
                var secondPayload = new TransitionPayload(30, 40);

                var firstRun = runner.Run(first, firstPayload, CancellationToken.None);
                var secondRun = runner.Run(second, secondPayload, CancellationToken.None);

                Assert.That(first.ExecuteCount, Is.EqualTo(1));
                Assert.That(first.WasCancelled, Is.True);
                Assert.That(first.SnapCount, Is.EqualTo(1));
                Assert.That(second.ExecuteCount, Is.EqualTo(1));
                Assert.That(second.LastPayload?.OldValue, Is.EqualTo(30));
                Assert.That(second.LastPayload?.NewValue, Is.EqualTo(40));
                Assert.That(second.SnapCount, Is.EqualTo(0));

                second.Complete();
                await secondRun;

                try
                {
                    await firstRun;
                }
                catch (OperationCanceledException)
                {
                    // 割り込みによるキャンセルは期待どおり。
                }

                runner.Dispose();
            });
    }
}
