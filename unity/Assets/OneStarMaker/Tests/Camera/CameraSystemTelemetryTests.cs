#nullable enable

using System.Linq;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Tests.SceneSystem.TestDoubles;
using UnityEngine;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Tests.CameraSystem
{
    /// <summary>
    /// CAM-10: CameraSystem telemetry collector / switch span / emitter のテスト。
    /// </summary>
    [TestFixture]
    public sealed class CameraSystemTelemetryTests
    {
        private FakeCameraBackend _backend = null!;
        private RuntimeCameraSystem _system = null!;
        private CameraSystemTelemetryCollector _collector = null!;
        private FakeTelemetrySink _sink = null!;
        private TelemetryLevel _originalLevel;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeCameraBackend();
            _system = new RuntimeCameraSystem(_backend);
            _collector = new CameraSystemTelemetryCollector();
            _sink = new FakeTelemetrySink();
            _originalLevel = AppTelemetry.Level;
            AppTelemetry.Level = TelemetryLevel.Verbose;
            AppTelemetry.AddSink(_sink);
        }

        [TearDown]
        public void TearDown()
        {
            // 未完了 span が残った場合は Tick で閉じ、後続テストを汚さない。
            if (AppTelemetry.CurrentSpanId.HasValue)
            {
                var level = AppTelemetry.Level;
                AppTelemetry.Level = TelemetryLevel.Verbose;
                _backend.SetBlending(new ViewId(1), isBlending: false);
                ((CameraView)_system.MainView).Tick(0.016f);
                AppTelemetry.Level = level;
            }

            AppTelemetry.RemoveSink(_sink);
            AppTelemetry.Level = _originalLevel;
        }

        [Test]
        public void CameraSystemTelemetryCollector_CapturesViewCountsAndStackDepths()
        {
            var mainView = (CameraView)_system.MainView;
            var splitView = (CameraView)_system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0.5f, 0f, 0.5f, 1f),
            });

            var renderTexture = new RenderTexture(64, 64, 0);
            try
            {
                var rtView = (CameraView)_system.CreateView(new CameraViewConfig
                {
                    ViewportRect = new Rect(0f, 0f, 0.25f, 0.25f),
                    TargetTexture = renderTexture,
                });

                var gameplay = new LogicalCamera("gameplay");
                var cutscene = new LogicalCamera("cutscene");
                using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
                using (splitView.Push(cutscene, CameraLayer.Cutscene, CameraBlendSpec.Cut))
                {
                    var snapshot = _collector.Capture(_system);

                    Assert.That(snapshot.TotalViewCount, Is.EqualTo(3));
                    Assert.That(snapshot.AdditionalViewCount, Is.EqualTo(2));

                    var mainSummary = snapshot.ViewSummaries.Single(v => v.ViewId == new ViewId(1));
                    Assert.That(mainSummary.StackDepthTotal, Is.EqualTo(1));
                    Assert.That(mainSummary.GameplayDepth, Is.EqualTo(1));
                    Assert.That(mainSummary.CutsceneDepth, Is.Zero);
                    Assert.That(mainSummary.DebugDepth, Is.Zero);

                    var splitSummary = snapshot.ViewSummaries.Single(v => v.ViewId == new ViewId(2));
                    Assert.That(splitSummary.StackDepthTotal, Is.EqualTo(1));
                    Assert.That(splitSummary.CutsceneDepth, Is.EqualTo(1));

                    var rtSummary = snapshot.ViewSummaries.Single(v => v.ViewId == new ViewId(3));
                    Assert.That(rtSummary.IsRenderTextureView, Is.True);
                    Assert.That(rtSummary.StackDepthTotal, Is.Zero);
                }
            }
            finally
            {
                Object.DestroyImmediate(renderTexture);
            }
        }

        [Test]
        public void CameraSystemTelemetryCollector_CapturesActiveCameraAndBlendCount()
        {
            var mainView = (CameraView)_system.MainView;
            var gameplay = new LogicalCamera("gameplay");
            var cutscene = new LogicalCamera("cutscene");

            using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: true);
                var blendingSnapshot = _collector.Capture(_system);

                Assert.That(blendingSnapshot.BlendingViewCount, Is.EqualTo(1));
                Assert.That(blendingSnapshot.ViewSummaries[0].ActiveCameraId, Is.EqualTo("gameplay"));
                Assert.That(blendingSnapshot.ViewSummaries[0].IsBlending, Is.True);

                _backend.SetBlending(new ViewId(1), isBlending: false);
                using (mainView.Push(cutscene, CameraLayer.Cutscene, new CameraBlendSpec { DurationSec = 0.5f }))
                {
                    _backend.SetBlending(new ViewId(1), isBlending: true);
                    mainView.Tick(0.016f);

                    var snapshot = _collector.Capture(_system);
                    Assert.That(snapshot.ViewSummaries[0].ActiveCameraId, Is.EqualTo("cutscene"));
                    Assert.That(snapshot.ViewSummaries[0].HasIncomingSnapshot, Is.True);
                    Assert.That(snapshot.BlendingViewCount, Is.EqualTo(1));
                }

                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);
            }
        }

        [Test]
        public void CameraTelemetryHash_ComputeActiveCameraIdHash_IsDeterministicAndAvoidsUnsetSentinel()
        {
            const string cameraId = "cutscene";
            var first = CameraTelemetryHash.ComputeActiveCameraIdHash(cameraId);
            var second = CameraTelemetryHash.ComputeActiveCameraIdHash(cameraId);

            Assert.That(first, Is.EqualTo(second), "同一 ID は常に同じ決定的 hash になる");
            Assert.That(first, Is.Not.EqualTo(-1), "未設定 sentinel と衝突しない");
        }

        [Test]
        public void CameraView_CameraSwitchSpan_EmitsVerboseWhenBlendCompletes()
        {
            var mainView = (CameraView)_system.MainView;
            var gameplay = new LogicalCamera("gameplay");
            var cutscene = new LogicalCamera("cutscene");

            using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);
                _sink.ClearRecords();

                using (mainView.Push(cutscene, CameraLayer.Cutscene, new CameraBlendSpec { DurationSec = 0.5f }))
                {
                    _backend.SetBlending(new ViewId(1), isBlending: true);
                    mainView.Tick(0.016f);

                    Assert.That(FindSwitchRecords().Count, Is.Zero, "ブレンド中は span を閉じない");

                    _backend.SetBlending(new ViewId(1), isBlending: false);
                    mainView.Tick(0.016f);

                    var records = FindSwitchRecords();
                    Assert.That(records, Has.Count.EqualTo(1));
                    Assert.That(records[0].Level, Is.EqualTo(TelemetryLevel.Verbose));
                    Assert.That(records[0].IsSuccess, Is.True);
                    Assert.That(records[0].MetadataValue.CameraViewId, Is.EqualTo(1));
                    Assert.That(
                        records[0].MetadataValue.CameraActiveCameraHash,
                        Is.EqualTo(CameraTelemetryHash.ComputeActiveCameraIdHash("cutscene")),
                        "CameraSwitch span は LogicalCamera.Id の決定的 hash を記録する");
                    Assert.That(records[0].MetadataValue.SceneFrom, Is.EqualTo(-1));
                    Assert.That(records[0].MetadataValue.SceneTo, Is.EqualTo(-1));
                    Assert.IsNull(AppTelemetry.CurrentSpanId, "span 完了後に current が残ってはいけない");
                }
            }
        }

        [Test]
        public void CameraView_CameraSwitchSpan_DoesNotEmitForInitialFallback()
        {
            _sink.ClearRecords();

            var mainView = (CameraView)_system.MainView;
            _backend.SetCurrentPose(new ViewId(1), CreatePose(Vector3.zero));
            mainView.Tick(0.016f);

            Assert.That(FindSwitchRecords(), Is.Empty, "初期 fallback 同期は span 対象外");
        }

        [Test]
        public void CameraView_CameraSwitchSpan_CutCompletesWithoutLeakingCurrentSpan()
        {
            var mainView = (CameraView)_system.MainView;
            var gameplay = new LogicalCamera("gameplay");

            _sink.ClearRecords();
            using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);
            }

            _backend.SetBlending(new ViewId(1), isBlending: false);
            mainView.Tick(0.016f);
            Assert.IsNull(AppTelemetry.CurrentSpanId, "Push/Pop の cut 切替後も current span が残らない");

            var cutscene = new LogicalCamera("cutscene");
            _sink.ClearRecords();
            using (mainView.Push(cutscene, CameraLayer.Cutscene, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);

                Assert.That(FindSwitchRecords(), Has.Count.EqualTo(1));
                Assert.IsNull(AppTelemetry.CurrentSpanId);
            }
        }

        [Test]
        public void CameraView_CameraSwitchSpan_ForcesPreviousSpanClosedOnRapidSwitch()
        {
            var mainView = (CameraView)_system.MainView;
            var first = new LogicalCamera("first");
            var second = new LogicalCamera("second");

            using (mainView.Push(first, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);
                _sink.ClearRecords();

                using (mainView.Push(second, CameraLayer.Cutscene, new CameraBlendSpec { DurationSec = 1f }))
                {
                    _backend.SetBlending(new ViewId(1), isBlending: true);
                    mainView.Tick(0.016f);

                    var third = new LogicalCamera("third");
                    using (mainView.Push(third, CameraLayer.Debug, CameraBlendSpec.Cut))
                    {
                        _backend.SetBlending(new ViewId(1), isBlending: false);
                        mainView.Tick(0.016f);

                        var records = FindSwitchRecords();
                        Assert.That(records, Has.Count.EqualTo(2));
                        Assert.That(records[0].IsSuccess, Is.False, "ブレンド完了前の再切替は前 span を失敗扱いで閉じる");
                        Assert.That(records[1].IsSuccess, Is.True);
                        Assert.IsNull(AppTelemetry.CurrentSpanId);
                    }
                }
            }
        }

        [Test]
        public void FinishSpan_SummaryLevel_FiltersCameraSwitchWithoutLeakingCurrentSpan()
        {
            AppTelemetry.Level = TelemetryLevel.Summary;
            var mainView = (CameraView)_system.MainView;
            var gameplay = new LogicalCamera("gameplay");

            using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                mainView.Tick(0.016f);
            }

            _backend.SetBlending(new ViewId(1), isBlending: false);
            mainView.Tick(0.016f);

            Assert.That(FindSwitchRecords(), Is.Empty, "Summary 設定下では Verbose span を書かない");
            Assert.IsNull(AppTelemetry.CurrentSpanId);
        }

        [Test]
        public void TelemetryStartType_CameraSwitch_RoundtripsThroughDebugSocketEnvelope()
        {
            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.CameraSwitch,
                startTimestampUtcTicks: 100,
                endTimestampUtcTicks: 200,
                elapsedMs: 1.5,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: new Metadata(cameraViewId: 3, cameraActiveCameraHash: 4));

            var envelope = DebugTelemetryEnvelopeV1.FromRecord(record);
            Assert.That(envelope.Name, Is.EqualTo("CameraSwitch"));
            Assert.That(envelope.CameraViewId, Is.EqualTo(3));
            Assert.That(envelope.CameraActiveCameraHash, Is.EqualTo(4));
            Assert.That(envelope.SceneFrom, Is.EqualTo(-1));
            Assert.That(envelope.SceneTo, Is.EqualTo(-1));
        }

        [Test]
        public void CameraSystemTelemetryEmitter_EmitSnapshot_WritesCameraMetadataFields()
        {
            var mainView = (CameraView)_system.MainView;
            var gameplay = new LogicalCamera("gameplay");
            using (mainView.Push(gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: true);
                _sink.ClearRecords();

                CameraSystemTelemetryEmitter.EmitSnapshot(_system);

                var record = _sink.Records.Single(r => r.Name == TelemetryStartType.CameraSystemSnapshot);
                Assert.That(record.MetadataValue.CameraTotalViewCount, Is.EqualTo(1));
                Assert.That(record.MetadataValue.CameraBlendingViewCount, Is.EqualTo(1));
                Assert.That(record.MetadataValue.CameraAdditionalViewCount, Is.EqualTo(0));
                Assert.That(record.MetadataValue.CameraMaxStackDepthTotal, Is.EqualTo(1));
                Assert.That(record.MetadataValue.SceneFrom, Is.EqualTo(-1));
                Assert.That(record.MetadataValue.ManagedMem, Is.Zero);
            }
        }

        private System.Collections.Generic.List<TelemetryRecord> FindSwitchRecords()
        {
            return _sink.Records
                .Where(r => r.Name == TelemetryStartType.CameraSwitch)
                .ToList();
        }

        private static CameraPose CreatePose(Vector3 position)
        {
            return new CameraPose
            {
                Position = position,
                Rotation = Quaternion.identity,
                FieldOfViewDegrees = 60f,
                NearClip = 0.3f,
                FarClip = 100f,
                Aspect = 16f / 9f,
            };
        }
    }
}
