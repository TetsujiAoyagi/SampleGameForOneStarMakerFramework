using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    public class InGameScene : SceneBase
    {
        private readonly ILogger<InGameScene> _logger;

        public InGameScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            ICameraBackgroundApplier cameraBackgroundApplier,
            ICameraSystem cameraSystem)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            if (cameraBackgroundApplier == null) 
            {
                throw new System.ArgumentNullException (nameof(cameraBackgroundApplier));
            }

            if (cameraSystem == null)
            {
                throw new System.ArgumentNullException(nameof(cameraSystem));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<InGameScene>();

            _logger.ZLogInformation($"Create InGameScene");

            cameraBackgroundApplier.SetClearFlag(cameraSystem.MainView, ClearFlag.Skybox, Color.black);
        }

    }
}
