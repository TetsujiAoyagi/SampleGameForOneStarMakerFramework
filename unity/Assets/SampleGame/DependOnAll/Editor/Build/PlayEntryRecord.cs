#nullable enable

using System;
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
        internal static void BeginPlanned(PlayEntryFacts? facts, PlayEntryPlan plan) =>
            Save(Create(facts, plan, PlayEntryState.Planned, observing: true));

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
            if (type == LogType.Log && !message.Contains(SealedLogMarker)) return;
            if (type == LogType.Warning) return;
            var document = Load();
            if (document == null || !document.record.observing) return;

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
                   " sealed=" + (record.sealedLog.Length != 0);
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
