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

        public IReadOnlyList<SetActiveCall> SetActiveCalls => _setActiveCalls;
        public IReadOnlyList<PostModifierCall> PostModifierCalls => _postModifierCalls;
        public IReadOnlyList<RegisterViewCall> RegisterViewCalls => _registerViewCalls;
        public IReadOnlyList<ViewId> ReleaseViewCalls => _releaseViewCalls;

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
        }
    }
}
