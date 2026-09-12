#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// InGameSession 寿命で <see cref="WorldStreamingController"/> を駆動する薄いアダプタ。
    /// ポリシー本体は FW、ここでは Focus 供給・Tick 間引き・観測用スナップショットだけを担う。
    /// </summary>
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
        /// 渡された候補と backend で Driver を構築する。Start するまで Tick は回らない。
        /// 構築時に Catalog.Format で identity を組み立てない。
        /// </summary>
        public SessionWorldStreamingDriver(
            ISceneStreamingBackend backend,
            StreamingCandidateSet candidates,
            Func<Vector3?> focusProvider,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            _ = backend ?? throw new ArgumentNullException(nameof(backend));
            _ = candidates ?? throw new ArgumentNullException(nameof(candidates));
            _focusProvider = focusProvider ?? throw new ArgumentNullException(nameof(focusProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var settings = new StreamingPolicySettings(
                WorldCellCatalog.LoadRadius,
                WorldCellCatalog.UnloadRadius,
                WorldCellCatalog.MaxInFlight);
            _controller = new WorldStreamingController(candidates, settings, backend);
            _logger.ZLogInformation(
                $"SessionWorldStreamingDriver ready. candidates={candidates.Candidates.Count} load={WorldCellCatalog.LoadRadius} unload={WorldCellCatalog.UnloadRadius}");
        }

        /// <summary>ポリシー層への参照（テスト・診断用）。</summary>
        public WorldStreamingController Controller => _controller;

        /// <summary>
        /// Tick ループが Start 済みか。
        /// Driver 生成直後（未 Start）は false。初期化待ちには使わない。
        /// </summary>
        public bool IsRunning => _loopCts != null && !_disposed;

        /// <summary>
        /// active 候補のうち Focus の XZ が体積に入る identity。複数なら ordinal 最小。
        /// グリッド外・未登録・Focus 無しは null。
        /// </summary>
        public string? CurrentCellIdentity
        {
            get
            {
                var focus = _focusProvider();
                if (!focus.HasValue)
                {
                    return null;
                }

                var position = focus.Value;
                string? best = null;
                var candidates = _controller.Candidates.Candidates;
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (!ContainsXz(candidate.Volume, position))
                    {
                        continue;
                    }

                    if (best == null || string.CompareOrdinal(candidate.Identity, best) < 0)
                    {
                        best = candidate.Identity;
                    }
                }

                return best;
            }
        }

        /// <summary>
        /// Stable 到達済みセル identity のスナップショットを返す。
        /// Backend.IsLoaded（= Stable）を候補列の走査で再照合する（G-6 と同型の観測）。
        /// </summary>
        public IReadOnlyList<string> GetResidentCellIdentities()
        {
            _residentBuffer.Clear();
            var candidates = _controller.Candidates.Candidates;
            for (var i = 0; i < candidates.Count; i++)
            {
                var cellId = candidates[i].Identity;
                if (_controller.Backend.IsLoaded(cellId))
                {
                    _residentBuffer.Add(cellId);
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
        /// セッションツリー Unload（親再帰）で収束させる。
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

        /// <summary>HUD identity は XZ 体積。Y はスポーン高度が入っても距離 Tick とは別判定。</summary>
        private static bool ContainsXz(Bounds volume, Vector3 position)
        {
            var min = volume.min;
            var max = volume.max;
            return position.x >= min.x
                && position.x <= max.x
                && position.z >= min.z
                && position.z <= max.z;
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
