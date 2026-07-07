#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Core;
using OneStarMaker.Runtime.CameraSystem.Effects;
using OneStarMaker.Runtime.CameraSystem.Geometry;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.CameraSystem.Modifiers;
using OneStarMaker.Runtime.CameraSystem.Stacking;
using OneStarMaker.Runtime.CameraSystem.Telemetry;

namespace OneStarMaker.Runtime.CameraSystem.Hosting
{
    /// <summary>
    /// 代表的な View 構成（メイン + 左右分割 2 面 + RT ミニマップ）を一括で組み立てる薄いセットアップ層。
    /// 追加 View の所有権を握り、Dispose でまとめて解放する。SceneStreaming 向け focus 供給元の生成も担う。
    /// </summary>
    /// <remarks>
    /// ここはあくまで組み立て（構成ポリシー）であり、カメラ制御そのものは <see cref="OneStarMaker.Runtime.CameraSystem.Core.CameraSystem"/> が担う。
    /// </remarks>
    public sealed class CameraSystemSliceSetup : IDisposable
    {
        private readonly OneStarMaker.Runtime.CameraSystem.Core.CameraSystem _system;
        private readonly List<ICameraView> _ownedAdditionalViews = new();

        private CameraSystemSliceSetup(
            OneStarMaker.Runtime.CameraSystem.Core.CameraSystem system,
            RenderTexture? minimapRenderTexture)
        {
            _system = system ?? throw new ArgumentNullException(nameof(system));

            SplitViewA = RegisterAdditionalView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.5f, 1f),
            });
            SplitViewB = RegisterAdditionalView(new CameraViewConfig
            {
                ViewportRect = new Rect(0.5f, 0f, 0.5f, 1f),
            });
            // ミニマップは RT 出力で毎フレーム描く必要がないため 2 フレームに 1 回へ間引く。
            MinimapView = RegisterAdditionalView(new CameraViewConfig
            {
                ViewportRect = new Rect(0f, 0f, 0.25f, 0.25f),
                TargetTexture = minimapRenderTexture,
                UpdateMode = RenderTextureUpdateMode.EveryNFrames,
                UpdateEveryNFrames = 2,
            });
        }

        public OneStarMaker.Runtime.CameraSystem.Core.CameraSystem System => _system;

        public ICameraView MainView => _system.MainView;

        public ICameraView SplitViewA { get; }

        public ICameraView SplitViewB { get; }

        public ICameraView MinimapView { get; }

        /// <summary>
        /// Backend を指定して CameraSystem と標準 View 一式を構築する。ミニマップ RT と fallback カメラは任意。
        /// </summary>
        public static CameraSystemSliceSetup Create(
            ICameraBackend backend,
            RenderTexture? minimapRenderTexture = null,
            LogicalCamera? fallbackCamera = null)
        {
            var system = new OneStarMaker.Runtime.CameraSystem.Core.CameraSystem(backend, fallbackCamera);
            return new CameraSystemSliceSetup(system, minimapRenderTexture);
        }

        /// <summary>
        /// SceneStreaming に渡す focus 供給元を構成する。分割 2 面は包含し、RT ミニマップは注視点として不適切なため除外する。
        /// </summary>
        /// <param name="includeMainView">true のとき MainView も先頭に含める。既定は分割 2 面のみを注視点とする。</param>
        public IReadOnlyList<CameraFocusSource> CreateStreamingFocusSources(bool includeMainView = false)
        {
            var sources = new List<CameraFocusSource>(includeMainView ? 4 : 3);

            if (includeMainView)
            {
                sources.Add(new CameraFocusSource { View = MainView, IncludeInStreaming = true });
            }

            sources.Add(new CameraFocusSource { View = SplitViewA, IncludeInStreaming = true });
            sources.Add(new CameraFocusSource { View = SplitViewB, IncludeInStreaming = true });
            sources.Add(new CameraFocusSource { View = MinimapView, IncludeInStreaming = false });
            return sources;
        }

        public CameraStackHandle PushCutscene(
            ICameraView view,
            LogicalCamera camera,
            in CameraBlendSpec blend) =>
            view.Push(camera, CameraLayer.Cutscene, blend);

        public CameraModifierHandle AddShake(
            ICameraView view,
            float durationSec,
            Vector3 amplitude) =>
            view.AddModifier(new ShakeModifier(amplitude, durationSec));

        public void Tick(float deltaTime) => _system.Tick(deltaTime);

        /// <inheritdoc />
        public void Dispose()
        {
            for (var i = _ownedAdditionalViews.Count - 1; i >= 0; i--)
            {
                _system.ReleaseView(_ownedAdditionalViews[i]);
            }

            _ownedAdditionalViews.Clear();
        }

        private ICameraView RegisterAdditionalView(in CameraViewConfig config)
        {
            var view = _system.CreateView(config);
            _ownedAdditionalViews.Add(view);
            return view;
        }
    }
}
