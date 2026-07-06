#nullable enable

using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>キャンセル・LoadCts・ペンディングアンロード テスト。</summary>
    [TestFixture]
    public class SceneDirectorCancellationTests : SceneDirectorTestBase
    {
        // ═══════════════════════════════════════════
        //  外部 ct キャンセル
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator AddScene_CancelDuringPreLoad_CleansUp() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var cts = new CancellationTokenSource();

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    cts.Cancel();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            try
            {
                await director.AddScene("TestScene", null, cts.Token);
                Assert.Fail("OperationCanceledException が throw されるべき");
            }
            catch (OperationCanceledException)
            {
                // 期待通り
            }

            Assert.IsFalse(director.ContainsScene("TestScene"));
            cts.Dispose();
        });

        [UnityTest]
        public IEnumerator AddScene_CancelDuringPreLoad_NoThrowIfUnloadSceneCanceled()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var preLoadStarted = new UniTaskCompletionSource();

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    preLoadStarted.TrySetResult();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            var externalCts = new CancellationTokenSource();
            var addTask = director.AddScene("TestScene", null, externalCts.Token);

            await preLoadStarted.Task;

            // UnloadScene → LoadCts.Cancel()
            await director.UnloadScene("TestScene");

            // 外部 ct はキャンセルされていないので例外は飲み込まれる
            await addTask;

            Assert.IsFalse(director.ContainsScene("TestScene"));
            externalCts.Dispose();
        });

        // ═══════════════════════════════════════════
        //  UnloadScene during loading (LoadCts / pending)
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator UnloadScene_DuringPreLoadWindow_CancelsViaLoadCts()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var preLoadStarted = new UniTaskCompletionSource();

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    preLoadStarted.TrySetResult();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await preLoadStarted.Task;

            await director.UnloadScene("TestScene");
            await addTask;

            Assert.IsFalse(director.ContainsScene("TestScene"));
        });

        [UnityTest]
        public IEnumerator UnloadScene_AfterPNR_RegistersPendingUnload()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();

            var gate = new UniTaskCompletionSource();
            director.UnitySceneLoadGate = gate;

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);

            await UniTask.Yield();
            await UniTask.Yield();

            await director.UnloadScene("TestScene");

            Assert.IsTrue(director.HasPendingUnload("TestScene"),
                "PNR 通過後の UnloadScene はペンディングに登録されるべき");

            gate.TrySetResult();
            await addTask;

            Assert.IsFalse(director.ContainsScene("TestScene"),
                "ペンディングアンロードは Stable 到達後に自動実行されるべき");
            Assert.IsFalse(director.HasPendingUnload("TestScene"),
                "ペンディングは実行後にクリアされるべき");
        });

        [UnityTest]
        public IEnumerator PendingUnload_ClearedOnCancelCleanup()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var preLoadStarted = new UniTaskCompletionSource();
            var cts = new CancellationTokenSource();

            var gate = new UniTaskCompletionSource();
            director.UnitySceneLoadGate = gate;

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    preLoadStarted.TrySetResult();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            var addTask = director.AddScene("TestScene", null, cts.Token);
            await preLoadStarted.Task;

            cts.Cancel();

            try
            {
                await addTask;
                Assert.Fail("OperationCanceledException が throw されるべき");
            }
            catch (OperationCanceledException)
            {
                // 期待通り
            }

            Assert.IsFalse(director.HasPendingUnload("TestScene"));
            Assert.IsFalse(director.ContainsScene("TestScene"));
            cts.Dispose();
        });

        [UnityTest]
        public IEnumerator UnloadScene_AfterPreLoadNonCancellationException_DoesNotThrow()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = _ => throw new InvalidOperationException("PreLoad failure");
            };

            try
            {
                await director.AddScene("TestScene", null, CancellationToken.None);
                Assert.Fail("InvalidOperationException が throw されるべき");
            }
            catch (InvalidOperationException)
            {
                // 期待通り
            }

            // 非 OCE 例外後、LoadCts が破棄済み CTS を指したままだと
            // UnloadScene のキャンセル窓判定で ObjectDisposedException になる。
            // 修正後は LoadCts が null クリアされ、保留アンロード登録に落ちる。
            await director.UnloadScene("TestScene");

            Assert.IsTrue(director.HasPendingUnload("TestScene"),
                "キャンセル窓が閉じているため保留アンロードとして登録されるべき");
        });

        // ═══════════════════════════════════════════
        //  CleanupCanceledScene が AfterUnLoad を呼ぶことの検証
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator CleanupCanceledScene_CallsAfterUnLoad() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var cts = new CancellationTokenSource();

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    cts.Cancel();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };

            try
            {
                await director.AddScene("TestScene", null, cts.Token);
                Assert.Fail("OperationCanceledException が throw されるべき");
            }
            catch (OperationCanceledException)
            {
                // 期待通り
            }

            // PreLoad で確保したリソースを解放するため、AfterUnLoad が呼ばれることを検証
            var scene = Factory.GetCreated("TestScene");
            Assert.IsTrue(scene.AfterUnLoadCalled,
                "CleanupCanceledScene は PreLoad で確保したリソースを解放するため AfterUnLoad を呼ぶべき");
            Assert.IsFalse(director.ContainsScene("TestScene"));
            cts.Dispose();
        });
    }
}
