#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
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
    /// CAM-06: CameraSystem.Tick が MainView と追加 View を更新し、解放済み View を除外することのテスト。
    /// </summary>
    [TestFixture]
    public class CameraSystemTickTests
    {
        private FakeCameraBackend _backend = null!;
        private RuntimeCameraSystem _system = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeCameraBackend();
            _system = new RuntimeCameraSystem(_backend);
        }

        [Test]
        public void CameraSystem_Tick_TicksMainAndAdditionalViews()
        {
            var mainView = (CameraView)_system.MainView;
            var splitView = (CameraView)_system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0.5f, 0f, 0.5f, 1f),
            });

            var mainPose = CreatePose(new Vector3(1f, 0f, 0f));
            var splitPose = CreatePose(new Vector3(2f, 0f, 0f));
            _backend.SetCurrentPose(new ViewId(1), mainPose);
            _backend.SetCurrentPose(new ViewId(2), splitPose);

            _system.Tick(0.016f);

            Assert.That(mainView.Snapshot.Pose, Is.EqualTo(mainPose));
            Assert.That(splitView.Snapshot.Pose, Is.EqualTo(splitPose));
            Assert.That(_backend.PostModifierCalls, Has.Count.EqualTo(2));
        }

        [Test]
        public void CameraSystem_Tick_SkipsReleasedViews()
        {
            var splitView = (CameraView)_system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0.5f, 0f, 0.5f, 1f),
            });

            _backend.SetCurrentPose(new ViewId(1), CreatePose(Vector3.zero));
            _backend.SetCurrentPose(new ViewId(2), CreatePose(new Vector3(100f, 0f, 0f)));

            _system.Tick(0.016f);
            _backend.ClearHistory();

            _system.ReleaseView(splitView);
            _system.Tick(0.016f);

            Assert.That(_backend.PostModifierCalls, Has.Count.EqualTo(1));
            Assert.That(_backend.PostModifierCalls[0].View, Is.EqualTo(new ViewId(1)));
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
