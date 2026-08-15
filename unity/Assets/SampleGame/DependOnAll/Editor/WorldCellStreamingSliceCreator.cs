#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneStarMaker.Editor.SceneGraph;
using OneStarMaker.Editor.Streaming;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using SampleGame.DependOnAll.Editor.Cells;
using SampleGame.InGame.Streaming;
using SampleGame.InGame.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace SampleGame.DependOnAll.Editor
{
    /// <summary>
    /// World + 4×4 Cell（250m）を一括生成し、InGameSession ツリーへ配線する。
    /// CCS-00: 実行物を <c>InGameSession/World/</c> 配下へ集約（フォルダ = 実行環境境界）。
    /// CCS-01〜02: 萌芽 Cell（南辺 0_0〜3_0）に Environment 子を OnDemand で付ける。
    /// グリッド縮小時は範囲外の <c>Cells/Cell_*</c> フォルダを丸ごと削除する。
    /// <para>
    /// このクラスはスキャフォールドであり、S-2（季節化）完了後の削除候補である。
    /// 恒久的な判断は <c>Cells/CellPopulationPlan.cs</c> にのみ置き、ここには「どの順で何を呼ぶか」と使い捨ての生成手続きだけを置く。
    /// 構造化の投資をしないと決めた（HANDOFF §2.1）。
    /// </para>
    /// </summary>
    public static class WorldCellStreamingSliceCreator
    {
        private const string MenuPath = "OneStarMaker/Sample/Create World + Cell Streaming Slice";
        private const string BatchMethod =
            "SampleGame.DependOnAll.Editor.WorldCellStreamingSliceCreator.CreateFromBatch";

        private const string SceneResourceMapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";
        private const string InGameSessionResourcePath = "Assets/OneStarMakerCommon/SceneMap/InGameSession.asset";
        /// <summary>カタログ側の World SceneResource（実体 .unity は SampleGame World 配下）。</summary>
        private const string WorldResourcePath = "Assets/OneStarMakerCommon/SceneMap/World.asset";
        private const string WorldScenePath = "Assets/SampleGame/InGame/InGameSession/World/World.unity";
        private const string WorldRootFolder = "Assets/SampleGame/InGame/InGameSession/World";
        private const string CellsRootFolder = WorldRootFolder + "/Cells";
        private const string MaterialsFolder = WorldRootFolder + "/Materials";
        private const string GridDefinitionPath = WorldRootFolder + "/WorldGridDefinition.asset";

        private const string SceneGraphNodesFolder = "Assets/SceneGraphData/Nodes";
        private const string SceneGraphCellsFolder = SceneGraphNodesFolder + "/Cells";
        private const string TotalGraphPath = "Assets/SceneGraphData/Graphs/Total.asset";
        private const string WorldNodePath = SceneGraphNodesFolder + "/World.asset";
        private const string InGameSessionNodePath = SceneGraphNodesFolder + "/InGameSession.asset";

        /// <summary>
        /// Environment 萌芽を付ける Cell 座標（4×4 の南辺すべて）。
        /// 北側は葉のままにして、引っ張られないことと作業単位分割を体感できるようにする。
        /// </summary>
        private static readonly Vector2Int[] EnvironmentSproutCells =
        {
            new(0, 0),
            new(1, 0),
            new(2, 0),
            new(3, 0),
        };

        /// <summary>Editor メニューから実行。</summary>
        [MenuItem(MenuPath)]
        public static void CreateFromMenu()
        {
            CreateCore();
        }

        /// <summary>
        /// Unity batchmode 用エントリ。
        /// <c>-executeMethod SampleGame.DependOnAll.Editor.WorldCellStreamingSliceCreator.CreateFromBatch</c>
        /// </summary>
        public static void CreateFromBatch()
        {
            try
            {
                CreateCore();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldCellStreamingSliceCreator] FAILED: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void CreateCore()
        {
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(SceneResourceMapPath)
                ?? throw new FileNotFoundException(SceneResourceMapPath);
            var session = AssetDatabase.LoadAssetAtPath<SceneResource>(InGameSessionResourcePath)
                ?? throw new FileNotFoundException(InGameSessionResourcePath);

            EnsureFolders();

            var worldResource = EnsureWorldResource(session, map);
            var definition = EnsureGridDefinition();

            var existingStates = CollectExistingStates(definition);
            var plan = CellPopulationPlan.Compute(
                new CellGridSpec(definition.GridWidth, definition.GridHeight, definition.Origin, definition.CellSize),
                existingStates);

            // 10×10 → 4×4 等の縮小で余った Cell_* フォルダを Map ごと除去する。
            DeleteOutOfGridCellFolders(map, worldResource, definition, plan);

            if (!WorldCellGenerator.Generate(definition, map, worldResource))
            {
                throw new System.InvalidOperationException("WorldCellGenerator.Generate が false を返しました。");
            }

            // Generate は CreateAsset 前に親._children / Map へ未保存 SO を差し込むため、
            // 保存後は fileID:0 が残ることがある。null 枠を先に畳んでから親子を再配線する。
            CompactNullMapEntries(map);
            RelinkWorldChildren(worldResource, definition);

            // 共有 Lit を World 階層に 1 本置き、各 Cell / Environment はそれを参照 + ランタイム MPB。
            var sharedLit = EnsureSharedLitMaterial();
            PopulateWorldScene(sharedLit);

            // ランタイム生成禁止: 床・目印は各 .unity に事前配置する。
            PopulateCellVisuals(definition, sharedLit, plan);

            // PopulateWorldScene の finally が untitled を残し得るため、萌芽作成前に破棄する。
            // （未保存 untitled があると Additive Open / NewScene が失敗する。）
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 萌芽: Cell 配下に Environment 子（OnDemand）。Ground を Environment 側へ移す。
            CreateEnvironmentSprouts(map, definition, sharedLit, plan);
            CompactNullMapEntries(map);
            // ディスク上の Cell / Environment SceneResource を Map へ再登録（null 枠除去後の再同期）。
            RelinkMapFromDisk(map, definition);

            RegisterAddressableScene(WorldScenePath);
            RegisterAddressableAsset(WorldMaterialBindings.SharedLitAssetPath);
            RegisterCellAddressables(definition);
            RegisterEnvironmentAddressables();

            // Session 子に World を必ず含める（NecessaryAlways）。
            EnsureChildLink(session, worldResource);

            // Scene Graph Editor 可視化: Node + Edge を同期し、Generate で Map ハッシュも揃える。
            SyncSceneGraph(definition);

            EditorUtility.SetDirty(session);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(worldResource);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var maxX = WorldCellCatalog.GridWidth - 1;
            var maxY = WorldCellCatalog.GridHeight - 1;
            Debug.Log(
                "[WorldCellStreamingSliceCreator] 完了。\n" +
                $"1. World + Cell_0_0 .. Cell_{maxX}_{maxY}（{WorldCellCatalog.CellSize}m, " +
                $"{WorldCellCatalog.GridWidth}x{WorldCellCatalog.GridHeight}）を {CellsRootFolder}/ に集約。\n" +
                "2. Environment_0_0 .. Environment_3_0 を萌芽（OnDemand・明示 Add）。\n" +
                "3. Scene Graph（Total）に Cell/Environment ノードを同期済み。\n" +
                $"BatchMethod={BatchMethod}");
        }

        /// <summary>
        /// Scene Graph 中間データに World → Cell_* → Environment_* を載せ、
        /// SceneResourceGenerator.Generate で Map / 親子 / GenerateHash を正本化する。
        /// Cell SceneResource 実体は既存の World/Cells パスを再利用する（Generator 側で identity 解決）。
        /// </summary>
        private static void SyncSceneGraph(WorldGridDefinition definition)
        {
            EnsureAssetFolder(SceneGraphCellsFolder);

            var totalGraph = AssetDatabase.LoadAssetAtPath<SceneGraphEdges>(TotalGraphPath)
                ?? throw new FileNotFoundException(TotalGraphPath);
            var worldNode = AssetDatabase.LoadAssetAtPath<SceneNodeData>(WorldNodePath)
                ?? throw new FileNotFoundException(WorldNodePath);
            var sessionNode = AssetDatabase.LoadAssetAtPath<SceneNodeData>(InGameSessionNodePath)
                ?? throw new FileNotFoundException(InGameSessionNodePath);

            totalGraph.GraphNodes.RemoveAll(node => node == null);
            totalGraph.AddNode(sessionNode);
            totalGraph.AddNode(worldNode);
            EnsureEdge(totalGraph, sessionNode, worldNode);

            // World ノードの payload / LoadType を実行シーンに揃える。
            EnsureSceneGraphNode(
                WorldCellCatalog.WorldIdentity,
                WorldNodePath,
                WorldScenePath,
                LoadType.NecessaryAlways);

            var keepIdentities = new HashSet<string>(System.StringComparer.Ordinal)
            {
                WorldCellCatalog.WorldIdentity,
            };

            var sproutSet = BuildSproutSet();
            for (var x = 0; x < definition.GridWidth; x++)
            {
                for (var y = 0; y < definition.GridHeight; y++)
                {
                    var cellId = CellIdentity.Format(x, y);
                    var cellScenePath = $"{CellsRootFolder}/{cellId}/{cellId}.unity";
                    var cellNodePath = $"{SceneGraphCellsFolder}/{cellId}.asset";
                    var cellNode = EnsureSceneGraphNode(
                        cellId,
                        cellNodePath,
                        cellScenePath,
                        LoadType.OnDemand);

                    totalGraph.AddNode(cellNode);
                    EnsureEdge(totalGraph, worldNode, cellNode);
                    keepIdentities.Add(cellId);

                    var coord = new Vector2Int(x, y);
                    if (!sproutSet.Contains(coord))
                    {
                        continue;
                    }

                    var envId = EnvironmentIdentity.Format(x, y);
                    var envScenePath = $"{CellsRootFolder}/{cellId}/{envId}.unity";
                    if (AssetDatabase.LoadAssetAtPath<SceneAsset>(envScenePath) == null)
                    {
                        continue;
                    }

                    var envNodePath = $"{SceneGraphCellsFolder}/{envId}.asset";
                    var envNode = EnsureSceneGraphNode(
                        envId,
                        envNodePath,
                        envScenePath,
                        LoadType.OnDemand);

                    totalGraph.AddNode(envNode);
                    EnsureEdge(totalGraph, cellNode, envNode);
                    keepIdentities.Add(envId);
                }
            }

            PruneStaleCellSceneGraphNodes(totalGraph, keepIdentities);
            EditorUtility.SetDirty(totalGraph);
            AssetDatabase.SaveAssets();

            var nodes = AssetDatabase.FindAssets("t:SceneNodeData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneNodeData>)
                .Where(node => node != null)
                .Cast<SceneNodeData>()
                .ToList();
            var graphs = AssetDatabase.FindAssets("t:SceneGraphEdges")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<SceneGraphEdges>)
                .Where(graph => graph != null)
                .Cast<SceneGraphEdges>()
                .ToList();

            if (!SceneResourceGenerator.Generate(nodes, graphs))
            {
                throw new System.InvalidOperationException(
                    "Scene Graph Generate が失敗しました。Console の検証エラーを確認してください。");
            }

            Debug.Log(
                $"[WorldCellStreamingSliceCreator] Scene Graph synced: " +
                $"cells+envs kept={keepIdentities.Count - 1} under World");
        }

        private static SceneNodeData EnsureSceneGraphNode(
            string identity,
            string nodeAssetPath,
            string scenePath,
            LoadType loadType)
        {
            EnsureAssetFolder(Path.GetDirectoryName(nodeAssetPath)!.Replace('\\', '/'));

            var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(nodeAssetPath);
            if (node == null)
            {
                node = ScriptableObject.CreateInstance<SceneNodeData>();
                AssetDatabase.CreateAsset(node, nodeAssetPath);
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath, AssetPathToGUIDOptions.OnlyExistingAssets);
            node.Identity = identity;
            node.NodeLoadType = loadType;
            node.Payloads.Clear();
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                node.Payloads.Add(new AssetPayload(string.Empty, new AssetReference(sceneGuid)));
            }

            EditorUtility.SetDirty(node);
            return node;
        }

        private static void EnsureEdge(SceneGraphEdges graph, SceneNodeData parent, SceneNodeData child)
        {
            if (graph.Edges.Any(edge => edge.Parent == parent && edge.Child == child))
            {
                return;
            }

            graph.RemoveEdgeByChild(child);
            graph.AddEdge(parent, child);
        }

        /// <summary>
        /// グリッド外・廃止した Cell_/Environment_ の SceneNodeData を Graph とディスクから除去する。
        /// </summary>
        private static void PruneStaleCellSceneGraphNodes(
            SceneGraphEdges totalGraph,
            HashSet<string> keepIdentities)
        {
            var stale = new List<SceneNodeData>();
            for (var i = 0; i < totalGraph.GraphNodes.Count; i++)
            {
                var node = totalGraph.GraphNodes[i];
                if (node == null)
                {
                    continue;
                }

                var id = node.Identity;
                if (!CellIdentity.IsCellId(id) && !EnvironmentIdentity.IsEnvironmentId(id))
                {
                    continue;
                }

                if (!keepIdentities.Contains(id))
                {
                    stale.Add(node);
                }
            }

            for (var i = 0; i < stale.Count; i++)
            {
                var node = stale[i];
                totalGraph.RemoveNode(node);
                var path = AssetDatabase.GetAssetPath(node);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[WorldCellStreamingSliceCreator] Removed stale SceneGraph node: {path}");
                }
            }

            // Cells フォルダに残った孤立ノードも掃除（Graph 未登録分）。
            if (AssetDatabase.IsValidFolder(SceneGraphCellsFolder))
            {
                var guids = AssetDatabase.FindAssets("t:SceneNodeData", new[] { SceneGraphCellsFolder });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var node = AssetDatabase.LoadAssetAtPath<SceneNodeData>(path);
                    if (node == null || keepIdentities.Contains(node.Identity))
                    {
                        continue;
                    }

                    totalGraph.RemoveNode(node);
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"[WorldCellStreamingSliceCreator] Removed orphan SceneGraph node: {path}");
                }
            }
        }

        private static List<CellExistingState> CollectExistingStates(WorldGridDefinition definition)
        {
            var result = new List<CellExistingState>();
            if (!AssetDatabase.IsValidFolder(CellsRootFolder))
            {
                return result;
            }

            var subFolders = AssetDatabase.GetSubFolders(CellsRootFolder);
            for (var i = 0; i < subFolders.Length; i++)
            {
                var folder = subFolders[i].Replace('\\', '/');
                var folderName = Path.GetFileName(folder);
                if (!CellIdentity.TryParse(folderName, out var coordinate))
                {
                    continue;
                }

                var cellScenePath = $"{folder}/{folderName}.unity";
                var hasCellRoot = AssetDatabase.LoadAssetAtPath<SceneAsset>(cellScenePath) != null
                    && SceneHasAuthoredRoot(cellScenePath, DemoCellScene.AuthoredRootName);

                var envScenePath = $"{folder}/{EnvironmentIdentity.Format(coordinate.x, coordinate.y)}.unity";
                var hasEnvScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(envScenePath) != null;
                var hasEnvRoot = hasEnvScene
                    && SceneHasAuthoredRoot(envScenePath, EnvironmentScene.AuthoredRootName);

                result.Add(new CellExistingState(
                    folderName, coordinate, hasCellRoot, hasEnvScene, hasEnvRoot));
            }

            return result;
        }

        private static bool SceneHasAuthoredRoot(string scenePath, string authoredRootName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root != null && root.name == authoredRootName)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        /// <summary>
        /// 現行グリッド外の <c>Cells/Cell_x_y/</c> をフォルダごと削除し、Map / World 子からも外す。
        /// Environment_* も同フォルダ内なので一緒に消える。Map に残った identity も掃除する。
        /// </summary>
        private static void DeleteOutOfGridCellFolders(
            SceneResourceMap map,
            SceneResource world,
            WorldGridDefinition definition,
            CellPopulationPlan plan)
        {
            if (!AssetDatabase.IsValidFolder(CellsRootFolder))
            {
                return;
            }

            var deletableCells = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < plan.DeletionEntries.Count; i++)
            {
                deletableCells.Add(plan.DeletionEntries[i].Identity);
            }

            var deletedFolders = 0;
            var guids = AssetDatabase.FindAssets("t:SceneResource", new[] { CellsRootFolder });
            var identitiesToDrop = new HashSet<string>(System.StringComparer.Ordinal);

            // 先に「範囲外 identity」を収集（フォルダ削除前にパスから読む）。
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                if (resource == null)
                {
                    continue;
                }

                if (CellIdentity.TryParse(resource.Identity, out var cellCoord))
                {
                    if (cellCoord.x >= definition.GridWidth
                        || cellCoord.y >= definition.GridHeight)
                    {
                        if (deletableCells.Contains(resource.Identity))
                        {
                            identitiesToDrop.Add(resource.Identity);
                        }
                    }

                    continue;
                }

                if (EnvironmentIdentity.TryParse(resource.Identity, out var envCoord))
                {
                    if (envCoord.x >= definition.GridWidth
                        || envCoord.y >= definition.GridHeight)
                    {
                        var cellId = CellIdentity.Format(envCoord.x, envCoord.y);
                        if (deletableCells.Contains(cellId))
                        {
                            identitiesToDrop.Add(resource.Identity);
                        }
                    }
                }
            }

            // サブフォルダ走査: Cell_* でグリッド外かつ削除計画にあるものだけ DeleteAsset(フォルダ)。
            var subFolders = AssetDatabase.GetSubFolders(CellsRootFolder);
            for (var i = 0; i < subFolders.Length; i++)
            {
                var folder = subFolders[i].Replace('\\', '/');
                var folderName = Path.GetFileName(folder);
                if (!CellIdentity.TryParse(folderName, out var coordinate))
                {
                    continue;
                }

                if (coordinate.x < definition.GridWidth
                    && coordinate.y < definition.GridHeight)
                {
                    continue;
                }

                var cellId = CellIdentity.Format(coordinate.x, coordinate.y);
                if (!deletableCells.Contains(cellId))
                {
                    Debug.LogWarning(
                        $"[WorldCellStreamingSliceCreator] 範囲外だが HandAuthored なので保持した: {cellId}");
                    continue;
                }

                identitiesToDrop.Add(cellId);
                if (EnvironmentIdentity.TryFromCellId(cellId, out var envId))
                {
                    identitiesToDrop.Add(envId);
                }

                if (AssetDatabase.DeleteAsset(folder))
                {
                    deletedFolders++;
                    Debug.Log($"[WorldCellStreamingSliceCreator] Deleted out-of-grid folder: {folder}");
                }
            }

            foreach (var identity in identitiesToDrop)
            {
                RemoveFromMap(map, identity);
            }

            // World._children から消えた参照を落とす（Relink 前のゴミ防止）。
            if (identitiesToDrop.Count > 0)
            {
                var worldSo = new SerializedObject(world);
                var childrenProp = worldSo.FindProperty("_children");
                var keep = new List<SceneResource>();
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
                CompactNullMapEntries(map);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[WorldCellStreamingSliceCreator] Out-of-grid cleanup: folders={deletedFolders}, " +
                $"identitiesDropped={identitiesToDrop.Count}");
        }

        private static void EnsureFolders()
        {
            EnsureAssetFolder(WorldRootFolder);
            EnsureAssetFolder(CellsRootFolder);
            EnsureAssetFolder(MaterialsFolder);
        }

        /// <summary>
        /// World 階層に置く共有 Lit を 1 本だけ確保する（GPU Instancing ON）。
        /// セル色はランタイム MaterialPropertyBlock に任せる。
        /// </summary>
        private static Material EnsureSharedLitMaterial()
        {
            var path = WorldMaterialBindings.SharedLitAssetPath;
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color");
                material = new Material(shader)
                {
                    name = "DemoCellLit",
                    enableInstancing = true,
                };
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", Color.white);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", Color.white);
                }

                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            return material;
        }

        private static void PopulateWorldScene(Material sharedLit)
        {
            var scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
            try
            {
                WorldMaterialBindings? bindings = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    bindings = root.GetComponent<WorldMaterialBindings>()
                        ?? root.GetComponentInChildren<WorldMaterialBindings>();
                    if (bindings != null)
                    {
                        break;
                    }
                }

                if (bindings == null)
                {
                    var go = new GameObject("WorldMaterialBindings");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    bindings = go.AddComponent<WorldMaterialBindings>();
                }

                var so = new SerializedObject(bindings);
                so.FindProperty("_sharedLit").objectReferenceValue = sharedLit;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[WorldCellStreamingSliceCreator] World.unity に共有 Lit を配線");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        /// <summary>
        /// 各 Cell .unity に DemoCellRoot を焼き込む。
        /// 萌芽 Cell は Marker のみ（Ground は Environment 側）。葉 Cell は従来どおり Ground+Marker。
        /// </summary>
        private static void PopulateCellVisuals(
            WorldGridDefinition definition,
            Material sharedLit,
            CellPopulationPlan plan)
        {
            var sceneFolder = definition.SceneOutputFolder.Replace('\\', '/').TrimEnd('/');
            var sproutSet = BuildSproutSet();
            var populated = 0;
            var skipped = 0;

            for (var i = 0; i < plan.PopulationEntries.Count; i++)
            {
                var entry = plan.PopulationEntries[i];
                var identity = entry.Identity;
                var scenePath = $"{sceneFolder}/{identity}/{identity}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    throw new FileNotFoundException($"Cell シーンがありません: {scenePath}");
                }

                if (entry.CellAction == CellPopulationAction.Skip)
                {
                    skipped++;
                    continue;
                }

                var includeGround = !sproutSet.Contains(entry.Coordinate);
                PopulateSingleCellScene(
                    scenePath,
                    entry.Coordinate.x,
                    entry.Coordinate.y,
                    sharedLit,
                    includeGround);
                populated++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[WorldCellStreamingSliceCreator] Populated authored visuals in {populated} cell scenes " +
                $"(skipped={skipped}, sprout cells={sproutSet.Count} keep Marker only)");
        }

        private static HashSet<Vector2Int> BuildSproutSet()
        {
            var set = new HashSet<Vector2Int>();
            for (var i = 0; i < EnvironmentSproutCells.Length; i++)
            {
                set.Add(EnvironmentSproutCells[i]);
            }

            return set;
        }

        private static void PopulateSingleCellScene(
            string scenePath,
            int x,
            int y,
            Material sharedLit,
            bool includeGround)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root != null && root.name == DemoCellScene.AuthoredRootName)
                    {
                        Object.DestroyImmediate(root);
                    }
                }

                var center = WorldCellCatalog.GetCellCenter(x, y);
                var motif = WorldCellCatalog.GetMotifIndex(x, y);

                var rootGo = new GameObject(DemoCellScene.AuthoredRootName);
                SceneManager.MoveGameObjectToScene(rootGo, scene);

                if (includeGround)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.name = "Ground";
                    ground.transform.SetParent(rootGo.transform, false);
                    ground.transform.position = center + new Vector3(0f, -0.5f, 0f);
                    ground.transform.localScale = new Vector3(
                        WorldCellCatalog.CellSize * 0.98f,
                        1f,
                        WorldCellCatalog.CellSize * 0.98f);
                    ground.GetComponent<MeshRenderer>().sharedMaterial = sharedLit;
                }

                // Marker: ストリーミング境界の目印。モチーフで形状・高さを変えてセル個性を出す。
                PlaceCellMarker(rootGo.transform, center, motif, sharedLit);
                // ローカル小物: 共有しないセル固有プロップ（ランタイム CreatePrimitive 禁止の代替）。
                PlaceCellLocalProps(rootGo.transform, center, x, y, motif, sharedLit);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        private static void PlaceCellMarker(Transform parent, Vector3 center, int motif, Material sharedLit)
        {
            // 0: 高い柱 / 1: 太い筒 / 2: 立方体塔 / 3: 低い球台
            GameObject marker;
            switch (motif)
            {
                case 1:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.transform.position = center + new Vector3(0f, 6f, 0f);
                    marker.transform.localScale = new Vector3(8f, 6f, 8f);
                    break;
                case 2:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    marker.transform.position = center + new Vector3(0f, 8f, 0f);
                    marker.transform.localScale = new Vector3(6f, 16f, 6f);
                    break;
                case 3:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.position = center + new Vector3(0f, 5f, 0f);
                    marker.transform.localScale = new Vector3(10f, 10f, 10f);
                    break;
                default:
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.transform.position = center + new Vector3(0f, 10f, 0f);
                    marker.transform.localScale = new Vector3(4f, 10f, 4f);
                    break;
            }

            marker.name = "Marker";
            marker.transform.SetParent(parent, false);
            marker.GetComponent<MeshRenderer>().sharedMaterial = sharedLit;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        private static void PlaceCellLocalProps(
            Transform parent,
            Vector3 center,
            int x,
            int y,
            int motif,
            Material sharedLit)
        {
            // セル座標から決定的にオフセットを決め、同じ (x,y) なら常に同じ配置。
            var propCount = 2 + motif;
            for (var i = 0; i < propCount; i++)
            {
                var angle = ((x * 37) + (y * 53) + (i * 97)) % 360 * Mathf.Deg2Rad;
                var radius = 28f + (i * 18f) + (motif * 6f);
                var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var type = (motif + i) % 3;
                GameObject prop = type switch
                {
                    0 => GameObject.CreatePrimitive(PrimitiveType.Cube),
                    1 => GameObject.CreatePrimitive(PrimitiveType.Cylinder),
                    _ => GameObject.CreatePrimitive(PrimitiveType.Sphere),
                };

                prop.name = $"Prop_{i}";
                prop.transform.SetParent(parent, false);
                prop.transform.position = pos + new Vector3(0f, 2f + i, 0f);
                prop.transform.localScale = type switch
                {
                    0 => new Vector3(6f, 4f + i * 2f, 6f),
                    1 => new Vector3(3f, 5f + i, 3f),
                    _ => new Vector3(5f, 5f, 5f),
                };
                prop.GetComponent<MeshRenderer>().sharedMaterial = sharedLit;
                Object.DestroyImmediate(prop.GetComponent<Collider>());
            }
        }

        /// <summary>
        /// 萌芽 Cell に Environment 子 SceneResource / .unity を付け、Ground を焼き込む。
        /// LoadType は必ず OnDemand（Cell Add で引っ張られない）。
        /// </summary>
        private static void CreateEnvironmentSprouts(
            SceneResourceMap map,
            WorldGridDefinition definition,
            Material sharedLit,
            CellPopulationPlan plan)
        {
            var cellsFolder = definition.SceneResourceOutputFolder.Replace('\\', '/').TrimEnd('/');
            var created = 0;

            for (var i = 0; i < EnvironmentSproutCells.Length; i++)
            {
                var coord = EnvironmentSproutCells[i];
                var cellId = CellIdentity.Format(coord.x, coord.y);
                var envId = EnvironmentIdentity.Format(coord.x, coord.y);
                var cellFolder = $"{cellsFolder}/{cellId}";
                var cellResourcePath = $"{cellFolder}/{cellId}.asset";
                var envScenePath = $"{cellFolder}/{envId}.unity";
                var envResourcePath = $"{cellFolder}/{envId}.asset";

                var cellResource = AssetDatabase.LoadAssetAtPath<SceneResource>(cellResourcePath);
                if (cellResource == null)
                {
                    throw new FileNotFoundException($"萌芽親 Cell がありません: {cellResourcePath}");
                }

                EnsureAssetFolder(cellFolder);
                EnsureEnvironmentSceneFile(envScenePath);

                var populateEnvironment = plan.ShouldPopulateEnvironment(coord);

                if (populateEnvironment)
                {
                    PopulateEnvironmentScene(envScenePath, coord.x, coord.y, sharedLit);
                }

                var envResource = EnsureEnvironmentResource(
                    envId,
                    envScenePath,
                    envResourcePath,
                    cellResource,
                    map);

                EnsureChildLink(cellResource, envResource);
                EditorUtility.SetDirty(cellResource);
                EditorUtility.SetDirty(envResource);
                created++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldCellStreamingSliceCreator] Environment sprouts: {created} (OnDemand under Cell)");
        }

        private static void EnsureEnvironmentSceneFile(string scenePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
            {
                return;
            }

            EnsureAssetFolder(Path.GetDirectoryName(scenePath)!.Replace('\\', '/'));
            // batchmode では untitled 未保存シーンがあると Additive NewScene が失敗する。
            // World.unity 作成と同様に Single で作り、パスへ即保存する。
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void PopulateEnvironmentScene(string scenePath, int x, int y, Material sharedLit)
        {
            // Single で開く（untitled 共存時の Additive 失敗を避ける）。
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root != null && root.name == EnvironmentScene.AuthoredRootName)
                {
                    Object.DestroyImmediate(root);
                }
            }

            var center = WorldCellCatalog.GetCellCenter(x, y);
            var motif = WorldCellCatalog.GetMotifIndex(x, y);
            var rootGo = new GameObject(EnvironmentScene.AuthoredRootName);
            SceneManager.MoveGameObjectToScene(rootGo, scene);

            // 作業単位分割の体感: Ground を Environment 側へ。
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(rootGo.transform, false);
            ground.transform.position = center + new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(
                WorldCellCatalog.CellSize * 0.98f,
                1f,
                WorldCellCatalog.CellSize * 0.98f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = sharedLit;

            // Environment 固有の小物（Cell の Prop_* とは別系統）。職種シーン側の個性。
            var envPropCount = 1 + motif;
            for (var i = 0; i < envPropCount; i++)
            {
                var angle = ((x * 19) + (y * 41) + (i * 73)) % 360 * Mathf.Deg2Rad;
                var radius = 40f + (i * 22f);
                var prop = GameObject.CreatePrimitive(
                    motif % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Cylinder);
                prop.name = $"EnvProp_{i}";
                prop.transform.SetParent(rootGo.transform, false);
                prop.transform.position = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    1.5f + i,
                    Mathf.Sin(angle) * radius);
                prop.transform.localScale = new Vector3(8f, 3f + i * 2f, 8f);
                prop.GetComponent<MeshRenderer>().sharedMaterial = sharedLit;
                Object.DestroyImmediate(prop.GetComponent<Collider>());
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static SceneResource EnsureEnvironmentResource(
            string envId,
            string scenePath,
            string resourcePath,
            SceneResource parentCell,
            SceneResourceMap map)
        {
            var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(resourcePath);
            if (resource == null)
            {
                resource = ScriptableObject.CreateInstance<SceneResource>();
                AssetDatabase.CreateAsset(resource, resourcePath);
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(scenePath, AssetPathToGUIDOptions.OnlyExistingAssets);
            if (string.IsNullOrEmpty(sceneGuid))
            {
                throw new FileNotFoundException($"Environment シーン GUID が解決できません: {scenePath}");
            }

            var so = new SerializedObject(resource);
            so.FindProperty("_identity").stringValue = envId;
            var sad = so.FindProperty("_sceneAssetDescription");
            sad.FindPropertyRelative("SceneIdentity").stringValue = envId;
            // 必ず OnDemand。NecessaryAlways にすると Cell Add で引っ張られる。
            sad.FindPropertyRelative("_loadType").enumValueIndex = (int)LoadType.OnDemand;
            var payloads = sad.FindPropertyRelative("_payloads");
            payloads.ClearArray();
            payloads.InsertArrayElementAtIndex(0);
            payloads.GetArrayElementAtIndex(0).FindPropertyRelative("Variant").stringValue = string.Empty;
            payloads.GetArrayElementAtIndex(0)
                .FindPropertyRelative("Reference")
                .FindPropertyRelative("m_AssetGUID").stringValue = sceneGuid;
            so.FindProperty("_parent").objectReferenceValue = parentCell;
            so.ApplyModifiedPropertiesWithoutUndo();

            UpsertMap(map, resource);
            return resource;
        }

        private static WorldGridDefinition EnsureGridDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WorldGridDefinition>(GridDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WorldGridDefinition>();
                AssetDatabase.CreateAsset(definition, GridDefinitionPath);
            }

            var so = new SerializedObject(definition);
            so.FindProperty("_origin").vector3Value = WorldCellCatalog.Origin;
            so.FindProperty("_cellSize").floatValue = WorldCellCatalog.CellSize;
            so.FindProperty("_gridWidth").intValue = WorldCellCatalog.GridWidth;
            so.FindProperty("_gridHeight").intValue = WorldCellCatalog.GridHeight;
            so.FindProperty("_parentSceneIdentity").stringValue = WorldCellCatalog.WorldIdentity;
            // .unity と SceneResource を同じ Cells ルートへ（サブフォルダは Generator 側）。
            so.FindProperty("_sceneOutputFolder").stringValue = CellsRootFolder;
            so.FindProperty("_sceneResourceOutputFolder").stringValue = CellsRootFolder;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static SceneResource EnsureWorldResource(SceneResource session, SceneResourceMap map)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldScenePath) == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, WorldScenePath);
            }

            var world = AssetDatabase.LoadAssetAtPath<SceneResource>(WorldResourcePath);
            if (world == null)
            {
                world = ScriptableObject.CreateInstance<SceneResource>();
                AssetDatabase.CreateAsset(world, WorldResourcePath);
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(WorldScenePath);
            var so = new SerializedObject(world);
            so.FindProperty("_identity").stringValue = WorldCellCatalog.WorldIdentity;
            var sad = so.FindProperty("_sceneAssetDescription");
            sad.FindPropertyRelative("SceneIdentity").stringValue = WorldCellCatalog.WorldIdentity;
            sad.FindPropertyRelative("_loadType").enumValueIndex = (int)LoadType.NecessaryAlways;
            var payloads = sad.FindPropertyRelative("_payloads");
            payloads.ClearArray();
            payloads.InsertArrayElementAtIndex(0);
            payloads.GetArrayElementAtIndex(0).FindPropertyRelative("Variant").stringValue = string.Empty;
            payloads.GetArrayElementAtIndex(0)
                .FindPropertyRelative("Reference")
                .FindPropertyRelative("m_AssetGUID").stringValue = sceneGuid;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureChildLink(session, world);
            UpsertMap(map, world);
            return world;
        }

        /// <summary>
        /// World._children を Cell SceneResource アセットで埋め直す。
        /// Environment 等の非 Cell 子は触らない（Cell 側の Children に残る）。
        /// </summary>
        private static void RelinkWorldChildren(SceneResource world, WorldGridDefinition definition)
        {
            var folder = definition.SceneResourceOutputFolder.Replace('\\', '/').TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:SceneResource", new[] { folder });
            var cells = new List<SceneResource>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cell = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                if (cell == null || !CellIdentity.IsCellId(cell.Identity))
                {
                    continue;
                }

                cells.Add(cell);
            }

            cells.Sort((a, b) => string.CompareOrdinal(a.Identity, b.Identity));

            var worldSo = new SerializedObject(world);
            var childrenProp = worldSo.FindProperty("_children");
            childrenProp.ClearArray();
            for (var i = 0; i < cells.Count; i++)
            {
                childrenProp.InsertArrayElementAtIndex(i);
                childrenProp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];

                var childSo = new SerializedObject(cells[i]);
                childSo.FindProperty("_parent").objectReferenceValue = world;
                childSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cells[i]);
            }

            worldSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            Debug.Log($"[WorldCellStreamingSliceCreator] Relinked {cells.Count} cells under World");
        }

        private static void EnsureChildLink(SceneResource parent, SceneResource child)
        {
            var parentSo = new SerializedObject(parent);
            var childrenProp = parentSo.FindProperty("_children");
            var found = false;
            for (var i = 0; i < childrenProp.arraySize; i++)
            {
                if (childrenProp.GetArrayElementAtIndex(i).objectReferenceValue == child)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1).objectReferenceValue = child;
            }

            parentSo.ApplyModifiedPropertiesWithoutUndo();

            var childSo = new SerializedObject(child);
            childSo.FindProperty("_parent").objectReferenceValue = parent;
            childSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpsertMap(SceneResourceMap map, SceneResource resource)
        {
            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");
            for (var i = 0; i < listProp.arraySize; i++)
            {
                var existing = listProp.GetArrayElementAtIndex(i).objectReferenceValue as SceneResource;
                if (existing == resource
                    || (existing != null
                        && string.Equals(existing.Identity, resource.Identity, System.StringComparison.Ordinal)))
                {
                    listProp.GetArrayElementAtIndex(i).objectReferenceValue = resource;
                    mapSo.ApplyModifiedPropertiesWithoutUndo();
                    return;
                }
            }

            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = resource;
            mapSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// SceneResourceMap の <c>{fileID: 0}</c> スロットを除去する。
        /// 未保存 SO を CreateAsset する前後で参照が切れた残骸や、旧パス削除の残骸がここに残る。
        /// ObjectReference 配列の DeleteArrayElementAtIndex は 1 回で null 化するだけなので、
        /// 非 null を集めて Clear → 差し戻す方式にする。
        /// </summary>
        private static void CompactNullMapEntries(SceneResourceMap map)
        {
            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");
            var keep = new List<SceneResource>(listProp.arraySize);
            var removed = 0;
            for (var i = 0; i < listProp.arraySize; i++)
            {
                var resource = listProp.GetArrayElementAtIndex(i).objectReferenceValue as SceneResource;
                if (resource == null)
                {
                    removed++;
                    continue;
                }

                keep.Add(resource);
            }

            if (removed == 0)
            {
                return;
            }

            listProp.ClearArray();
            for (var i = 0; i < keep.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];
            }

            mapSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(map);
            Debug.Log($"[WorldCellStreamingSliceCreator] Compacted {removed} null SceneResourceMap entries");
        }

        /// <summary>
        /// Cells 配下の全 SceneResource（Cell_* / Environment_*）を Map に再 Upsert する。
        /// Compact 後に「生きているアセットだけ」がカタログに揃うことを保証する。
        /// </summary>
        private static void RelinkMapFromDisk(SceneResourceMap map, WorldGridDefinition definition)
        {
            var folder = definition.SceneResourceOutputFolder.Replace('\\', '/').TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:SceneResource", new[] { folder });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                if (resource == null)
                {
                    continue;
                }

                if (!CellIdentity.IsCellId(resource.Identity)
                    && !EnvironmentIdentity.IsEnvironmentId(resource.Identity))
                {
                    continue;
                }

                UpsertMap(map, resource);
                count++;
            }

            EditorUtility.SetDirty(map);
            Debug.Log($"[WorldCellStreamingSliceCreator] Relinked {count} Cell/Environment resources into SceneResourceMap");
        }

        private static void RemoveFromMap(SceneResourceMap map, string identity)
        {
            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");
            for (var i = listProp.arraySize - 1; i >= 0; i--)
            {
                var resource = listProp.GetArrayElementAtIndex(i).objectReferenceValue as SceneResource;
                if (resource != null && resource.Identity == identity)
                {
                    listProp.DeleteArrayElementAtIndex(i);
                }
            }

            mapSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterCellAddressables(WorldGridDefinition definition)
        {
            var folder = definition.SceneOutputFolder.Replace('\\', '/').TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Environment は別メソッドで登録。ここでは Cell_*.unity のみ。
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (CellIdentity.IsCellId(fileName))
                {
                    RegisterAddressableScene(path);
                }
            }
        }

        private static void RegisterEnvironmentAddressables()
        {
            var guids = AssetDatabase.FindAssets("t:SceneAsset", new[] { CellsRootFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (EnvironmentIdentity.IsEnvironmentId(fileName))
                {
                    RegisterAddressableScene(path);
                }
            }
        }

        private static void RegisterAddressableScene(string scenePath)
            => RegisterAddressableAsset(scenePath);

        private static void RegisterAddressableAsset(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning("[WorldCellStreamingSliceCreator] AddressableAssetSettings が見つかりません。");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = assetPath;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
