#nullable enable

using System;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Core
{
    /// <summary>
    /// 1 View 分の状態を束ねる中核。CameraStack（どのカメラが勝つか）と CameraModifierStack（確定後の補正）を保持し、
    /// Tick 毎に Backend Pose → Modifier 適用 → Snapshot 確定の順で処理する。
    /// また CameraSwitch テレメトリ span の開始/完了を View 単位で管理する。
    /// </summary>
    public sealed class CameraView : ICameraView
    {
        private readonly ViewId _viewId;
        private readonly ICameraBackend _backend;
        private readonly CameraStack _stack;
        private readonly CameraModifierStack _modifierStack;
        private readonly CameraViewConfig _config;
        private CameraViewSnapshot _snapshot;
        private CameraViewSnapshot? _incomingSnapshot;
        private bool _hasSnapshot;
        private bool _isReleased;
        private TelemetrySpan? _cameraSwitchSpan;
        private bool _awaitingBlendCompletion;

        internal CameraView(
            ViewId viewId,
            ICameraBackend backend,
            LogicalCamera fallbackCamera,
            in CameraViewConfig config)
        {
            _viewId = viewId;
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _config = config;
            _stack = new CameraStack(fallbackCamera ?? throw new ArgumentNullException(nameof(fallbackCamera)));
            _modifierStack = new CameraModifierStack();
            _snapshot = CameraViewSnapshot.CreateInitial(fallbackCamera.ToPose(Vector3.zero, Quaternion.identity));

            _stack.ActiveCameraChanged += OnActiveCameraChanged;
            SyncActiveCameraToBackend(CameraBlendSpec.Cut);
        }

        public CameraViewConfig Config => _config;

        /// <inheritdoc />
        public CameraViewSnapshot Snapshot => _snapshot;

        /// <inheritdoc />
        public CameraViewSnapshot? IncomingSnapshot => _incomingSnapshot;

        internal ViewId ViewId => _viewId;

        internal CameraStack Stack => _stack;

        /// <inheritdoc />
        public CameraStackHandle Push(LogicalCamera camera, CameraLayer layer, in CameraBlendSpec blend)
        {
            ThrowIfReleased();
            return _stack.Push(camera, layer, blend);
        }

        /// <inheritdoc />
        public CameraModifierHandle AddModifier(ICameraPoseModifier modifier)
        {
            ThrowIfReleased();
            return _modifierStack.AddModifier(modifier);
        }

        /// <summary>
        /// 1 フレーム分の Pose を確定する。Backend の基準 Pose に Modifier を掛けて実カメラへ書き戻し、
        /// 前フレーム Pose との差分から速度付き Snapshot を作る。ブレンド完了を検知したら switch span を閉じる。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_isReleased)
            {
                return;
            }

            // Backend の基準 Pose を観測してから Modifier を適用し、最終結果だけを実カメラへ書き戻す。
            var basePose = _backend.GetCurrentPose(_viewId);
            var modifiedPose = basePose;
            _modifierStack.Apply(ref modifiedPose, deltaTime);
            _backend.ApplyPostModifier(_viewId, modifiedPose);

            _snapshot = _hasSnapshot
                ? CameraViewSnapshot.Create(modifiedPose, _snapshot.Pose, deltaTime)
                : CameraViewSnapshot.CreateInitial(modifiedPose);
            _hasSnapshot = true;
            UpdateIncomingSnapshot();
            TryCompleteCameraSwitchSpan();
        }

        internal CameraViewTelemetrySummary CreateTelemetrySummary()
        {
            return new CameraViewTelemetrySummary
            {
                ViewId = _viewId,
                StackDepthTotal = _stack.StackDepthTotal,
                GameplayDepth = _stack.GetLayerDepth(CameraLayer.Gameplay),
                CutsceneDepth = _stack.GetLayerDepth(CameraLayer.Cutscene),
                DebugDepth = _stack.GetLayerDepth(CameraLayer.Debug),
                ActiveCameraId = _stack.ActiveCamera.Id,
                IsBlending = _backend.IsBlending(_viewId),
                HasIncomingSnapshot = _incomingSnapshot.HasValue,
                IsRenderTextureView = _config.TargetTexture != null,
            };
        }

        internal void Release()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            ForceCompleteCameraSwitchSpan(isSuccess: false);
            _stack.ActiveCameraChanged -= OnActiveCameraChanged;
            _stack.Release();
            _modifierStack.Release();
            _backend.ReleaseView(_viewId);
            _incomingSnapshot = null;
        }

        internal bool IsReleased => _isReleased;

        private void OnActiveCameraChanged(ActiveCameraChangeInfo change)
        {
            _backend.SetActiveCamera(_viewId, change.NewCamera, change.BlendSpec);
            BeginCameraSwitchSpan();
        }

        private void SyncActiveCameraToBackend(in CameraBlendSpec blend)
        {
            _backend.SetActiveCamera(_viewId, _stack.ActiveCamera, blend);
        }

        // ブレンド中はブレンド先カメラの Pose を IncomingSnapshot として公開し、SceneStreaming が
        // 遷移完了を待たずに移動先を先読みできるようにする。非ブレンド時は先読み対象が無いので null に落とす。
        private void UpdateIncomingSnapshot()
        {
            if (!_backend.IsBlending(_viewId))
            {
                _incomingSnapshot = null;
                return;
            }

            // ここで問い合わせる ActiveCamera は、直前の ActiveCameraChanged →_backend.SetActiveCamera で
            // 必ず Backend にバインド済み（未知カメラでも SetActiveCamera が binding を生成する）。
            // そのため GetCameraPose の「未バインド時は原点」フォールバックには到達せず、原点の誤検知は起きない。
            var incomingPose = _backend.GetCameraPose(_stack.ActiveCamera);
            _incomingSnapshot = CameraViewSnapshot.CreateInitial(incomingPose);
        }

        private void ThrowIfReleased()
        {
            if (_isReleased)
            {
                throw new ObjectDisposedException(nameof(CameraView));
            }
        }

        // CameraSwitch span は Verbose 時のみ計測する。span は「切替開始〜ブレンド完了」を包み、
        // 完了は後続 Tick の IsBlending=false で検知する（下記 TryComplete）。
        private void BeginCameraSwitchSpan()
        {
            if (_isReleased || AppTelemetry.Level != TelemetryLevel.Verbose)
            {
                return;
            }

            // ブレンド完了前に次の切替が来た場合、前 span を失敗扱いで閉じてから新 span を開始する（span の入れ子を作らない）。
            ForceCompleteCameraSwitchSpan(isSuccess: false);

            _cameraSwitchSpan = AppTelemetry.StartSpan(TelemetryStartType.CameraSwitch, tags: null);
            _awaitingBlendCompletion = _cameraSwitchSpan.HasValue;
        }

        private void TryCompleteCameraSwitchSpan()
        {
            if (!_awaitingBlendCompletion || !_cameraSwitchSpan.HasValue)
            {
                return;
            }

            if (_backend.IsBlending(_viewId))
            {
                return;
            }

            FinishCameraSwitchSpan(isSuccess: true);
        }

        private void ForceCompleteCameraSwitchSpan(bool isSuccess)
        {
            if (!_awaitingBlendCompletion || !_cameraSwitchSpan.HasValue)
            {
                return;
            }

            FinishCameraSwitchSpan(isSuccess);
        }

        private void FinishCameraSwitchSpan(bool isSuccess)
        {
            // カメラ ID 文字列は生では送らず決定的 hash 化して記録する（テレメトリの安定比較とサイズ抑制のため）。
            var metadata = new Metadata(
                cameraViewId: _viewId.Value,
                cameraActiveCameraHash: CameraTelemetryHash.ComputeActiveCameraIdHash(_stack.ActiveCamera.Id));

            AppTelemetry.FinishSpan(
                _cameraSwitchSpan,
                metadata,
                isSuccess: isSuccess,
                level: TelemetryLevel.Verbose);

            _cameraSwitchSpan = null;
            _awaitingBlendCompletion = false;
        }
    }
}
