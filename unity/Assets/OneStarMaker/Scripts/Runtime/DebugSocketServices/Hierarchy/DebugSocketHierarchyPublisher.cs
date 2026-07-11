#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using OneStarMaker.Foundation.DebugSocket;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// ロード済み scene の hierarchy 走査、published 正本、snapshot/delta frame 生成を担う内部 publisher。
    ///
    /// <para>
    /// scene event の購読や session enqueue は <c>DebugSocketService</c> が維持する。
    /// 本クラスは <c>_gate</c> 排他下でのみ呼ばれ、自身は lock を持たない。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketHierarchyPublisher
    {
        private const string HierarchyScopeName = "Loaded Scenes";

        private readonly DebugSocketRuntimeNodeRegistry _runtimeNodeRegistry;
        private readonly Dictionary<long, HierarchyNodeDtoV1> _publishedHierarchyNodes = new();

        private long _hierarchyRevision;
        private long _publishedHierarchyRevision;

        public DebugSocketHierarchyPublisher(DebugSocketRuntimeNodeRegistry runtimeNodeRegistry)
        {
            _runtimeNodeRegistry = runtimeNodeRegistry ?? throw new ArgumentNullException(nameof(runtimeNodeRegistry));
        }

        /// <summary>
        /// 現在ロード済み scene から hierarchy node 一覧を取得する。
        /// snapshot と delta の両方で同じ正規化結果を使うため、列挙処理を 1 箇所へ寄せる。
        /// </summary>
        public HierarchyCaptureResult CaptureUnsafe()
        {
            var sceneCount = SceneManager.sceneCount;
            var nodes = new List<HierarchyNodeDtoV1>(sceneCount * 16);
            var seenNodeIds = new HashSet<long>();
            var traversalIndex = 0;

            for (var sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var roots = _runtimeNodeRegistry.GetRootGameObjectsNonAlloc(scene);
                for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                {
                    AppendHierarchyNodeRecursiveUnsafe(
                        scene,
                        roots[rootIndex].transform,
                        parentId: 0,
                        depth: 0,
                        ref traversalIndex,
                        nodes,
                        seenNodeIds);
                }
            }

            return new HierarchyCaptureResult(nodes, seenNodeIds);
        }

        /// <summary>
        /// capture 結果から全量 snapshot frame を生成し、published 正本を置き換える。
        /// </summary>
        public byte[] CreateSnapshotFrameUnsafe(HierarchyCaptureResult captureResult)
        {
            var revision = Interlocked.Increment(ref _hierarchyRevision);
            ReplacePublishedHierarchyUnsafe(captureResult.Nodes, revision);
            _runtimeNodeRegistry.PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);

            return DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.HierarchySnapshot,
                new HierarchySnapshotEnvelopeV1
                {
                    Revision = revision,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ScopeName = HierarchyScopeName,
                    Nodes = captureResult.Nodes.ToArray(),
                });
        }

        /// <summary>
        /// capture と published 正本の差分から delta frame を生成する。
        ///
        /// <para>
        /// capture / published state 更新 / token prune を同じ排他境界で扱う理由:
        /// capture 途中で別スレッドがセッション差し替えや state reset を挟むと、
        /// token 空間と published 正本が「half old / half new」になり、
        /// delta の BaseRevision と実体が不整合になる。service の <c>_gate</c> 内で
        /// capture から frame 生成・prune までを原子的に閉じることで、この中間状態を排除する。
        /// </para>
        ///
        /// <para>
        /// snapshot ではなく delta を送らない条件:
        /// </para>
        /// <list type="bullet">
        /// <item><description>published 正本が未初期化（初回または reset 直後）のときは delta を作れないため false を返す。呼び出し元は snapshot へフォールバックする。</description></item>
        /// <item><description>published 正本があり、差分が 0 件のときは false を返す。呼び出し元は frame を enqueue しない。</description></item>
        /// </list>
        /// </summary>
        public bool TryCreateDeltaFrameUnsafe(HierarchyCaptureResult captureResult, out byte[]? framedMessage)
        {
            var nodes = captureResult.Nodes;
            var currentNodes = new Dictionary<long, HierarchyNodeDtoV1>(nodes.Count);
            for (var index = 0; index < nodes.Count; index++)
            {
                currentNodes[nodes[index].NodeId] = nodes[index];
            }

            List<HierarchyNodeChangeDtoV1>? changes = null;
            long baseRevision;
            long revision;

            if (_publishedHierarchyRevision == 0 || _publishedHierarchyNodes.Count == 0)
            {
                framedMessage = Array.Empty<byte>();
                return false;
            }

            foreach (var published in _publishedHierarchyNodes)
            {
                if (!currentNodes.ContainsKey(published.Key))
                {
                    changes ??= new List<HierarchyNodeChangeDtoV1>();
                    changes.Add(new HierarchyNodeChangeDtoV1
                    {
                        ChangeKind = HierarchyChangeKind.Remove,
                        NodeId = published.Key,
                    });
                }
            }

            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (!_publishedHierarchyNodes.TryGetValue(node.NodeId, out var publishedNode) ||
                    !HierarchyNodeEquals(publishedNode, node))
                {
                    changes ??= new List<HierarchyNodeChangeDtoV1>();
                    changes.Add(CreateHierarchyNodeChange(node, HierarchyChangeKind.Upsert));
                }
            }

            if (changes == null || changes.Count == 0)
            {
                // full capture が成功したが差分が無かったケースでも、
                // token cache の stale entry はここで掃除しておく。
                _runtimeNodeRegistry.PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);
                framedMessage = Array.Empty<byte>();
                return false;
            }

            baseRevision = _publishedHierarchyRevision;
            revision = Interlocked.Increment(ref _hierarchyRevision);
            ReplacePublishedHierarchyUnsafe(nodes, revision);
            _runtimeNodeRegistry.PruneRuntimeNodeMappingsUnsafe(captureResult.SeenNodeIds);

            framedMessage = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.HierarchyDelta,
                new HierarchyDeltaEnvelopeV1
                {
                    BaseRevision = baseRevision,
                    Revision = revision,
                    CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ScopeName = HierarchyScopeName,
                    Changes = changes.ToArray(),
                });
            return true;
        }

        public bool HasPublishedStateUnsafe()
        {
            return _publishedHierarchyRevision != 0 && _publishedHierarchyNodes.Count != 0;
        }

        public void ResetUnsafe()
        {
            _publishedHierarchyNodes.Clear();
            _publishedHierarchyRevision = 0;
            _runtimeNodeRegistry.Reset();
        }

        /// <summary>
        /// 1 GameObject subtree を preorder で平坦化して snapshot node 配列へ積む。
        /// DebugStudio 側は <c>ParentId + Depth + TraversalIndex</c> を使って tree を再構築する。
        /// </summary>
        private void AppendHierarchyNodeRecursiveUnsafe(
            Scene scene,
            Transform transform,
            long parentId,
            int depth,
            ref int traversalIndex,
            List<HierarchyNodeDtoV1> nodes,
            HashSet<long> seenNodeIds)
        {
            var gameObject = transform.gameObject;
            var nodeId = _runtimeNodeRegistry.CreateRuntimeNodeIdUnsafe(gameObject);
            seenNodeIds.Add(nodeId);
            var flags = HierarchyNodeFlags.None;

            if (gameObject.activeSelf)
            {
                flags |= HierarchyNodeFlags.ActiveSelf;
            }

            if (gameObject.activeInHierarchy)
            {
                flags |= HierarchyNodeFlags.ActiveInHierarchy;
            }

            if (transform.parent == null)
            {
                flags |= HierarchyNodeFlags.SceneRoot;
            }

            if (transform.childCount > 0)
            {
                flags |= HierarchyNodeFlags.HasChildren;
            }

            if (string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal))
            {
                flags |= HierarchyNodeFlags.DontDestroyOnLoad;
            }

            nodes.Add(new HierarchyNodeDtoV1
            {
                NodeId = nodeId,
                ParentId = parentId,
                TypeId = 1,
                Flags = flags,
                Depth = depth,
                SiblingIndex = transform.GetSiblingIndex(),
                ChildCount = transform.childCount,
                TraversalIndex = traversalIndex++,
                Name = gameObject.name,
                TypeName = nameof(GameObject),
            });

            for (var childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                AppendHierarchyNodeRecursiveUnsafe(
                    scene,
                    transform.GetChild(childIndex),
                    nodeId,
                    depth + 1,
                    ref traversalIndex,
                    nodes,
                    seenNodeIds);
            }
        }

        private void ReplacePublishedHierarchyUnsafe(IReadOnlyList<HierarchyNodeDtoV1> nodes, long revision)
        {
            _publishedHierarchyNodes.Clear();
            for (var index = 0; index < nodes.Count; index++)
            {
                _publishedHierarchyNodes[nodes[index].NodeId] = nodes[index];
            }

            _publishedHierarchyRevision = revision;
        }

        private static HierarchyNodeChangeDtoV1 CreateHierarchyNodeChange(
            HierarchyNodeDtoV1 node,
            HierarchyChangeKind changeKind)
        {
            return new HierarchyNodeChangeDtoV1
            {
                ChangeKind = changeKind,
                NodeId = node.NodeId,
                ParentId = node.ParentId,
                TypeId = node.TypeId,
                Flags = node.Flags,
                Depth = node.Depth,
                SiblingIndex = node.SiblingIndex,
                ChildCount = node.ChildCount,
                TraversalIndex = node.TraversalIndex,
                Name = node.Name,
                TypeName = node.TypeName,
            };
        }

        private static bool HierarchyNodeEquals(HierarchyNodeDtoV1 left, HierarchyNodeDtoV1 right)
        {
            return left.NodeId == right.NodeId &&
                   left.ParentId == right.ParentId &&
                   left.TypeId == right.TypeId &&
                   left.Flags == right.Flags &&
                   left.Depth == right.Depth &&
                   left.SiblingIndex == right.SiblingIndex &&
                   left.ChildCount == right.ChildCount &&
                   left.TraversalIndex == right.TraversalIndex &&
                   string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
                   string.Equals(left.TypeName, right.TypeName, StringComparison.Ordinal);
        }

        internal sealed class HierarchyCaptureResult
        {
            public HierarchyCaptureResult(List<HierarchyNodeDtoV1> nodes, HashSet<long> seenNodeIds)
            {
                Nodes = nodes;
                SeenNodeIds = seenNodeIds;
            }

            public List<HierarchyNodeDtoV1> Nodes { get; }

            public HashSet<long> SeenNodeIds { get; }
        }
    }
}
