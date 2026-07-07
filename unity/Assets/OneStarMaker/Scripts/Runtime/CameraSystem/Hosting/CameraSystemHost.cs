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

namespace OneStarMaker.Runtime.CameraSystem.Hosting
{
    /// <summary>
    /// View 用の Unity Camera + CinemachineBrain のヒエラルキーを保持する常駐 Host。
    /// View ごとに一意な Cinemachine OutputChannel を割り当てて Brain 間の混線を防ぎ（Channel 分離）、
    /// 再生中は DontDestroyOnLoad でシーンをまたいで生存する。RenderTexture View の描画スケジューリングもここで駆動する。
    /// </summary>
    public sealed class CameraSystemHost : IDisposable
    {
        // Cinemachine OutputChannel の総数に対応する View 上限（1 View = 1 Channel を占有）。
        internal const int MaxViewCount = 16;

        private static CameraSystemHost? s_instance;

        private readonly GameObject _root;
        private readonly CameraSystemHostDriver _driver;
        private readonly Dictionary<ViewId, ViewEntry> _views = new();
        private readonly List<OutputChannels> _availableChannels = new(MaxViewCount);
        private bool _disposed;

        internal bool PersistAcrossScenes { get; private set; }

        private CameraSystemHost()
        {
            // 割当可能な Channel をプール化しておき、View 生成で払い出し・解放で返却する。
            for (var i = 0; i < MaxViewCount; i++)
            {
                _availableChannels.Add(ResolveChannelBySlotIndex(i));
            }

            _root = new GameObject("[CameraSystemHost]");
            PersistAcrossScenes = true;
            // DontDestroyOnLoad は再生時のみ有効。EditMode テストでは呼べないため分岐する（破棄も DestroyImmediate 側で対応）。
            if (Application.isPlaying)
            {
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            _driver = _root.AddComponent<CameraSystemHostDriver>();
            _driver.Initialize(this);
        }

        /// <summary>常駐 Host を生成する。二重 Initialize は例外（テスト TearDown 後に再生成可）。</summary>
        public static CameraSystemHost Initialize()
        {
            if (s_instance != null)
            {
                throw new InvalidOperationException("CameraSystemHost は既に Initialize 済みです。");
            }

            s_instance = new CameraSystemHost();
            return s_instance;
        }

        /// <summary>テスト用。Initialize 済みインスタンスを返す。未初期化なら null。</summary>
        internal static CameraSystemHost? Instance => s_instance;

        public GameObject Root => _root;

        internal IReadOnlyDictionary<ViewId, ViewEntry> Views => _views;

        /// <summary>View 用の Unity Camera + Brain を Host 配下へ生成する。</summary>
        internal ViewEntry CreateView(ViewId viewId, in CameraViewConfig config, bool isMainView)
        {
            ThrowIfDisposed();

            if (_views.ContainsKey(viewId))
            {
                throw new InvalidOperationException($"ViewId {viewId.Value} は既に登録済みです。");
            }

            if (_availableChannels.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Cinemachine OutputChannel の割当上限 ({MaxViewCount}) に達しました。");
            }

            var channel = _availableChannels[0];
            _availableChannels.RemoveAt(0);

            var viewName = isMainView ? "View_Main" : $"View_{viewId.Value}";
            var viewRoot = new GameObject(viewName);
            viewRoot.transform.SetParent(_root.transform, worldPositionStays: false);

            var unityCamera = viewRoot.AddComponent<Camera>();
            unityCamera.rect = config.ViewportRect;
            unityCamera.targetTexture = config.TargetTexture;
            unityCamera.enabled = config.TargetTexture == null
                                  || config.UpdateMode == RenderTextureUpdateMode.EveryFrame;

            var brain = viewRoot.AddComponent<CinemachineBrain>();
            brain.ChannelMask = channel;

            var entry = new ViewEntry(
                viewId,
                channel,
                viewRoot,
                unityCamera,
                brain,
                config);
            _views.Add(viewId, entry);
            return entry;
        }

        internal void DestroyView(ViewId viewId)
        {
            if (!_views.TryGetValue(viewId, out var entry))
            {
                return;
            }

            // 解放した Channel はプールへ返し、常に昇順を保って次回も低番から再利用する（割当の決定性）。
            _views.Remove(viewId);
            _availableChannels.Add(entry.Channel);
            _availableChannels.Sort(CompareChannels);
            // RT 参照を切ってから GameObject を破棄する（破棄後の RenderTexture 参照残りを避ける）。
            entry.Camera.targetTexture = null;
            DestroyRootObject(entry.Root);
        }

        internal void ProcessRenderScheduling()
        {
            foreach (var entry in _views.Values)
            {
                entry.ProcessRenderScheduling();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _views.Values)
            {
                entry.Camera.targetTexture = null;
            }

            _views.Clear();
            _availableChannels.Clear();

            if (_root != null)
            {
                DestroyRootObject(_root);
            }

            if (ReferenceEquals(s_instance, this))
            {
                s_instance = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CameraSystemHost));
            }
        }

