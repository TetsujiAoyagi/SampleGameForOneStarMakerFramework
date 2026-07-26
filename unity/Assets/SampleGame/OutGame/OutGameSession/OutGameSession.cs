using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using ZLogger;

namespace SampleGame.OutGame
{
    public class OutGameSession : SceneBase
    {
        private readonly ILogger<OutGameSession> _logger;

        public OutGameSession(
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
            _logger = loggerFactory.CreateLogger<OutGameSession>();

            _logger.ZLogInformation($"Create OutGameSession");

        }
    }
}
