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
    /// streaming Cell の構造的 child が共有する no-UI lifecycle scene。
    /// 職種（role）は見ない。配置済み VFX の marker があれば VfxSystem に登録する（VFX を置いていない companion では何もしない）。
    /// </summary>
    public sealed class CellCompanionScene : SceneBase
    {
        private readonly ILogger<CellCompanionScene> _logger;
        private readonly IVfxSystem _vfx;

        public CellCompanionScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory,
            IVfxSystem vfx)
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

            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            _logger = loggerFactory.CreateLogger<CellCompanionScene>();
            _logger.ZLogInformation($"Create CellCompanionScene {sceneResource.Identity}");
        }

        protected sealed override UIView? SearchUIView() => null;

        protected override void OnInitialize()
        {
            // 所有者はこの Scene。Unload の開始で VfxSystem が回収する（GameObject は壊さない）。
            PlacedVfxAdopter.AdoptAll(RootObjects, _vfx, SceneAssetOwner);
        }
    }
}
