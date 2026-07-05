#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// HP ゲージ画面シーン。
    /// </summary>
    public sealed class HpGaugeScene : SceneBase
    {
        public HpGaugeScene(SceneResource sceneResource, ISceneQuery sceneQuery)
            : base(sceneResource, sceneQuery)
        {
        }

        /// <inheritdoc />
        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            if (UIView is HpGauge.HpGaugeView hpGaugeView)
            {
                hpGaugeView.OnOpenDialogRequested += HandleOpenDialogRequested;
            }

            await UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override UniTask OnPreUnLoadedImpl()
        {
            if (UIView is HpGauge.HpGaugeView hpGaugeView)
            {
                hpGaugeView.OnOpenDialogRequested -= HandleOpenDialogRequested;
            }

            return UniTask.CompletedTask;
        }

        private void HandleOpenDialogRequested()
        {
            if (SceneQuery is not SceneDirector director)
            {
                Debug.LogError("[HpGaugeScene] SceneDirector を取得できません。");
                return;
            }

            director.AddScene("ConfirmDialog", null, CancellationToken.None).Forget();
        }
    }
}
