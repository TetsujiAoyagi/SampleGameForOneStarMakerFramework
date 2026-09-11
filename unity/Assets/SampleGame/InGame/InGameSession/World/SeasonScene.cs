#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// 季節枝の親。共有 Lit を Scene 寿命で PreLoad する。WorldScene の置換。
    /// </summary>
    /// <remarks>
    /// Material は Season が参照・事前ロードし、Cell は共有 + MPB で色分けする。
    /// GO 配線（WorldMaterialBindings の Find）はしない。論理ノードは RootObjects 空。
    /// ハンドルの明示 Dispose はしない。Season Unload の Scene owner 解放に委ねる。
    /// </remarks>
    public sealed class SeasonScene : SceneBase
    {
        private readonly ILogger<SeasonScene> _logger;
        private IAssetHandle<Material>? _sharedLitHandle;

        public SeasonScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<SeasonScene>();
            _logger.ZLogInformation($"Create SeasonScene {sceneResource.Identity}");
        }

        protected override async UniTask OnPreLoadedImpl(CancellationToken ct)
        {
            // キャンセル窓内の Scene スコープ PreLoad。owner は AssetOwner.Scene(Season_*).
            var key = AssetKey.FromAddress(WorldMaterialBindings.SharedLitAssetPath);
            _sharedLitHandle = await LoadSceneScopedAssetAsync<Material>(key, ct);
            _logger.ZLogInformation(
                $"OnPreLoadedImpl SeasonScene: shared lit preloaded ({WorldMaterialBindings.SharedLitAssetPath})");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            _logger.ZLogInformation($"OnLoadedImpl SeasonScene {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl SeasonScene {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            // Scene スコープハンドルは SceneAssetOwner 解放に任せる。明示 Dispose は二重解放を避ける。
            _sharedLitHandle = null;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;
    }
}
