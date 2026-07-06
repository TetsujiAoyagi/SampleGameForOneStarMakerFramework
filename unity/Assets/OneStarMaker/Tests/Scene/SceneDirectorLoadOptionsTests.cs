#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.SceneSystem
{
    /// <summary>
    /// AddScene / UnloadScene の priority と telemetryLevel 公開（T-03）のレッドテスト。
    /// </summary>
    [TestFixture]
    public class SceneDirectorLoadOptionsTests : SceneDirectorTestBase
    {
        // ═══════════════════════════════════════════
        //  priority
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator AddScene_PriorityArgument_ReachesUnitySceneLoad()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();

            await director.AddScene("TestScene", null, CancellationToken.None, priority: 10);

            Assert.AreEqual(10, director.LastLoadPriorities["TestScene"]);
        });

        [UnityTest]
        public IEnumerator AddScene_DefaultPriority_Is100()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();

            await director.AddScene("TestScene", null, CancellationToken.None);

            Assert.AreEqual(100, director.LastLoadPriorities["TestScene"]);
        });

        [UnityTest]
        public IEnumerator AddScene_Priority_AppliedToParentLoad()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupParentChild();

            await director.AddScene("Child", null, CancellationToken.None, priority: 10);

            Assert.AreEqual(10, director.LastLoadPriorities["Parent"],
                "子 AddScene で指定した priority が親ロードにも適用されるべき");
        });

        // ═══════════════════════════════════════════
        //  telemetryLevel
        // ═══════════════════════════════════════════

        [UnityTest]
        public IEnumerator AddScene_TelemetryLevelVerbose_EmitsVerboseRecord()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                await director.AddScene(
                    "TestScene",
                    null,
                    CancellationToken.None,
                    telemetryLevel: TelemetryLevel.Verbose);

                var record = FindRecord(sink.Records, TelemetryStartType.SceneLoad);
                Assert.IsNotNull(record, "SceneLoad スパンのテレメトリレコードが出力されるべき");
                Assert.AreEqual(TelemetryLevel.Verbose, record!.Value.Level);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        });

        [UnityTest]
        public IEnumerator AddScene_TelemetryLevelDefault_RemainsSummary()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                await director.AddScene("TestScene", null, CancellationToken.None);

                var record = FindRecord(sink.Records, TelemetryStartType.SceneLoad);
                Assert.IsNotNull(record, "SceneLoad スパンのテレメトリレコードが出力されるべき");
                Assert.AreEqual(TelemetryLevel.Summary, record!.Value.Level);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        });

        [UnityTest]
        public IEnumerator UnloadScene_TelemetryLevelVerbose_EmitsVerboseRecord()
            => UniTask.ToCoroutine(async () =>
        {
            var director = SetupSingleScene();
            var sink = new FakeTelemetrySink();
            var originalLevel = AppTelemetry.Level;

            try
            {
                AppTelemetry.Level = TelemetryLevel.Verbose;
                AppTelemetry.AddSink(sink);

                await director.AddScene("TestScene", null, CancellationToken.None);

                await director.UnloadScene("TestScene", telemetryLevel: TelemetryLevel.Verbose);

                var record = FindRecord(sink.Records, TelemetryStartType.SceneUnload);
                Assert.IsNotNull(record, "SceneUnload スパンのテレメトリレコードが出力されるべき");
                Assert.AreEqual(TelemetryLevel.Verbose, record!.Value.Level);
            }
            finally
            {
                AppTelemetry.RemoveSink(sink);
                AppTelemetry.Level = originalLevel;
            }
        });

        private static TelemetryRecord? FindRecord(
            IReadOnlyList<TelemetryRecord> records,
            TelemetryStartType startType)
        {
            for (var i = 0; i < records.Count; i++)
            {
                if (records[i].Name == startType)
                {
                    return records[i];
                }
            }

            return null;
        }
    }
}
