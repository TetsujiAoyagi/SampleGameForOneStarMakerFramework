#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using OneStarMaker.Runtime.CameraSystem.Cinemachine;
using Unity.Cinemachine;
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
    /// CAM-08: CinemachineCameraBackend / CameraSystemHost の Channel 分離・アクティブ切替・
    /// Scene-authored カメラの保全・RT 更新頻度・DontDestroyOnLoad 配置のテスト（要 EditMode 実カメラ）。
    /// </summary>
    [TestFixture]
    public class CinemachineBackendTests
    {
        private CameraSystemHost _host = null!;
        private CinemachineCameraBackend _backend = null!;

        [SetUp]
        public void SetUp()
        {
            if (CameraSystemHost.Instance != null)
            {
                CameraSystemHost.Instance.Dispose();
            }

            _host = CameraSystemHost.Initialize();
            _backend = new CinemachineCameraBackend(_host);
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Dispose();
            _host = null!;
        }

        [Test]
        public void CreateView_AssignsUniqueChannel_PerView()
        {
            _backend.RegisterView(new ViewId(1), new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 1f, 1f),
            }, isMainView: true);
            _backend.RegisterView(new ViewId(2), new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            }, isMainView: false);

            var channelA = _backend.GetBrainChannelMask(new ViewId(1));
            var channelB = _backend.GetBrainChannelMask(new ViewId(2));

            Assert.That(channelA, Is.Not.EqualTo(channelB));
            Assert.That(channelA, Is.EqualTo(OutputChannels.Default));
            Assert.That(channelB, Is.EqualTo(OutputChannels.Channel01));
        }

        [Test]
        public void CreateView_ExceedsChannelCapacity_Throws()
        {
            for (var i = 1; i <= CameraSystemHost.MaxViewCount; i++)
            {
                _backend.RegisterView(
                    new ViewId(i),
                    new CameraViewConfig { ViewportRect = new Rect(0f, 0f, 0.1f, 0.1f) },
                    isMainView: i == 1);
            }

            Assert.That(
                () => _backend.RegisterView(
                    new ViewId(CameraSystemHost.MaxViewCount + 1),
                    new CameraViewConfig { ViewportRect = Rect.zero },
                    isMainView: false),
                Throws.InvalidOperationException.With.Message.Contains("割当上限"));
        }

        [Test]
        public void SetActiveCamera_OnlyWinnerEnabled_OnItsChannel()
        {
            RegisterMainView();
            var cameraA = _backend.CreateManagedCamera(new ViewId(1), "a");
            var cameraB = _backend.CreateManagedCamera(new ViewId(1), "b");

            _backend.SetActiveCamera(new ViewId(1), cameraA, CameraBlendSpec.Cut);

            Assert.That(_backend.IsCinemachineCameraEnabled(cameraA), Is.True);
            Assert.That(_backend.IsCinemachineCameraEnabled(cameraB), Is.False);
            Assert.That(
                _backend.GetCinemachineOutputChannel(cameraA),
                Is.EqualTo(OutputChannels.Default));

            _backend.SetActiveCamera(new ViewId(1), cameraB, CameraBlendSpec.Cut);

            Assert.That(_backend.IsCinemachineCameraEnabled(cameraA), Is.False);
            Assert.That(_backend.IsCinemachineCameraEnabled(cameraB), Is.True);
        }

        [Test]
        public void SetActiveCamera_OtherViewCameras_Unaffected()
        {
            RegisterMainView();
            _backend.RegisterView(new ViewId(2), new CameraViewConfig
            {
                ViewportRect = new Rect(0.5f, 0f, 0.5f, 1f),
            }, isMainView: false);

            var viewACamera = _backend.CreateManagedCamera(new ViewId(1), "view-a");
            var viewBCamera = _backend.CreateManagedCamera(new ViewId(2), "view-b");
            var viewAOther = _backend.CreateManagedCamera(new ViewId(1), "view-a-other");

            _backend.SetActiveCamera(new ViewId(1), viewACamera, CameraBlendSpec.Cut);
            _backend.SetActiveCamera(new ViewId(2), viewBCamera, CameraBlendSpec.Cut);

            Assert.That(_backend.IsCinemachineCameraEnabled(viewACamera), Is.True);
            Assert.That(_backend.IsCinemachineCameraEnabled(viewAOther), Is.False);
            Assert.That(_backend.IsCinemachineCameraEnabled(viewBCamera), Is.True);

            _backend.SetActiveCamera(new ViewId(1), viewAOther, CameraBlendSpec.Cut);

            Assert.That(_backend.IsCinemachineCameraEnabled(viewACamera), Is.False);
            Assert.That(_backend.IsCinemachineCameraEnabled(viewAOther), Is.True);
            Assert.That(_backend.IsCinemachineCameraEnabled(viewBCamera), Is.True);
        }

        [Test]
        public void SetActiveCamera_BlendSpec_ReachesBrainDefaultBlend()
        {
            RegisterMainView();
            var camera = _backend.CreateManagedCamera(new ViewId(1), "blend-target");

            _backend.SetActiveCamera(
                new ViewId(1),
                camera,
                new CameraBlendSpec
                {
                    DurationSec = 0.75f,
                    Easing = CameraBlendEasing.EaseInOut,
                });

            var brain = _host.Views[new ViewId(1)].Brain;
            Assert.That(brain.DefaultBlend.Style, Is.EqualTo(CinemachineBlendDefinition.Styles.EaseInOut));
            Assert.That(brain.DefaultBlend.Time, Is.EqualTo(0.75f).Within(1e-5f));

            _backend.SetActiveCamera(
                new ViewId(1),
                camera,
                new CameraBlendSpec
                {
                    DurationSec = 0.25f,
                    Easing = CameraBlendEasing.Linear,
                });

            Assert.That(brain.DefaultBlend.Style, Is.EqualTo(CinemachineBlendDefinition.Styles.Linear));
            Assert.That(brain.DefaultBlend.Time, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void GetCurrentPose_AfterPostModifier_ReadsCurrentUnityCameraPose()
        {
            RegisterMainView();

            var modifiedPose = CreatePose(new Vector3(10f, 0f, 0f), 50f);
            _backend.ApplyPostModifier(new ViewId(1), modifiedPose);

            var unityCamera = _host.Views[new ViewId(1)].Camera;
            unityCamera.transform.SetPositionAndRotation(new Vector3(-3f, 2f, 8f), Quaternion.Euler(0f, 45f, 0f));
            unityCamera.fieldOfView = 35f;
            unityCamera.nearClipPlane = 0.1f;
            unityCamera.farClipPlane = 250f;

            var currentPose = _backend.GetCurrentPose(new ViewId(1));

            Assert.That(currentPose.Position, Is.EqualTo(new Vector3(-3f, 2f, 8f)));
            Assert.That(currentPose.Rotation.eulerAngles.y, Is.EqualTo(45f).Within(1e-4f));
            Assert.That(currentPose.FieldOfViewDegrees, Is.EqualTo(35f).Within(1e-5f));
            Assert.That(currentPose.NearClip, Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(currentPose.FarClip, Is.EqualTo(250f).Within(1e-5f));
        }

        [Test]
        public void ReleaseView_ManagedCamera_DestroyedInEditMode()
        {
            _backend.RegisterView(new ViewId(2), new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            }, isMainView: false);
            var camera = _backend.CreateManagedCamera(new ViewId(2), "managed-release");
            var cameraObject = _backend.GetCinemachineCameraGameObject(camera);

            Assert.That(cameraObject, Is.Not.Null);

            _backend.ReleaseView(new ViewId(2));

            Assert.That(cameraObject == null, Is.True);
        }

        [Test]
        public void ReleaseView_SceneAuthoredCamera_DisabledButNotDestroyed()
        {
            _backend.RegisterView(new ViewId(2), new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            }, isMainView: false);

            var sceneObject = new GameObject("SceneAuthoredReleaseCamera");
            try
            {
                var authoredCamera = sceneObject.AddComponent<CinemachineCamera>();
                authoredCamera.OutputChannel = OutputChannels.Channel05;
                authoredCamera.Priority = 42;
                authoredCamera.enabled = true;

                var logical = _backend.WrapSceneAuthoredCamera(new ViewId(2), authoredCamera, "scene-release");
                _backend.SetActiveCamera(new ViewId(2), logical, CameraBlendSpec.Cut);

                Assert.That(authoredCamera.OutputChannel, Is.EqualTo(OutputChannels.Default));
                Assert.That(authoredCamera.enabled, Is.True);

                _backend.ReleaseView(new ViewId(2));

                Assert.That(sceneObject == null, Is.False);
                var releasedCamera = sceneObject!.GetComponent<CinemachineCamera>();
                Assert.That(releasedCamera, Is.Not.Null);
                Assert.That(releasedCamera!.enabled, Is.False);
                Assert.That((int)releasedCamera.Priority, Is.EqualTo(0));
                Assert.That(releasedCamera.OutputChannel, Is.EqualTo(OutputChannels.Channel05));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sceneObject);
            }
        }

        [Test]
        public void WrapSceneAuthoredCamera_RegistersAsLogicalCamera()
        {
            RegisterMainView();

            var sceneObject = new GameObject("SceneAuthoredCamera");
            var authoredCamera = sceneObject.AddComponent<CinemachineCamera>();
            authoredCamera.OutputChannel = OutputChannels.Channel05;

            var logical = _backend.WrapSceneAuthoredCamera(new ViewId(1), authoredCamera, "cutscene-cam");

            Assert.That(logical, Is.Not.Null);
            Assert.That(logical.Id, Is.EqualTo("cutscene-cam"));
            Assert.That(
                _backend.GetCinemachineOutputChannel(logical),
                Is.EqualTo(OutputChannels.Default));

            _backend.SetActiveCamera(new ViewId(1), logical, CameraBlendSpec.Cut);
            Assert.That(_backend.IsCinemachineCameraEnabled(logical), Is.True);

            UnityEngine.Object.DestroyImmediate(sceneObject);
        }

        [Test]
        public void Host_CreatesHierarchy_UnderDontDestroyOnLoad()
        {
            RegisterMainView();

            Assert.That(_host.PersistAcrossScenes, Is.True);
            if (Application.isPlaying)
            {
                Assert.That(_host.Root.scene.name, Is.EqualTo("DontDestroyOnLoad"));
            }

            var viewMain = _host.Root.transform.Find("View_Main");
            Assert.That(viewMain, Is.Not.Null);
            Assert.That(viewMain!.GetComponent<Camera>(), Is.Not.Null);
            Assert.That(viewMain.GetComponent<CinemachineBrain>(), Is.Not.Null);
        }

        [Test]
        public void RtView_UpdateFrequency_SkipsFrames()
        {
            var renderTexture = new RenderTexture(64, 64, 16);
            try
            {
                _backend.RegisterView(new ViewId(1), new CameraViewConfig
                {
                    ViewportRect = new Rect(0f, 0f, 0.25f, 0.25f),
                    TargetTexture = renderTexture,
                    UpdateMode = RenderTextureUpdateMode.EveryNFrames,
                    UpdateEveryNFrames = 3,
                }, isMainView: true);

                const int simulatedFrames = 7;
                for (var i = 0; i < simulatedFrames; i++)
                {
                    _backend.AdvanceRenderScheduling();
                }

                Assert.That(_backend.GetRenderRequestCount(new ViewId(1)), Is.EqualTo(3));
            }
            finally
            {
                if (_host.Views.TryGetValue(new ViewId(1), out var entry))
                {
                    entry.Camera.targetTexture = null;
                }

                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        [Test]
        public void CameraSystem_RegisterView_CreatesHostHierarchy()
        {
            var system = new RuntimeCameraSystem(_backend);

            Assert.That(system.MainView, Is.Not.Null);

            var viewMain = _host.Root.transform.Find("View_Main");
            Assert.That(viewMain, Is.Not.Null);
        }

        [Test]
        public void CameraSystem_CreateAdditionalView_WithCinemachineBackend_DoesNotReuseFallbackBinding()
        {
            var system = new RuntimeCameraSystem(_backend);

            var additionalView = system.CreateView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });

            Assert.That(additionalView, Is.Not.Null);
            Assert.That(_host.Views.ContainsKey(new ViewId(1)), Is.True);
            Assert.That(_host.Views.ContainsKey(new ViewId(2)), Is.True);
            Assert.That(
                _backend.GetBrainChannelMask(new ViewId(1)),
                Is.Not.EqualTo(_backend.GetBrainChannelMask(new ViewId(2))));
        }

        private void RegisterMainView()
        {
            _backend.RegisterView(new ViewId(1), new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 1f, 1f),
            }, isMainView: true);
        }

        private static CameraPose CreatePose(Vector3 position, float fieldOfView)
        {
            return new CameraPose
            {
                Position = position,
                Rotation = Quaternion.identity,
                FieldOfViewDegrees = fieldOfView,
                NearClip = 0.3f,
                FarClip = 100f,
                Aspect = 16f / 9f,
            };
        }
    }
}
