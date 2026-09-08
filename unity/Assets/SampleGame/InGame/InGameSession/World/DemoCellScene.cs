#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// T-07 用の実証セル。見た目は Cell .unity に事前配置し、runtime では authored root だけを検証する。
    /// ロード判断・隣接参照・UIView は持たない（CellScene / R-2〜R-5）。
    /// </summary>
    public sealed class DemoCellScene : CellScene
    {
        /// <summary>Editor が各 Cell シーンに置くルート名。ランタイム検証の契約。</summary>
        public const string AuthoredRootName = "DemoCellRoot";

        private readonly ILogger<DemoCellScene> _logger;

        public DemoCellScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<DemoCellScene>();
            _logger.ZLogInformation($"Create DemoCellScene {sceneResource.Identity}");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            var root = FindAuthoredRoot();
            if (root == null)
            {
                throw new System.InvalidOperationException(
                    $"Cell '{SceneResource.Identity}' に '{AuthoredRootName}' がありません。" +
                    " OneStarMaker/Sample/Create World + Cell Streaming Slice を再実行してください。");
            }

            _logger.ZLogInformation(
                $"OnLoadedImpl {SceneResource.Identity}: authored root OK");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl() => UniTask.CompletedTask;

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;

        /// <summary>RootObjects 配下（または直下）の事前配置ルートを探す。</summary>
        private GameObject? FindAuthoredRoot()
        {
            for (var i = 0; i < RootObjects.Count; i++)
            {
                var go = RootObjects[i];
                if (go == null)
                {
                    continue;
                }

                if (go.name == AuthoredRootName)
                {
                    return go;
                }

                var child = go.transform.Find(AuthoredRootName);
                if (child != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

    }
}
