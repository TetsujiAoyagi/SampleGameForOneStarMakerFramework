#nullable enable

using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using SampleGame.Common.TransitionArgs;
using SampleGame.InGame.LevelStreaming;
using SampleGame.InGame.Player;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLogger;

namespace SampleGame.InGame
{
    /// <summary>
    /// InGame セッションの親シーン。
    /// PlayerScene / InGameUI へのサービスハブと、暫定 LevelStreamCoordinator の所有者を兼ねる。
    /// （四季 Level 自体は Cell Streaming 正典へ全面書き直し予定。ここはハブ契約を先に固める。）
    /// </summary>
    public class InGameSession : SceneBase, IInGameSessionServices
    {
        private readonly ILogger<InGameSession> _logger;
        private readonly LevelStreamTransitionBridge _transitionBridge = new();
        private LevelStreamCoordinator<InGameSession>? _coordinator;
        private IFlightReadModel? _flight;

        /// <inheritdoc />
        public LevelStreamCoordinator<InGameSession>? Coordinator => _coordinator;

        /// <inheritdoc />
        public ILevelStreamTransitionFeedback TransitionFeedback => _transitionBridge;

        /// <summary>UI 購読用に具象ブリッジも公開（Shown/Hidden イベント）。</summary>
        public LevelStreamTransitionBridge TransitionBridge => _transitionBridge;

        /// <inheritdoc />
        public IFlightReadModel? Flight => _flight;

        /// <inheritdoc />
        public Vector3? FocusWorldPosition => _flight?.Position;

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
            _logger.ZLogInformation($"Flight registered for session hub");
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

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            var initialLevel = ResolveInitialLevelIdentity();
            if (string.IsNullOrEmpty(initialLevel))
            {
                // 初期 Level が無いと Coordinator を立てられない。Play 起点や SceneFlow の引数を確認する。
                _logger.ZLogWarning($"OnLoadedImpl: initialLevel unresolved — Coordinator not created");
                return;
            }

            // Overlay のランタイム GO は作らない。演出は TransitionBridge → InGameUI MVVM。
            _coordinator = new LevelStreamCoordinator<InGameSession>(
                SceneController,
                SceneQuery,
                _logger,
                _transitionBridge,
                initialLevel);

            _logger.ZLogInformation($"[InGameSession] Coordinator ready. initialLevel={initialLevel}");
            await UniTask.CompletedTask;
        }

        protected override async UniTask OnStabledImpl()
        {
            // 普通にデッドロックしたのでコメントアウト

            // OnDemand Level は親ロードでは載らない。
            // OnLoaded 中の AddScene は親ロード完了待ちとデッドロックし得るため、Stable 後に載せる。
            // Player の Bootstrap は Forget 待機なので、ここで Add すれば解除される。
            // SceneFlow 側の AddScene と二重になっても EnsureLevelLoadedAsync は冪等。
            //if (_coordinator != null)
            //{
            //    var level = _coordinator.CurrentLevelIdentity;
            //    // シーン寿命に紐づく CT が無いため None。AddScene 失敗は例外で親ロードに伝播させる。
            //    await _coordinator.EnsureLevelLoadedAsync(level, CancellationToken.None);
            //    _logger.ZLogInformation($"[InGameSession] Initial level ensured: {level}");
            //}
            await UniTask.CompletedTask;
        }

        private string? ResolveInitialLevelIdentity()
        {
            var args = Context?.GetValueType<InGameArgs>();
            if (args.HasValue)
            {
                var fromArgs = args.Value.TransitionLevel.idToName();
                if (!string.IsNullOrEmpty(fromArgs))
                {
                    return fromArgs;
                }
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && IsSeasonLevelIdentity(activeScene.name))
            {
                return activeScene.name;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                if (IsSeasonLevelIdentity(scene.name))
                {
                    return scene.name;
                }
            }

            _logger.ZLogWarning(
                $"[InGameSession] Initial level could not be resolved. Provide InGameArgs.TransitionLevel or Play from a season level scene in SeasonWorldCatalog.Chain.");
            return null;
        }

        private static bool IsSeasonLevelIdentity(string sceneName)
        {
            return SeasonWorldCatalog.IndexOf(sceneName) >= 0;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            _coordinator?.Dispose();
            _coordinator = null;
            _flight = null;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            return UniTask.CompletedTask;
        }
    }
}
