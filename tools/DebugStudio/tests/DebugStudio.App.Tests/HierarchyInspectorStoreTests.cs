#nullable enable

using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests;

/// <summary>
/// hierarchy / inspector の R2 系ストア挙動を固定する回帰テスト。
/// 差分適用の整合性と、遅延到着した inspector detail の巻き戻し防止を主に確認する。
/// </summary>
public sealed class HierarchyInspectorStoreTests
{
    [Fact]
    public void HierarchyStore_baseRevision一致のdeltaは適用される()
    {
        var store = new HierarchyStore();
        store.ApplySnapshot(new HierarchySnapshotEnvelopeV1
        {
            Revision = 1,
            ScopeName = "Loaded Scenes",
            Nodes =
            [
                CreateNode(100, 0, "Root", 0, 0),
            ],
        });

        var snapshot = store.ApplyDelta(new HierarchyDeltaEnvelopeV1
        {
            BaseRevision = 1,
            Revision = 2,
            ScopeName = "Loaded Scenes",
            Changes =
            [
                CreateUpsert(200, 100, "Child", 1, 1),
            ],
        });

        Assert.Equal(2, snapshot.Revision);
        Assert.Equal(2, snapshot.NodeCount);
        Assert.Contains(store.GetSnapshot(), node => node.NodeId == 200 && node.ParentId == 100);
    }

    [Fact]
    public void HierarchyStore_baseRevision不一致のdeltaは無視される()
    {
        var store = new HierarchyStore();
        store.ApplySnapshot(new HierarchySnapshotEnvelopeV1
        {
            Revision = 3,
            ScopeName = "Loaded Scenes",
            Nodes =
            [
                CreateNode(100, 0, "Root", 0, 0),
            ],
        });

        var snapshot = store.ApplyDelta(new HierarchyDeltaEnvelopeV1
        {
            BaseRevision = 2,
            Revision = 4,
            ScopeName = "Loaded Scenes",
            Changes =
            [
                CreateUpsert(200, 100, "Child", 1, 1),
            ],
        });

        Assert.Equal(3, snapshot.Revision);
        Assert.Single(store.GetSnapshot());
        Assert.DoesNotContain(store.GetSnapshot(), node => node.NodeId == 200);
    }

    [Fact]
    public void HierarchyStore_removeで選択中ノードが消えたら選択解除される()
    {
        var store = new HierarchyStore();
        store.ApplySnapshot(new HierarchySnapshotEnvelopeV1
        {
            Revision = 1,
            ScopeName = "Loaded Scenes",
            Nodes =
            [
                CreateNode(100, 0, "Root", 0, 0),
                CreateNode(200, 100, "Child", 1, 1),
            ],
        });
        store.SetSelectedNodeId(200);

        var snapshot = store.ApplyDelta(new HierarchyDeltaEnvelopeV1
        {
            BaseRevision = 1,
            Revision = 2,
            ScopeName = "Loaded Scenes",
            Changes =
            [
                new HierarchyNodeChangeDtoV1
                {
                    ChangeKind = HierarchyChangeKind.Remove,
                    NodeId = 200,
                },
            ],
        });

        Assert.Null(snapshot.SelectedNodeId);
        Assert.DoesNotContain(store.GetSnapshot(), node => node.NodeId == 200);
    }

    [Fact]
    public void InspectorStore_別targetのdetailでは現在表示を巻き戻さない()
    {
        var store = new InspectorStore();
        store.BeginQuery(100, "Root", "GameObject");

        store.ApplyDetail(new InspectorDetailEnvelopeV1
        {
            TargetId = 200,
            TargetName = "Other",
            TargetTypeName = "GameObject",
            Revision = 1,
            State = InspectorDetailState.Ready,
            Message = "wrong target",
        });

        var snapshot = store.GetSnapshotState();
        Assert.Equal(100, snapshot.TargetId);
        Assert.Equal(InspectorDetailState.Pending, snapshot.DetailState);
    }

    [Fact]
    public void InspectorStore_古いrevisionのdetailでは新しい表示を巻き戻さない()
    {
        var store = new InspectorStore();
        store.BeginQuery(100, "Root", "GameObject");
        store.ApplyDetail(new InspectorDetailEnvelopeV1
        {
            TargetId = 100,
            TargetName = "Root",
            TargetTypeName = "GameObject",
            Revision = 10,
            State = InspectorDetailState.Ready,
            Message = "latest",
            Sections =
            [
                new InspectorSectionDtoV1
                {
                    DisplayName = "GameObject",
                    Properties =
                    [
                        new InspectorPropertyDtoV1
                        {
                            DisplayName = "Name",
                            ValueText = "Root",
                        },
                    ],
                },
            ],
        });

        store.ApplyDetail(new InspectorDetailEnvelopeV1
        {
            TargetId = 100,
            TargetName = "Root",
            TargetTypeName = "GameObject",
            Revision = 9,
            State = InspectorDetailState.Ready,
            Message = "stale",
        });

        var snapshot = store.GetSnapshotState();
        Assert.Equal(10, snapshot.Revision);
        Assert.Equal(1, snapshot.PropertyCount);
        Assert.Equal("latest", snapshot.Message);
    }

    private static HierarchyNodeDtoV1 CreateNode(long nodeId, long parentId, string name, int depth, int traversalIndex)
    {
        return new HierarchyNodeDtoV1
        {
            NodeId = nodeId,
            ParentId = parentId,
            TypeId = 1,
            Name = name,
            TypeName = "GameObject",
            Depth = depth,
            TraversalIndex = traversalIndex,
        };
    }

    private static HierarchyNodeChangeDtoV1 CreateUpsert(long nodeId, long parentId, string name, int depth, int traversalIndex)
    {
        return new HierarchyNodeChangeDtoV1
        {
            ChangeKind = HierarchyChangeKind.Upsert,
            NodeId = nodeId,
            ParentId = parentId,
            TypeId = 1,
            Name = name,
            TypeName = "GameObject",
            Depth = depth,
            TraversalIndex = traversalIndex,
        };
    }
}
