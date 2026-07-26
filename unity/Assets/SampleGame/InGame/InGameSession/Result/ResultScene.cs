using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.Result
{
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

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<ResultScene>();

            _logger.ZLogInformation($"Create ResultScene");

        }

        protected override async UniTask OnStabledImpl()
        {
            await exitInGameScene(CancellationToken.None);
        }

        private UniTask exitInGameScene(CancellationToken ct)
        {
            return SceneFlow.EnterOutGame(
                sceneController: SceneController,
                toOutGameScene: SceneIds.Title,
                sceneContext: null,
                ct: ct);
        }
    }
}
