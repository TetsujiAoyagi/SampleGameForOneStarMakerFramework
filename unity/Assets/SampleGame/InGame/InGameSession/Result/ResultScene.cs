#nullable enable

using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using System.Threading;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.Result
{
    /// <summary>
    /// InGame 終了の受け。SwitchScene の前に世界操作の受付だけ止める。
    /// </summary>
    /// <remarks>
    /// drain は lifecycle 内で await しない。遅延 companion Add が Session 終了中に祖先を再ロードしないようにする。
    /// </remarks>
    public class ResultScene : SceneBase
    {
        private readonly ILogger<ResultScene> _logger;

        public ResultScene(
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

            _logger = loggerFactory.CreateLogger<ResultScene>();
            _logger.ZLogInformation($"Create ResultScene");
        }

        protected override async UniTask OnStabledImpl()
        {
            // 同期の受付停止だけ。枝 drain は待たない。
            ResolveSessionServices().StopWorldOperations();
            await ExitInGameScene(CancellationToken.None);
        }

        private IInGameSessionServices ResolveSessionServices()
        {
            var parent = SceneResource.Parent
                ?? throw new System.InvalidOperationException("Result には InGameSession 親が必要です。");

            if (SceneQuery.GetLoadedScene(parent.Identity) is not IInGameSessionServices services)
            {
                throw new System.InvalidOperationException(
                    $"親シーン '{parent.Identity}' は IInGameSessionServices を提供していません。");
            }

            return services;
        }

        private UniTask ExitInGameScene(CancellationToken ct)
        {
            return SceneFlow.EnterOutGame(
                sceneController: SceneController,
                toOutGameScene: SceneIds.Title,
                sceneContext: null,
                ct: ct);
        }
    }
}
