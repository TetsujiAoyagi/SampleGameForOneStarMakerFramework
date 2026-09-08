#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>streaming Cell の構造的 child が共有する no-UI lifecycle scene。</summary>
    public sealed class CellCompanionScene : SceneBase
    {
        private readonly ILogger<CellCompanionScene> _logger;

        public CellCompanionScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (sceneResource.Parent == null || !sceneResource.Parent.StreamByDistance)
            {
                throw new ArgumentException("CellCompanionScene requires a structural streaming Cell parent.", nameof(sceneResource));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<CellCompanionScene>();
            _logger.ZLogInformation($"Create CellCompanionScene {sceneResource.Identity}");
        }

        protected sealed override UIView? SearchUIView() => null;
    }
}
