#nullable enable

using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.CameraSystem.Hosting;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
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
        private readonly ICameraBackgroundApplier _cameraBackgroundApplier;
        private readonly ICameraSystem _cameraSystem;
        /// <summary>
        /// Bootstrap が構成した唯一の <see cref="ILoggerFactory"/> を受け取る。
        /// Game 層で <see cref="OneStarMaker.Foundation.Logging.AppLoggerFactory"/> を再生成すると、
        /// rolling file と DebugSocket への出力経路が分断される。
        /// </summary>
        /// <param name="loggerFactory">アプリ起動時に一度だけ構成された logger factory。</param>
        public GameSceneFactory(ILoggerFactory loggerFactory, ICameraSystem cameraSystem, ICameraBackgroundApplier backgroundApplier)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
            // OutGame は MainView の背景を初期化するため、CameraSystem と applier は任意依存ではない。
            // null を許して Scene 生成まで遅延させると、初期化失敗後に破棄済み Host を使う事故を
            // 原因から遠い OutGameScene で起こす。Composition Root で失敗を確定させる。
            _cameraSystem = cameraSystem ?? throw new System.ArgumentNullException(nameof(cameraSystem));
            _cameraBackgroundApplier = backgroundApplier ?? throw new System.ArgumentNullException(nameof(backgroundApplier));
        }

        public SceneBase? CreateSceneClass(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
        {
            return sceneResource.Identity switch
            {
                "Title" => new TitleScene(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "OutGameScene" => new OutGame.OutGameScene(sceneResource, sceneQuery, sceneController, _loggerFactory, _cameraBackgroundApplier, _cameraSystem),
                "OutGameSession" => new OutGame.OutGameSession(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "HomeScene" => new OutGame.Home.HomeScene(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "InGameScene" => new InGame.InGameScene(sceneResource, sceneQuery, sceneController, _loggerFactory, _cameraBackgroundApplier, _cameraSystem),
                "InGameSession" => new InGame.InGameSession(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "InGameUI" => new InGame.InGameUI(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "SpringLevel" => new InGame.InGameImplments.SpringLevel(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "SummerLevel" => new InGame.InGameImplments.SummerLevel(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "AutumnLevel" => new InGame.InGameImplments.AutumnLevel(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "WinterLevel" => new InGame.InGameImplments.WinterLevel(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "Result" => new InGame.Result.ResultScene(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "HpGauge" => new HpGaugeScene(sceneResource, sceneQuery, sceneController, _loggerFactory),
                "ConfirmDialog" => new ConfirmDialogScene(sceneResource, sceneQuery, sceneController, _loggerFactory),
                _ => null,
            };
        }
    }
}
