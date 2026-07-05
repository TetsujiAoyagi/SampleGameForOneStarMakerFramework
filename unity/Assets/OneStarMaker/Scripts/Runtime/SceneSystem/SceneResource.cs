#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
        /// 指定 Variant の Addressables シーン参照を返す。
        /// </summary>
        public AssetReference? GetSceneReference(string variant)
            => _sceneAssetDescription?.ResolveReference(variant);

    }
}
