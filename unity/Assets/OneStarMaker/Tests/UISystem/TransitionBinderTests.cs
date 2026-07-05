#nullable enable

using System.Collections;
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
    /// <see cref="TransitionBinder.BindTransition{T}"/> の Pairwise 接続を検証する。
    /// </summary>
    [TestFixture]
    public class TransitionBinderTests
    {
        [UnityTest]
        public IEnumerator BindTransition_DoesNotRunOnInitialValue()
            => UniTask.ToCoroutine(async () =>
            {
                await UniTask.CompletedTask;

                var source = new ReactiveProperty<int>(100);
                var runner = new BehaviorRunner(new Label(), InterruptPolicy.Restart);
                var behavior = new ManualBehavior();

                using var binding = source.BindTransition(runner, behavior);

                Assert.That(behavior.ExecuteCount, Is.EqualTo(0));

                behavior.Complete();
                runner.Dispose();
                source.Dispose();
            });

        [UnityTest]
        public IEnumerator BindTransition_RunsWithOldAndNewPayloadOnValueChange()
            => UniTask.ToCoroutine(async () =>
            {
                await UniTask.CompletedTask;

                var source = new ReactiveProperty<int>(100);
                var runner = new BehaviorRunner(new Label(), InterruptPolicy.Restart);
                var behavior = new ManualBehavior();

                using var binding = source.BindTransition(runner, behavior);
                source.Value = 200;

                Assert.That(behavior.ExecuteCount, Is.EqualTo(1));
                Assert.That(behavior.LastPayload?.GetOld<int>(), Is.EqualTo(100));
                Assert.That(behavior.LastPayload?.GetNew<int>(), Is.EqualTo(200));

                behavior.Complete();
                runner.Dispose();
                source.Dispose();
            });

        [UnityTest]
        public IEnumerator BindTransition_DoesNotRunAfterDispose()
            => UniTask.ToCoroutine(async () =>
            {
                await UniTask.CompletedTask;

                var source = new ReactiveProperty<int>(100);
                var runner = new BehaviorRunner(new Label(), InterruptPolicy.Restart);
                var behavior = new ManualBehavior();

                var binding = source.BindTransition(runner, behavior);
                binding.Dispose();

                source.Value = 200;
                source.Value = 300;

                Assert.That(behavior.ExecuteCount, Is.EqualTo(0));

                runner.Dispose();
                source.Dispose();
            });
    }
}
