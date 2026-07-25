#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using ZLogger;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// HP ゲージ画面シーン。
    /// </summary>
    public sealed class HpGaugeScene : SceneBase
    {
        private readonly ILogger<HpGaugeScene> _logger;

        public HpGaugeScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<HpGaugeScene>();
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
                _logger.ZLogError($"SceneDirector を取得できません。");
                return;
            }

            director.AddScene("ConfirmDialog", null, CancellationToken.None).Forget();
        }
    }
}
