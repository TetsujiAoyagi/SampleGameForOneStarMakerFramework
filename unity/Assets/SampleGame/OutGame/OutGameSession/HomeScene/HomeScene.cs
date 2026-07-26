using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using SampleGame.Common.TransitionArgs;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ZLogger;

namespace SampleGame.OutGame.Home
{
    public class HomeScene : SceneBase
    {
        private readonly ILogger<HomeScene> _logger;

        public HomeScene(
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

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<HomeScene>();

            _logger.ZLogInformation($"Create HomeScene");

        }


        protected override async UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl HomeScene");
            await SceneFlow.EnterInGame(
                sceneController: SceneController,
                toInGameScene: SceneIds.SpringLevel,
                CancellationToken.None);
        }
    }
}
