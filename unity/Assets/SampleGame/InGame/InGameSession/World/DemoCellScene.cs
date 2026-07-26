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
    /// T-07 用の実証セル。見た目は Cell .unity に事前配置し、色だけ MaterialPropertyBlock で乗せる。
    /// ロード判断・隣接参照・UIView は持たない（CellScene / R-2〜R-5）。
    /// </summary>
    /// <remarks>
    /// 共有 Material は親 World が参照・PreLoad する。ここでの new Material / CreatePrimitive は禁止。
    /// MPB はレンダラ単位のオーバーライドであり、マテリアルアセットを増やさない（案 2）。
    /// <para>
    /// 萌芽 Cell（Environment 子を持つもの）では Ground が Environment 側へ移っている。
    /// Marker はストリーミング境界の目印として Cell に残る。Ground 不在時の tint は no-op。
    /// </para>
    /// </remarks>
    public sealed class DemoCellScene : CellScene
    {
        /// <summary>Editor が各 Cell シーンに置くルート名。ランタイム検証の契約。</summary>
        public const string AuthoredRootName = "DemoCellRoot";

        private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

            // 共有 Material はそのまま。セル識別色だけ MPB で乗せる（アセット増殖なし）。
            ApplyCellTint(root);
            _logger.ZLogInformation(
                $"OnLoadedImpl {SceneResource.Identity}: authored root OK, MPB tint applied");
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

        /// <summary>
        /// Ground / Marker / Prop_* にセル座標色を MPB で載せる。
        /// sharedMaterial は書き換えず、インスタンス Material も作らない。
        /// </summary>
        private void ApplyCellTint(GameObject root)
        {
            var tint = WorldCellCatalog.GetCellTint(Coordinate.x, Coordinate.y);
            var markerTint = Color.Lerp(tint, Color.white, 0.35f);
            var propTint = Color.Lerp(tint, Color.black, 0.15f);

            ApplyTintToChild(root.transform, "Ground", tint);
            ApplyTintToChild(root.transform, "Marker", markerTint);

            // Editor が焼くローカル小物（Prop_0 ..）。セル固有の見た目差。
            for (var i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child != null && child.name.StartsWith("Prop_", System.StringComparison.Ordinal))
                {
                    ApplyTintToRenderer(child.GetComponent<MeshRenderer>(), propTint);
                }
            }
        }

        private static void ApplyTintToChild(Transform root, string childName, Color color)
        {
            var child = root.Find(childName);
            if (child == null)
            {
                return;
            }

            ApplyTintToRenderer(child.GetComponent<MeshRenderer>(), color);
        }

        private static void ApplyTintToRenderer(MeshRenderer? renderer, Color color)
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
