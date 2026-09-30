#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace OneStarMaker.Editor.TestObservation
{
    [Serializable] internal sealed class TestLeaf
    {
        public string id = "", name = "", fullname = "", uniqueName = "", assemblyName = "", runState = "";
    }

    [Serializable] internal sealed class TestEvent
    {
        public long sequence, ticks;
        public int domainOrdinal;
        public string kind = "", id = "", result = "", label = "", utc = "";
    }

    [Serializable] internal sealed class TestResultLeaf
    {
        public string id = "", name = "", fullname = "", result = "", label = "";
    }

    [Serializable] internal sealed class TestDomain
    {
        public int ordinal;
        public string id = "", bootstrapUtc = "", reloadBeforeUtc = "";
        public long bootstrapTicks, reloadBeforeTicks;
    }

    [Serializable] internal sealed class AssemblyObservation
    {
        public int domainOrdinal;
        public long ticks;
        public string observedAtUtc = "", fullName = "", location = "", loadedModuleVersionId = "", diskSha256 = "", status = "", failure = "";
    }

    [Serializable] internal sealed class TestClock
    {
        public string status = "unobserved", runStartedUtc = "", runFinishedUtc = "";
        public long frequency, runStartedTicks, runFinishedTicks;
        public double durationMs;
    }

    [Serializable] internal sealed class TestObservationState
    {
        public int schemaVersion = 1;
        public string recordKind = "unity-test-observation", invocationId = "", projectPath = "", platform = "EditMode";
        public int processId;
        public string status = "incomplete";
        public List<string> failure = new List<string>();
        public bool @sealed;
        public long sealSequence, sequence;
        public TestClock clock = new TestClock();
        public List<TestDomain> domains = new List<TestDomain>();
        public List<TestLeaf> selected = new List<TestLeaf>();
        public List<TestEvent> events = new List<TestEvent>();
        public List<TestResultLeaf> results = new List<TestResultLeaf>();
        public List<AssemblyObservation> assemblies = new List<AssemblyObservation>();
        public bool runStarted, runFinished;

        internal static TestObservationState Create(string invocation, string project, int pid, long frequency)
        {
            return new TestObservationState { invocationId = invocation, projectPath = project,
                processId = pid, clock = new TestClock { frequency = frequency } };
        }

        // 失敗は後続の正常 callback で消さない。欠測した時点の理由を最後の seal まで運ぶ。
        internal void Fail(string reason, bool invalid = true)
        {
            sequence++;
            if (!failure.Contains(reason)) failure.Add(reason);
            status = invalid ? "invalid" : (status == "invalid" ? "invalid" : "incomplete");
        }

        internal void Bootstrap(string id, string utc, long ticks, long frequency)
        {
            if (@sealed) { Fail("seal 後に domain が開始しました"); return; }
            // RunFinished 後の復帰は終了候補を覆せない。reload 境界の保存状態が壊れてもここで拒否する。
            if (runFinished) Fail("RunFinished 後に domain が復帰しました");
            if (clock.frequency != frequency || frequency <= 0) { Fail("時計 frequency が変化しました"); return; }
            var ordinal = domains.Count;
            if (ordinal > 0 && string.IsNullOrEmpty(domains[ordinal - 1].reloadBeforeUtc))
                Fail("beforeReload のない domain 復帰です");
            domains.Add(new TestDomain { ordinal = ordinal, id = id, bootstrapUtc = utc, bootstrapTicks = ticks });
            sequence++;
        }

        internal void BeforeReload(string utc, long ticks)
        {
            if (@sealed) { Fail("seal 後に reload が起きました"); return; }
            // 終端 callback 以後の reload を通常の寿命延長として扱うと、旧 complete 候補が seal されてしまう。
            if (runFinished) Fail("RunFinished 後に reload が起きました");
            if (domains.Count == 0) { Fail("bootstrap 前の reload です"); return; }
            var domain = domains[domains.Count - 1];
            if (!string.IsNullOrEmpty(domain.reloadBeforeUtc)) { Fail("beforeReload が重複しました"); return; }
            domain.reloadBeforeUtc = utc;
            domain.reloadBeforeTicks = ticks;
            sequence++;
        }

        internal void RunStarted(IEnumerable<TestLeaf> leaves, string utc, long ticks)
        {
            if (@sealed || runStarted || runFinished) { Fail("RunStarted が重複または seal 後です"); return; }
            runStarted = true;
            clock.runStartedUtc = utc;
            clock.runStartedTicks = ticks;
            selected.AddRange(leaves);
            sequence++;
            if (selected.Count == 0 || selected.Any(x => string.IsNullOrEmpty(x.id)) ||
                selected.Select(x => x.id).Distinct(StringComparer.Ordinal).Count() != selected.Count)
                Fail("初回選択 leaf が空または ID 重複です");
        }

        internal void Event(string kind, string id, string result, string label, string utc, long ticks)
        {
            if (@sealed || !runStarted || runFinished) { Fail("実行範囲外の test callback です"); return; }
            if (!selected.Any(x => x.id == id)) { Fail("選択外の test callback です: " + id); return; }
            if (kind != "started" && kind != "finished") { Fail("未知の event 種別です"); return; }
            if (kind == "started" && events.Any(x => x.id == id && x.kind == "finished"))
                Fail("finished 後に started が来ました: " + id);
            if (kind == "finished" && !events.Any(x => x.id == id && x.kind == "started"))
                Fail("started 前に finished が来ました: " + id, false);
            if (kind == "finished" && !KnownResult(result)) Fail("未知の test result です: " + result);
            if (kind == "started" && (result != "" || label != "")) Fail("started に結果が含まれます");
            sequence++;
            events.Add(new TestEvent { sequence = sequence, domainOrdinal = domains.Count - 1,
                kind = kind, id = id, result = result, label = label, utc = utc, ticks = ticks });
        }

        internal void RunFinished(IEnumerable<TestResultLeaf> leaves, string utc, long ticks)
        {
            if (@sealed || !runStarted || runFinished) { Fail("RunFinished が重複または範囲外です"); return; }
            runFinished = true;
            clock.runFinishedUtc = utc;
            clock.runFinishedTicks = ticks;
            clock.durationMs = 1000.0 * (ticks - clock.runStartedTicks) / clock.frequency;
            clock.status = "observed";
            results.AddRange(leaves);
            sequence++;
            ValidateResults();
            ValidateAssemblies();
            if (failure.Count == 0) status = "complete";
        }

        internal void AddAssemblies(IEnumerable<AssemblyObservation> values)
        {
            if (@sealed) { Fail("seal 後に assembly 観測が来ました"); return; }
            assemblies.AddRange(values);
            sequence++;
            ValidateAssemblies();
        }

        internal void Seal()
        {
            // RunFinished は候補に過ぎない。終了時にだけ完了を封印し、遅い異常は sequence 差で拒否する。
            if (@sealed) { Fail("seal が重複しました"); return; }
            if (!runStarted || !runFinished) Fail("RunStarted または RunFinished が欠測しました", false);
            if (domains.Count > 0 && !string.IsNullOrEmpty(domains[domains.Count - 1].reloadBeforeUtc))
                Fail("reload 復帰前に終了しました", false);
            if (runFinished && domains.Any(x => x.bootstrapTicks > clock.runFinishedTicks ||
                (x.reloadBeforeTicks > 0 && x.reloadBeforeTicks > clock.runFinishedTicks)))
                Fail("RunFinished の時計より後の domain/reload が含まれます");
            ValidateResults();
            ValidateAssemblies();
            sequence++;
            @sealed = true;
            sealSequence = sequence;
        }

        private void ValidateResults()
        {
            if (!runFinished) return;
            var selectedIds = selected.Select(x => x.id).ToArray();
            var resultIds = results.Select(x => x.id).ToArray();
            if (!selectedIds.SequenceEqual(resultIds, StringComparer.Ordinal)) Fail("選択と終端 leaf の ID/順序が不一致です");
            foreach (var result in results)
            {
                if (!KnownResult(result.result)) { Fail("未知の終端 result です: " + result.result); continue; }
                var started = events.Where(x => x.id == result.id && x.kind == "started").ToArray();
                var finished = events.Where(x => x.id == result.id && x.kind == "finished").ToArray();
                // retry/repeat の複数 attempt は今回の対象外。ID が同じでも間引かない。
                if (started.Length > 1 || finished.Length > 1) Fail("複数 attempt を検出しました: " + result.id);
                if (result.result != "Skipped" && (started.Length != 1 || finished.Length != 1))
                    Fail("実行 leaf の callback が欠測しました: " + result.id, false);
                if (result.result == "Skipped" && started.Length != finished.Length)
                    Fail("Skipped の callback が片方だけです: " + result.id);
                if (finished.Length == 1 && (finished[0].result != result.result || finished[0].label != result.label))
                    Fail("callback と終端結果が不一致です: " + result.id);
            }
        }

        private void ValidateAssemblies()
        {
            foreach (var item in assemblies)
                if (item.status != "observed" || item.location == "" || item.diskSha256 == "" || item.loadedModuleVersionId == "")
                    Fail("assembly identity が取得不能です: " + item.fullName, false);
            foreach (var group in assemblies.GroupBy(x => x.fullName, StringComparer.Ordinal))
            {
                if (group.GroupBy(x => x.domainOrdinal + ":" + x.ticks).Any(x => x.Count() > 1))
                    Fail("同名 assembly の複数実体です: " + group.Key);
                if (group.Select(x => x.location + "|" + x.loadedModuleVersionId + "|" + x.diskSha256)
                    .Distinct(StringComparer.Ordinal).Count() > 1) Fail("assembly identity が実行中に変化しました: " + group.Key);
            }
        }

        private static bool KnownResult(string value) => value == "Passed" || value == "Failed" || value == "Skipped" || value == "Inconclusive";
    }
}
