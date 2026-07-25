using Microsoft.Extensions.Logging;
using UnityEngine;
using OneStarMaker.Runtime.SceneSystem;
using ZLogger;

namespace SampleGame.InGame
{
    public class InGameScene : SceneBase
    {
        private readonly ILogger<InGameScene> _logger;

        public InGameScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<InGameScene>();

            _logger.ZLogInformation($"Create InGameScene");
        }
    }
}
