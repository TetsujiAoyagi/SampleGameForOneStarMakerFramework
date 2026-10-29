#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.Rendering.Vfx;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// streaming Cell の構造的 child が共有する no-UI lifecycle scene。
    /// 職種（role）は見ない。配置済み VFX の marker があれば VfxSystem に登録する（VFX を置いていない companion では何もしない）。
    /// 局所 VFX の trigger（<see cref="LocalVfxEventTrigger"/>）があれば、その source をこの Scene 所有でロードし、
    /// unload の開始で trigger を全て終わらせる（trigger を置いていない companion では何もしない）。
    /// </summary>
    public sealed class CellCompanionScene : SceneBase
    {
        private readonly ILogger<CellCompanionScene> _logger;
        private readonly IVfxSystem _vfx;
        private readonly ILoggerFactory _loggerFactory;
        private LocalVfxEventBinder? _localVfxEvents;

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
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<CellCompanionScene>();
            _logger.ZLogInformation($"Create CellCompanionScene {sceneResource.Identity}");
        }

        protected sealed override UIView? SearchUIView() => null;

        protected override void OnInitialize()
        {
            // 所有者はこの Scene。Unload の開始で VfxSystem が回収する（GameObject は壊さない）。
            PlacedVfxAdopter.AdoptAll(RootObjects, _vfx, SceneAssetOwner);
            _localVfxEvents = LocalVfxEventBinder.Create(RootObjects, _vfx, _loggerFactory, SceneResource.Identity);
        }

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            if (_localVfxEvents == null)
            {
                return;
            }

            // OnLoadedImpl はキャンセル窓の外なので、この Scene の unload はロードの完了（Stable）まで保留され、
            // ReleaseScene がロードの完了より前に起きない（Scene 所有の asset が誰にも解放されずに残る経路を通らない）。
            // handle は個別に Release しない。解放は Scene の ReleaseScene に任せ、instance の回収（下の EndAll）より後になる。
            await _localVfxEvents.PrepareAllAsync(async address =>
                (await LoadSceneScopedAssetAsync<UnityEngine.Object>(AssetKey.FromAddress(address), ct)).Value);
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            // Unity Scene の unload と source の解放より前に、trigger から起動した instance を回収する。
            _localVfxEvents?.EndAll();
            return UniTask.CompletedTask;
        }
    }
}
