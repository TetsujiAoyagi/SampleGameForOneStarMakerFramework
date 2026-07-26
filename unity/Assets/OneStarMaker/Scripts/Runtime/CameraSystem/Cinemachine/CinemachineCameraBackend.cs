#nullable enable

using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Cinemachine
{
    /// <summary>
    /// ポリシー層の指示を Cinemachine Channel / Brain へ翻訳する Backend（正典 §7）。
    /// Cinemachine 型はこのクラスと Host / Tests に閉じ込める（G-2）。
    /// </summary>
    public sealed class CinemachineCameraBackend : ICameraBackend, ICameraFrameDriver
    {
        private readonly CameraSystemHost _host;
        private readonly Dictionary<ViewId, CameraSystemHost.ViewEntry> _viewEntries = new();
        private readonly Dictionary<LogicalCamera, CameraBinding> _bindings = new();
        private readonly Dictionary<ViewId, HashSet<LogicalCamera>> _viewCameras = new();
        private readonly Dictionary<ViewId, LogicalCamera?> _activeCameras = new();
        private int _managedCameraCounter;

        public CinemachineCameraBackend(CameraSystemHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <inheritdoc />
        public void RegisterView(ViewId view, in CameraViewConfig config, bool isMainView)
        {
            if (_viewEntries.ContainsKey(view))
            {
                throw new InvalidOperationException($"ViewId {view.Value} は Backend に既に登録済みです。");
            }

            // 主 View 判定は ViewId の値ではなくポリシー層から渡された isMainView に従う（採番規則との結合を避ける）。
            var entry = _host.CreateView(view, config, isMainView);
            _viewEntries.Add(view, entry);
            _viewCameras[view] = new HashSet<LogicalCamera>();
            _activeCameras[view] = null;
        }

        /// <inheritdoc />
        public void ReleaseView(ViewId view)
        {
            if (!_viewEntries.ContainsKey(view))
            {
                return;
            }

            foreach (var logical in _viewCameras[view])
            {
                if (_bindings.TryGetValue(logical, out var binding))
                {
                    if (binding.IsSceneAuthored)
                    {
                        // Scene-authored camera はシーン所有物なので破棄しない。
                        // ただし View 解放後に同じ Channel が再利用されても別 Brain に拾われないよう、
                        // 無効化を維持したうえでオーサリング時の Channel へ戻す。
                        binding.Camera.enabled = false;
                        binding.Camera.Priority = InactivePriority;
                        binding.Camera.OutputChannel = binding.OriginalOutputChannel;
                    }
                    else
                    {
                        DestroyCameraObject(binding.Camera.gameObject);
                    }
                }

                _bindings.Remove(logical);
            }

            _viewCameras.Remove(view);
            _activeCameras.Remove(view);
            _viewEntries.Remove(view);
            _host.DestroyView(view);
        }

        /// <inheritdoc />
        /// <remarks>
        /// CinemachineCamera を Host 配下に生成し、論理カメラへバインドする。
        /// Game 層は <see cref="ICameraSystem.CreateManagedCamera"/> 経由でのみ到達する（CM 型非露出）。
        /// </remarks>
        public LogicalCamera CreateManagedCamera(ViewId view, string id)
        {
            EnsureViewRegistered(view);
            var logical = new LogicalCamera(id ?? throw new ArgumentNullException(nameof(id)));
            var entry = _viewEntries[view];

            var cameraObject = new GameObject($"CM_{id}");
            cameraObject.transform.SetParent(entry.CinemachineCameraRoot.transform, worldPositionStays: false);
            var cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
            ConfigureCameraForView(cinemachineCamera, entry, logical);
            // CM3 では Follow/LookAt Transform だけでは動かない。Body/Aim パイプラインが必須。
            EnsureTrackingPipeline(cinemachineCamera);
            cinemachineCamera.enabled = false;

            RegisterBinding(logical, new CameraBinding(
                cinemachineCamera,
                view,
                isSceneAuthored: false,
                cinemachineCamera.OutputChannel,
                cinemachineCamera.enabled,
                cinemachineCamera.Priority));
            return logical;
        }

        /// <inheritdoc />
        public void SetFollow(LogicalCamera camera, Transform? follow)
        {
            ResolveBinding(camera).Camera.Follow = follow;
        }

        /// <inheritdoc />
        public void SetLookAt(LogicalCamera camera, Transform? lookAt)
        {
            ResolveBinding(camera).Camera.LookAt = lookAt;
        }

        /// <inheritdoc />
        public void ApplyLens(LogicalCamera camera)
        {
            var binding = ResolveBinding(camera);
            binding.Camera.Lens.FieldOfView = camera.FieldOfViewDegrees;
            binding.Camera.Lens.NearClipPlane = camera.NearClip;
            binding.Camera.Lens.FarClipPlane = camera.FarClip;
        }

        /// <inheritdoc />
        public void ReleaseManagedCamera(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!_bindings.TryGetValue(camera, out var binding))
            {
                return;
            }

            if (_viewCameras.TryGetValue(binding.ViewId, out var set))
            {
                set.Remove(camera);
            }

            if (_activeCameras.TryGetValue(binding.ViewId, out var active) &&
                ReferenceEquals(active, camera))
            {
                _activeCameras[binding.ViewId] = null;
            }

            _bindings.Remove(camera);

            if (binding.IsSceneAuthored)
            {
                // オーサリングカメラはシーン所有。無効化してバインドだけ外す。
                binding.Camera.enabled = false;
                binding.Camera.Priority = InactivePriority;
                binding.Camera.Follow = null;
                binding.Camera.LookAt = null;
            }
            else
            {
                DestroyCameraObject(binding.Camera.gameObject);
            }
        }

        /// <summary>
        /// バインド済み論理カメラを解決する。未生成のまま Follow/LookAt を触ると原因が遠いので明示失敗させる。
        /// </summary>
        private CameraBinding ResolveBinding(LogicalCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException(nameof(camera));
            }

            if (!_bindings.TryGetValue(camera, out var binding))
            {
                throw new InvalidOperationException(
                    $"論理カメラ '{camera.Id}' は Backend に未登録です。CreateManagedCamera または WrapSceneAuthoredCamera 後に呼び出してください。");
            }

            return binding;
        }

        /// <summary>
        /// シーン配置済み CinemachineCamera を論理カメラへラップ登録する（F-9 / G-2 例外）。
        /// ラップ時点で View の OutputChannel へ書き換える。
        /// </summary>
        public LogicalCamera WrapSceneAuthoredCamera(
            ViewId view,
            CinemachineCamera authoredCamera,
            string? id = null)
        {
            if (authoredCamera == null)
            {
                throw new ArgumentNullException(nameof(authoredCamera));
            }

            EnsureViewRegistered(view);
            var entry = _viewEntries[view];
            var originalOutputChannel = authoredCamera.OutputChannel;
            var originalEnabled = authoredCamera.enabled;
            var originalPriority = authoredCamera.Priority;
            authoredCamera.OutputChannel = entry.Channel;

            var logicalId = id ?? authoredCamera.name;
            var logical = new LogicalCamera(logicalId);
            RegisterBinding(logical, new CameraBinding(
                authoredCamera,
                view,
                isSceneAuthored: true,
                originalOutputChannel,
                originalEnabled,
                originalPriority));
            return logical;
        }

        /// <inheritdoc />
        public void SetActiveCamera(ViewId view, LogicalCamera camera, in CameraBlendSpec blend)
        {
            EnsureViewRegistered(view);
            var entry = _viewEntries[view];
            entry.Brain.DefaultBlend = CreateBlendDefinition(blend);

            if (!_bindings.TryGetValue(camera, out var winnerBinding))
            {
                winnerBinding = CreateBindingForUnknownLogicalCamera(view, camera, entry);
            }

            EnsureCameraBelongsToView(winnerBinding, view);
            winnerBinding.Camera.OutputChannel = entry.Channel;

            foreach (var logical in _viewCameras[view])
            {
                if (!_bindings.TryGetValue(logical, out var binding))
                {
                    continue;
                }

                var isWinner = ReferenceEquals(logical, camera);
                binding.Camera.enabled = isWinner;
                binding.Camera.Priority = isWinner ? ActivePriority : InactivePriority;
            }

            _activeCameras[view] = camera;
        }

        /// <inheritdoc />
        public CameraPose GetCurrentPose(ViewId view)
        {
            EnsureViewRegistered(view);
            return ReadUnityCameraPose(_viewEntries[view].Camera);
        }

        /// <inheritdoc />
        public CameraPose GetCameraPose(LogicalCamera camera)
        {
            if (!_bindings.TryGetValue(camera, out var binding))
            {
                return camera.ToPose(Vector3.zero, Quaternion.identity);
            }

            return ReadCinemachinePose(binding.Camera, camera);
        }

        /// <inheritdoc />
        public bool IsBlending(ViewId view)
        {
            EnsureViewRegistered(view);
            return _viewEntries[view].Brain.IsBlending;
        }

        /// <inheritdoc />
        public void ApplyPostModifier(ViewId view, in CameraPose finalPose)
        {
            EnsureViewRegistered(view);

            var unityCamera = _viewEntries[view].Camera;
            unityCamera.transform.SetPositionAndRotation(finalPose.Position, finalPose.Rotation);
            unityCamera.fieldOfView = finalPose.FieldOfViewDegrees;
            unityCamera.nearClipPlane = finalPose.NearClip;
            unityCamera.farClipPlane = finalPose.FarClip;
        }

        /// <summary>
        /// UpdateSystem が所有するカメラフレームを進める。
        /// RenderTexture の有効化判定を先に済ませ、そのフレームに描画する View の Brain も含めて
        /// 同じ frameIndex / deltaTime で手動更新する。ここ以外から ManualUpdate を呼ばないことで、
        /// Brain 出力 → Modifier → Snapshot の順序と「1 render frame に 1 回」の契約を守る。
        /// </summary>
        void ICameraFrameDriver.AdvanceFrame(uint frameIndex, float deltaTime)
        {
            _host.ProcessRenderScheduling();

            // Cinemachine 3.1.7 の ManualUpdate overload は int の frame index を要求する。
            // UpdateSystem は uint を正本にしているため、長時間実行で符号境界を越えても
            // 連続したビット列を渡せる unchecked 変換を使う。
            var cinemachineFrameIndex = unchecked((int)frameIndex);
            foreach (var entry in _viewEntries.Values)
            {
                entry.Brain.ManualUpdate(cinemachineFrameIndex, deltaTime);
            }
        }

        /// <summary>テスト用。Host の RT 更新スケジューリングを 1 フレーム分進める。</summary>
        internal void AdvanceRenderScheduling() => _host.ProcessRenderScheduling();

        /// <summary>テスト用。View の Brain ChannelMask を取得する。</summary>
        internal OutputChannels GetBrainChannelMask(ViewId view)
        {
            EnsureViewRegistered(view);
            return _viewEntries[view].Brain.ChannelMask;
        }

        /// <summary>テスト用。View の Brain が UpdateSystem による ManualUpdate 用に設定されているかを返す。</summary>
        internal CinemachineBrain.UpdateMethods GetBrainUpdateMethod(ViewId view)
        {
            EnsureViewRegistered(view);
            return _viewEntries[view].Brain.UpdateMethod;
        }

        /// <summary>テスト用。論理カメラに紐づく CinemachineCamera の有効状態。</summary>
        internal bool IsCinemachineCameraEnabled(LogicalCamera camera) =>
            _bindings.TryGetValue(camera, out var binding) && binding.Camera.enabled;

        /// <summary>テスト用。論理カメラの OutputChannel。</summary>
        internal OutputChannels GetCinemachineOutputChannel(LogicalCamera camera) =>
            _bindings.TryGetValue(camera, out var binding)
                ? binding.Camera.OutputChannel
                : default;

        /// <summary>テスト用。RT View のレンダリング要求回数。</summary>
        internal int GetRenderRequestCount(ViewId view)
        {
            EnsureViewRegistered(view);
            return _viewEntries[view].RenderRequestCount;
        }

        /// <summary>テスト用。論理カメラに紐づく GameObject。</summary>
        internal GameObject? GetCinemachineCameraGameObject(LogicalCamera camera) =>
            _bindings.TryGetValue(camera, out var binding)
                ? binding.Camera.gameObject
                : null;

        private const int ActivePriority = 100;
        private const int InactivePriority = 0;

        private CameraBinding CreateBindingForUnknownLogicalCamera(
            ViewId view,
            LogicalCamera camera,
            CameraSystemHost.ViewEntry entry)
        {
            _managedCameraCounter++;
            var cameraObject = new GameObject($"CM_auto_{camera.Id}_{_managedCameraCounter}");
            cameraObject.transform.SetParent(entry.CinemachineCameraRoot.transform, worldPositionStays: false);
            var cinemachineCamera = cameraObject.AddComponent<CinemachineCamera>();
            ConfigureCameraForView(cinemachineCamera, entry, camera);
            EnsureTrackingPipeline(cinemachineCamera);
            cinemachineCamera.enabled = false;

            var binding = new CameraBinding(
                cinemachineCamera,
                view,
                isSceneAuthored: false,
                cinemachineCamera.OutputChannel,
                cinemachineCamera.enabled,
                cinemachineCamera.Priority);
            RegisterBinding(camera, binding);
            return binding;
        }

        private void RegisterBinding(LogicalCamera logical, CameraBinding binding)
        {
            _bindings[logical] = binding;
            _viewCameras[binding.ViewId].Add(logical);
        }

        private static void ConfigureCameraForView(
            CinemachineCamera cinemachineCamera,
            CameraSystemHost.ViewEntry entry,
            LogicalCamera logical)
        {
            cinemachineCamera.OutputChannel = entry.Channel;
            cinemachineCamera.Lens.FieldOfView = logical.FieldOfViewDegrees;
            cinemachineCamera.Lens.NearClipPlane = logical.NearClip;
            cinemachineCamera.Lens.FarClipPlane = logical.FarClip;
        }

        /// <summary>
        /// 追従カメラ用の最小パイプラインを付与する。
        /// Cinemachine 3 は Target.Follow/LookAt だけでは Passive のまま動かず、
        /// Body（位置）と Aim（向き）コンポーネントが無いとプレイヤーを追わない。
        /// オフセットは Game 側の Follow Transform 階層に持たせ、ここではゼロオフセットで追従する。
        /// </summary>
        private static void EnsureTrackingPipeline(CinemachineCamera cinemachineCamera)
        {
            if (cinemachineCamera.GetComponent<CinemachineFollow>() == null)
            {
                var follow = cinemachineCamera.gameObject.AddComponent<CinemachineFollow>();
                follow.FollowOffset = Vector3.zero;
            }

            if (cinemachineCamera.GetComponent<CinemachineHardLookAt>() == null)
            {
                cinemachineCamera.gameObject.AddComponent<CinemachineHardLookAt>();
            }
        }

        private static CinemachineBlendDefinition CreateBlendDefinition(in CameraBlendSpec blend)
        {
            if (blend.DurationSec <= 0f)
            {
                return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }

            var style = blend.Easing switch
            {
                CameraBlendEasing.EaseInOut => CinemachineBlendDefinition.Styles.EaseInOut,
                _ => CinemachineBlendDefinition.Styles.Linear,
            };

            return new CinemachineBlendDefinition(style, blend.DurationSec);
        }

        private static CameraPose ReadUnityCameraPose(Camera camera)
        {
            var aspect = camera.aspect > 0f
                ? camera.aspect
                : 16f / 9f;

            return new CameraPose
            {
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
                FieldOfViewDegrees = camera.fieldOfView,
                NearClip = camera.nearClipPlane,
                FarClip = camera.farClipPlane,
                Aspect = aspect,
            };
        }

        private static CameraPose ReadCinemachinePose(CinemachineCamera cinemachineCamera, LogicalCamera logical)
        {
            var lens = cinemachineCamera.Lens;
            return new CameraPose
            {
                Position = cinemachineCamera.transform.position,
                Rotation = cinemachineCamera.transform.rotation,
                FieldOfViewDegrees = lens.FieldOfView,
                NearClip = lens.NearClipPlane,
                FarClip = lens.FarClipPlane,
                Aspect = logical.Aspect > 0f ? logical.Aspect : 16f / 9f,
            };
        }

        private void EnsureViewRegistered(ViewId view)
        {
            if (!_viewEntries.ContainsKey(view))
            {
                throw new InvalidOperationException($"ViewId {view.Value} は Backend に未登録です。");
            }
        }

        private static void EnsureCameraBelongsToView(CameraBinding binding, ViewId view)
        {
            if (binding.ViewId != view)
            {
                throw new InvalidOperationException(
                    $"LogicalCamera は ViewId {binding.ViewId.Value} に属します。ViewId {view.Value} へは切替できません。");
            }
        }

        private static void DestroyCameraObject(GameObject cameraObject)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(cameraObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class CameraBinding
        {
            public CameraBinding(
                CinemachineCamera camera,
                ViewId viewId,
                bool isSceneAuthored,
                OutputChannels originalOutputChannel,
                bool originalEnabled,
                int originalPriority)
            {
                Camera = camera;
                ViewId = viewId;
                IsSceneAuthored = isSceneAuthored;
                OriginalOutputChannel = originalOutputChannel;
                OriginalEnabled = originalEnabled;
                OriginalPriority = originalPriority;
            }

            public CinemachineCamera Camera { get; }
            public ViewId ViewId { get; }
            public bool IsSceneAuthored { get; }
            public OutputChannels OriginalOutputChannel { get; }
            public bool OriginalEnabled { get; }
            public int OriginalPriority { get; }
        }
    }
}
