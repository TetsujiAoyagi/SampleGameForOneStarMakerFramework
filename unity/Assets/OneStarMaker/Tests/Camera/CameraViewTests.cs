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
    /// CAM-05: CameraView の Push 反映 / Snapshot 確定 / Modifier 適用順 / View 解放と所有権のテスト。
    /// </summary>
    [TestFixture]
    public class CameraViewTests
    {
        private FakeCameraBackend _backend = null!;
        private RuntimeCameraSystem _system = null!;
        private LogicalCamera _gameplay = null!;
        private LogicalCamera _cutscene = null!;
        private CameraView _mainView = null!;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeCameraBackend();
            _system = new RuntimeCameraSystem(_backend);
            _mainView = (CameraView)_system.MainView;
            _gameplay = new LogicalCamera("gameplay");
            _cutscene = new LogicalCamera("cutscene");
            _backend.ClearHistory();
        }

        [Test]
        public void Push_WinnerChanged_BackendReceivesSetActiveWithBlend()
        {
            var blend = new CameraBlendSpec
            {
                DurationSec = 0.75f,
                Easing = CameraBlendEasing.EaseInOut,
            };

            using (_mainView.Push(_gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.ClearHistory();
                using (_mainView.Push(_cutscene, CameraLayer.Cutscene, blend))
                {
                    Assert.That(_backend.SetActiveCalls.Count, Is.EqualTo(1));
                    Assert.That(_backend.SetActiveCalls[0].Camera, Is.SameAs(_cutscene));
                    Assert.That(_backend.SetActiveCalls[0].Blend.DurationSec, Is.EqualTo(0.75f).Within(1e-5f));
                    Assert.That(_backend.SetActiveCalls[0].Blend.Easing, Is.EqualTo(CameraBlendEasing.EaseInOut));
                }
            }
        }

        [Test]
        public void Push_NonWinning_NoBackendCall()
        {
            using (_mainView.Push(_gameplay, CameraLayer.Cutscene, CameraBlendSpec.Cut))
            {
                _backend.ClearHistory();
                using (_mainView.Push(_cutscene, CameraLayer.Gameplay, CameraBlendSpec.Cut))
                {
                    Assert.That(_backend.SetActiveCalls, Is.Empty);
                    Assert.That(_mainView, Is.Not.Null);
                }
            }
        }

        [Test]
        public void Tick_Snapshot_ReflectsBackendCurrentPose()
        {
            var pose = CreatePose(new Vector3(1f, 2f, 3f));
            _backend.SetCurrentPose(new ViewId(1), pose);

            _mainView.Tick(0.016f);

            Assert.That(_mainView.Snapshot.Pose, Is.EqualTo(pose));
        }

        [Test]
        public void InitialSnapshot_UsesFallbackCameraPose()
        {
            var fallback = new LogicalCamera("custom-fallback")
            {
                FieldOfViewDegrees = 45f,
                NearClip = 0.2f,
                FarClip = 500f,
                Aspect = 2f,
            };
            var system = new RuntimeCameraSystem(new FakeCameraBackend(), fallback);
            var view = (CameraView)system.MainView;

            Assert.That(view.Snapshot.Pose.Position, Is.EqualTo(Vector3.zero));
            Assert.That(view.Snapshot.Pose.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(view.Snapshot.Pose.FieldOfViewDegrees, Is.EqualTo(45f));
            Assert.That(view.Snapshot.Pose.NearClip, Is.EqualTo(0.2f));
            Assert.That(view.Snapshot.Pose.FarClip, Is.EqualTo(500f));
            Assert.That(view.Snapshot.Pose.Aspect, Is.EqualTo(2f));
            Assert.That(view.Snapshot.Frustum.ContainsPoint(new Vector3(0f, 0f, 10f)), Is.True);
        }

        [Test]
        public void Tick_FirstSnapshotVelocity_IsZero()
        {
            var pose = CreatePose(new Vector3(10f, 0f, 5f));
            _backend.SetCurrentPose(new ViewId(1), pose);

            _mainView.Tick(0.5f);

            Assert.That(_mainView.Snapshot.Pose, Is.EqualTo(pose));
            Assert.That(_mainView.Snapshot.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Tick_Blending_IncomingSnapshotExposed()
        {
            var currentPose = CreatePose(Vector3.zero);
            var incomingPose = CreatePose(new Vector3(100f, 0f, 0f));

            using (_mainView.Push(_cutscene, CameraLayer.Cutscene, new CameraBlendSpec { DurationSec = 1f }))
            {
                _backend.SetCurrentPose(new ViewId(1), currentPose);
                _backend.SetCameraPose(_cutscene, incomingPose);
                _backend.SetBlending(new ViewId(1), isBlending: true);

                _mainView.Tick(0.016f);

                Assert.That(_mainView.IncomingSnapshot, Is.Not.Null);
                Assert.That(_mainView.IncomingSnapshot!.Value.Pose, Is.EqualTo(incomingPose));
            }
        }

        [Test]
        public void Tick_NotBlending_IncomingSnapshotIsNull()
        {
            using (_mainView.Push(_gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut))
            {
                _backend.SetBlending(new ViewId(1), isBlending: false);
                _mainView.Tick(0.016f);

                Assert.That(_mainView.IncomingSnapshot, Is.Null);
            }
        }

        [Test]
        public void Tick_ModifierApplied_AfterPoseObservation()
        {
            var basePose = CreatePose(Vector3.zero);
            _backend.SetCurrentPose(new ViewId(1), basePose);

            using (_mainView.AddModifier(new FixedOffsetModifier(new Vector3(0f, 3f, 0f))))
            {
                _mainView.Tick(0.016f);

                Assert.That(_backend.PostModifierCalls.Count, Is.EqualTo(1));
                Assert.That(
                    _backend.PostModifierCalls[0].Pose.Position,
                    Is.EqualTo(new Vector3(0f, 3f, 0f)));
                Assert.That(_mainView.Snapshot.Pose.Position, Is.EqualTo(new Vector3(0f, 3f, 0f)));
            }
        }

        [Test]
        public void Tick_Velocity_ComputedAcrossTicks()
        {
            var firstPose = CreatePose(Vector3.zero);
            var secondPose = CreatePose(new Vector3(0f, 0f, 10f));
            const float deltaTime = 0.5f;

            _backend.SetCurrentPose(new ViewId(1), firstPose);
            _mainView.Tick(deltaTime);

            _backend.SetCurrentPose(new ViewId(1), secondPose);
            _mainView.Tick(deltaTime);

            Assert.That(_mainView.Snapshot.Velocity, Is.EqualTo(new Vector3(0f, 0f, 20f)));
        }

        [Test]
        public void CreateView_RenderTextureConfig_HeldByView()
        {
            var config = new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.25f, 0.25f),
                TargetTexture = null,
                UpdateMode = RenderTextureUpdateMode.EveryNFrames,
                UpdateEveryNFrames = 3,
            };

            var view = (CameraView)_system.CreateView(config);

            Assert.That(view.Config.UpdateMode, Is.EqualTo(RenderTextureUpdateMode.EveryNFrames));
            Assert.That(view.Config.UpdateEveryNFrames, Is.EqualTo(3));
            Assert.That(view.Config.ViewportRect, Is.EqualTo(new Rect(0f, 0f, 0.25f, 0.25f)));
        }

        [Test]
        public void Constructor_MainView_RegistersBackendView()
        {
            var backend = new FakeCameraBackend();
            var system = new RuntimeCameraSystem(backend);

            Assert.That(system.MainView, Is.Not.Null);
            Assert.That(backend.RegisterViewCalls.Count, Is.EqualTo(1));
            Assert.That(backend.RegisterViewCalls[0].View, Is.EqualTo(new ViewId(1)));
            Assert.That(backend.RegisterViewCalls[0].Config.ViewportRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(backend.RegisterViewCalls[0].IsMainView, Is.True, "MainView は isMainView=true で登録される");
        }

        [Test]
        public void CreateView_RegistersAdditionalBackendView()
        {
            var config = new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
                UpdateMode = RenderTextureUpdateMode.EveryNFrames,
                UpdateEveryNFrames = 2,
            };

            var view = _system.CreateView(config);

            Assert.That(view, Is.Not.Null);
            Assert.That(_backend.RegisterViewCalls.Count, Is.EqualTo(1));
            Assert.That(_backend.RegisterViewCalls[0].View, Is.EqualTo(new ViewId(2)));
            Assert.That(_backend.RegisterViewCalls[0].Config.UpdateEveryNFrames, Is.EqualTo(2));
            Assert.That(_backend.RegisterViewCalls[0].IsMainView, Is.False, "追加 View は isMainView=false で登録される");
        }

        [Test]
        public void ReleaseView_HandleDisposeAfterRelease_IsSafe()
        {
            var view = (CameraView)_system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });

            var handle = view.Push(_gameplay, CameraLayer.Gameplay, CameraBlendSpec.Cut);
            _system.ReleaseView(view);

            Assert.DoesNotThrow(() => handle.Dispose());
            handle.Dispose();
            Assert.That(handle.IsDisposed, Is.True);
        }

        [Test]
        public void ReleaseView_AdditionalView_ReleasesBackendView()
        {
            var view = _system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });

            _backend.ClearHistory();
            _system.ReleaseView(view);

            Assert.That(_backend.ReleaseViewCalls.Count, Is.EqualTo(1));
            Assert.That(_backend.ReleaseViewCalls[0], Is.EqualTo(new ViewId(2)));
        }

        [Test]
        public void ReleaseView_ExternalView_ThrowsArgumentException()
        {
            var externalView = new ExternalCameraView();

            Assert.That(
                () => _system.ReleaseView(externalView),
                Throws.ArgumentException.With.Property("ParamName").EqualTo("view"));
        }

        [Test]
        public void ReleaseView_ViewFromAnotherSystem_ThrowsArgumentException()
        {
            var otherSystem = new RuntimeCameraSystem(new FakeCameraBackend());
            var otherView = otherSystem.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });

            Assert.That(
                () => _system.ReleaseView(otherView),
                Throws.ArgumentException.With.Property("ParamName").EqualTo("view"));

            Assert.That(
                () => _system.ReleaseView(otherSystem.MainView),
                Throws.ArgumentException.With.Property("ParamName").EqualTo("view"));
        }

        [Test]
        public void ReleaseView_AlreadyReleasedView_ThrowsArgumentException()
        {
            var view = _system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });

            _system.ReleaseView(view);

            Assert.That(
                () => _system.ReleaseView(view),
                Throws.ArgumentException.With.Property("ParamName").EqualTo("view"));
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

        private sealed class FixedOffsetModifier : ICameraPoseModifier
        {
            private readonly Vector3 _offset;

            public FixedOffsetModifier(Vector3 offset) => _offset = offset;

            public bool Apply(ref CameraPose pose, float deltaTime)
            {
                pose = pose.WithPosition(pose.Position + _offset);
                return true;
            }
        }

        private sealed class ExternalCameraView : ICameraView
        {
            public CameraViewSnapshot Snapshot => default;
            public CameraViewSnapshot? IncomingSnapshot => null;

            public CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend)
            {
                throw new System.NotSupportedException();
            }

            public CameraModifierHandle AddModifier(ICameraPoseModifier modifier)
            {
                throw new System.NotSupportedException();
            }
        }
    }
}
