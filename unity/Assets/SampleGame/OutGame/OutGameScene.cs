using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.OutGame.Background;
using UnityEngine;

namespace SampleGame.OutGame
{
    public class OutGameScene : SceneBase, IOutGameBackgroundRequests
    {
        private readonly ILogger<OutGameScene> _logger;
        private readonly OutGameBackgroundController _backgroundController = new();

        /// <inheritdoc />
        public OutGameBackgroundDefinition? Current => _backgroundController.Current;

        public OutGameScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ILoggerFactory loggerFactory,
            ICameraBackgroundApplier cameraBackgroundApplier,
            ICameraSystem cameraSystem) : base(sceneResource, sceneQuery)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            if (cameraBackgroundApplier == null)
            {
                throw new System.ArgumentNullException(nameof(cameraBackgroundApplier));
            }

            if (cameraSystem == null)
            {
                throw new System.ArgumentNullException(nameof(cameraSystem));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<OutGameScene>();

            _logger.LogInformation($"Create OutGameScene");

            // OutGame の背景色は View_Main の描画設定であり、シーンの旧 Main Camera には設定しない。
            // 依存を必須化して Bootstrap 失敗をここまで持ち込まず、破棄済み Host への書き込みも防ぐ。
            cameraBackgroundApplier.SetClearFlag(cameraSystem.MainView, ClearFlag.Color, Color.red);
        }

        /// <inheritdoc />
        public void Request(OutGameBackgroundDefinition definition)
        {
            _backgroundController.Request(definition);
        }

        /// <inheritdoc />
        protected override void OnInitialize()
        {
            if (UIView is not OutGameBackgroundView backgroundView)
            {
                throw new System.InvalidOperationException(
                    "OutGameScene には OutGameBackgroundView が1つ必要です。");
            }

            backgroundView.Connect(_backgroundController);
        }
    }
}
