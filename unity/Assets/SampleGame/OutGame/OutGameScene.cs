using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.Title;
using UnityEngine;

namespace SampleGame.OutGame
{
    public class OutGameScene : SceneBase
    {
        private readonly ILogger<OutGameScene> _logger;
        public OutGameScene(SceneResource sceneResource, ISceneQuery sceneQuery, ILoggerFactory loggerFactory) : base(sceneResource, sceneQuery)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<OutGameScene>();

            _logger.LogInformation($"Create OutGameScene");
        }
    }
}
