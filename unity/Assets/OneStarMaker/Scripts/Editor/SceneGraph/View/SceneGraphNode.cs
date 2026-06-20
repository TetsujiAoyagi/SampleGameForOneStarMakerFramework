#nullable enable

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// GraphView 上の1ノードを表す。
    /// SceneNodeData（Model）のビジュアル表現。
    /// </summary>
    public sealed class SceneGraphNode : Node
    {
        /// <summary>バインドされた SceneNodeData。</summary>
        public SceneNodeData NodeData { get; }

        /// <summary>子ノードへの出力ポート。</summary>
        public Port OutputPort { get; }

        /// <summary>親ノードからの入力ポート。</summary>
        public Port InputPort { get; }

        public SceneGraphNode(SceneNodeData nodeData)
        {
            NodeData = nodeData;

            title = nodeData.Identity;
            name = nodeData.Identity;

            // ── 入力ポート（親からの接続を受け取る）──
            InputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Input,
                Port.Capacity.Single,  // 最大1親
                typeof(SceneNodeData));
            InputPort.portName = "Parent";
            inputContainer.Add(InputPort);

            // ── 出力ポート（子への接続を出す）──
            OutputPort = InstantiatePort(
                Orientation.Horizontal,
                Direction.Output,
                Port.Capacity.Multi,   // 複数の子を持てる
                typeof(SceneNodeData));
            OutputPort.portName = "Children";
            outputContainer.Add(OutputPort);

            // ── LoadType 表示 ──
            var loadTypeLabel = new Label(nodeData.NodeLoadType.ToString());
            loadTypeLabel.name = "load-type-label";
            loadTypeLabel.AddToClassList("scene-graph-node__load-type");
            mainContainer.Add(loadTypeLabel);

            // ── スタイル ──
            AddToClassList("scene-graph-node");

            RefreshExpandedState();
            RefreshPorts();
        }

        /// <summary>
        /// 表示を Model の更新に合わせてリフレッシュする。
        /// </summary>
        public void UpdateFromModel()
        {
            title = NodeData.Identity;
            name = NodeData.Identity;

            var loadTypeLabel = mainContainer.Q<Label>("load-type-label");
            if (loadTypeLabel != null)
            {
                loadTypeLabel.text = NodeData.NodeLoadType.ToString();
            }
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
        }
    }
}
