#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneStarMaker.Editor.SceneGraph
{
    /// <summary>
    /// クリップボードへ書き出す 1 ノード分のデータ。
    /// </summary>
    [Serializable]
    internal sealed class SceneGraphClipboardEntry
    {
        /// <summary>コピー元 SceneNodeData のアセット GUID。</summary>
        public string NodeGuid = string.Empty;

        /// <summary>表示・複製時のベース名に使う。</summary>
        public string Identity = string.Empty;

        /// <summary>(int)LoadType。</summary>
        public int LoadType;

        /// <summary>コピー時点の座標（絶対）。</summary>
        public Vector2 Position;
    }

    /// <summary>
    /// クリップボード内でのノード間の親子リンク。Nodes 配列の index を指す。
    /// </summary>
    [Serializable]
    internal sealed class SceneGraphClipboardLink
    {
        public int ParentIndex;
        public int ChildIndex;
    }

    /// <summary>
    /// SceneGraph クリップボードの直列化フォーマット全体。
    /// </summary>
    [Serializable]
    internal sealed class SceneGraphClipboardData
    {
        public const string TypeTag = "OneStarMaker.SceneGraph.Clipboard";
        public const int CurrentVersion = 1;

        /// <summary>
        /// 他ツールの GraphView データを弾くためのマジック。
        /// R4: JsonUtility.FromJson は JSON に無いキーをフィールド初期化子の値のままにするため、
        /// ここを TypeTag で初期化すると「"Type" キーを持たない JSON」が CanPaste を通ってしまう。
        /// 初期化子は空文字にし、Serialize 時に明示的に TypeTag を設定する。
        /// </summary>
        public string Type = string.Empty;

        public int Version = CurrentVersion;

        public string SourceGraphGuid = string.Empty;

        public List<SceneGraphClipboardEntry> Nodes = new();

        public List<SceneGraphClipboardLink> Edges = new();
    }

    /// <summary>
    /// SceneGraph クリップボードの直列化 / 逆直列化。
    /// AssetDatabase にも UnityEditor にも依存しない純粋関数として書く（テスト可能性のため）。
    /// GUID → SceneNodeData の解決は呼び出し側（ViewModel / View）に置く。
    /// </summary>
    internal static class SceneGraphClipboard
    {
        public static string Serialize(SceneGraphClipboardData data)
        {
            // R4: Type は常にここで確定させる（呼び出し側の初期化子任せにしない）。
            data.Type = SceneGraphClipboardData.TypeTag;
            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// 直列化された文字列を復元する。壊れた JSON / 空文字は例外を投げず null を返す。
        /// </summary>
        public static SceneGraphClipboardData? TryDeserialize(string? json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                var data = JsonUtility.FromJson<SceneGraphClipboardData>(json);
                return data;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>直列化文字列がこのフォーマットとして貼り付け可能か。</summary>
        public static bool CanPaste(string? json)
        {
            return CanPaste(TryDeserialize(json));
        }

        /// <summary>逆直列化済みデータが貼り付け可能な内容か。</summary>
        public static bool CanPaste(SceneGraphClipboardData? data)
        {
            if (data == null) return false;
            if (data.Type != SceneGraphClipboardData.TypeTag) return false;
            if (data.Version != SceneGraphClipboardData.CurrentVersion) return false;
            if (data.Nodes == null || data.Nodes.Count == 0) return false;
            return true;
        }

        /// <summary>
        /// コピー集合内のエッジだけを抽出し、Nodes 配列の index ベースのリンクへ変換する。
        /// 両端が nodeGuids に含まれていないエッジは除外する。
        /// </summary>
        /// <param name="nodeGuids">Nodes 配列の並び順に対応する GUID リスト。</param>
        /// <param name="allEdges">判定対象の全エッジ（親 GUID, 子 GUID）。コピー集合外のノードを含んでよい。</param>
        public static List<SceneGraphClipboardLink> BuildInternalLinks(
            IReadOnlyList<string> nodeGuids,
            IEnumerable<(string ParentGuid, string ChildGuid)> allEdges)
        {
            var links = new List<SceneGraphClipboardLink>();
            if (nodeGuids == null || allEdges == null) return links;

            var indexByGuid = new Dictionary<string, int>();
            for (int i = 0; i < nodeGuids.Count; i++)
            {
                var guid = nodeGuids[i];
                if (!string.IsNullOrEmpty(guid) && !indexByGuid.ContainsKey(guid))
                {
                    indexByGuid[guid] = i;
                }
            }

            foreach (var (parentGuid, childGuid) in allEdges)
            {
                if (string.IsNullOrEmpty(parentGuid) || string.IsNullOrEmpty(childGuid)) continue;

                if (indexByGuid.TryGetValue(parentGuid, out var parentIndex) &&
                    indexByGuid.TryGetValue(childGuid, out var childIndex))
                {
                    links.Add(new SceneGraphClipboardLink { ParentIndex = parentIndex, ChildIndex = childIndex });
                }
            }

            return links;
        }

        /// <summary>
        /// Nodes 配列のうち、内部リンク上で親を持たない index を返す。
        /// 複製時に「コピー集合外の親」を引き継ぐ対象を決めるために使う。
        /// </summary>
        public static List<int> GetIndicesWithoutInternalParent(
            int nodeCount, IReadOnlyList<SceneGraphClipboardLink> links)
        {
            var result = new List<int>();
            if (nodeCount <= 0) return result;

            var hasInternalParent = new bool[nodeCount];
            if (links != null)
            {
                foreach (var link in links)
                {
                    if (link.ChildIndex >= 0 && link.ChildIndex < nodeCount)
                    {
                        hasInternalParent[link.ChildIndex] = true;
                    }
                }
            }

            for (int i = 0; i < nodeCount; i++)
            {
                if (!hasInternalParent[i])
                    result.Add(i);
            }

            return result;
        }
    }
}
