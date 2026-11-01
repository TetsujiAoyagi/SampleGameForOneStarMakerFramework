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
    /// GO 配線はしない。論理ノードは RootObjects 空。
    /// ハンドルの明示 Dispose はしない。Season Unload の Scene owner 解放に委ねる。
    /// 共有 Lit は親の InGameSession が付随 asset <c>Lit</c> として 1 回だけ宣言し、4 つの Season がそれぞれ自分の所有で読む
    /// （address も path も持たない。Content Directory の Play では directory の entry から読む）。
    /// </remarks>
    public sealed class SeasonScene : SceneBase
    {
        /// <summary>共有 Lit を宣言している祖先 node と、その付随 asset の名前。</summary>
        private const string SharedLitOwner = "InGameSession";
        private const string SharedLitName = "Lit";

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
            _sharedLitHandle = await LoadAttachedAssetAsync<Material>(SharedLitOwner, SharedLitName, ct);
            _logger.ZLogInformation(
                $"OnPreLoadedImpl SeasonScene: shared lit preloaded ({SharedLitOwner}:{SharedLitName})");
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
