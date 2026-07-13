#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.UpdateSystem;
using OneStarMaker.Foundation.UpdateSystem.World;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using RuntimeCameraSystem = OneStarMaker.Runtime.CameraSystem.Core.CameraSystem;

namespace OneStarMaker.Tests.CameraSystem
{
    /// <summary>
    /// CameraSystemUpdateElement が Unity の MonoBehaviour を介さず、UpdateSystem の 1 tick 内で
    /// Brain 相当の更新から Modifier / Snapshot 更新までを一方向に進めることを検証する。
    /// </summary>
    [TestFixture]
    public sealed class CameraSystemUpdateElementTests
    {
        [Test]
        public void LateUpdate_AdvancesFrameBeforeCameraSystemTick_UsingTheSameDeltaTime()
        {
            var events = new List<string>();
            var frameDriver = new RecordingFrameDriver(events);
            var backend = new FakeCameraBackend
            {
                OnPostModifierApplied = () => events.Add("policy-tick"),
            };
            var system = new RuntimeCameraSystem(backend);
            var element = CameraSystemUpdateElement.CreateForTests(frameDriver, system);
            var coordinator = new UpdateCoordinator();
            var layer = coordinator.GetOrCreateLayer("Camera");
            layer.SetTimeScale(0.5f);
            coordinator.RegisterElement("Camera", element);
            coordinator.ActivatePendingRegistrations();

            coordinator.RunUpdate(deltaTime: 0.5f, unscaledDeltaTime: 0.5f);
            coordinator.RunLateUpdate(deltaTime: 0.5f, unscaledDeltaTime: 0.5f);

            Assert.That(events, Is.EqualTo(new[] { "advance-frame", "policy-tick" }));
            Assert.That(frameDriver.LastFrameIndex, Is.EqualTo(1u));
            Assert.That(frameDriver.LastDeltaTime, Is.EqualTo(0.25f));
            Assert.That(backend.PostModifierCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void Deactivate_StopsFrameDriverAndCameraSystemTickBeforeUnregisterIsApplied()
        {
            var events = new List<string>();
            var frameDriver = new RecordingFrameDriver(events);
            var backend = new FakeCameraBackend
            {
                OnPostModifierApplied = () => events.Add("policy-tick"),
            };
            var system = new RuntimeCameraSystem(backend);
            var element = CameraSystemUpdateElement.CreateForTests(frameDriver, system);
            var context = new UpdateFrameContext(1, 0.1f, 0.1f, 1f, isPaused: false);

            element.Deactivate();
            element.OnElementLateUpdate(context);

            Assert.That(events, Is.Empty);
            Assert.That(backend.PostModifierCalls, Is.Empty);
        }

        [Test]
        public void UpdatePhase_DoesNotAdvanceTheCamera()
        {
            var events = new List<string>();
            var frameDriver = new RecordingFrameDriver(events);
            var backend = new FakeCameraBackend
            {
                OnPostModifierApplied = () => events.Add("policy-tick"),
            };
            var system = new RuntimeCameraSystem(backend);
            var element = CameraSystemUpdateElement.CreateForTests(frameDriver, system);
            var context = new UpdateFrameContext(1, 0.1f, 0.1f, 1f, isPaused: false);

            element.OnElementUpdate(context);

            Assert.That(events, Is.Empty);
            Assert.That(backend.PostModifierCalls, Is.Empty);
        }

        private sealed class RecordingFrameDriver : ICameraFrameDriver
        {
            private readonly List<string> _events;

            public RecordingFrameDriver(List<string> events)
            {
                _events = events;
            }

            public uint LastFrameIndex { get; private set; }
            public float LastDeltaTime { get; private set; }

            public void AdvanceFrame(uint frameIndex, float deltaTime)
            {
                LastFrameIndex = frameIndex;
                LastDeltaTime = deltaTime;
                _events.Add("advance-frame");
            }
        }
    }
}
