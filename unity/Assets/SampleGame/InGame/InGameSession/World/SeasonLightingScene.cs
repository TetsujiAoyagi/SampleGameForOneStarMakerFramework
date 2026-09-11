#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 季節 Lighting の空 scaffold。S-4c の lease は書かない。
    /// Factory が未知 identity で null を返さないための受け皿。
    /// </summary>
    public sealed class SeasonLightingScene : SceneBase
    {
        private readonly ILogger<SeasonLightingScene> _logger;

        public SeasonLightingScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<SeasonLightingScene>();
            _logger.ZLogInformation($"Create SeasonLightingScene {sceneResource.Identity}");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct) => UniTask.CompletedTask;

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl SeasonLightingScene {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl() => UniTask.CompletedTask;

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;
    }
}
