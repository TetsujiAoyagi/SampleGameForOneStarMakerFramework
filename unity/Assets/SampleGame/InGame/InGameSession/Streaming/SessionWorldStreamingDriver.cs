#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// InGameSession 寿命で <see cref="WorldStreamingController"/> を駆動する薄いアダプタ。
    /// ポリシー本体は FW、ここでは Focus 供給・Tick 間引き・観測用スナップショットだけを担う。
    /// </summary>
    /// <remarks>
    /// UpdateSystem Layer への正式編入は後続。T-07 では Session 内の非同期ループで十分。
    /// </remarks>
    public sealed class SessionWorldStreamingDriver : IDisposable
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly WorldStreamingController _controller;
        private readonly Func<Vector3?> _focusProvider;
        private readonly List<string> _residentBuffer = new();
        private CancellationTokenSource? _loopCts;
        private Vector3? _lastTickFocus;
        private bool _disposed;

        /// <summary>
        /// Driver を構築する。Start するまで Tick は回らない。
        /// </summary>
        /// <param name="sceneDirector">Full ティアのメカニズム（AddScene / UnloadScene）。</param>
        /// <param name="focusProvider">注視点。未登録時は null（Tick をスキップ）。</param>
        /// <param name="logger">診断ログ。</param>
        public SessionWorldStreamingDriver(
            SceneDirector sceneDirector,
            Func<Vector3?> focusProvider,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            _ = sceneDirector ?? throw new ArgumentNullException(nameof(sceneDirector));
            _focusProvider = focusProvider ?? throw new ArgumentNullException(nameof(focusProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var config = new StreamingConfig(
                WorldCellCatalog.CreateGridConfig(),
                WorldCellCatalog.GridWidth,
                WorldCellCatalog.GridHeight,
                WorldCellCatalog.LoadRadius,
                WorldCellCatalog.UnloadRadius,
                WorldCellCatalog.MaxInFlight);

            // FW の本実装 Backend。ISceneController 抽象ではなく SceneDirector 具象が必要。
            var backend = new SceneDirectorStreamingBackend(sceneDirector);
            _controller = new WorldStreamingController(config, backend);
            _logger.ZLogInformation(
                $"SessionWorldStreamingDriver ready. grid={WorldCellCatalog.GridWidth}x{WorldCellCatalog.GridHeight} load={WorldCellCatalog.LoadRadius} unload={WorldCellCatalog.UnloadRadius}");
        }

        /// <summary>ポリシー層への参照（テスト・診断用）。</summary>
        public WorldStreamingController Controller => _controller;

        /// <summary>
        /// Tick ループが Start 済みか。
        /// Driver 生成直後（未 Start）は false。Player bootstrap はこれを待つこと。
        /// </summary>
        public bool IsRunning => _loopCts != null && !_disposed;

        /// <summary>Focus が載っているセル identity。グリッド外 / Focus 無しは null。</summary>
        public string? CurrentCellIdentity
        {
            get
            {
                var focus = _focusProvider();
                return focus.HasValue ? WorldCellCatalog.TryGetCellIdentity(focus.Value) : null;
            }
        }

        /// <summary>
        /// Stable 到達済みセル identity のスナップショットを返す。
        /// Backend.IsLoaded（= Stable）をグリッド走査で再照合する（G-6 と同型の観測）。
        /// 内部バッファは再利用するが、戻り値は毎回新規配列にして呼び出し側へのエイリアス漏れを防ぐ。
        /// </summary>
        public IReadOnlyList<string> GetResidentCellIdentities()
        {
            _residentBuffer.Clear();
            for (var x = 0; x < WorldCellCatalog.GridWidth; x++)
            {
                for (var y = 0; y < WorldCellCatalog.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    if (_controller.Backend.IsLoaded(cellId))
                    {
                        _residentBuffer.Add(cellId);
                    }
                }
            }

            return _residentBuffer.Count == 0
                ? Array.Empty<string>()
                : _residentBuffer.ToArray();
        }

        /// <summary>非同期 Tick ループを開始する。二重 Start は無視。</summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (_loopCts != null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            RunLoopAsync(_loopCts.Token).Forget();
            _logger.ZLogInformation($"SessionWorldStreamingDriver loop started");
        }

        /// <summary>ループを止め、以降の Tick を止める。</summary>
        public void Stop()
        {
            if (_loopCts == null)
            {
                return;
            }

            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
            _logger.ZLogInformation($"SessionWorldStreamingDriver loop stopped");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Tick ループのみ止める。進行中の Backend RequestAdd/Remove は SceneDirector 側の
        /// セッションツリー Unload（親再帰）で収束させる（T-06.5: Backend は CancellationToken.None）。
        /// WSC ポリシー本体へ Cancel API を足すのは本スライスの非スコープ。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
        }

        private async UniTaskVoid RunLoopAsync(CancellationToken ct)
        {
            try
            {
                // 初回は Focus が来るまで待つ（Player の RegisterFlight 前に空 Tick しない）。
                await UniTask.WaitUntil(
                    () => _focusProvider().HasValue,
                    cancellationToken: ct);

                while (!ct.IsCancellationRequested)
                {
                    TickOnce();
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(WorldCellCatalog.TickIntervalSeconds),
                        cancellationToken: ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Session teardown
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"SessionWorldStreamingDriver loop failed");
            }
        }

        /// <summary>
        /// 1 回分のポリシー評価。
        /// 間引き: 定周期に加え、Focus が 1/4 セル以上動いたときも即 Tick（正典 §8）。
        /// </summary>
        private void TickOnce()
        {
            var focus = _focusProvider();
            if (!focus.HasValue)
            {
                return;
            }

            var position = focus.Value;
            var movedFar = !_lastTickFocus.HasValue
                || Vector3.Distance(_lastTickFocus.Value, position) >= WorldCellCatalog.CellSize * 0.25f;

            // 定周期ループから呼ばれるため、ここでは常に Tick する。
            // movedFar は将来「イベント駆動 Tick」へ切り替えるときの判定口として残す。
            _ = movedFar;
            _controller.Tick(position);
            _lastTickFocus = position;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionWorldStreamingDriver));
            }
        }
    }
}
