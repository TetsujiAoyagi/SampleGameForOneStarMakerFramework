#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.Player;
using SampleGame.InGame.Streaming;
using System.Threading;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// InGame セッションの親シーン。
    /// PlayerScene / InGameUI へのサービスハブと、Cell Streaming（WSC）所有者を兼ねる。
    /// </summary>
    /// <remarks>
    /// Full ティアは Unity シーンを SceneDirector.AddScene/UnloadScene で載せる戦略（正典 D-1/D-2）。
    /// 距離判断は <see cref="SessionWorldStreamingDriver"/> → FW の WorldStreamingController に集約する。
    /// 職種子（Environment_*）の明示ロードは <see cref="SessionCellChildLoadDriver"/> が別ループで行い、
    /// WSC の desired set には混ぜない（引っ張られないことの実証）。
    /// </remarks>
    public class InGameSession : SceneBase, IInGameSessionServices
    {
        private static readonly IReadOnlyList<string> EmptyResidents = System.Array.Empty<string>();

        private readonly ILogger<InGameSession> _logger;
        private SessionWorldStreamingDriver? _streamingDriver;
        private SessionCellChildLoadDriver? _childLoadDriver;
        private IFlightReadModel? _flight;

        /// <inheritdoc />
        public IFlightReadModel? Flight => _flight;

        /// <inheritdoc />
        public Vector3? FocusWorldPosition => _flight?.Position;

        /// <inheritdoc />
        public string? CurrentCellIdentity => _streamingDriver?.CurrentCellIdentity;

        /// <inheritdoc />
        public IReadOnlyList<string> ResidentCellIdentities
            => _streamingDriver?.GetResidentCellIdentities() ?? EmptyResidents;

        /// <inheritdoc />
        public IReadOnlyList<string> LoadedChildSceneIdentities
            => _childLoadDriver?.GetLoadedChildIdentities() ?? EmptyResidents;

        /// <inheritdoc />
        /// <remarks>
        /// Driver 生成だけでは false。OnStabled で Tick ループが Start された後に true。
        /// Player bootstrap は「セル Add が走り得る状態」をこのフラグで待つ。
        /// </remarks>
        public bool IsStreamingActive => _streamingDriver is { IsRunning: true };

        public InGameSession(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<InGameSession>();
            _logger.ZLogInformation($"Create InGameSession");
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
                    "InGameSession の Cell Streaming には SceneDirector が必要です。" +
                    $"実際の型: {SceneController.GetType().FullName}");
            }

            _streamingDriver = new SessionWorldStreamingDriver(
                sceneDirector,
                () => FocusWorldPosition,
                _logger);

            // 子シーン明示ロードは WSC と別ライフサイクル。距離判断には混ぜない。
            _childLoadDriver = new SessionCellChildLoadDriver(
                sceneDirector,
                sceneDirector,
                () => ResidentCellIdentities,
                _logger);

            _logger.ZLogInformation($"[InGameSession] WorldStreamingDriver + CellChildLoadDriver created");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            // World（NecessaryAlways）は親ロード時に既に載っている。
            // セルの初回 Add は Player が Focus を登録した直後の Driver Tick に任せる。
            // （OnLoaded 中の AddScene は親ロード完了待ちとデッドロックし得るため、ここでも Ensure しない。）
            _streamingDriver?.Start();
            // Cell Stable 後の Environment 明示 Add。Cell Add 瞬間にはまだ走らない（別ループ）。
            _childLoadDriver?.Start();
            _logger.ZLogInformation($"[InGameSession] WorldStreamingDriver + CellChildLoadDriver started");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            _childLoadDriver?.Dispose();
            _childLoadDriver = null;
            _streamingDriver?.Dispose();
            _streamingDriver = null;
            _flight = null;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            return UniTask.CompletedTask;
        }
    }
}
