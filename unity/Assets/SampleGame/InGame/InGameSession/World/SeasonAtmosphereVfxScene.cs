#nullable enable

using System;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Season 直下の Atmosphere VFX Scene（例: Spring_AtmosphereVFX）の no-UI lifecycle scene。
    /// 配置済み VFX は Initialize で VfxSystem に登録し、有効化・時間の追従・回収は VfxSystem に任せる。
    /// VFX 側の診断ログは Framework が 1 回だけ出す契約なので、ここも生成時の 1 行以外を出さない。
    /// </summary>
    public sealed class SeasonAtmosphereVfxScene : SceneBase
    {
        private readonly ILogger<SeasonAtmosphereVfxScene> _logger;
        private readonly IVfxSystem _vfx;

        public SeasonAtmosphereVfxScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            IVfxSystem vfx)
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

            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _logger = loggerFactory.CreateLogger<SeasonAtmosphereVfxScene>();
            _logger.ZLogInformation($"Create SeasonAtmosphereVfxScene {sceneResource.Identity}");
        }

        protected sealed override UIView? SearchUIView() => null;

        protected override void OnInitialize()
        {
            // 所有者はこの Scene。Unload の開始で VfxSystem が回収する（GameObject は壊さない）。
            PlacedVfxAdopter.AdoptAll(RootObjects, _vfx, SceneAssetOwner);
        }
    }
}
