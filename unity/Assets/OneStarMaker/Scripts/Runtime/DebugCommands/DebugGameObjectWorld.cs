#nullable enable

using System.Collections.Generic;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// 1回の読み取りがシーンを見られたか。
    /// NotFound は選択を消す理由になる。Unavailable と MissingRenderer は選択を変えない。
    /// </summary>
    public enum DebugGameObjectReadStatus
    {
        Ok = 0,
        NotFound = 1,
        Unavailable = 2,
        MissingRenderer = 3,
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

        /// <summary>
        /// 見つかった GameObject だけに SetActive する。親の active は変えない。
        /// </summary>
        DebugGameObjectReadStatus TrySetActive(ulong instanceId, bool active, out DebugGameObjectRow row, out string failure);

        /// <summary>local position、local euler、local scale を読む。</summary>
        DebugGameObjectReadStatus TryGetTransform(ulong instanceId, out DebugGameObjectTransform value, out string failure);

        /// <summary>
        /// 渡された local 成分だけを書く。書いていない成分と、親の world 位置は変えない。
        /// </summary>
        DebugGameObjectReadStatus TrySetTransform(
            ulong instanceId,
            DebugGameObjectTransformChange change,
            out DebugGameObjectTransform value,
            out string failure);

        /// <summary>
        /// 対象 GameObject 自身の Renderer のうち、index の 1 件だけ enabled を書く。
        /// 範囲外は MissingRenderer で、どの Renderer も変えない。
        /// </summary>
        DebugGameObjectReadStatus TrySetRendererEnabled(
            ulong instanceId,
            int rendererIndex,
            bool enabled,
            out DebugGameObjectRenderer value,
            out string failure);
    }

    /// <summary>local transform の1成分。Unity へ書くときは float に収まる有限値だけを渡す。</summary>
    public readonly struct DebugGameObjectVector
    {
        public DebugGameObjectVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }

    /// <summary>読み戻した local transform。instanceId が主キーで、パスは含まない。</summary>
    public readonly struct DebugGameObjectTransform
    {
        public DebugGameObjectTransform(
            ulong instanceId,
            DebugGameObjectVector localPosition,
            DebugGameObjectVector localEulerAngles,
            DebugGameObjectVector localScale)
        {
            InstanceId = instanceId;
            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
        }

        public ulong InstanceId { get; }

        public DebugGameObjectVector LocalPosition { get; }

        public DebugGameObjectVector LocalEulerAngles { get; }

        public DebugGameObjectVector LocalScale { get; }
    }

    /// <summary>set で渡された成分だけが Has* になる。</summary>
    public readonly struct DebugGameObjectTransformChange
    {
        public DebugGameObjectTransformChange(
            bool hasPosition,
            DebugGameObjectVector position,
            bool hasEuler,
            DebugGameObjectVector euler,
            bool hasScale,
            DebugGameObjectVector scale)
        {
            HasPosition = hasPosition;
            Position = position;
            HasEuler = hasEuler;
            Euler = euler;
            HasScale = hasScale;
            Scale = scale;
        }

        public bool HasPosition { get; }

        public DebugGameObjectVector Position { get; }

        public bool HasEuler { get; }

        public DebugGameObjectVector Euler { get; }

        public bool HasScale { get; }

        public DebugGameObjectVector Scale { get; }
    }

    /// <summary>1つの Renderer を変えたあとの応答。index は component 順で、パスは含まない。</summary>
    public readonly struct DebugGameObjectRenderer
    {
        public DebugGameObjectRenderer(
            ulong instanceId,
            int rendererIndex,
            int rendererCount,
            bool enabled,
            string? typeName)
        {
            InstanceId = instanceId;
            RendererIndex = rendererIndex;
            RendererCount = rendererCount;
            Enabled = enabled;
            TypeName = typeName ?? string.Empty;
        }

        public ulong InstanceId { get; }

        public int RendererIndex { get; }

        public int RendererCount { get; }

        public bool Enabled { get; }

        public string TypeName { get; }
    }
}
