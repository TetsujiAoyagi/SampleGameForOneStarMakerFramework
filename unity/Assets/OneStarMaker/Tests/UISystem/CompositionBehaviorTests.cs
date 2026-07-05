#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <see cref="SequenceBehavior"/> / <see cref="ParallelBehavior"/> の合成挙動を検証する。
    /// </summary>
    [TestFixture]
    public class CompositionBehaviorTests
    {
        [UnityTest]
        public IEnumerator Sequence_ExecutesStepsInOrder()
            => UniTask.ToCoroutine(async () =>
            {
                var context = CreateContext();
                var order = new List<string>();
                var sequence = new SequenceBehavior(
                    new RecordingBehavior("first", order),
                    new RecordingBehavior("second", order),
                    new RecordingBehavior("third", order));

                await sequence.ExecuteAsync(context, CancellationToken.None);

                Assert.That(order, Is.EqualTo(new[] { "first", "second", "third" }));
            });

        [UnityTest]
        public IEnumerator Sequence_CancellationSkipsRemainingSteps()
            => UniTask.ToCoroutine(async () =>
            {
                var context = CreateContext();
                var order = new List<string>();
                var blocking = new ManualBehavior();
                var sequence = new SequenceBehavior(
                    blocking,
                    new RecordingBehavior("skipped", order));

                using var cts = new CancellationTokenSource();
                var run = sequence.ExecuteAsync(context, cts.Token);
                cts.Cancel();

                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(order, Is.Empty);
                Assert.That(blocking.WasCancelled, Is.True);
            });

        [UnityTest]
        public IEnumerator Parallel_WaitsForAllSteps()
            => UniTask.ToCoroutine(async () =>
            {
                var context = CreateContext();
                var order = new List<string>();
                var parallel = new ParallelBehavior(
                    new RecordingBehavior("alpha", order),
                    new RecordingBehavior("beta", order));

                await parallel.ExecuteAsync(context, CancellationToken.None);

                Assert.That(order.Count, Is.EqualTo(2));
                Assert.That(order, Does.Contain("alpha"));
                Assert.That(order, Does.Contain("beta"));
            });

        [UnityTest]
        public IEnumerator Parallel_CancellationPropagatesToAllSteps()
            => UniTask.ToCoroutine(async () =>
            {
                var context = CreateContext();
                var first = new ManualBehavior();
                var second = new ManualBehavior();
                var parallel = new ParallelBehavior(first, second);

                using var cts = new CancellationTokenSource();
                var run = parallel.ExecuteAsync(context, cts.Token);
                cts.Cancel();

                try
                {
                    await run;
                }
                catch (OperationCanceledException)
                {
                }

                Assert.That(first.WasCancelled, Is.True);
                Assert.That(second.WasCancelled, Is.True);
            });

        [UnityTest]
        public IEnumerator NestedComposition_ExecutesInnerSequenceBeforeOuterTail()
            => UniTask.ToCoroutine(async () =>
            {
                var context = CreateContext();
                var order = new List<string>();
                var inner = new SequenceBehavior(
                    new RecordingBehavior("inner-a", order),
                    new RecordingBehavior("inner-b", order));
                var outer = new SequenceBehavior(
                    inner,
                    new RecordingBehavior("outer-c", order));

                await outer.ExecuteAsync(context, CancellationToken.None);

                Assert.That(order, Is.EqualTo(new[] { "inner-a", "inner-b", "outer-c" }));
            });

        private static UIBehaviorContext CreateContext()
        {
            var target = new Label();
            var visualState = new VisualStateStore();
            return new UIBehaviorContext(
                target,
                new TransitionPayload(null, null),
                visualState,
                null);
        }
    }
}
