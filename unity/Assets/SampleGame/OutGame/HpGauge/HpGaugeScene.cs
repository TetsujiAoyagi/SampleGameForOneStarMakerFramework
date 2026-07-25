#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// HP ゲージ画面シーン。
    /// </summary>
    public sealed class HpGaugeScene : SceneBase
    {
        public HpGaugeScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

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
            SceneController.AddScene("ConfirmDialog", null, CancellationToken.None).Forget();
        }
    }
}
