#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Foundation.Telemetry;
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
    /// カメラ制御ポリシーの中枢実装。View 群の生成/解放/Tick を束ね、実描画は <see cref="ICameraBackend"/> に委譲する。
    /// ここには Cinemachine 依存を持ち込まず「いつ・どの View を更新し、いつテレメトリを出すか」だけを決める。
    /// </summary>
    public sealed class CameraSystem : ICameraSystem
    {
        private readonly ICameraBackend _backend;
        private readonly LogicalCamera _fallbackCamera;
        private readonly List<CameraView> _views = new();
        private int _nextViewId = 1;
        private readonly CameraView _mainView;
        private int _telemetrySnapshotCooldown;

        public CameraSystem(ICameraBackend backend, LogicalCamera? fallbackCamera = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _fallbackCamera = fallbackCamera ?? new LogicalCamera("fallback");
            _mainView = CreateViewInternal(
                new CameraViewConfig
                {
                    ViewportRect = new UnityEngine.Rect(0f, 0f, 1f, 1f),
                    UpdateMode = RenderTextureUpdateMode.EveryFrame,
                    UpdateEveryNFrames = 1,
                },
                isMain: true);
        }

        /// <inheritdoc />
        public ICameraView MainView => _mainView;

        internal int TotalViewCount => 1 + _views.Count;

        internal int AdditionalViewCount => _views.Count;

        /// <inheritdoc />
        public ICameraView CreateView(in CameraViewConfig config) =>
            CreateViewInternal(config, isMain: false);

        /// <inheritdoc />
        public LogicalCamera CreateManagedCamera(ICameraView view, string id)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("論理カメラ Id は空にできません。", nameof(id));
            }

            // ViewId は internal のため、Game 層は ICameraView だけを渡し、ここで具象 CameraView へ正規化する。
            if (view is not CameraView cameraView)
            {
                throw new ArgumentException("この CameraSystem が生成した CameraView を指定してください。", nameof(view));
            }

            if (cameraView.IsReleased)
            {
                throw new InvalidOperationException("解放済み View には論理カメラを生成できません。");
            }

            return _backend.CreateManagedCamera(cameraView.ViewId, id);
        }

        /// <inheritdoc />
        public void SetFollow(LogicalCamera camera, UnityEngine.Transform? follow)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _backend.SetFollow(camera, follow);
        }

        /// <inheritdoc />
        public void SetLookAt(LogicalCamera camera, UnityEngine.Transform? lookAt)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _backend.SetLookAt(camera, lookAt);
        }

        /// <inheritdoc />
        public void ApplyLens(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _backend.ApplyLens(camera);
        }

        /// <inheritdoc />
        public void ReleaseManagedCamera(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            _backend.ReleaseManagedCamera(camera);
        }

        /// <summary>
        /// 全 View を 1 フレーム分更新し、周期到来時にテレメトリ snapshot を発行する。
        /// MainView を先に更新してから追加 View を走査する。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過秒数。</param>
        public void Tick(float deltaTime)
        {
            TickView(_mainView, deltaTime);

            for (var i = 0; i < _views.Count; i++)
            {
                TickView(_views[i], deltaTime);
            }

            EmitPeriodicTelemetrySnapshotIfDue();
        }

        /// <summary>
        /// テレメトリ収集用に、生存中の全 View の要約を buffer へ書き出す。
        /// 解放済み View は除外し、MainView は常に含める。
        /// </summary>
        internal void CollectViewTelemetrySummaries(List<CameraViewTelemetrySummary> buffer)
        {
            buffer.Add(_mainView.CreateTelemetrySummary());

            for (var i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                if (!view.IsReleased)
                {
                    buffer.Add(view.CreateTelemetrySummary());
                }
            }
        }

        /// <inheritdoc />
        public void ReleaseView(ICameraView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (ReferenceEquals(view, _mainView))
            {
                throw new InvalidOperationException("MainView は Release できません。");
            }

            if (view is not CameraView cameraView)
            {
                // 外部実装を黙殺すると所有権の取り違えが成功扱いになるため、API 境界で明示的に弾く。
                throw new ArgumentException("この CameraSystem が生成した CameraView を指定してください。", nameof(view));
            }

            if (!_views.Contains(cameraView))
            {
                // MainView、別 System の View、Release 済み View はいずれもこの System の管理対象外として扱う。
                // 二重 Release を no-op にすると所有権バグを隠すため、明示的に失敗させる。
                throw new ArgumentException("この CameraSystem が管理中の追加 CameraView を指定してください。", nameof(view));
            }

            cameraView.Release();
            _views.Remove(cameraView);
        }

        private CameraView CreateViewInternal(in CameraViewConfig config, bool isMain)
        {
            var viewId = new ViewId(_nextViewId++);
            _backend.RegisterView(viewId, config, isMain);
            var view = new CameraView(viewId, _backend, CreateFallbackCameraForView(viewId), config);
            if (!isMain)
            {
                _views.Add(view);
            }

            return view;
        }

        private static void TickView(CameraView view, float deltaTime)
        {
            if (!view.IsReleased)
            {
                view.Tick(deltaTime);
            }
        }

        private void EmitPeriodicTelemetrySnapshotIfDue()
        {
            if (!AppTelemetry.IsEnabled)
            {
                return;
            }

            // snapshot は毎フレームだと過剰なので約 60 Tick に 1 回へ間引く（60fps なら概ね毎秒 1 回）。
            _telemetrySnapshotCooldown++;
            if (_telemetrySnapshotCooldown < 60)
            {
                return;
            }

            _telemetrySnapshotCooldown = 0;
            CameraSystemTelemetryEmitter.EmitSnapshot(this);
        }

        private LogicalCamera CreateFallbackCameraForView(ViewId viewId)
        {
            return new LogicalCamera($"fallback:{viewId.Value}")
            {
                FieldOfViewDegrees = _fallbackCamera.FieldOfViewDegrees,
                NearClip = _fallbackCamera.NearClip,
                FarClip = _fallbackCamera.FarClip,
                Aspect = _fallbackCamera.Aspect,
                CullingMask = _fallbackCamera.CullingMask,
                VolumeProfile = _fallbackCamera.VolumeProfile,
            };
        }
    }
}
