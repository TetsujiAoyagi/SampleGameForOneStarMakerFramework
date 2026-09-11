#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.Streaming
{
    /// <summary>
    /// 季節枝操作の直列化、active 枝、両 driver の生成破棄、発行済み操作の所有、失敗の伝播。
    /// Session 終了時は受付停止だけで drain しない。具象 SceneDirector は見ない。
    /// </summary>
    /// <remarks>
    /// 枝操作は 1 件だけ。操作中の別 Season は待たせず拒否する。同一 Season だけ no-op。
    /// UnloadScene の戻りは寿命終了ではない。捕捉した SceneBase の終端イベントまで待つ。
    /// Add 完了 ≠ Stable。WorldReady は源流 Cell の Stable 後、距離 Tick の Start より前に完了する。
    /// </remarks>
    internal sealed class SessionSeasonController : IDisposable
    {
        private static readonly string[] SeasonIdentities =
        {
            SeasonCellNames.Season("Spring"),
            SeasonCellNames.Season("Summer"),
            SeasonCellNames.Season("Autumn"),
            SeasonCellNames.Season("Winter"),
        };

        private static readonly IReadOnlyList<string> EmptyIdentities = Array.Empty<string>();

        private readonly ISceneQuery _query;
        private readonly ISceneVolumeQuery _volumes;
        private readonly IssuedSceneOperationRegistry _registry = new();
        private readonly SceneTerminalTracker _tracker;
        private readonly IssuedStreamingBackend _streamingBackend;
        private readonly IssuedSceneController _issuedScenes;
        private readonly CellCompanionSet _companionSet;
        private readonly Func<Vector3?> _focusProvider;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly Func<ISceneStreamingBackend, StreamingCandidateSet, SessionWorldStreamingDriver> _streamingDriverFactory;
        private readonly Func<ISceneController, ISceneQuery, Func<IReadOnlyList<string>>, SessionCellCompanionLoadDriver> _companionDriverFactory;
        private readonly UniTaskCompletionSource _worldReady = new();
        private readonly CancellationTokenSource _stopCts = new();

        private SessionWorldStreamingDriver? _streamingDriver;
        private SessionCellCompanionLoadDriver? _companionDriver;
        private SceneBase? _activeSeasonScene;
        private string? _activeSeason;
        private string? _inFlightSeason;
        private bool _branchBusy;
        private bool _stopped;
        private bool _disposed;
        private bool _worldReadySucceeded;

        /// <summary>
        /// 純な枝コントローラを構築する。購読は最初の Add/Unload より前（生成時）に始める。
        /// SceneDirector 具象は受け取らず、adapter / backend / query だけを見る。
        /// </summary>
        internal SessionSeasonController(
            ISceneController sceneController,
            ISceneQuery sceneQuery,
            ISceneVolumeQuery volumeQuery,
            ISceneStreamingBackend streamingBackend,
            ISceneTerminalEvents terminalEvents,
            CellCompanionSet companionSet,
            Func<Vector3?> focusProvider,
            Microsoft.Extensions.Logging.ILogger logger,
            Func<ISceneStreamingBackend, StreamingCandidateSet, SessionWorldStreamingDriver>? streamingDriverFactory = null,
            Func<ISceneController, ISceneQuery, Func<IReadOnlyList<string>>, SessionCellCompanionLoadDriver>? companionDriverFactory = null)
        {
            _query = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
            _volumes = volumeQuery ?? throw new ArgumentNullException(nameof(volumeQuery));
            _companionSet = companionSet;
            _focusProvider = focusProvider ?? throw new ArgumentNullException(nameof(focusProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // 終端購読は Add より前。購読開始前のイベントは replay しない。
            _tracker = new SceneTerminalTracker(terminalEvents, _registry.CompleteInstance);
            _streamingBackend = new IssuedStreamingBackend(this, streamingBackend);
            _issuedScenes = new IssuedSceneController(this, sceneController);
            _streamingDriverFactory = streamingDriverFactory
                ?? ((backend, candidates) => new SessionWorldStreamingDriver(backend, candidates, _focusProvider, _logger));
            _companionDriverFactory = companionDriverFactory
                ?? ((controller, query, residents) =>
                    new SessionCellCompanionLoadDriver(controller, query, residents, _companionSet, _logger));
        }

        /// <summary>初期 Season / 源流 Cell が Stable し、距離 Tick 開始前の準備が終わったか。</summary>
        internal bool IsWorldReady => _worldReadySucceeded;

        /// <summary>距離 Tick が Start 済みか。初期化待ちには使わない。</summary>
        internal bool IsStreamingActive => _streamingDriver is { IsRunning: true };

        /// <summary>active 候補のうち Focus の XZ が体積に入る identity。HUD はこれを使う。</summary>
        internal string? CurrentCellIdentity => _streamingDriver?.CurrentCellIdentity;

        internal IReadOnlyList<string> GetResidentCellIdentities()
            => _streamingDriver?.GetResidentCellIdentities() ?? EmptyIdentities;

        internal IReadOnlyList<string> GetLoadedCompanionIdentities()
            => _companionDriver?.GetLoadedCompanionIdentities() ?? EmptyIdentities;

        /// <summary>テストが発行済み所有を観測する口。本番の子シーンは見ない。</summary>
        internal IssuedSceneOperationRegistry Registry => _registry;

        /// <summary>
        /// WorldReady まで待つ。内部は CompletionSource なので、完了後の呼び出しは同じ結果を即返す。
        /// </summary>
        internal UniTask WaitUntilWorldReady(CancellationToken ct)
            => _worldReady.Task.AttachExternalCancellation(ct);

        /// <summary>
        /// 初期 Spring + 源流 Cell_0_4 を fire-and-forget する。lifecycle フックから await しない。
        /// </summary>
        internal void BeginInitialEnsure(CancellationToken sessionCt)
            => EnsureInitialWorldAsync(sessionCt).Forget();

        /// <summary>
        /// 初期枝を載せる。成功したら WorldReady を完了し、その後に距離 Driver.Start する。
        /// 失敗した Add の FW 回収と例外は待つ。枝全体の issued drain は待たない。
        /// </summary>
        internal async UniTask EnsureInitialWorldAsync(CancellationToken ct)
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stopCts.Token);
                await RequestSeasonAsync("Spring", 0, 4, linked.Token);
                _worldReadySucceeded = true;
                _worldReady.TrySetResult();
                // 距離 Tick は WorldReady より後。Focus 待ちに入るだけで、入力解禁は Player 側。
                _streamingDriver?.Start();
            }
            catch (OperationCanceledException)
            {
                _worldReady.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Season branch ensure failed");
                _worldReady.TrySetException(ex);
                throw;
            }
        }

        /// <summary>
        /// 季節枝の排他入口。操作中の別 Season は拒否。同一 Season は no-op。
        /// 次 Season の Add は、旧枝の発行済み操作と捕捉個体の終端が揃ってから。
        /// </summary>
        internal async UniTask RequestSeasonAsync(string season, int originX, int originY, CancellationToken ct)
        {
            if (_stopped)
            {
                throw new InvalidOperationException("Session 終了後の季節操作は拒否します。");
            }

            var seasonIdentity = SeasonCellNames.Season(season);
            if (string.Equals(_activeSeason, season, StringComparison.Ordinal) && _worldReadySucceeded)
            {
                return;
            }

            if (_branchBusy)
            {
                if (string.Equals(_inFlightSeason, season, StringComparison.Ordinal))
                {
                    return;
                }

                throw new InvalidOperationException("枝操作中の別 Season 要求は拒否します。");
            }

            _branchBusy = true;
            _inFlightSeason = season;
            SceneBase? capturedSeason = null;
            SceneBase? capturedOrigin = null;
            try
            {
                await TearDownActiveBranchAsync(ct);

                capturedSeason = await AddAndCaptureStableAsync(seasonIdentity, ct);
                AssertSingleStableSeason(seasonIdentity);
                // Lighting は親 Add が載せる NecessaryAlways。Game 側で観測個体として登録する。
                RegisterNecessaryAlwaysChildren(capturedSeason);

                var candidates = SeasonCandidateSelection.FromSeasonChildren(
                    capturedSeason.SceneResource.Children,
                    _volumes);
                CreateDrivers(candidates);

                var originIdentity = SeasonCellNames.Cell(season, originX, originY);
                capturedOrigin = await AddAndCaptureStableAsync(originIdentity, ct);

                _companionDriver?.Start();
                _activeSeasonScene = capturedSeason;
                _activeSeason = season;
                // 切替後は既に WorldReady 済みなので距離 Tick を再開する。初回 Ensure は呼び出し側が Start する。
                if (_worldReadySucceeded)
                {
                    _streamingDriver?.Start();
                }
            }
            catch (Exception ex) when (_stopped || ct.IsCancellationRequested)
            {
                _worldReady.TrySetCanceled();
                throw new OperationCanceledException("Season branch canceled.", ex, ct);
            }
            catch
            {
                // 失敗した Add は FW が回収済み。ここでは捕捉できた Season / 源流だけを手放す。
                if (!_stopped)
                {
                    await ReleaseCapturedOnFailureAsync(capturedSeason, capturedOrigin, ct);
                }

                throw;
            }
            finally
            {
                _inFlightSeason = null;
                _branchBusy = false;
            }
        }

        /// <summary>
        /// 新規 Add / 季節切替を同期的に拒否し、発行を止め、WorldReady を cancel する。drain はしない。
        /// </summary>
        internal void StopWorldOperations()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _stopCts.Cancel();
            _streamingDriver?.Stop();
            _companionDriver?.Stop();
            _registry.StopNewIssues();
            _worldReady.TrySetCanceled();
        }

        /// <inheritdoc />
        /// <remarks>
        /// driver と購読を外すだけ。発行済みの await はしない。残留は FW のツリー Unload が所有する。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopWorldOperations();
            _companionDriver?.Dispose();
            _companionDriver = null;
            _streamingDriver?.Dispose();
            _streamingDriver = null;
            _tracker.Dispose();
            _registry.Dispose();
            _stopCts.Dispose();
        }

        private async UniTask TearDownActiveBranchAsync(CancellationToken ct)
        {
            if (_activeSeasonScene == null)
            {
                return;
            }

            var oldSeason = _activeSeasonScene;
            // companion は Stop だけの再 Start ができない（CTS が残る）ので破棄して作り直す。
            _streamingDriver?.Dispose();
            _streamingDriver = null;
            _companionDriver?.Dispose();
            _companionDriver = null;

            await UnloadCapturedAsync(oldSeason, ct);
            await _registry.WaitAllIncomplete(ct);
            _activeSeasonScene = null;
            _activeSeason = null;
        }

        private async UniTask<SceneBase> AddAndCaptureStableAsync(string identity, CancellationToken ct)
        {
            await _issuedScenes.AddScene(
                identity,
                afterOnLoadedTask: null,
                ct: ct);

            // Add の戻りは Stable を保証しない。個体を捕捉してから Query で同一個体の Stable を待つ。
            var captured = _query.GetLoadedScene(identity);
            if (captured == null)
            {
                throw new InvalidOperationException($"AddScene('{identity}') 完了後に個体を捕捉できません。");
            }

            var gen = _tracker.RegisterObservedInstance(captured);
            _registry.BindLatestAdd(identity, gen);

            await UniTask.WaitUntil(
                () => _query.IsSceneStable(identity) && ReferenceEquals(_query.GetLoadedScene(identity), captured),
                cancellationToken: ct);

            return captured;
        }

        private async UniTask UnloadCapturedAsync(SceneBase captured, CancellationToken ct)
        {
            var identity = captured.SceneResource.Identity;
            // Game の登録履歴に未終端個体も未完了 Add も無ければ no-op。FW 辞書は覗かない。
            if (!_tracker.HasLiveInstance(identity) && !_registry.HasIncompleteAdd(identity))
            {
                return;
            }

            await _issuedScenes.UnloadScene(identity);
            // UnloadScene はロード中だと pending して即 return する。寿命終端はイベントで待つ。
            await _tracker.WaitForInstance(captured, ct);
        }

        private async UniTask ReleaseCapturedOnFailureAsync(
            SceneBase? capturedSeason,
            SceneBase? capturedOrigin,
            CancellationToken ct)
        {
            if (capturedOrigin != null && _tracker.HasLiveInstance(capturedOrigin.SceneResource.Identity))
            {
                try
                {
                    await UnloadCapturedAsync(capturedOrigin, ct);
                }
                catch (Exception ex)
                {
                    _logger.ZLogWarning(ex, $"Failed origin unload during ensure recovery");
                }
            }

            if (capturedSeason != null && _tracker.HasLiveInstance(capturedSeason.SceneResource.Identity))
            {
                try
                {
                    await UnloadCapturedAsync(capturedSeason, ct);
                }
                catch (Exception ex)
                {
                    _logger.ZLogWarning(ex, $"Failed season unload during ensure recovery");
                }
            }
        }

        private void RegisterNecessaryAlwaysChildren(SceneBase seasonScene)
        {
            var children = seasonScene.SceneResource.Children;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.LoadType != LoadType.NecessaryAlways || !_query.IsSceneStable(child.Identity))
                {
                    continue;
                }

                var instance = _query.GetLoadedScene(child.Identity);
                if (instance == null)
                {
                    continue;
                }

                var gen = _tracker.RegisterObservedInstance(instance);
                _registry.BindLatestAdd(child.Identity, gen);
            }
        }

        private void CreateDrivers(StreamingCandidateSet candidates)
        {
            _streamingDriver?.Dispose();
            _companionDriver?.Dispose();
            _streamingDriver = _streamingDriverFactory(_streamingBackend, candidates);
            _companionDriver = _companionDriverFactory(
                _issuedScenes,
                _query,
                () => _streamingDriver?.GetResidentCellIdentities() ?? EmptyIdentities);
        }

        private void AssertSingleStableSeason(string expectedIdentity)
        {
            for (var i = 0; i < SeasonIdentities.Length; i++)
            {
                var identity = SeasonIdentities[i];
                if (string.Equals(identity, expectedIdentity, StringComparison.Ordinal))
                {
                    continue;
                }

                if (_query.IsSceneStable(identity))
                {
                    throw new InvalidOperationException(
                        $"Stable な Season が複数あります: '{identity}' と '{expectedIdentity}'。");
                }
            }
        }

        private void ThrowIfCannotIssueAdd(string identity)
        {
            if (_stopped)
            {
                throw new InvalidOperationException($"発行停止後の Add は拒否します: '{identity}'。");
            }
        }

        /// <summary>
        /// 距離 driver 向け decorator。backend を呼ぶ前に発行登録する。
        /// </summary>
        private sealed class IssuedStreamingBackend : ISceneStreamingBackend
        {
            private readonly SessionSeasonController _owner;
            private readonly ISceneStreamingBackend _inner;

            internal IssuedStreamingBackend(SessionSeasonController owner, ISceneStreamingBackend inner)
            {
                _owner = owner;
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public async UniTask RequestAdd(string cellId, int priority)
            {
                _owner.ThrowIfCannotIssueAdd(cellId);
                if (!_owner._registry.TryRegisterAdd(cellId, out var opId))
                {
                    throw new InvalidOperationException($"発行停止後の RequestAdd は拒否します: '{cellId}'。");
                }

                try
                {
                    await _inner.RequestAdd(cellId, priority);
                    var captured = _owner._query.GetLoadedScene(cellId);
                    if (captured != null)
                    {
                        var gen = _owner._tracker.RegisterObservedInstance(captured);
                        _owner._registry.BindInstance(opId, gen);
                    }
                }
                catch
                {
                    // 失敗した Add は FW が回収してから throw する。追加の Game drain はしない。
                    _owner._registry.CompleteOp(opId);
                    throw;
                }
            }

            public async UniTask RequestRemove(string cellId)
            {
                if (!_owner._registry.TryRegisterUnload(cellId, out var opId))
                {
                    throw new InvalidOperationException($"発行停止後の RequestRemove は拒否します: '{cellId}'。");
                }

                try
                {
                    await _inner.RequestRemove(cellId);
                }
                catch
                {
                    _owner._registry.CompleteOp(opId);
                    throw;
                }
            }

            public bool IsLoaded(string cellId) => _inner.IsLoaded(cellId);
        }

        /// <summary>
        /// Ensure / companion 向け decorator。登録してから Add/Unload する。
        /// 既に発行した Add に属する回収 Unload は Stop 後も許可する。
        /// </summary>
        private sealed class IssuedSceneController : ISceneController
        {
            private readonly SessionSeasonController _owner;
            private readonly ISceneController _inner;

            internal IssuedSceneController(SessionSeasonController owner, ISceneController inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public async UniTask AddScene(
                string sceneIdentify,
                Func<UniTask>? afterOnLoadedTask,
                CancellationToken ct,
                SceneContext? context = null,
                IProgress<SceneLoadProgress>? progress = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
                IReadOnlyDictionary<string, string>? telemetryTags = null,
                int priority = 100,
                OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
            {
                _owner.ThrowIfCannotIssueAdd(sceneIdentify);
                if (!_owner._registry.TryRegisterAdd(sceneIdentify, out var opId))
                {
                    throw new InvalidOperationException($"発行停止後の AddScene は拒否します: '{sceneIdentify}'。");
                }

                try
                {
                    await _inner.AddScene(
                        sceneIdentify,
                        afterOnLoadedTask,
                        ct,
                        context,
                        progress,
                        loadingDisplay,
                        telemetryTags,
                        priority,
                        telemetryLevel);
                    _owner._registry.RememberAddOp(sceneIdentify, opId);
                }
                catch
                {
                    _owner._registry.CompleteOp(opId);
                    throw;
                }
            }

            public async UniTask UnloadScene(
                string sceneIdentify,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
                IReadOnlyDictionary<string, string>? telemetryTags = null,
                OneStarMaker.Foundation.Telemetry.TelemetryLevel telemetryLevel = OneStarMaker.Foundation.Telemetry.TelemetryLevel.Summary)
            {
                if (!_owner._registry.TryRegisterUnload(sceneIdentify, out var opId))
                {
                    throw new InvalidOperationException($"発行停止後の UnloadScene は拒否します: '{sceneIdentify}'。");
                }

                if (_owner._tracker.TryGetLiveGeneration(sceneIdentify, out var gen))
                {
                    _owner._registry.BindInstance(opId, gen);
                }

                await _inner.UnloadScene(sceneIdentify, loadingDisplay, telemetryTags, telemetryLevel);
            }

            public UniTask SwitchScene(
                string? fromSceneIdentify,
                string toSceneIdentify,
                CancellationToken ct,
                SceneContext? context = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
                IReadOnlyDictionary<string, string>? telemetryTags = null)
                => _inner.SwitchScene(fromSceneIdentify, toSceneIdentify, ct, context, loadingDisplay, telemetryTags);

            public UniTask GoBack(
                CancellationToken ct,
                SceneContext? context = null,
                LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
                IReadOnlyDictionary<string, string>? telemetryTags = null)
                => _inner.GoBack(ct, context, loadingDisplay, telemetryTags);

            public void ClearHistory() => _inner.ClearHistory();
        }
    }
}
