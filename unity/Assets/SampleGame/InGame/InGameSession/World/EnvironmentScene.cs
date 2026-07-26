#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.InGame.Streaming;
using UnityEngine;
using ZLogger;

namespace SampleGame.InGame.World
{
    /// <summary>
    /// Cell 配下の Environment 職種シーン（萌芽スライス）。
    /// <see cref="CellScene"/> は継承しない — 距離境界のメタデータは親 Cell が持つ。
    /// ロード判断もしない（親 Cell Add で自動ロードされない OnDemand。明示 Add はデモ配線側）。
    /// </summary>
    public sealed class EnvironmentScene : SceneBase
    {
        /// <summary>Editor が Environment .unity に置くルート名。</summary>
        public const string AuthoredRootName = "EnvironmentRoot";

        private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly ILogger<EnvironmentScene> _logger;

        public EnvironmentScene(
            SceneResource sceneResource,
            ISceneQuery sceneQuery,
            ISceneController sceneController,
            ILoggerFactory loggerFactory)
            : base(sceneResource, sceneQuery, sceneController)
        {
            if (!EnvironmentIdentity.IsEnvironmentId(sceneResource.Identity))
            {
                throw new System.ArgumentException(
                    $"EnvironmentScene は Environment_{{x}}_{{y}} 専用です: {sceneResource.Identity}",
                    nameof(sceneResource));
            }

            _logger = loggerFactory.CreateLogger<EnvironmentScene>();
            _logger.ZLogInformation($"Create EnvironmentScene {sceneResource.Identity}");
        }

        protected override UniTask OnLoadedImpl(CancellationToken ct)
        {
            var root = FindAuthoredRoot();
            if (root == null)
            {
                throw new System.InvalidOperationException(
                    $"Environment '{SceneResource.Identity}' に '{AuthoredRootName}' がありません。" +
                    " OneStarMaker/Sample/Create World + Cell Streaming Slice を再実行してください。");
            }

            // 共有 Lit は World PreLoad 済み。ここでも MPB のみ（アセット増殖なし）。
            if (EnvironmentIdentity.TryParse(SceneResource.Identity, out var coordinate))
            {
                ApplyGroundTint(root, coordinate.x, coordinate.y);
            }

            _logger.ZLogInformation(
                $"OnLoadedImpl {SceneResource.Identity}: authored root OK, Ground MPB applied");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnStabledImpl()
        {
            _logger.ZLogInformation($"OnStabledImpl {SceneResource.Identity}");
            return UniTask.CompletedTask;
        }

        protected override UniTask OnPreUnLoadedImpl() => UniTask.CompletedTask;

        protected override UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;

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

        private static void ApplyGroundTint(GameObject root, int x, int y)
        {
            var tint = WorldCellCatalog.GetCellTint(x, y);
            var accent = Color.Lerp(tint, Color.white, 0.2f);

            ApplyTint(root.transform.Find("Ground")?.GetComponent<MeshRenderer>(), tint);

            // Editor が焼く Environment 側の小物（EnvProp_*）。
            for (var i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child != null && child.name.StartsWith("EnvProp_", System.StringComparison.Ordinal))
                {
                    ApplyTint(child.GetComponent<MeshRenderer>(), accent);
                }
            }
        }

        private static void ApplyTint(MeshRenderer? renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }

            SharedPropertyBlock.Clear();
            SharedPropertyBlock.SetColor(BaseColorId, color);
            SharedPropertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(SharedPropertyBlock);
        }
    }
}
