#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using R3;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// <see cref="SceneDirector.CaptureDebugSnapshot"/> が SceneDirector の状態を完全に写し、写しても状態を変えないことを固定する。
    /// 比較の相手は写しを通らない読み方（テスト用の internal の accessor と、private field の reflection）にし、写しで写しを確かめない。
    /// </summary>
    [TestFixture]
    public sealed class SceneDirectorDebugSnapshotTests : SceneDirectorTestBase
    {
        // ═══════════════════════════════════════════
        //  AC1: 管理集合・各 SceneState・unload の待ちを完全に写す
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Capture_ParentAndChild_CopiesManagedSetStatesAndPendingUnloads() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupParentChild();
            await director.AddScene("Parent", null, CancellationToken.None);

            var snapshot = director.CaptureDebugSnapshot();

            var expected = ReadManagedScenes(director);
            Assert.That(expected.Keys, Is.EquivalentTo(new[] { "Parent", "Child" }), "前提: 親と NecessaryAlways の子が管理集合にある");
            Assert.That(snapshot.Scenes.Select(s => s.Id), Is.EquivalentTo(expected.Keys), "管理集合の id を過不足なく写す");
            Assert.That(snapshot.Scenes.Select(s => s.Id).Distinct().Count(), Is.EqualTo(snapshot.Scenes.Count), "id が重複しない");
            foreach (var entry in snapshot.Scenes)
            {
                Assert.That(entry.State, Is.EqualTo(director.GetSceneState(entry.Id)), entry.Id);
            }

            Assert.That(snapshot.PendingUnloads, Is.EquivalentTo(ReadPendingUnloads(director)));
            Assert.That(snapshot.History, Is.Empty);
            Assert.That(snapshot.CanGoBack, Is.False);
        });

        [Test]
        public void Capture_EmptyDirector_IsEmpty()
        {
            var director = SetupSingleScene();

            var snapshot = director.CaptureDebugSnapshot();

            Assert.That(snapshot.Scenes, Is.Empty);
            Assert.That(snapshot.PendingUnloads, Is.Empty);
            Assert.That(snapshot.History, Is.Empty);
            Assert.That(snapshot.CanGoBack, Is.False);
        }

        [Test]
        public void Snapshot_ExposedLists_CannotBeRewrittenFromOutside()
        {
            var scenes = new[] { new SceneDirectorDebugSnapshot.SceneEntry("A", SceneState.Stable) };
            var pendingUnloads = new[] { "A" };
            var history = new[] { new SceneDirectorDebugSnapshot.HistoryEntry("from", "A") };
            var snapshot = new SceneDirectorDebugSnapshot(scenes, pendingUnloads, history, true);

            // 渡した側の配列を後で書き換えても、写しは変わらない。
            scenes[0] = new SceneDirectorDebugSnapshot.SceneEntry("B", SceneState.None);
            pendingUnloads[0] = "B";
            history[0] = new SceneDirectorDebugSnapshot.HistoryEntry("B", "B");

            // 公開した列を書き換え可能な型へ cast しても、写しは書き換えられない。
            AssertCannotRewrite(snapshot.Scenes, scenes[0]);
            AssertCannotRewrite(snapshot.PendingUnloads, "B");
            AssertCannotRewrite(snapshot.History, history[0]);

            Assert.That(snapshot.Scenes.Select(s => (s.Id, s.State)), Is.EqualTo(new[] { ("A", SceneState.Stable) }));
            Assert.That(snapshot.PendingUnloads, Is.EqualTo(new[] { "A" }));
            Assert.That(ToPairs(snapshot.History), Is.EqualTo(new[] { ("from", "A") }));
        }

        // ═══════════════════════════════════════════
        //  AC2: 履歴の順と内容、CanGoBack
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Capture_AfterTwoSwitches_HistoryIsNewestFirst() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSiblings("A", "B", "C");
            await director.AddScene("A", null, CancellationToken.None);
            await director.SwitchScene("A", "B", CancellationToken.None);
            await director.SwitchScene("B", "C", CancellationToken.None);

            var snapshot = director.CaptureDebugSnapshot();

            Assert.That(ToPairs(snapshot.History), Is.EqualTo(new[] { ("B", "C"), ("A", "B") }));
            Assert.That(ToPairs(snapshot.History), Is.EqualTo(ReadHistory(director)), "stack の列挙の順（新しい順）と同じ");
            Assert.That(snapshot.CanGoBack, Is.True);
            Assert.That(snapshot.CanGoBack, Is.EqualTo(director.CanGoBack));
        });

        [UnityTest]
        public IEnumerator Capture_AfterSwitchFailsPastPointOfNoReturn_HistoryEntryRemains() => UniTask.ToCoroutine(async () =>
        {
            // 今の挙動の記録: 履歴は PoNR の後、unload と add の前に積まれるので、遷移先の読み込みが失敗しても entry は残る。
            // 写しの history は「成功した遷移の記録」ではなく「戻り先の管理用の stack」であることを固定する。
            var director = SetupSiblings("A", "B");
            await director.AddScene("A", null, CancellationToken.None);
            Factory.OnCreated = scene =>
            {
                if (scene.SceneResource.Identity == "B")
                {
                    scene.PreLoadAction = _ => throw new InvalidOperationException("snapshot-test: B fails to preload");
                }
            };

            Exception? thrown = null;
            try
            {
                await director.SwitchScene("A", "B", CancellationToken.None);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.That(thrown, Is.InstanceOf<InvalidOperationException>(), "前提: PoNR の後の遷移が失敗する");
            var snapshot = director.CaptureDebugSnapshot();
            Assert.That(ToPairs(snapshot.History), Is.EqualTo(new[] { ("A", "B") }));
            Assert.That(snapshot.CanGoBack, Is.True);
            Assert.That(snapshot.Scenes.Select(s => s.Id), Is.EquivalentTo(ReadManagedScenes(director).Keys));
        });

        // ═══════════════════════════════════════════
        //  AC3: 写しは状態を変えない
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Capture_Repeated_DoesNotChangeStateOrEmitEvents() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSiblings("A", "B");
            await director.AddScene("A", null, CancellationToken.None);
            await director.SwitchScene("A", "B", CancellationToken.None);

            var eventCount = 0;
            using var subscription = director.OnSceneEvent.Subscribe(_ => eventCount++);

            var scenesBefore = ReadManagedScenes(director);
            var pendingBefore = ReadPendingUnloads(director);
            var historyBefore = ReadHistory(director);
            var canGoBackBefore = director.CanGoBack;

            var first = director.CaptureDebugSnapshot();
            for (var i = 0; i < 100; i++)
            {
                director.CaptureDebugSnapshot();
            }

            var last = director.CaptureDebugSnapshot();

            Assert.That(ReadManagedScenes(director), Is.EquivalentTo(scenesBefore), "管理集合と各 SceneState");
            Assert.That(ReadPendingUnloads(director), Is.EquivalentTo(pendingBefore), "unload の待ちの集合");
            Assert.That(ReadHistory(director), Is.EqualTo(historyBefore), "履歴の内容と順");
            Assert.That(director.CanGoBack, Is.EqualTo(canGoBackBefore));
            Assert.That(eventCount, Is.EqualTo(0), "写しは OnSceneEvent を出さない");

            Assert.That(ToSceneMap(last), Is.EquivalentTo(ToSceneMap(first)));
            Assert.That(last.PendingUnloads, Is.EquivalentTo(first.PendingUnloads));
            Assert.That(ToPairs(last.History), Is.EqualTo(ToPairs(first.History)));
            Assert.That(last.CanGoBack, Is.EqualTo(first.CanGoBack));
        });

        // ═══════════════════════════════════════════
        //  AC4: 操作の途中で写しても例外にならず、途中の状態がそのまま写り、操作は完了する
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator Capture_WhileAddSceneIsWaitingForUnityLoad_ShowsTheInProgressState() => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var gate = new UniTaskCompletionSource();
            director.UnitySceneLoadGate = gate;

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await UniTask.Yield();
            await UniTask.Yield();

            var during = director.CaptureDebugSnapshot();

            Assert.That(during.Scenes.Select(s => s.Id), Is.EqualTo(new[] { "TestScene" }), "PreLoad の前に管理集合へ入った Scene が載る");
            Assert.That(during.Scenes[0].State, Is.EqualTo(director.GetSceneState("TestScene")));
            Assert.That(during.Scenes[0].State, Is.Not.EqualTo(SceneState.Stable), "前提: まだ Stable ではない");

            gate.TrySetResult();
            await addTask;

            Assert.That(director.GetSceneState("TestScene"), Is.EqualTo(SceneState.Stable), "写しの後も操作が完了する");
            var after = director.CaptureDebugSnapshot();
            Assert.That(after.Scenes.Single().State, Is.EqualTo(SceneState.Stable));
        });

        [UnityTest]
        public IEnumerator Capture_DuringCancelCleanup_ShowsLoadCanceled() => UniTask.ToCoroutine(async () =>
        {
            // SceneDirectorCancellationTests.AddScene_CancelDuringPreLoad_CleansUp と同じ手順で取消し、
            // 後始末が LoadCanceled に遷移した後の Unity Scene の unload の中で写す。
            var director = SetupSingleScene();
            var cts = new CancellationTokenSource();
            SceneDirectorDebugSnapshot? duringCleanup = null;

            Factory.OnCreated = scene =>
            {
                scene.PreLoadAction = async ct =>
                {
                    cts.Cancel();
                    await UniTask.WaitUntilCanceled(ct);
                    ct.ThrowIfCancellationRequested();
                };
            };
            director.UnitySceneUnloadAction = _ =>
            {
                duringCleanup = director.CaptureDebugSnapshot();
                return UniTask.CompletedTask;
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
            finally
            {
                cts.Dispose();
            }

            Assert.That(duringCleanup, Is.Not.Null, "前提: 後始末の途中で写せた");
            var entry = duringCleanup!.Scenes.Single(s => s.Id == "TestScene");
            Assert.That(entry.State, Is.EqualTo(SceneState.LoadCanceled), "途中の状態をそのまま写す");
            Assert.That(director.ContainsScene("TestScene"), Is.False, "写しの後も後始末が完了する");
            Assert.That(director.CaptureDebugSnapshot().Scenes, Is.Empty);
        });

        // ─── helpers ───

        private TestableSceneDirector SetupSiblings(params string[] identities)
        {
            var resources = new SceneResource[identities.Length];
            for (var i = 0; i < identities.Length; i++)
            {
                resources[i] = SceneTestHelper.CreateSceneResource(identities[i], LoadType.OnDemand);
                CreatedSOs.Add(resources[i]);
            }

            Map = SceneTestHelper.CreateSceneResourceMap(resources);
            CreatedSOs.Add(Map);

            Director = new TestableSceneDirector(Factory, UICommon, Map, AssetManagement, new FakeLoadingDisplay());
            return Director;
        }

        private static void AssertCannotRewrite<T>(IReadOnlyList<T> exposed, T replacement)
        {
            Assert.That(exposed, Is.Not.InstanceOf<T[]>(), "内部の配列をそのまま公開しない");
            if (exposed is IList<T> list)
            {
                Assert.Throws<NotSupportedException>(() => list[0] = replacement);
            }
        }

        private static (string From, string To)[] ToPairs(IReadOnlyList<SceneDirectorDebugSnapshot.HistoryEntry> history)
            => history.Select(h => (h.FromSceneId, h.ToSceneId)).ToArray();

        private static Dictionary<string, SceneState> ToSceneMap(SceneDirectorDebugSnapshot snapshot)
            => snapshot.Scenes.ToDictionary(s => s.Id, s => s.State, StringComparer.Ordinal);

        // 以下は写しを通らない独立の読み方。private field を reflection で読み、各 Scene の状態はテスト用の accessor で読む。

        private static Dictionary<string, SceneState> ReadManagedScenes(SceneDirector director)
        {
            var scenes = (IDictionary)GetPrivateField(director, "_currentScenes");
            var result = new Dictionary<string, SceneState>(StringComparer.Ordinal);
            foreach (string id in scenes.Keys)
            {
                result.Add(id, director.GetSceneState(id));
            }

            return result;
        }

        private static string[] ReadPendingUnloads(SceneDirector director)
            => ((IEnumerable<string>)GetPrivateField(director, "_pendingUnloads")).ToArray();

        private static (string From, string To)[] ReadHistory(SceneDirector director)
        {
            var stack = (IEnumerable)GetPrivateField(director, "_sceneHistory");
            var result = new List<(string, string)>();
            foreach (var entry in stack)
            {
                var type = entry.GetType();
                var from = (string)type.GetField("FromSceneId")!.GetValue(entry)!;
                var to = (string)type.GetField("ToSceneId")!.GetValue(entry)!;
                result.Add((from, to));
            }

            return result.ToArray();
        }

        private static object GetPrivateField(SceneDirector director, string name)
            => typeof(SceneDirector).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(director)!;
    }
}
