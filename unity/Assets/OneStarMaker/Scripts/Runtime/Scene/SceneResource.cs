#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンの定義情報を保持する ScriptableObject。
    /// 親子ツリー構造、Addressable ロード情報、LoadType を持つ。
    /// SceneGraph Editor の Generate で生成される。直接作成禁止（CreateAssetMenu 削除済み）。
    /// </summary>
    public class SceneResource : ScriptableObject
    {
        [SerializeField]
        private string _identity = string.Empty;

        [SerializeField]
        private SceneAssetDescription? _sceneAssetDescription;

        [SerializeField]
        private SceneResource? _parent;

        [SerializeField]
        private List<SceneResource> _children = new();

        /// <summary>シーンの一意識別子。</summary>
        public string Identity
        {
            get => _identity;
            internal set => _identity = value;
        }

        /// <summary>埋め込み SceneAssetDescription。</summary>
        public SceneAssetDescription? SceneAssetDescription => _sceneAssetDescription;

        /// <summary>ロードタイミング種別。</summary>
        public LoadType LoadType
            => _sceneAssetDescription?.LoadType ?? LoadType.OnDemand;

        /// <summary>
        /// BuildSystem 向け Payload 列挙。
        /// SceneResourceMapSource が SceneAssetDescription を直接参照できないための公開 API。
        /// </summary>
        public IReadOnlyList<AssetPayload> GetPayloads()
            => _sceneAssetDescription?.Payloads ?? System.Array.Empty<AssetPayload>();

        /// <summary>親シーン。null ならルート。</summary>
        public SceneResource? Parent
        {
            get => _parent;
            internal set => _parent = value;
        }

        /// <summary>子シーンのリスト。</summary>
        public IReadOnlyList<SceneResource> Children => _children;

        /// <summary>
        /// Addressables でシーンをロードする。
        /// </summary>
        /// <param name="variant">バリアント名。</param>
        /// <param name="loadMode">シーンロードモード。</param>
        /// <param name="activateOnLoad">ロード後に即アクティブにするか。</param>
        /// <param name="priority">ロード優先度。</param>
        /// <returns>ロードハンドル。該当アセットがなければ null。</returns>
        public AsyncOperationHandle<SceneInstance>? Load(
            string variant = "",
            LoadSceneMode loadMode = LoadSceneMode.Additive,
            bool activateOnLoad = true,
            int priority = 100)
        {
            return _sceneAssetDescription?.Load(variant, loadMode, activateOnLoad, priority);
        }

        /// <summary>
        /// 全アセット参照をリリースする。
        /// </summary>
        public void ReleaseAll()
        {
            _sceneAssetDescription?.ReleaseAll();
        }
    }
}
