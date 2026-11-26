#nullable enable

using System;
using System.Collections.Generic;
using OneStarMaker.Runtime.DebugCommands;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// デバッグコマンド <c>osm.scenes</c>: SceneDirector の管理集合・各 <see cref="SceneState"/>・遷移の履歴を、その時点の値として返す。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 写しを出力に変える薄い adapter で、状態の取得は <see cref="SceneDirector.CaptureDebugSnapshot"/> だけが行う。
    /// 登録するのは composition root（<c>FrameworkDebugCommands.Compose</c>）で、Editor の Play の時だけである。
    /// </para>
    /// <para>
    /// 出力の規則（このコマンドの物で、デバッグコマンド全体の契約ではない）: 項目は足すだけで、名前を変えない・消さない・意味を変えない。
    /// 列挙値は整数ではなく名前で出す。<c>unavailable:</c> の接頭辞は本コマンドの暫定の失敗の文言である。
    /// </para>
    /// </remarks>
    internal static class ScenesDebugCommand
    {
        /// <summary>登録名。Ordinal の完全一致で振り分ける。</summary>
        internal const string CommandName = "osm.scenes";

        internal const string CapturedMessage = "captured";
        internal const string SceneDirectorNotCreatedMessage = "unavailable: scene-director-not-created";

        /// <summary>
        /// handler を作る。対象の SceneDirector は登録の時ではなく実行の時に <paramref name="getSceneDirector"/> で解決する。
        /// </summary>
        /// <remarks>
        /// SceneDirector は Play ごとに作り直され、登録（DebugSocketService の生成）は SceneDirector の生成より前に起きる。
        /// 登録の時に SceneDirector を closure に捕まえたり、写しを cache したりすると、旧 Play の値を出しうるので行わない。
        /// payload は読まない。handler の中で await しない（catalog は handler を同期で呼ぶので、1 回の応答が一つの時点の値になる）。
        /// </remarks>
        internal static DebugCommandHandler CreateHandler(Func<SceneDirector?> getSceneDirector)
        {
            // SceneDirector は UnityEngine.Object ではないので ?. で判定してよい。
            return _ => Format(getSceneDirector()?.CaptureDebugSnapshot(), Time.frameCount);
        }

        /// <summary>写しを結果にする純関数。snapshot が null（SceneDirector がまだ無い・既に解放された）なら読めない失敗にする。</summary>
        internal static DebugCommandResult Format(SceneDirectorDebugSnapshot? snapshot, int capturedFrame)
        {
            if (snapshot == null)
            {
                return DebugCommandResult.Fail(SceneDirectorNotCreatedMessage);
            }

            // pendingUnload は管理集合と交差した射影。待ちの集合にあって管理集合に無い id は行として出さない。
            var pending = new HashSet<string>(snapshot.PendingUnloads, StringComparer.Ordinal);

            var sorted = new SceneDirectorDebugSnapshot.SceneEntry[snapshot.Scenes.Count];
            for (var i = 0; i < sorted.Length; i++)
            {
                sorted[i] = snapshot.Scenes[i];
            }

            Array.Sort(sorted, (a, b) => string.CompareOrdinal(a.Id, b.Id));

            var rows = new SceneRow[sorted.Length];
            for (var i = 0; i < sorted.Length; i++)
            {
                rows[i] = new SceneRow
                {
                    id = sorted[i].Id,
                    // enum をそのまま入れると JsonUtility は整数で書くので、名前の文字列にする。
                    state = sorted[i].State.ToString(),
                    pendingUnload = pending.Contains(sorted[i].Id),
                };
            }

            var history = new HistoryRow[snapshot.History.Count];
            for (var i = 0; i < history.Length; i++)
            {
                history[i] = new HistoryRow
                {
                    from = snapshot.History[i].FromSceneId,
                    to = snapshot.History[i].ToSceneId,
                };
            }

            var payload = new ScenesPayload
            {
                capturedFrame = capturedFrame,
                scenes = rows,
                history = history,
                canGoBack = snapshot.CanGoBack,
            };

            return DebugCommandResult.Ok(CapturedMessage, JsonUtility.ToJson(payload));
        }

        // JsonUtility の DTO。public で readonly でない field だけを持ち、property を使わない（serializer が getter を呼ばないように）。
        // JSON の項目の順は field の宣言の順になるので、宣言の順を変えない。

        [Serializable]
        private sealed class ScenesPayload
        {
            public int capturedFrame;
            public SceneRow[] scenes = Array.Empty<SceneRow>();
            public HistoryRow[] history = Array.Empty<HistoryRow>();
            public bool canGoBack;
        }

        [Serializable]
        private sealed class SceneRow
        {
            public string id = string.Empty;
            public string state = string.Empty;
            public bool pendingUnload;
        }

        [Serializable]
        private sealed class HistoryRow
        {
            public string from = string.Empty;
            public string to = string.Empty;
        }
    }
}
