#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.AssetManagement;
using OneStarMaker.Tests.SceneSystem.Helpers;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// <c>osm.scenes</c> の出力の形（AC5）を、SceneDirector を作らずに直接組み立てた写しで固定する。
    /// </summary>
    [TestFixture]
    public sealed class ScenesDebugCommandFormatTests
    {
        [Test]
        public void Format_WritesStateNamesSortedRowsProjectedPendingAndFieldOrder()
        {
            var snapshot = new SceneDirectorDebugSnapshot(
                new[]
                {
                    new SceneDirectorDebugSnapshot.SceneEntry("b", SceneState.Stable),
                    new SceneDirectorDebugSnapshot.SceneEntry("A", SceneState.PreLoading),
                    new SceneDirectorDebugSnapshot.SceneEntry("a", SceneState.LoadCanceled),
                },
                // "ghost" は管理集合に無い待ちの id。行として出ないこと。
                new[] { "a", "ghost" },
                new[]
                {
                    new SceneDirectorDebugSnapshot.HistoryEntry("x", "y"),
                    new SceneDirectorDebugSnapshot.HistoryEntry("w", "x"),
                },
                canGoBack: true);

            var result = ScenesDebugCommand.Format(snapshot, 42);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Is.EqualTo("captured"));
            // state は名前の文字列、scenes は id の Ordinal 順（"A" < "a" < "b"）、history は渡した順（新しい順）のまま、
            // field の順は capturedFrame・scenes・history・canGoBack。
            Assert.That(result.PayloadJson, Is.EqualTo(
                "{\"capturedFrame\":42," +
                "\"scenes\":[" +
                "{\"id\":\"A\",\"state\":\"PreLoading\",\"pendingUnload\":false}," +
                "{\"id\":\"a\",\"state\":\"LoadCanceled\",\"pendingUnload\":true}," +
                "{\"id\":\"b\",\"state\":\"Stable\",\"pendingUnload\":false}]," +
                "\"history\":[{\"from\":\"x\",\"to\":\"y\"},{\"from\":\"w\",\"to\":\"x\"}]," +
                "\"canGoBack\":true}"));
        }

        [Test]
        public void Format_EmptySnapshot_WritesEmptyArrays()
        {
            var snapshot = new SceneDirectorDebugSnapshot(
                Array.Empty<SceneDirectorDebugSnapshot.SceneEntry>(),
                Array.Empty<string>(),
                Array.Empty<SceneDirectorDebugSnapshot.HistoryEntry>(),
                canGoBack: false);

            var result = ScenesDebugCommand.Format(snapshot, 0);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PayloadJson, Is.EqualTo("{\"capturedFrame\":0,\"scenes\":[],\"history\":[],\"canGoBack\":false}"));
        }

        [Test]
        public void Format_EscapesQuoteAndBackslashInIds()
        {
            const string id = "q\"b\\s";
            var snapshot = new SceneDirectorDebugSnapshot(
                new[] { new SceneDirectorDebugSnapshot.SceneEntry(id, SceneState.Stable) },
                Array.Empty<string>(),
                new[] { new SceneDirectorDebugSnapshot.HistoryEntry(id, "to") },
                canGoBack: true);

            var result = ScenesDebugCommand.Format(snapshot, 1);

            StringAssert.Contains("\"id\":\"q\\\"b\\\\s\"", result.PayloadJson);
            StringAssert.Contains("\"from\":\"q\\\"b\\\\s\"", result.PayloadJson);
            var parsed = ScenesPayloadReader.Parse(result.PayloadJson);
            Assert.That(parsed.scenes.Single().id, Is.EqualTo(id));
            Assert.That(parsed.history.Single().from, Is.EqualTo(id));
        }

        [Test]
        public void Format_NullSnapshot_IsUnavailableFailure()
        {
            var result = ScenesDebugCommand.Format(null, 5);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("unavailable: scene-director-not-created"));
            Assert.That(result.PayloadJson, Is.Empty);
        }
    }

    /// <summary>
    /// <c>osm.scenes</c> の handler が実行の時に対象を解決すること（AC6）と、unload の待ちを出力に写すこと（AC4b）を固定する。
    /// </summary>
    [TestFixture]
    public sealed class ScenesDebugCommandHandlerTests : SceneDirectorTestBase
    {
        [UnityTest]
        public IEnumerator Handler_ResolvesTheDirectorAtExecutionTime() => UniTask.ToCoroutine(async () =>
        {
            var first = SetupParentChild();
            await first.AddScene("Parent", null, CancellationToken.None);

            var otherResource = SceneTestHelper.CreateSceneResource("Other", LoadType.OnDemand);
            CreatedSOs.Add(otherResource);
            var otherMap = SceneTestHelper.CreateSceneResourceMap(otherResource);
            CreatedSOs.Add(otherMap);
            var otherAssets = new Runtime.AssetManagement.AssetManagement(new FakeAssetBackend());
            var second = new TestableSceneDirector(Factory, UICommon, otherMap, otherAssets);
            try
            {
                await second.AddScene("Other", null, CancellationToken.None);

                SceneDirector? current = null;
                var handler = ScenesDebugCommand.CreateHandler(() => current);

                var unavailable = handler("{}");
                Assert.That(unavailable.Success, Is.False);
                Assert.That(unavailable.Message, Is.EqualTo("unavailable: scene-director-not-created"));

                current = first;
                var fromFirst = handler("{}");
                Assert.That(fromFirst.Success, Is.True);
                Assert.That(ScenesPayloadReader.Parse(fromFirst.PayloadJson).scenes.Select(s => s.id),
                    Is.EqualTo(new[] { "Child", "Parent" }));

                current = second;
                var fromSecond = handler("{}");
                Assert.That(ScenesPayloadReader.Parse(fromSecond.PayloadJson).scenes.Select(s => s.id),
                    Is.EqualTo(new[] { "Other" }), "別の SceneDirector を返すと、その SceneDirector の値だけが出る");

                current = null;
                Assert.That(handler("{}").Message, Is.EqualTo("unavailable: scene-director-not-created"),
                    "登録の時ではなく実行の時に解決する（前の値を cache しない）");
            }
            finally
            {
                second.Dispose();
                otherAssets.ReleaseAll();
            }
        });

        [UnityTest]
        public IEnumerator Handler_PendingUnloadAfterPointOfNoReturn_IsMarkedOnlyOnThatRow() => UniTask.ToCoroutine(async () =>
        {
            // SceneDirectorCancellationTests.UnloadScene_AfterPNR_RegistersPendingUnload と同じ手順。
            var director = SetupSingleScene();
            var gate = new UniTaskCompletionSource();
            director.UnitySceneLoadGate = gate;
            var handler = ScenesDebugCommand.CreateHandler(() => director);

            var addTask = director.AddScene("TestScene", null, CancellationToken.None);
            await UniTask.Yield();
            await UniTask.Yield();
            await director.UnloadScene("TestScene");
            Assert.That(director.HasPendingUnload("TestScene"), Is.True, "前提: PoNR の後の UnloadScene は待ちに入る");

            var during = handler("{}");
            var rows = ScenesPayloadReader.Parse(during.PayloadJson).scenes;
            Assert.That(during.Success, Is.True);
            Assert.That(rows.Where(r => r.pendingUnload).Select(r => r.id), Is.EqualTo(new[] { "TestScene" }));
            Assert.That(rows.Single(r => r.id == "TestScene").state, Is.EqualTo(director.GetSceneState("TestScene").ToString()));

            gate.TrySetResult();
            await addTask;

            var after = handler("{}");
            Assert.That(ScenesPayloadReader.Parse(after.PayloadJson).scenes.Any(r => r.id == "TestScene"), Is.False,
                "unload の完了の後は行が無い");
            Assert.That(director.CaptureDebugSnapshot().PendingUnloads, Is.Empty, "写しの unload の待ちの集合も空");
        });
    }

    /// <summary>テストが出力を読むための DTO（製品の DTO とは別に持ち、出力の項目名を独立に確かめる）。</summary>
    internal static class ScenesPayloadReader
    {
        internal static Payload Parse(string json)
        {
            var payload = JsonUtility.FromJson<Payload>(json);
            Assert.That(payload, Is.Not.Null, json);
            return payload;
        }

        [Serializable]
        internal sealed class Payload
        {
            public int capturedFrame;
            public Row[] scenes = Array.Empty<Row>();
            public HistoryRow[] history = Array.Empty<HistoryRow>();
            public bool canGoBack;
        }

        [Serializable]
        internal sealed class Row
        {
            public string id = string.Empty;
            public string state = string.Empty;
            public bool pendingUnload;
        }

        [Serializable]
        internal sealed class HistoryRow
        {
            public string from = string.Empty;
            public string to = string.Empty;
        }
    }
}
