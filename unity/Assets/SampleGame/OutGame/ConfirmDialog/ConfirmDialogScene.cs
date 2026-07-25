#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// 確認ダイアログシーン。結果通知後に自シーンをアンロードする。
    /// </summary>
    public sealed class ConfirmDialogScene : SceneBase
    {
        public ConfirmDialogScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
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
            SceneController.UnloadScene(SceneResource.Identity).Forget();
        }
    }
}