        // 再生時は次フレーム破棄の Destroy、EditMode テストでは即時破棄の DestroyImmediate を使い分ける。
        private static void DestroyRootObject(GameObject root)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static OutputChannels ResolveChannelBySlotIndex(int slotIndex) =>
            slotIndex switch
            {
                0 => OutputChannels.Default,
                1 => OutputChannels.Channel01,
                2 => OutputChannels.Channel02,
                3 => OutputChannels.Channel03,
                4 => OutputChannels.Channel04,
                5 => OutputChannels.Channel05,
                6 => OutputChannels.Channel06,
                7 => OutputChannels.Channel07,
                8 => OutputChannels.Channel08,
                9 => OutputChannels.Channel09,
                10 => OutputChannels.Channel10,
                11 => OutputChannels.Channel11,
                12 => OutputChannels.Channel12,
                13 => OutputChannels.Channel13,
                14 => OutputChannels.Channel14,
                15 => OutputChannels.Channel15,
                _ => throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, null),
            };

        private static int CompareChannels(OutputChannels left, OutputChannels right) =>
            ((int)left).CompareTo((int)right);

        /// <summary>
        /// 1 View 分の Unity 側リソース（Camera / Brain / 割当 Channel / 設定）をまとめ、
        /// RenderTexture View の描画頻度制御を担う。
        /// </summary>
        internal sealed class ViewEntry
        {
            private int _frameCounter;

            public ViewEntry(
                ViewId viewId,
                OutputChannels channel,
                GameObject root,
                Camera camera,
                CinemachineBrain brain,
                in CameraViewConfig config)
            {
                ViewId = viewId;
                Channel = channel;
                Root = root;
                Camera = camera;
                Brain = brain;
                Config = config;
            }

            public ViewId ViewId { get; }
            public OutputChannels Channel { get; }
            public GameObject Root { get; }
            public Camera Camera { get; }
            public CinemachineBrain Brain { get; }
            public CameraViewConfig Config { get; }
            public int RenderRequestCount { get; private set; }

            // RenderTexture View の描画頻度を Config に従って制御する。画面直描画（TargetTexture==null）は対象外。
            public void ProcessRenderScheduling()
            {
                if (Config.TargetTexture == null)
                {
                    return;
                }

                switch (Config.UpdateMode)
                {
                    case RenderTextureUpdateMode.EveryFrame:
                        RequestRender();
                        break;

                    case RenderTextureUpdateMode.EveryNFrames:
                        // interval フレームに 1 回だけ Camera を有効化し、それ以外のフレームは描画を止めて GPU を節約する。
                        _frameCounter++;
                        var interval = Math.Max(1, Config.UpdateEveryNFrames);
                        if ((_frameCounter - 1) % interval == 0)
                        {
                            RequestRender();
                        }
                        else
                        {
                            Camera.enabled = false;
                        }

                        break;

                    case RenderTextureUpdateMode.Manual:
                        // 自動描画しない。呼び出し側が明示的に描画要求する運用。
                        break;
                }
            }

            private void RequestRender()
            {
                RenderRequestCount++;
                Camera.enabled = true;
            }
        }

        // Host 本体は純 C# のため MonoBehaviour の LateUpdate を受け取れない。
        // この Driver を Host root に貼り、毎フレームの RT スケジューリング駆動だけを橋渡しする。
        private sealed class CameraSystemHostDriver : MonoBehaviour
        {
            private CameraSystemHost? _host;

            public void Initialize(CameraSystemHost host) =>
                _host = host ?? throw new ArgumentNullException(nameof(host));

            private void LateUpdate()
            {
                _host?.ProcessRenderScheduling();
            }
        }
    }
}
