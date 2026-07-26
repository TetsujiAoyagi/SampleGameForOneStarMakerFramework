#nullable enable

using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.CameraSystem.Abstractions;
using OneStarMaker.Runtime.SceneSystem;
using R3;
using SampleGame.Common;
using SampleGame.Common.TransitionArgs;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame
{
    public class InGameSession : SceneBase
    {
        private readonly ILogger<InGameSession> _logger;

        private ReactiveProperty<SceneIds> _currentSceneIds = new();
        public SceneIds CurrentSceneId => _currentSceneIds.Value;

        public InGameSession(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            // Scene ごとのカテゴリを維持するため、文字列カテゴリではなく型付き logger を使用する。
            // DebugStudio 側で発生元 Scene を絞り込めることを優先する。
            _logger = loggerFactory.CreateLogger<InGameSession>();

            _logger.ZLogInformation($"Create InGameSession");

        }

        protected override async UniTask OnLoadedImpl(CancellationToken ct)
        {
            // OutGameからの遷移か確認する。違うならInGame関係のSceneが既に読み込まれているはず
            var args = Context?.GetValueType<InGameArgs>();
            if(args == null || args.HasValue == false)
            {
                return;
            }

            _currentSceneIds.Value = args.Value.TransitionLevel;

            // 実際のレベル読み込み
            await SceneController.AddScene(
                _currentSceneIds.Value.idToName(),
                afterOnLoadedTask: null,
                ct: ct);
        }

        protected override UniTask OnAfterUnLoadedImpl()
        {
            _currentSceneIds.Dispose();

            return UniTask.CompletedTask;
        }
    }
}
