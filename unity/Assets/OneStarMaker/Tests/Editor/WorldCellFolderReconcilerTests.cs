#nullable enable

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor
{
    [TestFixture]
    public sealed class WorldCellFolderReconcilerTests
    {
        private const string Root = "Assets/TestReconciler";
        private const string CellsRoot = Root + "/Cells";
        private const string GraphRoot = Root + "/Graph";
        private const string MapPath = Root + "/Map.asset";
        private const string WorldPath = Root + "/World.asset";
        private const string GraphPath = GraphRoot + "/Total.asset";
        private readonly List<Object> _transientObjects = new();

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            foreach (var obj in _transientObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            _transientObjects.Clear();
        }

        [Test]
        public void ReconcileStages_RemoveReferencesBeforeDeletingOutOfTargetFolder()
        {
            var orphanFolder = CellsRoot + "/Arbitrary_Orphan";
            EnsureFolder(orphanFolder);
            EnsureFolder(GraphRoot);

            var map = CreatePersisted<SceneResourceMap>(MapPath);
            var world = CreatePersistedSceneResource(WorldPath, "World");
            var orphan = CreatePersistedSceneResource(
                orphanFolder + "/Arbitrary_Orphan.asset", "Arbitrary_Orphan");
            var retained = CreatePersistedSceneResource(Root + "/Retained.asset", "Retained");
            AddMapEntry(map, orphan);
            AddMapEntry(map, retained);
            AddChild(world, orphan);
            AddChild(world, retained);

            var graph = CreatePersisted<SceneGraphEdges>(GraphPath);
            var node = CreatePersisted<SceneNodeData>(GraphRoot + "/Arbitrary_Orphan.asset");
            node.Identity = "Arbitrary_Orphan";
            graph.AddNode(node);
            var retainedNode = CreatePersisted<SceneNodeData>(GraphRoot + "/Retained.asset");
            retainedNode.Identity = "Retained";
            graph.AddNode(retainedNode);
            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();

            var identitiesToDrop = new HashSet<string>(System.StringComparer.Ordinal)
            {
                "Arbitrary_Orphan",
            };
            InvokeStage(
                "RemoveReferencesBeforeFolderDeletion",
                new object[] { map, world, identitiesToDrop, GraphRoot, GraphPath });
            Assert.That(AssetDatabase.IsValidFolder(orphanFolder), Is.True,
                "参照除去時点では対象フォルダがまだ存在すること");
            Assert.That(map.SceneResources, Has.Count.EqualTo(1),
                "Map に null hole を残さず保持リソースを残すこと");
            Assert.That(map.SceneResources[0], Is.SameAs(retained));
            Assert.That(map.GetSceneResource("Retained"), Is.SameAs(retained),
                "保持リソースの dictionary を最新化すること");
            Assert.That(map.GetSceneResource("Arbitrary_Orphan"), Is.Null,
                "Map dictionary も参照除去後に更新されること");
            Assert.That(world.Children, Has.Count.EqualTo(1));
            Assert.That(world.Children[0], Is.SameAs(retained));
            foreach (var graphNode in graph.GraphNodes)
            {
                Assert.That(graphNode == null || graphNode.Identity != "Arbitrary_Orphan", Is.True);
            }
            Assert.That(graph.GraphNodes, Has.Count.EqualTo(1));
            Assert.That(graph.GraphNodes[0], Is.Not.Null);
            Assert.That(graph.GraphNodes[0]!.Identity, Is.EqualTo("Retained"));

            var deletedFolders = (int)InvokeStage(
                "DeleteFoldersAfterReferences", new object[] { new List<string> { orphanFolder } });
            Assert.That(deletedFolders, Is.EqualTo(1));
            Assert.That(AssetDatabase.IsValidFolder(orphanFolder), Is.False,
                "範囲外フォルダが削除されること");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneResource>(
                orphanFolder + "/Arbitrary_Orphan.asset"), Is.Null,
                "フォルダ内の SceneResource asset も削除されること");
            var reloadedMap = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(MapPath);
            var reloadedWorld = AssetDatabase.LoadAssetAtPath<SceneResource>(WorldPath);
            var reloadedGraph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(GraphPath);
            Assert.That(reloadedMap, Is.Not.Null);
            Assert.That(reloadedMap!.SceneResources, Has.Count.EqualTo(1));
            Assert.That(reloadedMap.SceneResources[0], Is.Not.Null);
            Assert.That(reloadedMap.SceneResources[0]!.Identity, Is.EqualTo("Retained"));
            Assert.That(reloadedMap.GetSceneResource("Retained"), Is.Not.Null,
                "保持リソースの dictionary を保持すること");
            Assert.That(reloadedMap.GetSceneResource("Arbitrary_Orphan"), Is.Null,
                "フォルダ削除前に Map 参照が除去されること");
            Assert.That(reloadedWorld, Is.Not.Null);
            Assert.That(reloadedWorld!.Children, Has.Count.EqualTo(1),
                "フォルダ削除前に World children 参照が除去されること");
            Assert.That(reloadedWorld.Children[0], Is.Not.Null);
            Assert.That(reloadedWorld.Children[0]!.Identity, Is.EqualTo("Retained"));
            Assert.That(reloadedGraph, Is.Not.Null);
            var orphanNodeRemains = false;
            foreach (var graphNode in reloadedGraph!.GraphNodes)
            {
                if (graphNode != null && graphNode.Identity == "Arbitrary_Orphan")
                {
                    orphanNodeRemains = true;
                    break;
                }
            }

            Assert.That(orphanNodeRemains, Is.False,
                "フォルダ削除前に SceneGraph 参照が除去されること");
            Assert.That(reloadedGraph.GraphNodes, Has.Count.EqualTo(1));
            Assert.That(reloadedGraph.GraphNodes[0], Is.Not.Null);
            Assert.That(reloadedGraph.GraphNodes[0]!.Identity, Is.EqualTo("Retained"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneNodeData>(
                GraphRoot + "/Arbitrary_Orphan.asset"), Is.Null,
                "SceneGraph ノード asset も削除されること");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneNodeData>(
                GraphRoot + "/Retained.asset"), Is.Not.Null);
        }

        private static object? InvokeStage(string methodName, object[] arguments)
        {
            var reconcilerType = typeof(CellPopulationPlan).Assembly.GetType(
                "SampleGame.DependOnAll.Editor.Streaming.Cells.State.WorldCellFolderReconciler");
            Assert.That(reconcilerType, Is.Not.Null);
            var method = reconcilerType!.GetMethod(
                methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method!.Invoke(null, arguments);
        }

        private T CreatePersisted<T>(string path) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _transientObjects.Add(asset);
            AssetDatabase.CreateAsset(asset, path);
            _transientObjects.Remove(asset);
            return asset;
        }

        private SceneResource CreatePersistedSceneResource(string path, string identity)
        {
            var resource = CreatePersisted<SceneResource>(path);
            resource.Identity = identity;
            EditorUtility.SetDirty(resource);
            AssetDatabase.SaveAssets();
            return resource;
        }

        private static void AddMapEntry(SceneResourceMap map, SceneResource resource)
        {
            var so = new SerializedObject(map);
            var list = so.FindProperty("_sceneResources");
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = resource;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddChild(SceneResource parent, SceneResource child)
        {
            var so = new SerializedObject(parent);
            var children = so.FindProperty("_children");
            children.InsertArrayElementAtIndex(children.arraySize);
            children.GetArrayElementAtIndex(children.arraySize - 1).objectReferenceValue = child;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
