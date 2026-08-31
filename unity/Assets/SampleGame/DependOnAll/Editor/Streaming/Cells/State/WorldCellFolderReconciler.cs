#nullable enable

using System.Collections.Generic;
using System.IO;
using OneStarMaker.Editor.SceneGraph;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Generation;
using SampleGame.DependOnAll.Editor.Streaming.Cells.Planning;
using UnityEditor;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.DependOnAll.Editor.Streaming.Cells.State
{
    /// <summary>target 集合外の Cell identity フォルダと参照を整合させる。</summary>
    internal static class WorldCellFolderReconciler
    {
        private const string CellsRootFolder = "Assets/SampleGame/InGame/InGameSession/World/Cells";
        private const string SceneGraphCellsFolder = "Assets/SceneGraphData/Nodes/Cells";
        private const string TotalGraphPath = "Assets/SceneGraphData/Graphs/Total.asset";

        public static void DeleteOutOfGridCellFolders(
            SceneResourceMap map,
            SceneResource world,
            CellPopulationPlan plan)
            => Reconcile(
                map,
                world,
                plan,
                CellsRootFolder,
                SceneGraphCellsFolder,
                TotalGraphPath);

        // 本番は上記定数を使い、テストでは EditMode 用にルートだけ差し替える。
        internal static void Reconcile(
            SceneResourceMap map,
            SceneResource world,
            CellPopulationPlan plan,
            string cellsRootFolder,
            string sceneGraphCellsFolder,
            string totalGraphPath)
        {
            if (map == null) throw new System.ArgumentNullException(nameof(map));
            if (world == null) throw new System.ArgumentNullException(nameof(world));
            if (plan == null) throw new System.ArgumentNullException(nameof(plan));
            if (string.IsNullOrWhiteSpace(cellsRootFolder))
                throw new System.ArgumentException("Cells ルートが未指定です。", nameof(cellsRootFolder));
            if (string.IsNullOrWhiteSpace(sceneGraphCellsFolder))
                throw new System.ArgumentException("SceneGraph ルートが未指定です。", nameof(sceneGraphCellsFolder));
            if (string.IsNullOrWhiteSpace(totalGraphPath))
                throw new System.ArgumentException("Total graph が未指定です。", nameof(totalGraphPath));
            if (!AssetDatabase.IsValidFolder(cellsRootFolder)) return;

            var identitiesToDrop = new HashSet<string>(System.StringComparer.Ordinal);
            var foldersToDelete = new List<string>();
            var subFolders = AssetDatabase.GetSubFolders(cellsRootFolder);
            for (var i = 0; i < subFolders.Length; i++)
            {
                var folder = subFolders[i].Replace('\\', '/');
                var folderIdentity = Path.GetFileName(folder);
                if (!plan.IsDeletable(folderIdentity))
                {
                    continue;
                }

                // フォルダ内の全 SceneResource identity を先に確保し、Environment_* も同時に除去する。
                var resourceGuids = AssetDatabase.FindAssets("t:SceneResource", new[] { folder });
                for (var r = 0; r < resourceGuids.Length; r++)
                {
                    var resourcePath = AssetDatabase.GUIDToAssetPath(resourceGuids[r]);
                    var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(resourcePath);
                    if (resource != null)
                    {
                        identitiesToDrop.Add(resource.Identity);
                    }
                }

                identitiesToDrop.Add(folderIdentity);
                foldersToDelete.Add(folder);
            }

            if (identitiesToDrop.Count == 0)
            {
                return;
            }

            RemoveReferencesBeforeFolderDeletion(
                map, world, identitiesToDrop, sceneGraphCellsFolder, totalGraphPath);
            var deletedFolders = DeleteFoldersAfterReferences(foldersToDelete);
            UnityEngine.Debug.Log(
                $"[WorldCellFolderReconciler] Out-of-grid cleanup: folders={deletedFolders}, identitiesDropped={identitiesToDrop.Count}");
        }

        private static void RemoveReferencesBeforeFolderDeletion(
            SceneResourceMap map,
            SceneResource world,
            HashSet<string> identitiesToDrop,
            string sceneGraphCellsFolder,
            string totalGraphPath)
        {
            // 先に Graph から収集済み identity を除去する。フォルダ削除後に名前を再構築しない。
            RemoveFromSceneGraph(identitiesToDrop, sceneGraphCellsFolder, totalGraphPath);

            // Map の compact / dictionary 再構築は OneStarMaker.Editor の境界へ集約する。
            WorldCellGenerator.RemoveSceneResourcesFromMap(map, identitiesToDrop);
            var worldSo = new SerializedObject(world);
            var childrenProp = worldSo.FindProperty("_children");
            var keep = new List<SceneResource>(childrenProp.arraySize);
            for (var i = 0; i < childrenProp.arraySize; i++)
            {
                var child = childrenProp.GetArrayElementAtIndex(i).objectReferenceValue as SceneResource;
                if (child != null && !identitiesToDrop.Contains(child.Identity))
                {
                    keep.Add(child);
                }
            }

            childrenProp.ClearArray();
            for (var i = 0; i < keep.Count; i++)
            {
                childrenProp.InsertArrayElementAtIndex(i);
                childrenProp.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            }

            worldSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            EditorUtility.SetDirty(map);
        }

        private static int DeleteFoldersAfterReferences(IReadOnlyList<string> foldersToDelete)
        {
            // Graph / Map / World の参照を落とした後で、Cell 作業単位フォルダを削除する。
            var deletedFolders = 0;
            for (var i = 0; i < foldersToDelete.Count; i++)
            {
                if (AssetDatabase.DeleteAsset(foldersToDelete[i]))
                {
                    deletedFolders++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deletedFolders;
        }

        private static void RemoveFromSceneGraph(
            HashSet<string> identities,
            string sceneGraphCellsFolder,
            string totalGraphPath)
        {
            if (identities.Count == 0)
            {
                return;
            }

            var totalGraph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(totalGraphPath);
            if (totalGraph != null)
            {
                var graphNodes = new List<SceneNodeData>();
                for (var i = 0; i < totalGraph.GraphNodes.Count; i++)
                {
                    var node = totalGraph.GraphNodes[i];
                    if (node != null && identities.Contains(node.Identity))
                    {
                        graphNodes.Add(node);
                    }
                }

                for (var i = 0; i < graphNodes.Count; i++)
                {
                    var node = graphNodes[i];
                    totalGraph.RemoveNode(node);
                    var path = AssetDatabase.GetAssetPath(node);
                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }

                EditorUtility.SetDirty(totalGraph);
            }

            if (!AssetDatabase.IsValidFolder(sceneGraphCellsFolder))
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:SceneNodeData", new[] { sceneGraphCellsFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);
                if (node != null && identities.Contains(node.Identity))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

    }
}
