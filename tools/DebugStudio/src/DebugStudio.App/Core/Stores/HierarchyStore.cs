#nullable enable

using System;
using System.Collections.Generic;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// 最新 hierarchy を保持する store。
///
/// <para>
/// 初期 wave では viewer-first を優先し、
/// 「最新スナップショット + 上書き可能な差分」を安全に保持するだけに責務を絞る。
/// Unity オブジェクト参照は持たず、node id ベースで扱うため WPF 側も疎結合に保てる。
/// </para>
/// </summary>
public sealed class HierarchyStore
{
    private readonly object _gate = new();
    private readonly Dictionary<long, HierarchyNodeRecord> _nodes = new();
    private long _revision;
    private long _capturedAtUnixTimeMilliseconds;
    private string _scopeName = "Hierarchy";
    private long? _selectedNodeId;

    public event Action<HierarchyStoreSnapshot>? Changed;

    public HierarchyStoreSnapshot ApplySnapshot(HierarchySnapshotEnvelopeV1 snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        HierarchyStoreSnapshot state;
        lock (_gate)
        {
            _nodes.Clear();
            foreach (var node in snapshot.Nodes)
            {
                _nodes[node.NodeId] = HierarchyNodeRecord.FromDto(node);
            }

            _revision = snapshot.Revision;
            _capturedAtUnixTimeMilliseconds = snapshot.CapturedAtUnixTimeMilliseconds;
            _scopeName = string.IsNullOrWhiteSpace(snapshot.ScopeName) ? "Hierarchy" : snapshot.ScopeName;

            if (_selectedNodeId.HasValue && !_nodes.ContainsKey(_selectedNodeId.Value))
            {
                _selectedNodeId = null;
            }

            state = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(state);
        return state;
    }

    public HierarchyStoreSnapshot ApplyDelta(HierarchyDeltaEnvelopeV1 delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        HierarchyStoreSnapshot state;
        lock (_gate)
        {
            // BaseRevision がずれている delta は欠落/順序逆転の可能性が高い。
            // 壊れた tree を作るより、次の snapshot で再同期する方を優先する。
            if (_revision != delta.BaseRevision)
            {
                return CreateSnapshotUnsafe();
            }

            foreach (var change in delta.Changes)
            {
                switch (change.ChangeKind)
                {
                    case HierarchyChangeKind.Upsert:
                        _nodes[change.NodeId] = HierarchyNodeRecord.FromChange(change);
                        break;
                    case HierarchyChangeKind.Remove:
                        _nodes.Remove(change.NodeId);
                        if (_selectedNodeId == change.NodeId)
                        {
                            _selectedNodeId = null;
                        }

                        break;
                }
            }

            _revision = delta.Revision;
            _capturedAtUnixTimeMilliseconds = delta.CapturedAtUnixTimeMilliseconds;
            if (!string.IsNullOrWhiteSpace(delta.ScopeName))
            {
                _scopeName = delta.ScopeName;
            }

            state = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(state);
        return state;
    }

    public HierarchyStoreSnapshot SetSelectedNodeId(long? selectedNodeId)
    {
        HierarchyStoreSnapshot state;
        lock (_gate)
        {
            _selectedNodeId = selectedNodeId.HasValue && _nodes.ContainsKey(selectedNodeId.Value)
                ? selectedNodeId
                : null;
            state = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(state);
        return state;
    }

    public HierarchyStoreSnapshot Clear()
    {
        HierarchyStoreSnapshot state;
        lock (_gate)
        {
            _nodes.Clear();
            _revision = 0;
            _capturedAtUnixTimeMilliseconds = 0;
            _scopeName = "Hierarchy";
            _selectedNodeId = null;
            state = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(state);
        return state;
    }

    public HierarchyStoreSnapshot GetSnapshotState()
    {
        lock (_gate)
        {
            return CreateSnapshotUnsafe();
        }
    }

    public IReadOnlyList<HierarchyNodeRecord> GetSnapshot()
    {
        lock (_gate)
        {
            return CreateSortedNodesUnsafe();
        }
    }

    /// <summary>
    /// hierarchy export 用に state と node 列を同一 lock 下で複製する。
    /// これにより export service は revision / selected node / node 列が揃った snapshot を扱える。
    /// </summary>
    public HierarchyRetainedSnapshot GetRetainedSnapshot()
    {
        lock (_gate)
        {
            return new HierarchyRetainedSnapshot(
                CreateSnapshotUnsafe(),
                CreateSortedNodesUnsafe());
        }
    }

    private HierarchyStoreSnapshot CreateSnapshotUnsafe()
    {
        return new HierarchyStoreSnapshot(
            _revision,
            _capturedAtUnixTimeMilliseconds,
            _scopeName,
            _nodes.Count,
            _selectedNodeId);
    }

    private HierarchyNodeRecord[] CreateSortedNodesUnsafe()
    {
        var nodes = new List<HierarchyNodeRecord>(_nodes.Values);
        nodes.Sort(static (left, right) =>
        {
            var traversalComparison = left.TraversalIndex.CompareTo(right.TraversalIndex);
            return traversalComparison != 0
                ? traversalComparison
                : left.NodeId.CompareTo(right.NodeId);
        });
        return nodes.ToArray();
    }
}
