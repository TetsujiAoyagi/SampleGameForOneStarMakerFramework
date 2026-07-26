#nullable enable

using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.Common;
using SampleGame.OutGame.Background;
using System;
using System.Threading;
using ZLogger;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面シーン。
    /// </summary>
    public sealed class TitleScene : SceneBase
    {
        private readonly ILogger<TitleScene> _logger;

        public TitleScene(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<TitleScene>();

            _logger.ZLogInformation($"Create TitleScene");
        }

        protected override void OnInitialize()
        {
            _logger.ZLogInformation($"Initialized.");
        }


        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            _logger.ZLogInformation($"Loaded.");

            if (UIView is TitleView titleView)
            {
                titleView.OnStartRequested += HandleStartRequested;

                if (titleView.BackgroundDefinition != null)
                {
                    RequestParentBackground(titleView.BackgroundDefinition);
                }
            }

            await UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            if (UIView is TitleView titleView)
            {
                titleView.OnStartRequested -= HandleStartRequested;
            }

            return UniTask.CompletedTask;
        }

        private void HandleStartRequested()
        {
            _logger.ZLogInformation($"Start requested.");
            // 投げっぱなし
            SwitchToHomeAsync(CancellationToken.None).Forget();
        }

        private async UniTaskVoid SwitchToHomeAsync(CancellationToken ct)
        {
            try
            {
                await SceneController.SwitchScene(
                    fromSceneIdentify: SceneIds.Title.idToName(),
                    toSceneIdentify: SceneIds.HomeScene.idToName(),
                    ct: ct); // Show キャンセル不要なら None
            }
            catch (OperationCanceledException)
            {
                // Show 中キャンセルだけ。通常は何もしない
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Title → Home 失敗 {ex.Message}");
                // 必要なら UI にエラー表示。再 throw しない（Forget 経路）
            }
        }

        private void RequestParentBackground(OutGameBackgroundDefinition definition)
        {
            var parent = SceneResource.Parent
                ?? throw new System.InvalidOperationException("TitleScene には OutGame 親シーンが必要です。");

            if (SceneQuery.GetLoadedScene(parent.Identity) is not IOutGameBackgroundRequests requests)
            {
                throw new System.InvalidOperationException(
                    $"親シーン '{parent.Identity}' は共有背景要求を提供していません。");
            }

            requests.Request(definition);
        }
    }
}
