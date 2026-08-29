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

        /// <summary>
        /// このシーンが占めるワールド AABB（34-ondemand-spatial-policy.md §5）。
        /// Editor の再計算機構が自動で書く。空（size == zero）なら空間に属さない。
        /// </summary>
        [SerializeField]
        private Bounds _volume;

        /// <summary>
        /// 距離政策の候補かどうか（§34 §5）。
        /// 職種分割の子シーン（現状の Environment）は false。距離の単位は人が開く作業単位。
        /// </summary>
        [SerializeField]
        private bool _streamByDistance;

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
        /// このシーンが占めるワールド AABB。空間政策の距離計算はこの中心を使う（§34 §5）。
        /// 空（size == zero）は「空間に属さない」の表明であり、原点の点ではない。
        /// </summary>
        public Bounds Volume
        {
            get => _volume;
            internal set => _volume = value;
        }

        /// <summary>距離政策の候補か（§34 §5）。</summary>
        public bool StreamByDistance
        {
            get => _streamByDistance;
            internal set => _streamByDistance = value;
        }

        /// <summary>
        /// 指定 Variant の Addressables シーン参照を返す。
        /// </summary>
        public AssetReference? GetSceneReference(string variant)
            => _sceneAssetDescription?.ResolveReference(variant);

    }
}
