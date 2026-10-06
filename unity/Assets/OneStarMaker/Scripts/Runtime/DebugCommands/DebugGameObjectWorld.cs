#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// 1回の読み取りがシーンを見られたか。
    /// NotFound は選択を消す理由になる。Unavailable は選択を変えない。
    /// </summary>
    public enum DebugGameObjectReadStatus
    {
        Ok = 0,
        NotFound = 1,
        Unavailable = 2,
    }

    /// <summary>
    /// コマンド応答の1行。階層パスは表示であり、検索キーではない。
    /// </summary>
    public readonly struct DebugGameObjectRow
    {
        public DebugGameObjectRow(
            ulong instanceId,
            bool hasParent,
            ulong parentInstanceId,
            string? name,
            string? sceneName,
            string? displayPath,
            bool activeSelf,
            bool activeInHierarchy,
            int siblingIndex,
            int childCount)
        {
            InstanceId = instanceId;
            HasParent = hasParent;
            ParentInstanceId = parentInstanceId;
            Name = name ?? string.Empty;
            SceneName = sceneName ?? string.Empty;
            DisplayPath = displayPath ?? string.Empty;
            ActiveSelf = activeSelf;
            ActiveInHierarchy = activeInHierarchy;
            SiblingIndex = siblingIndex;
            ChildCount = childCount;
        }

        public ulong InstanceId { get; }

        public bool HasParent { get; }

        public ulong ParentInstanceId { get; }

        public string Name { get; }

        public string SceneName { get; }

        public string DisplayPath { get; }

        public bool ActiveSelf { get; }

        public bool ActiveInHierarchy { get; }

        public int SiblingIndex { get; }

        public int ChildCount { get; }
    }

    /// <summary>
    /// 要求のあいだだけシーンを読む口。実装は呼び出しの終了後にシーンオブジェクトを保持しない。
    /// </summary>
    public interface IDebugGameObjectWorld
    {
        DebugGameObjectReadStatus TryDescribe(ulong instanceId, out DebugGameObjectRow row, out string failure);

        /// <summary>
        /// destination を空にしてから、offset 件を飛ばし、limit 件まで追加する。
        /// 次の1件が見えたら hasMore を true にして止める。
        /// </summary>
        DebugGameObjectReadStatus TryCollectPage(
            int offset,
            int limit,
            List<DebugGameObjectRow> destination,
            out bool hasMore,
            out string failure);
    }
}
