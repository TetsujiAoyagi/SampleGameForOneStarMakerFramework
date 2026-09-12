#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.Streaming;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using System.Threading;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// InGame セッションの親シーン。
    /// PlayerScene / InGameUI へのサービスハブ。季節枝と両 driver の composition は
    /// <see cref="SessionSeasonController"/> に委譲する。
    /// </summary>
    /// <remarks>
    /// OnLoaded だけが SceneDirector 具象を adapter / backend 工場へ渡す。
    /// OnStabled は Ensure を Forget して即 return する（lifecycle 内で AddScene を await しない）。
    /// OnPreUnload は Dispose のみで drain を await しない。
    /// </remarks>
    public class InGameSession : SceneBase, IInGameSessionServices
    {
        private static readonly IReadOnlyList<string> EmptyResidents = System.Array.Empty<string>();

        private readonly ILogger<InGameSession> _logger;
        private readonly CellCompanionSet _companionSet;
        private SessionSeasonController? _seasonController;
        private SceneDirectorTerminalEvents? _terminalEvents;
        private CancellationTokenSource? _sessionCts;
        private IFlightReadModel? _flight;

        /// <inheritdoc />
        public IFlightReadModel? Flight => _flight;

        /// <inheritdoc />
        public Vector3? FocusWorldPosition => _flight?.Position;

        /// <inheritdoc />
        public string? CurrentCellIdentity => _seasonController?.CurrentCellIdentity;

        /// <inheritdoc />
        public IReadOnlyList<string> ResidentCellIdentities
            => _seasonController?.GetResidentCellIdentities() ?? EmptyResidents;

        /// <inheritdoc />
        public IReadOnlyList<string> LoadedChildSceneIdentities
            => _seasonController?.GetLoadedCompanionIdentities() ?? EmptyResidents;

        /// <inheritdoc />
        public bool IsStreamingActive => _seasonController is { IsStreamingActive: true };

        /// <inheritdoc />
        public bool IsWorldReady => _seasonController is { IsWorldReady: true };

        public InGameSession(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            CellCompanionSet companionSet)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<InGameSession>();
            _companionSet = companionSet;
            _logger.ZLogInformation($"Create InGameSession");
        }

        /// <inheritdoc />
        public UniTask WaitUntilWorldReady(CancellationToken ct)
        {
            if (_seasonController == null)
            {
                throw new System.InvalidOperationException("SessionSeasonController が未生成です。");
            }

            return _seasonController.WaitUntilWorldReady(ct);
        }

        /// <inheritdoc />
        public void StopWorldOperations()
        {
            _seasonController?.StopWorldOperations();
        }

        /// <inheritdoc />
        public void RegisterFlight(IFlightReadModel flight)
        {
            _flight = flight ?? throw new System.ArgumentNullException(nameof(flight));
            _logger.ZLogInformation($"Flight registered for session hub (Focus supplier for Cell Streaming)");
        }

        /// <inheritdoc />
        public void UnregisterFlight(IFlightReadModel flight)
        {
            if (_flight == null || !ReferenceEquals(_flight, flight))
            {
                return;
            }

            _flight = null;
            _logger.ZLogInformation($"Flight unregistered from session hub");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            // SceneDirectorStreamingBackend は SceneDirector 具象を要求する。
            // Composition Root は常に SceneDirector を ISceneController として渡す前提。
            if (SceneController is not SceneDirector sceneDirector)
            {
                throw new System.InvalidOperationException(
                    "InGameSession の季節枝には SceneDirector が必要です。" +
                    $"実際の型: {SceneController.GetType().FullName}");
            }

            _sessionCts = new CancellationTokenSource();
            _terminalEvents = new SceneDirectorTerminalEvents(sceneDirector);
            var backend = new SceneDirectorStreamingBackend(sceneDirector);
            _seasonController = new SessionSeasonController(
                sceneDirector,
                sceneDirector,
                sceneDirector,
                backend,
                _terminalEvents,
                _companionSet,
                () => FocusWorldPosition,
                _logger);

            _logger.ZLogInformation($"[InGameSession] SessionSeasonController created");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            // OnLoaded 中の AddScene は親ロード完了待ちとデッドロックし得る。Ensure は Forget。
            var sessionCt = _sessionCts != null ? _sessionCts.Token : CancellationToken.None;
            _seasonController?.BeginInitialEnsure(sessionCt);
            _logger.ZLogInformation($"[InGameSession] initial season ensure started");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            // 発行済みの await はしない。残留 loading 子は FW のツリー Unload が所有する。
            _seasonController?.Dispose();
            _seasonController = null;
            _terminalEvents?.Dispose();
            _terminalEvents = null;
            if (_sessionCts != null)
            {
                _sessionCts.Cancel();
                _sessionCts.Dispose();
                _sessionCts = null;
            }

            _flight = null;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            return UniTask.CompletedTask;
        }
    }
}
