#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Runtime.CameraSystem;
using UnityEngine;
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
    /// CAM-05 用 FakeBackend。呼び出し履歴と POV / ブレンド状態をテストから決定的に制御する。
    /// </summary>
    public sealed class FakeCameraBackend : ICameraBackend
    {
        public readonly struct SetActiveCall
        {
            public SetActiveCall(ViewId view, LogicalCamera camera, in CameraBlendSpec blend)
            {
                View = view;
                Camera = camera;
                Blend = blend;
            }

            public ViewId View { get; }
            public LogicalCamera Camera { get; }
            public CameraBlendSpec Blend { get; }
        }

        public readonly struct PostModifierCall
        {
            public PostModifierCall(ViewId view, in CameraPose pose)
            {
                View = view;
                Pose = pose;
            }

            public ViewId View { get; }
            public CameraPose Pose { get; }
        }

        public readonly struct RegisterViewCall
        {
            public RegisterViewCall(ViewId view, in CameraViewConfig config, bool isMainView)
            {
                View = view;
                Config = config;
                IsMainView = isMainView;
            }

            public ViewId View { get; }
            public CameraViewConfig Config { get; }
            public bool IsMainView { get; }
        }

        private readonly List<SetActiveCall> _setActiveCalls = new();
        private readonly List<PostModifierCall> _postModifierCalls = new();
        private readonly List<RegisterViewCall> _registerViewCalls = new();
        private readonly List<ViewId> _releaseViewCalls = new();
        private readonly Dictionary<ViewId, CameraPose> _currentPoses = new();
        private readonly Dictionary<LogicalCamera, CameraPose> _cameraPoses = new();
        private readonly Dictionary<ViewId, bool> _blendingStates = new();
        private readonly Dictionary<LogicalCamera, Transform?> _follows = new();
        private readonly Dictionary<LogicalCamera, Transform?> _lookAts = new();
        private readonly Dictionary<LogicalCamera, ViewId> _managedCameras = new();

        public IReadOnlyList<SetActiveCall> SetActiveCalls => _setActiveCalls;
        public IReadOnlyList<PostModifierCall> PostModifierCalls => _postModifierCalls;
        public IReadOnlyList<RegisterViewCall> RegisterViewCalls => _registerViewCalls;
        public IReadOnlyList<ViewId> ReleaseViewCalls => _releaseViewCalls;
        public IReadOnlyDictionary<LogicalCamera, Transform?> FollowTargets => _follows;
        public IReadOnlyDictionary<LogicalCamera, Transform?> LookAtTargets => _lookAts;
        /// <summary>
        /// Tick の相対順序を検証するテスト用フック。
        /// 本番コードの契約ではなく、FakeBackend を使うテストが FrameDriver の完了後に
        /// Modifier 適用まで進んだことを観測するためだけに提供する。
        /// </summary>
        public Action? OnPostModifierApplied { get; set; }

        public void ClearHistory()
        {
            _setActiveCalls.Clear();
            _postModifierCalls.Clear();
            _registerViewCalls.Clear();
            _releaseViewCalls.Clear();
        }

        public void SetCurrentPose(ViewId view, CameraPose pose) => _currentPoses[view] = pose;

        public void SetCameraPose(LogicalCamera camera, CameraPose pose) => _cameraPoses[camera] = pose;

        public void SetBlending(ViewId view, bool isBlending) => _blendingStates[view] = isBlending;

        /// <inheritdoc />
        public void RegisterView(ViewId view, in CameraViewConfig config, bool isMainView)
        {
            _registerViewCalls.Add(new RegisterViewCall(view, config, isMainView));
        }

        /// <inheritdoc />
        public void ReleaseView(ViewId view)
        {
            _releaseViewCalls.Add(view);
        }

        /// <inheritdoc />
        public void SetActiveCamera(ViewId view, LogicalCamera camera, in CameraBlendSpec blend)
        {
            _setActiveCalls.Add(new SetActiveCall(view, camera, blend));
        }

        /// <inheritdoc />
        public CameraPose GetCurrentPose(ViewId view) =>
            _currentPoses.TryGetValue(view, out var pose) ? pose : default;

        /// <inheritdoc />
        public CameraPose GetCameraPose(LogicalCamera camera) =>
            _cameraPoses.TryGetValue(camera, out var pose) ? pose : default;

        /// <inheritdoc />
        public bool IsBlending(ViewId view) =>
            _blendingStates.TryGetValue(view, out var blending) && blending;

        /// <inheritdoc />
        public void ApplyPostModifier(ViewId view, in CameraPose finalPose)
        {
            _postModifierCalls.Add(new PostModifierCall(view, finalPose));
            OnPostModifierApplied?.Invoke();
        }

        /// <inheritdoc />
        public LogicalCamera CreateManagedCamera(ViewId view, string id)
        {
            var logical = new LogicalCamera(id ?? throw new ArgumentNullException(nameof(id)));
            _managedCameras[logical] = view;
            return logical;
        }

        /// <inheritdoc />
        public void SetFollow(LogicalCamera camera, Transform? follow)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _follows[camera] = follow;
        }

        /// <inheritdoc />
        public void SetLookAt(LogicalCamera camera, Transform? lookAt)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _lookAts[camera] = lookAt;
        }

        /// <inheritdoc />
        public void ApplyLens(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            // Fake は Pose 辞書を持たないため、呼び出しが例外なく通ることだけを保証する。
        }

        /// <inheritdoc />
        public void ReleaseManagedCamera(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _managedCameras.Remove(camera);
            _follows.Remove(camera);
            _lookAts.Remove(camera);
        }
    }
}
