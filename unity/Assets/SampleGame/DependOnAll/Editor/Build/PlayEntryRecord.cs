#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SampleGame.DependOnAll.Editor.Build
{
    internal enum PlayEntryState { Planned, EnteredPlay, Blocked, StartupFailed }

    /// <summary>
    /// 直近の Play の policy（Play の前に決めた規則と計画）と record（その Play で起きた事実）。別の型にして混ぜない。
    /// record には観測した事実だけを書き、Runtime を変えずに観測できない項目は <c>unknown</c> と出す（推測で埋めない）。
    /// <para>
    /// SessionState に置くので Play と domain reload を跨いで残り、Editor process と共に消える。Console の 1 行と観測 UI はどちらもこの値から作る。
    /// </para>
    /// </summary>
    internal static class PlayEntryRecord
    {
        internal const string SessionKey = "OSM.PlayEntry.Record.v1";
        internal const string Unknown = "unknown";
        internal const string SealedLogMarker = "Addressables backend sealed";
        /// <summary>EnteredPlayMode からこの秒数の間の Error / Exception を起動の失敗として数える。後のゲーム中の Error と区別する境界。</summary>
        internal const double StartupObservationSeconds = 15;
        internal const string LogPrefix = "[PlayEntry] ";
        /// <summary>起動の取り込みの段の成功の log（Runtime の EditorPlaySnapshotIntake.LogPrefix と同じ文字列）。</summary>
        internal const string IntakeLogMarker = "[AppInit] Editor play snapshot intake: ";
        internal const string OriginSnapshot = "snapshot";
        internal const string OriginSnapshotThenDirectory = "snapshot->directory";
        /// <summary>解放の担当。Scene の unload は SceneDirector（map に無い Scene は Play の終了）、複製の file は Play 入口。</summary>
        internal const string ReleaseOwners = "scenes: SceneDirector (unmapped scenes: end of Play); copy files: Play entry";

        internal static event Action? Changed;

        [Serializable]
        internal sealed class Policy
        {
            public string runPurpose = "";
            public string route = "";
            public string action = "";
            public string rule = "";
            public string reason = "";
            public string[] placedKeys = Array.Empty<string>();
            public string[] warnings = Array.Empty<string>();
            public string[] recoveries = Array.Empty<string>();
            public bool alignFastMode;
            /// <summary>Play の前に固定した snapshot の宣言（混成の経路だけ）。</summary>
            public SnapshotScene[] snapshot = Array.Empty<SnapshotScene>();
        }

        [Serializable]
        internal sealed class SnapshotScene
        {
            public string identity = "";
            public string sourcePath = "";
            public bool dirty;
            public string sha256 = "";
            public bool active;
        }

        [Serializable]
        internal sealed class SceneOrigin
        {
            public string identity = "";
            public string origin = "";
            /// <summary>最後に load された path（由来が変わった時は directory の Scene の path）。</summary>
            public string path = "";
        }

        [Serializable]
        internal sealed class Record
        {
            public string state = "";
            public string effectiveMode = "";
            public string revision = "";
            public bool variantProfileActive;
            // Runtime の log だけでは initializer の成功を判定できない。
            public string initializer = Unknown;
            public int errorCount;
            public int exceptionCount;
            public string firstFailure = "";
            public string sealedLog = "";
            public double enteredPlayTime;
            public bool observing;
            /// <summary>起動の取り込みの段が log に出した Scene 名（宣言とは別の欄）。</summary>
            public string[] intake = Array.Empty<string>();
            /// <summary>Scene ごとの由来。取り込みで snapshot、起動の後に同名が別の path から読まれたら snapshot->directory。</summary>
            public SceneOrigin[] origins = Array.Empty<SceneOrigin>();
            /// <summary>Play の後に複製の file が残っていないか（yes / no）。snapshot の無い Play と、まだ Play 中の時は空。</summary>
            public string copiesRemoved = "";
        }

        [Serializable]
        internal sealed class Document
        {
            public Policy policy = new();
            public Record record = new();
        }

        internal static Document? Load()
        {
            var json = SessionState.GetString(SessionKey, string.Empty);
            return json.Length == 0 ? null : JsonUtility.FromJson<Document>(json);
        }

        internal static void Clear()
        {
            SessionState.EraseString(SessionKey);
            Changed?.Invoke();
        }

        /// <summary>Play に入る計画を書き、log の観測を始める。起動の失敗は EnteredPlayMode より前の BeforeSceneLoad で起きる。</summary>
        internal static void BeginPlanned(PlayEntryFacts? facts, PlayEntryPlan plan, IReadOnlyList<SnapshotScene>? snapshot = null)
        {
            var document = Create(facts, plan, PlayEntryState.Planned, observing: true);
            document.policy.snapshot = snapshot?.ToArray() ?? Array.Empty<SnapshotScene>();
            Save(document);
        }

        internal static void MarkBlocked(PlayEntryFacts? facts, PlayEntryPlan plan)
        {
            var document = Create(facts, plan, PlayEntryState.Blocked, observing: false);
            Save(document);
            Debug.LogWarning(FormatConsoleLine(document));
        }

        /// <summary>EnteredPlayMode。既に失敗を観測していれば StartupFailed を保つ。</summary>
        internal static void MarkEnteredPlay(double now)
        {
            var document = Load();
            if (document == null || !document.record.observing) return;
            document.record.enteredPlayTime = now;
            if (document.record.errorCount + document.record.exceptionCount == 0) document.record.state = nameof(PlayEntryState.EnteredPlay);
            Save(document);
            Debug.Log(FormatConsoleLine(document));
        }

        /// <summary>Application.logMessageReceived の 1 件。観測窓の外と観測していない時は無視する。</summary>
        internal static void ObserveLog(string message, LogType type, double now)
        {
            if (type == LogType.Log && !message.Contains(SealedLogMarker) && !message.StartsWith(IntakeLogMarker, StringComparison.Ordinal)) return;
            if (type == LogType.Warning) return;
            var document = Load();
            if (document == null || !document.record.observing) return;

            if (type == LogType.Log && message.StartsWith(IntakeLogMarker, StringComparison.Ordinal))
            {
                var scene = ParseToken(message, "scene=");
                if (scene.Length != 0 && !document.record.intake.Contains(scene, StringComparer.Ordinal))
                {
                    document.record.intake = document.record.intake.Append(scene).ToArray();
                    SetOrigin(document, scene, OriginSnapshot, ParseToken(message, "copy="));
                    Save(document);
                }
                return;
            }

            if (type == LogType.Log)
            {
                document.record.sealedLog = message;
                Save(document);
                return;
            }

            var entered = document.record.enteredPlayTime;
            if (entered > 0 && now - entered > StartupObservationSeconds) return;
            if (type == LogType.Exception) document.record.exceptionCount++;
            else document.record.errorCount++;
            if (document.record.firstFailure.Length == 0) document.record.firstFailure = message;
            var firstFailure = document.record.state != nameof(PlayEntryState.StartupFailed);
            document.record.state = nameof(PlayEntryState.StartupFailed);
            Save(document);
            // Warning にする。Error で出すと入口自身の 1 行が観測中の Error 件数に混ざる。
            if (firstFailure) Debug.LogWarning(FormatConsoleLine(document));
        }

        /// <summary>
        /// 宣言の Scene が、起動の後に複製以外の path で load された。由来を snapshot->directory にし、Warning を 1 回の Play で identity ごとに
        /// 1 回だけ出す（黙って切り替わらない。2 回目以降は path だけを更新する）。宣言に無い identity と、観測していない時は無視する。
        /// </summary>
        internal static void MarkSourceChanged(string identity, string path)
        {
            var document = Load();
            if (document == null || !document.record.observing) return;
            if (!document.policy.snapshot.Any(x => string.Equals(x.identity, identity, StringComparison.Ordinal))) return;
            var first = !document.record.origins.Any(x => string.Equals(x.identity, identity, StringComparison.Ordinal)
                                                          && x.origin == OriginSnapshotThenDirectory);
            SetOrigin(document, identity, OriginSnapshotThenDirectory, path);
            Save(document);
            if (first)
                Debug.LogWarning(LogPrefix + "Scene '" + identity + "' was loaded again from " + path +
                                 " instead of the play snapshot; it now runs the built content, not the scene you were editing.");
        }

        /// <summary>snapshot のある Play の後、複製の file が残っていないかを書く。snapshot の無い Play では何もしない。</summary>
        internal static void MarkCopiesRemoved(bool removed)
        {
            var document = Load();
            if (document == null || document.policy.snapshot.Length == 0) return;
            document.record.copiesRemoved = removed ? "yes" : "no";
            Save(document);
        }

        /// <summary>Edit mode に戻った。観測を閉じ、record は読めるまま残す。</summary>
        internal static void StopObserving()
        {
            var document = Load();
            if (document == null || !document.record.observing) return;
            document.record.observing = false;
            Save(document);
        }

        internal static string FormatConsoleLine(Document document)
        {
            var policy = document.policy;
            var record = document.record;
            var warnings = policy.warnings.Length == 0 ? "" : " warnings=[" + string.Join(" | ", policy.warnings) + "]";
            return LogPrefix + "policy: purpose=" + policy.runPurpose + " route=" + policy.route + " action=" + policy.action +
                   " rule=" + policy.rule + " keys=" + policy.placedKeys.Length + " reason=\"" + policy.reason + "\"" + warnings +
                   (policy.recoveries.Length == 0 ? "" : " recovery=[" + string.Join(", ", policy.recoveries) + "]") +
                   " | record: state=" + record.state + " mode=" + record.effectiveMode + " revision=" + record.revision +
                   " variantProfile=" + record.variantProfileActive + " initializer=" + record.initializer +
                   " errors=" + record.errorCount + " exceptions=" + record.exceptionCount +
                   " sealed=" + (record.sealedLog.Length != 0) + FormatSnapshot(document);
        }

        /// <summary>Console の 1 行の snapshot の部分。宣言（identity、元の path、Dirty、SHA-256）、取り込み、由来、複製の残り、解放の担当。</summary>
        internal static string FormatSnapshot(Document document)
        {
            var policy = document.policy;
            var record = document.record;
            if (policy.snapshot.Length == 0) return "";
            return " | snapshot: declared=[" + string.Join("; ", policy.snapshot.Select(x =>
                       x.identity + (x.active ? "*" : "") + " from " + x.sourcePath + (x.dirty ? " dirty" : "") +
                       " sha256=" + (x.sha256.Length >= 12 ? x.sha256.Substring(0, 12) : x.sha256))) + "]" +
                   " intake=[" + string.Join(", ", record.intake) + "]" +
                   " origins=[" + string.Join(", ", record.origins.Select(x => x.identity + "=" + x.origin)) + "]" +
                   " copiesRemoved=" + (record.copiesRemoved.Length == 0 ? "pending" : record.copiesRemoved) +
                   " release=(" + ReleaseOwners + ")";
        }

        private static void SetOrigin(Document document, string identity, string origin, string path)
        {
            var origins = document.record.origins.ToList();
            var index = origins.FindIndex(x => string.Equals(x.identity, identity, StringComparison.Ordinal));
            var entry = new SceneOrigin { identity = identity, origin = origin, path = path };
            if (index < 0) origins.Add(entry);
            else origins[index] = entry;
            document.record.origins = origins.ToArray();
        }

        private static string ParseToken(string message, string name)
        {
            var start = message.IndexOf(name, StringComparison.Ordinal);
            if (start < 0) return "";
            start += name.Length;
            var end = message.IndexOf(' ', start);
            return end < 0 ? message.Substring(start) : message.Substring(start, end - start);
        }

        internal static PlayEntryRecovery ParseRecoveries(Policy policy) =>
            policy.recoveries.Aggregate(PlayEntryRecovery.None,
                (all, name) => Enum.TryParse<PlayEntryRecovery>(name, out var one) ? all | one : all);

        private static Document Create(PlayEntryFacts? facts, PlayEntryPlan plan, PlayEntryState state, bool observing)
        {
            return new Document
            {
                policy = new Policy
                {
                    runPurpose = facts?.RunPurpose ?? "",
                    route = facts?.Route.ToString() ?? "",
                    action = plan.Action.ToString(),
                    rule = plan.Rule.ToString(),
                    reason = plan.Reason,
                    placedKeys = plan.Environment.Select(x => x.Key).ToArray(),
                    warnings = plan.Warnings.ToArray(),
                    recoveries = Enum.GetValues(typeof(PlayEntryRecovery)).Cast<PlayEntryRecovery>()
                        .Where(x => x != PlayEntryRecovery.None && plan.Recovery.HasFlag(x)).Select(x => x.ToString()).ToArray(),
                    alignFastMode = plan.AlignFastMode,
                },
                record = new Record
                {
                    state = state.ToString(),
                    effectiveMode = plan.EffectiveMode,
                    // 素通りの directory は入口が revision を選んでいないので、観測できない。
                    revision = plan.Revision.Length != 0 ? plan.Revision
                        : plan.Action == PlayEntryAction.PassThrough && plan.EffectiveMode == PlayEntryDecision.Directory ? Unknown : "",
                    variantProfileActive = facts?.HasVariantProfile ?? false,
                    observing = observing,
                },
            };
        }

        private static void Save(Document document)
        {
            SessionState.SetString(SessionKey, JsonUtility.ToJson(document));
            Changed?.Invoke();
        }
    }
}
