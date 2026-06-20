#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// シーングラフのエッジ（親子関係）をグラフ単位で保持する中間データ。
    /// ノード位置とは別ファイルに保存し、コンフリクトを最小化する。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSceneGraphEdges", menuName = "OneStarMaker/SceneGraph/Graph Edges")]
    public class SceneGraphEdges : ScriptableObject
    {
        [Serializable]
        public struct Edge
        {
            public SceneNodeData? Parent;
            public SceneNodeData? Child;
        }

        [SerializeField]
        private string _graphName = string.Empty;

        [SerializeField]
        private List<SceneNodeData?> _nodes = new();

        [SerializeField]
        private List<Edge> _edges = new();

        /// <summary>グラフ名。</summary>
        public string GraphName
        {
            get => _graphName;
            set => _graphName = value;
        }

        /// <summary>このグラフに属するノード一覧。</summary>
        public List<SceneNodeData?> GraphNodes => _nodes;

        /// <summary>エッジ一覧。</summary>
        public List<Edge> Edges => _edges;

        /// <summary>
        /// ノードをグラフに追加する。
        /// </summary>
        public void AddNode(SceneNodeData node)
        {
            if (!_nodes.Contains(node))
                _nodes.Add(node);
        }

        /// <summary>
        /// ノードをグラフから削除する。関連エッジも削除する。
        /// </summary>
        public void RemoveNode(SceneNodeData node)
        {
            _nodes.Remove(node);
            RemoveAllEdgesForNode(node);
        }

        /// <summary>
        /// ノードがこのグラフに属しているか。
        /// </summary>
        public bool ContainsNode(SceneNodeData node) => _nodes.Contains(node);

        /// <summary>
        /// 指定ノードの親を取得する。
        /// </summary>
        public SceneNodeData? GetParent(SceneNodeData node)
        {
            foreach (var edge in _edges)
            {
                if (edge.Child == node)
                    return edge.Parent;
            }
            return null;
        }

        /// <summary>
        /// 指定ノードの子を取得する。
        /// </summary>
        public List<SceneNodeData> GetChildren(SceneNodeData node)
        {
            var children = new List<SceneNodeData>();
            foreach (var edge in _edges)
            {
                if (edge.Parent == node && edge.Child != null)
                    children.Add(edge.Child);
            }
            return children;
        }

        /// <summary>
        /// ルートノード（親を持たないノード）を取得する。
        /// グラフに属するノードのみを対象とする。
        /// </summary>
        public List<SceneNodeData> GetRoots()
        {
            var childSet = new HashSet<SceneNodeData>();
            foreach (var edge in _edges)
            {
                if (edge.Child != null)
                    childSet.Add(edge.Child);
            }

            var roots = new List<SceneNodeData>();
            foreach (var node in _nodes)
            {
                if (node != null && !childSet.Contains(node))
                    roots.Add(node);
            }
            return roots;
        }

        /// <summary>
        /// ルートノード（親を持たないノード）を取得する（外部ノードリスト指定版）。
        /// </summary>
        public List<SceneNodeData> GetRoots(IEnumerable<SceneNodeData> allNodes)
        {
            var childSet = new HashSet<SceneNodeData>();
            foreach (var edge in _edges)
            {
                if (edge.Child != null)
                    childSet.Add(edge.Child);
            }

            var roots = new List<SceneNodeData>();
            foreach (var node in allNodes)
            {
                if (node != null && !childSet.Contains(node))
                    roots.Add(node);
            }
            return roots;
        }

        /// <summary>
        /// proposedParent → proposedChild のエッジを追加した場合にサイクルが発生するか。
        /// DFS で到達可能性をチェックする。
        /// </summary>
        public bool WouldCreateCycle(SceneNodeData proposedParent, SceneNodeData proposedChild)
        {
            if (proposedParent == proposedChild) return true;

            // proposedChild から proposedParent に到達可能ならサイクル
            var visited = new HashSet<SceneNodeData>();
            return CanReach(proposedChild, proposedParent, visited);
        }

        /// <summary>
        /// エッジを追加する。サイクルチェックは呼び出し側で行うこと。
        /// </summary>
        public void AddEdge(SceneNodeData parent, SceneNodeData child)
        {
            _edges.Add(new Edge { Parent = parent, Child = child });
        }

        /// <summary>
        /// 指定の子ノードに接続されたエッジを削除する。
        /// </summary>
        public bool RemoveEdgeByChild(SceneNodeData child)
        {
            for (int i = _edges.Count - 1; i >= 0; i--)
            {
                if (_edges[i].Child == child)
                {
                    _edges.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 指定ノードに関わるエッジをすべて削除する。
        /// </summary>
        public void RemoveAllEdgesForNode(SceneNodeData node)
        {
            for (int i = _edges.Count - 1; i >= 0; i--)
            {
                if (_edges[i].Parent == node || _edges[i].Child == node)
                    _edges.RemoveAt(i);
            }
        }

        private bool CanReach(SceneNodeData from, SceneNodeData target, HashSet<SceneNodeData> visited)
        {
            if (!visited.Add(from)) return false;

            // from の子を辿る（子方向の到達可能性チェック）
            var children = GetChildren(from);
            foreach (var child in children)
            {
                if (child == target) return true;
                if (CanReach(child, target, visited)) return true;
            }
            return false;
        }
    }
}
