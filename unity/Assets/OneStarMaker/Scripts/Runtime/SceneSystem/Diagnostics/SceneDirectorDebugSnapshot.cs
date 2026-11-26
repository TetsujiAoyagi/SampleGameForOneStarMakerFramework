#nullable enable

using System;
using System.Collections.Generic;

// namespace は folder（Diagnostics）で分けない。SceneDirector がこの型を返し、診断のコマンドが SceneDirector を読むので、
// 別の namespace にすると namespace の間に循環ができる（前例: DebugSocketServices/Hierarchy/ も親と同じ namespace）。
namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneDirector の Scene の状態を、ある一つの時点で写した不変の値（観測用）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 正本は SceneDirector の private な状態で、この値は写しにすぎない。保持しても正本に追従しないので、
    /// 読むたびに <see cref="SceneDirector.CaptureDebugSnapshot"/> で取り直す（要求された時だけ作り、継続して作らない）。
    /// </para>
    /// <para>
    /// コマンドと JSON から独立させてある。将来、Scene の変化を push で送る時や Unity の Inspector 向けのデバッグ表示を作る時も、
    /// 状態の取得をこの型に揃え、取得の経路を重複させない。語彙は <see cref="SceneEvent"/> と同じ
    /// （Scene の id は SceneIdentify と同じ文字列、状態は <see cref="SceneState"/>）にして、event を写しに当てて更新できるようにする。
    /// 項目は足すだけにし、名前と意味を変えない。
    /// </para>
    /// </remarks>
    internal sealed class SceneDirectorDebugSnapshot
    {
        private readonly SceneEntry[] _scenes;
        private readonly string[] _pendingUnloads;
        private readonly HistoryEntry[] _history;

        /// <param name="scenes">管理集合の Scene。順は問わない（出力側が並べる）。</param>
        /// <param name="pendingUnloads">unload の待ちの id の全集合。管理集合に無い id も含めてそのまま渡す。</param>
        /// <param name="history">遷移の履歴。新しい順。</param>
        /// <param name="canGoBack">SceneDirector の CanGoBack の値。</param>
        public SceneDirectorDebugSnapshot(
            IReadOnlyList<SceneEntry> scenes,
            IReadOnlyCollection<string> pendingUnloads,
            IReadOnlyList<HistoryEntry> history,
            bool canGoBack)
        {
            if (scenes == null) throw new ArgumentNullException(nameof(scenes));
            if (pendingUnloads == null) throw new ArgumentNullException(nameof(pendingUnloads));
            if (history == null) throw new ArgumentNullException(nameof(history));

            // 呼出し側が後で配列を書き換えても写しが変わらないよう、ここで複写する。
            _scenes = new SceneEntry[scenes.Count];
            for (var i = 0; i < scenes.Count; i++)
            {
                _scenes[i] = scenes[i];
            }

            _pendingUnloads = new string[pendingUnloads.Count];
            var p = 0;
            foreach (var id in pendingUnloads)
            {
                _pendingUnloads[p++] = id;
            }

            _history = new HistoryEntry[history.Count];
            for (var i = 0; i < history.Count; i++)
            {
                _history[i] = history[i];
            }

            CanGoBack = canGoBack;
        }

        /// <summary>
        /// SceneDirector の管理集合にある Scene。PreLoad の前に管理集合へ入った時から unload の完了で外れるまでが載り、
        /// <see cref="SceneEventType.Added"/>（Stable に達した時）より前の Scene も含む。Unity のロード済みの全 Scene ではない。
        /// </summary>
        public IReadOnlyList<SceneEntry> Scenes => _scenes;

        /// <summary>
        /// unload の待ちの id の全集合。管理集合の部分集合とは限らない（取消の後始末は先に管理集合から外し、後でまとめて待ちを外す）。
        /// </summary>
        public IReadOnlyList<string> PendingUnloads => _pendingUnloads;

        /// <summary>
        /// 戻り先の管理用の stack の中身（新しい順）。成功した遷移だけの記録ではなく、遷移の途中や失敗の後にも entry がありうる。
        /// </summary>
        public IReadOnlyList<HistoryEntry> History => _history;

        /// <summary>SceneDirector の CanGoBack（履歴があるか）。GoBack が成功することの保証ではない。</summary>
        public bool CanGoBack { get; }

        /// <summary>管理集合の 1 つの Scene。</summary>
        public readonly struct SceneEntry
        {
            public SceneEntry(string id, SceneState state)
            {
                Id = id;
                State = state;
            }

            public string Id { get; }

            public SceneState State { get; }
        }

        /// <summary>履歴の 1 つの entry。</summary>
        public readonly struct HistoryEntry
        {
            public HistoryEntry(string fromSceneId, string toSceneId)
            {
                FromSceneId = fromSceneId;
                ToSceneId = toSceneId;
            }

            public string FromSceneId { get; }

            public string ToSceneId { get; }
        }
    }
}
