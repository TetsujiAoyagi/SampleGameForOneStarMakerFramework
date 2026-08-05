#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.SceneSystem;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// 中間データ（SceneNodeData + SceneGraphEdges）から
    /// ランタイム SceneResource / SceneResourceMap を生成する。
    /// 冪等: 同じ入力から常に同一出力を保証する。
    /// </summary>
    public static class SceneResourceGenerator
    {
        private const string DefaultOutputFolder = "Assets/OneStarMakerCommon/SceneMap";
        private const string DefaultMapPath = "Assets/OneStarMakerCommon/SceneMap/SceneResourceMap.asset";

        /// <summary>
        /// Generate を実行する。
        /// </summary>
        /// <param name="allNodes">全ノード。</param>
        /// <param name="allGraphEdges">全グラフエッジ。</param>
        /// <param name="outputFolder">SceneResource 出力先フォルダ。</param>
        /// <param name="mapPath">SceneResourceMap 出力先パス。</param>
        /// <returns>true: 成功、false: バリデーションエラーで中止。</returns>
        public static bool Generate(
            IReadOnlyList<SceneNodeData> allNodes,
            IReadOnlyList<SceneGraphEdges> allGraphEdges,
            string outputFolder = DefaultOutputFolder,
            string mapPath = DefaultMapPath)
        {
            // ── Step 1: バリデーション ──
            var validationResults = SceneGraphValidator.ValidateAll(allNodes, allGraphEdges);
            var hasErrors = false;

            foreach (var result in validationResults)
            {
                switch (result.Severity)
                {
                    case SceneGraphValidator.Severity.Error:
                        Debug.LogError($"[SceneGraph Generate] {result}");
                        hasErrors = true;
                        break;
                    case SceneGraphValidator.Severity.Warning:
                        Debug.LogWarning($"[SceneGraph Generate] {result}");
                        break;
                    default:
                        Debug.Log($"[SceneGraph Generate] {result}");
                        break;
                }
            }

            if (hasErrors)
            {
                Debug.LogError("[SceneGraph Generate] Aborted due to validation errors.");
                return false;
            }

            // ── Step 2: 出力先の準備 ──
            EnsureDirectoryExists(outputFolder);
            EnsureDirectoryExists(Path.GetDirectoryName(mapPath)!);

            // ── Step 3: SceneResource の生成/更新 ──
            var nodeToResource = new Dictionary<SceneNodeData, SceneResource>();

            // 既存 SceneResource の Identity → パスの索引を「ループの外で 1 回だけ」作る。
            // ノードごとに AssetDatabase.FindAssets + LoadAssetAtPath を回すと O(ノード数 × 既存数) になり、
            // ワールドストリーミングのセル数（数百）では Generate が数万回のアセットロードで固まる。
            var existingResourcePathByIdentity = BuildSceneResourcePathIndex();

            foreach (var node in allNodes)
            {
                if (node == null) continue;

                // CCS: Cell/Environment は World/Cells 配下に同居するため、
                // identity 一致の既存アセットがあればそのパスを正とする（SceneMap への二重生成を防ぐ）。
                var existingPath = FindExistingSceneResourcePath(existingResourcePathByIdentity, node.Identity);
                var assetPath = string.IsNullOrEmpty(existingPath)
                    ? $"{outputFolder}/{node.Identity}.asset"
                    : existingPath;
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(assetPath);

                if (resource == null)
                {
                    EnsureDirectoryExists(Path.GetDirectoryName(assetPath)!.Replace('\\', '/'));
                    resource = ScriptableObject.CreateInstance<SceneResource>();
                    AssetDatabase.CreateAsset(resource, assetPath);
                }

                // SerializedObject 経由で書き込む（Undo と dirty flag のため）
                var so = new SerializedObject(resource);
                so.FindProperty("_identity").stringValue = node.Identity;

                // SceneAssetDescription を SceneNodeData の _loadType + _payloads から組み立てる
                var sadProp = so.FindProperty("_sceneAssetDescription");
                if (sadProp != null)
                {
                    // LoadType
                    var loadTypeProp = sadProp.FindPropertyRelative("_loadType");
                    if (loadTypeProp != null)
                    {
                        loadTypeProp.enumValueIndex = (int)node.NodeLoadType;
                    }

                    // Payloads を要素単位でコピー（W-1: boxedValue 脱却）
                    var srcSo = new SerializedObject(node);
                    var srcPayloads = srcSo.FindProperty("_payloads");
                    var dstPayloads = sadProp.FindPropertyRelative("_payloads");
                    if (srcPayloads != null && dstPayloads != null)
                    {
                        CopyPayloadsElementWise(srcPayloads, dstPayloads);
                    }
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                nodeToResource[node] = resource;
            }

            // ── Step 4: 親子関係の設定 ──
            // まず全 SceneResource の Parent/Children をクリア
            foreach (var resource in nodeToResource.Values)
            {
                var so = new SerializedObject(resource);
                so.FindProperty("_parent").objectReferenceValue = null;
                var childrenProp = so.FindProperty("_children");
                childrenProp.ClearArray();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Edge から設定
            foreach (var graphEdges in allGraphEdges)
            {
                if (graphEdges == null) continue;

                foreach (var edge in graphEdges.Edges)
                {
                    if (edge.Parent == null || edge.Child == null) continue;
                    if (!nodeToResource.TryGetValue(edge.Parent, out var parentResource)) continue;
                    if (!nodeToResource.TryGetValue(edge.Child, out var childResource)) continue;

                    // Child の Parent を設定
                    var childSo = new SerializedObject(childResource);
                    childSo.FindProperty("_parent").objectReferenceValue = parentResource;
                    childSo.ApplyModifiedPropertiesWithoutUndo();

                    // Parent の Children に追加
                    var parentSo = new SerializedObject(parentResource);
                    var childrenProp = parentSo.FindProperty("_children");
                    childrenProp.InsertArrayElementAtIndex(childrenProp.arraySize);
                    childrenProp.GetArrayElementAtIndex(childrenProp.arraySize - 1)
                        .objectReferenceValue = childResource;
                    parentSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // ── Step 5: SceneResourceMap の生成/更新 ──
            var map = AssetDatabase.LoadAssetAtPath<SceneResourceMap>(mapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<SceneResourceMap>();
                AssetDatabase.CreateAsset(map, mapPath);
            }

            var mapSo = new SerializedObject(map);
            var listProp = mapSo.FindProperty("_sceneResources");
            listProp.ClearArray();

            // Identity のアルファベット順で安定したシリアライズ順序
            var sortedResources = nodeToResource.Values
                .OrderBy(r => r.Identity)
                .ToList();

            for (int i = 0; i < sortedResources.Count; i++)
            {
                listProp.InsertArrayElementAtIndex(i);
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = sortedResources[i];
            }

            // Generate ハッシュの計算・書き込み（W-3: Generate 忘れ検出用）
            var hash = ComputeCurrentHash(allNodes, allGraphEdges);
            var hashProp = mapSo.FindProperty("_generateHash");
            if (hashProp != null)
            {
                hashProp.stringValue = hash;
            }

            mapSo.ApplyModifiedPropertiesWithoutUndo();

            // ── Step 6: 保存 ──
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Step 7: 生成後の整合性チェック（W-2）──
            var integrityIssues = VerifyGeneratedIntegrity(nodeToResource, allGraphEdges);
            foreach (var issue in integrityIssues)
            {
                Debug.LogWarning($"[SceneGraph Generate] Post-generate integrity issue: {issue}");
            }

            Debug.Log($"[SceneGraph Generate] Successfully generated {nodeToResource.Count} SceneResources. Hash={hash}");
            return true;
        }

        /// <summary>
        /// 不要になった SceneResource を削除する（中間データに存在しないもの）。
        /// </summary>
        public static void CleanupOrphanedResources(
            IReadOnlyList<SceneNodeData> allNodes,
            string outputFolder = DefaultOutputFolder)
        {
            var validIdentities = new HashSet<string>();
            foreach (var node in allNodes)
            {
                if (node != null && !string.IsNullOrWhiteSpace(node.Identity))
                    validIdentities.Add(node.Identity);
            }

            var guids = AssetDatabase.FindAssets("t:SceneResource", new[] { outputFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                if (resource != null && !validIdentities.Contains(resource.Identity))
                {
                    Debug.Log($"[SceneGraph Generate] Deleting orphaned SceneResource: {path}");
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        /// <summary>
        /// 生成後の Parent/Children 双方向整合性を検査する（W-2）。
        /// </summary>
        private static List<string> VerifyGeneratedIntegrity(
            Dictionary<SceneNodeData, SceneResource> nodeToResource,
            IReadOnlyList<SceneGraphEdges> allGraphEdges)
        {
            var issues = new List<string>();

            foreach (var resource in nodeToResource.Values)
            {
                // Parent → Children の双方向チェック
                if (resource.Parent != null)
                {
                    var parentChildren = resource.Parent.Children;
                    var found = false;
                    for (int i = 0; i < parentChildren.Count; i++)
                    {
                        if (parentChildren[i] == resource) { found = true; break; }
                    }
                    if (!found)
                    {
                        issues.Add(
                            $"'{resource.Identity}' has Parent='{resource.Parent.Identity}' but is not in Parent's Children list.");
                    }
                }

                foreach (var child in resource.Children)
                {
                    if (child == null) continue;
                    if (child.Parent != resource)
                    {
                        issues.Add(
                            $"'{resource.Identity}' lists '{child.Identity}' as child, but child's Parent is '{child.Parent?.Identity ?? "null"}'.");
                    }
                }
            }

            return issues;
        }

        /// <summary>
        /// 中間データから Generate ハッシュを算出する（W-3）。
        /// SceneNodeData の Identity/LoadType/Payloads + Edge 構造をダイジェストする。
        /// ViewModel から呼び出して「Generate 忘れ」を検出する用途でも使用。
        /// </summary>
        public static string ComputeCurrentHash(
            IReadOnlyList<SceneNodeData> allNodes,
            IReadOnlyList<SceneGraphEdges> allGraphEdges)
        {
            var sb = new StringBuilder();

            // ノード情報（Identity 昇順で安定化）
            var sortedNodes = allNodes
                .Where(n => n != null)
                .OrderBy(n => n.Identity)
                .ToList();

            foreach (var node in sortedNodes)
            {
                sb.Append(node.Identity).Append('|');
                sb.Append((int)node.NodeLoadType).Append('|');
                sb.Append(node.Payloads.Count).Append('|');
                foreach (var payload in node.Payloads)
                {
                    sb.Append(payload?.Reference?.AssetGUID ?? "null").Append(',');
                    sb.Append(payload?.Variant ?? "").Append(';');
                }
                sb.AppendLine();
            }

            // エッジ情報（グラフ名 + Parent→Child 昇順で安定化）
            foreach (var graph in allGraphEdges.Where(g => g != null).OrderBy(g => g.GraphName))
            {
                sb.Append("GRAPH:").Append(graph.GraphName).AppendLine();
                var sortedEdges = graph.Edges
                    .Where(e => e.Parent != null && e.Child != null)
                    .OrderBy(e => e.Parent!.Identity)
                    .ThenBy(e => e.Child!.Identity);
                foreach (var edge in sortedEdges)
                {
                    sb.Append(edge.Parent!.Identity).Append("->")
                      .Append(edge.Child!.Identity).AppendLine();
                }
            }

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            // 先頭8バイトを16進文字列で（16文字。衝突確率は Generate 忘れ検出用途として十分）
            var hashSb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                hashSb.Append(bytes[i].ToString("x2"));
            }
            return hashSb.ToString();
        }

        /// <summary>
        /// プロジェクト内の既存 SceneResource を走査し、Identity → アセットパスの索引を作る。
        /// **必ずループの外で 1 回だけ呼ぶこと。** ノードごとに呼ぶと O(ノード数 × 既存数) になる。
        /// Identity が重複している場合は最初に見つかったものを採用する（重複自体は V-2 が検出する）。
        /// </summary>
        private static Dictionary<string, string> BuildSceneResourcePathIndex()
        {
            var index = new Dictionary<string, string>(System.StringComparer.Ordinal);

            var guids = AssetDatabase.FindAssets("t:SceneResource");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var resource = AssetDatabase.LoadAssetAtPath<SceneResource>(path);
                if (resource == null) continue;

                var identity = resource.Identity;
                if (string.IsNullOrWhiteSpace(identity)) continue;
                if (index.ContainsKey(identity)) continue;

                index[identity] = path.Replace('\\', '/');
            }

            return index;
        }

        /// <summary>
        /// 索引から identity 一致の既存 SceneResource パスを引く（無ければ null）。
        /// </summary>
        private static string? FindExistingSceneResourcePath(
            Dictionary<string, string> pathByIdentity, string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return null;
            return pathByIdentity.TryGetValue(identity, out var path) ? path : null;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Replace("\\", "/").Split('/');
                var current = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }
        }

        /// <summary>
        /// AssetPayload 配列を要素単位でコピーする（W-1）。
        /// boxedValue の暗黙的ディープコピーに依存せず、
        /// フィールド単位で転記することで AssetReference の安全性を保証する。
        /// </summary>
        private static void CopyPayloadsElementWise(SerializedProperty src, SerializedProperty dst)
        {
            if (src == null || dst == null) return;

            dst.ClearArray();

            for (int i = 0; i < src.arraySize; i++)
            {
                dst.InsertArrayElementAtIndex(i);
                var srcElement = src.GetArrayElementAtIndex(i);
                var dstElement = dst.GetArrayElementAtIndex(i);

                // Reference (AssetReference)
                var srcRef = srcElement.FindPropertyRelative("Reference");
                var dstRef = dstElement.FindPropertyRelative("Reference");
                if (srcRef != null && dstRef != null)
                {
                    // AssetReference は m_AssetGUID + m_SubObjectName + m_SubObjectType を持つ
                    CopyPropertyByPath(srcRef, dstRef, "m_AssetGUID");
                    CopyPropertyByPath(srcRef, dstRef, "m_SubObjectName");
                    CopyPropertyByPath(srcRef, dstRef, "m_SubObjectType");
                }

                // Variant (string)
                var srcVariant = srcElement.FindPropertyRelative("Variant");
                var dstVariant = dstElement.FindPropertyRelative("Variant");
                if (srcVariant != null && dstVariant != null)
                {
                    dstVariant.stringValue = srcVariant.stringValue;
                }
            }
        }

        private static void CopyPropertyByPath(
            SerializedProperty srcParent, SerializedProperty dstParent, string relativePath)
        {
            var srcChild = srcParent.FindPropertyRelative(relativePath);
            var dstChild = dstParent.FindPropertyRelative(relativePath);
            if (srcChild == null || dstChild == null) return;

            switch (srcChild.propertyType)
            {
                case SerializedPropertyType.String:
                    dstChild.stringValue = srcChild.stringValue;
                    break;
                case SerializedPropertyType.Integer:
                    dstChild.intValue = srcChild.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    dstChild.enumValueIndex = srcChild.enumValueIndex;
                    break;
                default:
                    // フォールバック: boxedValue を使用
                    dstChild.boxedValue = srcChild.boxedValue;
                    break;
            }
        }
    }
}
