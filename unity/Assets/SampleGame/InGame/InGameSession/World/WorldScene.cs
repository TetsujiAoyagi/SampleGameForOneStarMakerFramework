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
    /// セル群の親コンテナ。SceneDirector ツリー上のぶら下げ点と、
    /// 実証用共有マテリアルの PreLoad オーナーを兼ねる。
    /// ロード判断は持たない（WorldStreamingController 側）。
    /// </summary>
    /// <remarks>
    /// Material は一つ上（World）が参照・事前ロードし、Cell は共有 + MPB で色分けする。
    /// 子シーン（将来の Environment 等）は NecessaryAlways で引っ張らない（設計合意）。
    /// </remarks>
    public sealed class WorldScene : SceneBase
    {
        private readonly ILogger<WorldScene> _logger;
        private WorldMaterialBindings? _materials;
        private IAssetHandle<Material>? _sharedLitHandle;

        public WorldScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            _logger = loggerFactory.CreateLogger<WorldScene>();
            _logger.ZLogInformation($"Create WorldScene");
        }

        /// <summary>
        /// 共有 Lit を Scene 寿命で PreLoad する（キャンセル窓内 = R-1）。
        /// World は NecessaryAlways なので、Cell ストリーミング開始前にウォームされる。
        /// </summary>
        protected override async UniTask OnPreLoadedImpl(CancellationToken ct)
        {
            // シーン内参照の解決は RootObjects が揃う Loaded 以降が確実だが、
            // Addressables アドレスでの PreLoad はここで行い ResidentCache を温める。
            var key = AssetKey.FromAddress(WorldMaterialBindings.SharedLitAssetPath);
            _sharedLitHandle = await LoadSceneScopedAssetAsync<Material>(key, ct);
            _logger.ZLogInformation(
                $"OnPreLoadedImpl WorldScene: shared lit preloaded ({WorldMaterialBindings.SharedLitAssetPath})");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            _materials = FindRootComponent<WorldMaterialBindings>();
            if (_materials == null || _materials.SharedLit == null)
            {
                throw new System.InvalidOperationException(
                    "World.unity に WorldMaterialBindings / SharedLit がありません。" +
                    " Create World + Cell Streaming Slice を再実行してください。");
            }

            // シーン参照と PreLoad ハンドルが同一アセットであることを軽く確認する（診断用）。
            if (_sharedLitHandle?.Value != null
                && !ReferenceEquals(_sharedLitHandle.Value, _materials.SharedLit))
            {
                _logger.ZLogWarning(
                    $"World SharedLit シーン参照と PreLoad ハンドルが不一致です。Addressables 登録を確認してください。");
            }

            _logger.ZLogInformation($"OnLoadedImpl WorldScene (shared materials ready)");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl WorldScene");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl()
        {
            _materials = null;
            // Scene スコープハンドルは SceneAssetOwner 解放に任せる。明示 Dispose は二重解放を避ける。
            _sharedLitHandle = null;
            return UniTask.CompletedTask;
        }

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;
    }
}
