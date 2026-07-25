#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using ZLogger;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// 確認ダイアログシーン。結果通知後に自シーンをアンロードする。
    /// </summary>
    public sealed class ConfirmDialogScene : SceneBase
    {
        private readonly ILogger<ConfirmDialogScene> _logger;

        public ConfirmDialogScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<ConfirmDialogScene>();
        }

        /// <inheritdoc />
        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            if (UIView is ConfirmDialog.ConfirmDialogView dialogView)
            {
                dialogView.Decided += HandleDecided;
            }

            await UniTask.CompletedTask;
        }

        /// <inheritdoc />
        protected override UniTask OnPreUnLoadedImpl()
        {
            if (UIView is ConfirmDialog.ConfirmDialogView dialogView)
            {
                dialogView.Decided -= HandleDecided;
            }

            return UniTask.CompletedTask;
        }

        private void HandleDecided(bool _)
        {
            if (SceneQuery is not SceneDirector director)
            {
                _logger.ZLogError($"SceneDirector を取得できません。");
                return;
            }

            director.UnloadScene(SceneResource.Identity).Forget();
        }
    }
}
