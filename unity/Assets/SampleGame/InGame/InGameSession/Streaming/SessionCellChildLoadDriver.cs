#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.World;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// Cell が Stable になったあと、対応する Environment 子を明示 <c>AddScene</c> するデモ用ドライバ。
    /// WSC / 距離判断には一切触れない（引っ張られないことの実証口）。
    /// </summary>
    /// <remarks>
    /// Unload は親 Cell の再帰破棄に任せる。ここから RequestRemove は出さない。
    /// 子の有無はロード済み Cell の <see cref="SceneResource.Children"/> で判定する
    /// （Map 直参照を避け、SceneQuery 読み取り面だけで完結させる）。
    /// </remarks>
    public sealed class SessionCellChildLoadDriver : IDisposable
    {
        private readonly ISceneController _sceneController;
        private readonly ISceneQuery _sceneQuery;
        private readonly Func<IReadOnlyList<string>> _residentCellsProvider;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly HashSet<string> _inFlightAdds = new(StringComparer.Ordinal);
        private CancellationTokenSource? _loopCts;
        private bool _disposed;

        /// <summary>
        /// デモ用ドライバを構築する。Start するまでポーリングしない。
        /// </summary>
        /// <param name="sceneController">明示 Add のメカニズム。</param>
        /// <param name="sceneQuery">Stable 観測・ロード済み Cell の Children 解決。</param>
        /// <param name="residentCellsProvider">Stable 済み Cell identity のスナップショット供給。</param>
        /// <param name="logger">診断ログ。</param>
        public SessionCellChildLoadDriver(
            ISceneController sceneController,
            ISceneQuery sceneQuery,
            Func<IReadOnlyList<string>> residentCellsProvider,
            Microsoft.Extensions.Logging.ILogger logger)
        {
            _sceneController = sceneController ?? throw new ArgumentNullException(nameof(sceneController));
            _sceneQuery = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
            _residentCellsProvider = residentCellsProvider
                ?? throw new ArgumentNullException(nameof(residentCellsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>ポーリングが Start 済みか（Cancel 済み・Dispose 済みは false）。</summary>
        public bool IsRunning
            => !_disposed && _loopCts is { IsCancellationRequested: false };

        /// <summary>
        /// 常駐 Cell 配下で Stable 到達済みの職種子（Environment_*）identity を列挙する。
        /// HUD 観測用。戻り値は毎回新規配列。
        /// </summary>
        public IReadOnlyList<string> GetLoadedChildIdentities()
        {
            var residents = _residentCellsProvider();
            if (residents.Count == 0)
            {
                return Array.Empty<string>();
            }

            var loaded = new List<string>();
            for (var i = 0; i < residents.Count; i++)
            {
                if (!TryResolveEnvironmentChildId(residents[i], out var envId))
                {
                    continue;
                }

                if (_sceneQuery.IsSceneStable(envId))
                {
                    loaded.Add(envId);
                }
            }

            return loaded.Count == 0 ? Array.Empty<string>() : loaded.ToArray();
        }

        /// <summary>ポーリングループを開始する。二重 Start は無視。</summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (_loopCts != null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            RunLoopAsync(_loopCts.Token).Forget();
            _logger.ZLogInformation($"SessionCellChildLoadDriver loop started");
        }

        /// <summary>
        /// ポーリングを止め、進行中 Add の PreLoad キャンセル窓へ Cancel を届ける。
        /// CTS 自体は <see cref="Dispose"/> まで残し、Stop 直後の Add が
        /// <see cref="CancellationToken.None"/> に落ちないようにする。
        /// </summary>
        public void Stop()
        {
            if (_loopCts == null || _loopCts.IsCancellationRequested)
            {
                return;
            }

            _loopCts.Cancel();
            _logger.ZLogInformation($"SessionCellChildLoadDriver loop stopped");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _loopCts?.Dispose();
            _loopCts = null;
            _inFlightAdds.Clear();
            _disposed = true;
        }

        /// <summary>
        /// AddScene に渡すトークン。未 Start / Stop 済み / Dispose 済みなら
        /// 既にキャンセル済みのトークンを返し、teardown 中の新規 Add を PreLoad で止める。
        /// </summary>
        private CancellationToken GetAddCancellationToken()
        {
            if (_disposed || _loopCts == null || _loopCts.IsCancellationRequested)
            {
                return new CancellationToken(canceled: true);
            }

            return _loopCts.Token;
        }

        private async UniTaskVoid RunLoopAsync(CancellationToken ct)
        {
            try
            {
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
                _logger.ZLogError(ex, $"SessionCellChildLoadDriver loop failed");
            }
        }

        /// <summary>
        /// 1 回分の明示 Add 評価。テストからも呼べる。
        /// </summary>
        public void TickOnce()
        {
            if (!IsRunning)
            {
                return;
            }

            var residents = _residentCellsProvider();
            for (var i = 0; i < residents.Count; i++)
            {
                TryAddEnvironmentForCell(residents[i]);
            }
        }

        private void TryAddEnvironmentForCell(string cellId)
        {
            if (!IsRunning)
            {
                return;
            }

            // resident 供給は Backend.IsLoaded（Stable）前提。念のため Query でも確認する。
            var cellIsStable = _sceneQuery.IsSceneStable(cellId);
            if (!TryResolveEnvironmentChildId(cellId, out var envId))
            {
                // 萌芽のない葉 Cell。何もしない（引っ張られない／子なし）。
                return;
            }

            var childLoaded = _sceneQuery.IsSceneStable(envId);
            if (!CellChildLoadRules.ShouldAddChild(
                    cellIsStable: cellIsStable,
                    childExistsInMap: true,
                    childIsLoaded: childLoaded))
            {
                return;
            }

            if (!_inFlightAdds.Add(envId))
            {
                return;
            }

            // 親 Cell identity も渡し、Add 完了時に親が生きているか再照合する。
            ObserveAddAsync(cellId, envId).Forget();
        }

        /// <summary>
        /// ロード済み Cell の Children に Environment_* があればその identity を返す。
        /// Cell 未ロード、または萌芽未配線なら false。
        /// </summary>
        private bool TryResolveEnvironmentChildId(string cellId, out string envId)
        {
            envId = string.Empty;
            if (!EnvironmentIdentity.TryFromCellId(cellId, out var expectedEnvId))
            {
                return false;
            }

            var cellScene = _sceneQuery.GetLoadedScene(cellId);
            if (cellScene == null)
            {
                return false;
            }

            var children = cellScene.SceneResource.Children;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child != null
                    && string.Equals(child.Identity, expectedEnvId, StringComparison.Ordinal))
                {
                    envId = expectedEnvId;
                    return true;
                }
            }

            return false;
        }

        private async UniTaskVoid ObserveAddAsync(string cellId, string envId)
        {
            var ct = GetAddCancellationToken();
            try
            {
                // Add 直前にもう一度親を見る。WSC が先に Unload していたら親再ロードを起こさない。
                if (!_sceneQuery.IsSceneStable(cellId) || ct.IsCancellationRequested)
                {
                    return;
                }

                _logger.ZLogInformation($"CellChildLoad: explicit AddScene {envId} (parent={cellId})");
                await _sceneController.AddScene(
                    envId,
                    afterOnLoadedTask: null,
                    ct: ct,
                    loadingDisplay: LoadingDisplayType.None);

                // Add 中に親 Cell が Unload された／範囲外になった場合、子が単独で残らないよう明示破棄する。
                // （親再帰 Unload は「既に載っている子」向け。後発 Add の完了とは別経路。）
                if (!_sceneQuery.IsSceneStable(cellId)
                    && _sceneQuery.IsSceneLoaded(envId))
                {
                    _logger.ZLogInformation(
                        $"CellChildLoad: parent {cellId} gone after Add — UnloadScene {envId}");
                    await _sceneController.UnloadScene(envId, LoadingDisplayType.None);
                }
            }
            catch (OperationCanceledException)
            {
                // Driver Stop / Session teardown。PreLoad 窓内キャンセル。
            }
            catch (Exception ex)
            {
                // 失敗は次 Tick の再評価に委ねる（WSC G-6 と同型）。
                _logger.ZLogWarning(ex, $"CellChildLoad: AddScene failed for {envId}");
            }
            finally
            {
                _inFlightAdds.Remove(envId);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionCellChildLoadDriver));
            }
        }
    }
}
