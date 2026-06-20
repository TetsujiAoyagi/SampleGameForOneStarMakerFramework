#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーングラフのノード位置情報をグラフ単位で保持する中間データ。
    /// エッジデータとは別ファイルに保存し、位置変更によるコンフリクトを回避する。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneGraphLayout", menuName = "OneStarMaker/SceneGraph/Graph Layout")]
    public class SceneGraphLayout : ScriptableObject
    {
        [Serializable]
        public struct NodePosition
        {
            public SceneNodeData? Node;
            public Vector2 Position;
        }

        [SerializeField]
        private List<NodePosition> _positions = new();

        /// <summary>ノード位置一覧。</summary>
        public List<NodePosition> Positions => _positions;

        /// <summary>
        /// 指定ノードの位置を取得する。未登録なら Vector2.zero。
        /// </summary>
        public Vector2 GetPosition(SceneNodeData node)
        {
            foreach (var pos in _positions)
            {
                if (pos.Node == node)
                    return pos.Position;
            }
            return Vector2.zero;
        }

        /// <summary>
        /// 指定ノードの位置を設定する。未登録なら追加。
        /// </summary>
        public void SetPosition(SceneNodeData node, Vector2 position)
        {
            for (int i = 0; i < _positions.Count; i++)
            {
                if (_positions[i].Node == node)
                {
                    _positions[i] = new NodePosition { Node = node, Position = position };
                    return;
                }
            }
            _positions.Add(new NodePosition { Node = node, Position = position });
        }

        /// <summary>
        /// 指定ノードの位置情報を削除する。
        /// </summary>
        public bool RemovePosition(SceneNodeData node)
        {
            for (int i = _positions.Count - 1; i >= 0; i--)
            {
                if (_positions[i].Node == node)
                {
                    _positions.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
