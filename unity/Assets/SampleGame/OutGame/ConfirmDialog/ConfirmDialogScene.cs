#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;

namespace SampleGame.OutGame.Scenes
{
    /// <summary>
    /// 確認ダイアログシーン。結果通知後に自シーンをアンロードする。
    /// </summary>
    public sealed class ConfirmDialogScene : SceneBase
    {
        public ConfirmDialogScene(SceneResource sceneResource, ISceneQuery sceneQuery)
            : base(sceneResource, sceneQuery)
        {
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
                Debug.LogError("[ConfirmDialogScene] SceneDirector を取得できません。");
                return;
            }

            director.UnloadScene(SceneResource.Identity).Forget();
        }
    }
}
