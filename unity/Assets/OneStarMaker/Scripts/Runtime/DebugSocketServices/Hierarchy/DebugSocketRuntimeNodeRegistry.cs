#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// hierarchy と inspector が共有する service-local stable token を管理する。
    ///
    /// <para>
    /// Unity の object identity を wire に出さない。
    /// Unity 6.5 以降は SceneHandle や内部 identity をそのまま流す前提が obsolete API に強く依存し、
    /// viewer 側が必要とするのは「ノードを安定して識別できること」だけである。
    /// そこで wire には service が採番した token のみを載せ、Unity 内部の identity はこの registry 内へ閉じ込める。
    /// </para>
    ///
    /// <para>
    /// 呼び出し元の <c>DebugSocketService</c> が <c>_gate</c> で排他する前提で動作する。
    /// registry 自体は lock を持たない。
    /// Unity API を使うメソッドは main thread で service から呼ばれる既存前提を維持する。
    /// </para>
    /// </summary>
    internal sealed class DebugSocketRuntimeNodeRegistry
    {
        private readonly Dictionary<ulong, long> _runtimeIdentityToNodeIds = new();
        private readonly Dictionary<long, ulong> _nodeIdToRuntimeIdentities = new();
        private readonly Dictionary<long, GameObject> _nodeIdToGameObjects = new();
        private readonly List<GameObject> _rootGameObjectBuffer = new(32);
        private long _nextRuntimeNodeId = 1;

        /// <summary>
        /// 辞書と GameObject 参照を消すが、採番値は戻さない。
        ///
        /// <para>
        /// 旧セッションから遅れて届いた inspector query が、
        /// 新セッションの別オブジェクトへ偶然 alias しないことを優先する。
        /// </para>
        /// </summary>
        public void Reset()
        {
            _runtimeIdentityToNodeIds.Clear();
            _nodeIdToRuntimeIdentities.Clear();
            _nodeIdToGameObjects.Clear();
        }

        public long CreateRuntimeNodeIdUnsafe(GameObject gameObject)
        {
            var runtimeIdentityKey = EntityId.ToULong(gameObject.GetEntityId());

            if (_runtimeIdentityToNodeIds.TryGetValue(runtimeIdentityKey, out var existingNodeId))
            {
                if (_nodeIdToGameObjects.TryGetValue(existingNodeId, out var existingGameObject) &&
                    existingGameObject == gameObject)
                {
                    return existingNodeId;
                }

                RemoveRuntimeNodeMappingUnsafe(existingNodeId);
            }

            var nodeId = _nextRuntimeNodeId++;
            _runtimeIdentityToNodeIds[runtimeIdentityKey] = nodeId;
            _nodeIdToRuntimeIdentities[nodeId] = runtimeIdentityKey;
            _nodeIdToGameObjects[nodeId] = gameObject;
            return nodeId;
        }

        public List<GameObject> GetRootGameObjectsNonAlloc(Scene scene)
        {
            _rootGameObjectBuffer.Clear();
            scene.GetRootGameObjects(_rootGameObjectBuffer);
            return _rootGameObjectBuffer;
        }

        public void RemoveRuntimeNodeMappingUnsafe(long nodeId)
        {
            if (_nodeIdToRuntimeIdentities.TryGetValue(nodeId, out var runtimeIdentityKey))
            {
                if (_runtimeIdentityToNodeIds.TryGetValue(runtimeIdentityKey, out var mappedNodeId) &&
                    mappedNodeId == nodeId)
                {
                    _runtimeIdentityToNodeIds.Remove(runtimeIdentityKey);
                }

                _nodeIdToRuntimeIdentities.Remove(nodeId);
            }

            _nodeIdToGameObjects.Remove(nodeId);
        }

        public void PruneRuntimeNodeMappingsUnsafe(HashSet<long> seenNodeIds)
        {
            List<long>? staleNodeIds = null;
            foreach (var pair in _nodeIdToRuntimeIdentities)
            {
                if (seenNodeIds.Contains(pair.Key))
                {
                    continue;
                }

                staleNodeIds ??= new List<long>();
                staleNodeIds.Add(pair.Key);
            }

            if (staleNodeIds == null)
            {
                return;
            }

            for (var index = 0; index < staleNodeIds.Count; index++)
            {
                RemoveRuntimeNodeMappingUnsafe(staleNodeIds[index]);
            }
        }

        public bool TryFindGameObjectByNodeId(long targetId, out Scene scene, out GameObject? gameObject)
        {
            if (!_nodeIdToGameObjects.TryGetValue(targetId, out gameObject) || gameObject == null)
            {
                RemoveRuntimeNodeMappingUnsafe(targetId);
                scene = default;
                gameObject = null;
                return false;
            }

            scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                RemoveRuntimeNodeMappingUnsafe(targetId);
                gameObject = null;
                scene = default;
                return false;
            }

            return true;
        }
    }
}
