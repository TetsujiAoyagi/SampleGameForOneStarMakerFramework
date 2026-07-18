#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.Background;
using ZLogger;

namespace SampleGame.OutGame.Title
{
    /// <summary>
    /// タイトル画面シーン。
    /// </summary>
    public sealed class TitleScene : SceneBase
    {
        private readonly ILogger<TitleScene> _logger;

        public TitleScene(SceneResource sceneResource, ISceneQuery sceneQuery, ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery)
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
