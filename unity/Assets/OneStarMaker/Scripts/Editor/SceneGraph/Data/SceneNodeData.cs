#nullable enable

using System.Collections.Generic;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーングラフの1ノードを表す中間データ。
    /// 1シーン = 1ファイルで保存し、Git マージ衝突を最小化する。
    /// SceneAssetDescription は Generate 時に生成されるため、ここでは持たない。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneNode", menuName = "OneStarMaker/SceneGraph/Node Data")]
    public class SceneNodeData : ScriptableObject
    {
        [SerializeField]
        private string _identity = string.Empty;

        [SerializeField]
        private LoadType _loadType = LoadType.OnDemand;

        [SerializeField]
        private List<AssetPayload> _payloads = new();

        /// <summary>シーンの一意識別子。</summary>
        public string Identity
        {
            get => _identity;
            set => _identity = value;
        }

        /// <summary>ロードタイミング種別。</summary>
        public LoadType NodeLoadType
        {
            get => _loadType;
            set => _loadType = value;
        }

        /// <summary>Addressable シーン参照リスト（バリアント対応）。</summary>
        public List<AssetPayload> Payloads => _payloads;

        /// <summary>
        /// W-5: Payload[0] の SceneAsset が変更されたとき Identity を自動同期する。
        /// Inspector 直接編集や Undo/Redo 時にも反応する。
        /// </summary>
        private void OnValidate()
        {
            if (_payloads.Count == 0) return;

            var payload0 = _payloads[0];
            if (payload0?.Reference == null) return;

            var guid = payload0.Reference.AssetGUID;
            if (string.IsNullOrEmpty(guid)) return;

            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath)) return;

            var assetName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetName)) return;

            // Identity が既に一致していれば何もしない
            if (_identity == assetName) return;

            _identity = assetName;
            name = assetName;

            // アセットファイル名のリネームは ViewModel 経由で行うため、
            // ここではフィールド同期のみ。
            // エディタ上で表示を更新するため dirty フラグを立てる。
            EditorUtility.SetDirty(this);
        }
    }
}
