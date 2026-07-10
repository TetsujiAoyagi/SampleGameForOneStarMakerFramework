#nullable enable

using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.Scenes;
using SampleGame.OutGame.Title;

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// SceneResource の Identity から具体的な SceneBase を生成するファクトリ。
    /// DependOnAll に配置し、全 Game 層の SceneBase を知る唯一のクラス。
    /// </summary>
    public sealed class GameSceneFactory : ISceneFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Bootstrap が構成した唯一の <see cref="ILoggerFactory"/> を受け取る。
        /// Game 層で <see cref="OneStarMaker.Foundation.Logging.AppLoggerFactory"/> を再生成すると、
        /// rolling file と DebugSocket への出力経路が分断される。
        /// </summary>
        /// <param name="loggerFactory">アプリ起動時に一度だけ構成された logger factory。</param>
        public GameSceneFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        }

        public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery)
        {
            return sceneResource.Identity switch
            {
                "Title" => new TitleScene(sceneResource, sceneQuery, _loggerFactory),
                "HpGauge" => new HpGaugeScene(sceneResource, sceneQuery, _loggerFactory),
                "ConfirmDialog" => new ConfirmDialogScene(sceneResource, sceneQuery, _loggerFactory),
                _ => null,
            };
        }
    }
}
