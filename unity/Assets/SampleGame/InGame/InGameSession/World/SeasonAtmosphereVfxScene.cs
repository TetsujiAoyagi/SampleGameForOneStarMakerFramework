#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Season 直下の Atmosphere VFX Scene（例: Spring_AtmosphereVFX）の no-UI lifecycle scene。
    /// 配置済み VFX の有効化は Scene 内の PlacedVfxActivator が判定結果を見て行うので、ここは何もしない。
    /// VFX 側の診断ログは Framework が 1 回だけ出す契約なので、ここも生成時の 1 行以外を出さない。
    /// </summary>
    public sealed class SeasonAtmosphereVfxScene : SceneBase
    {
        private readonly ILogger<SeasonAtmosphereVfxScene> _logger;

        public SeasonAtmosphereVfxScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            // Cell の子は CellCompanionScene の担当。streaming の親を許すと分類の誤りが
            // Cell の再帰破棄に乗って見えにくくなるので、ここで早く落とす。
            if (sceneResource.Parent == null || sceneResource.Parent.StreamByDistance)
            {
                throw new ArgumentException("SeasonAtmosphereVfxScene requires a non-streaming Season parent.", nameof(sceneResource));
            }
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger<SeasonAtmosphereVfxScene>();
            _logger.ZLogInformation($"Create SeasonAtmosphereVfxScene {sceneResource.Identity}");
        }

        protected sealed override UIView? SearchUIView() => null;
    }
}
