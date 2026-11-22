#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using I = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayInventory;
using V = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayVerdict;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// 第 2 process（HANDOFF AC2〜AC5・AC8）。第 1 process の出力を照合し、6 つの round を 3 つずつ、Player の process を 1 つずつ順に起動する。
    /// launcher は起動（pid、時刻、渡した環境変数、除いた環境変数の名前）と終わり方（終了 code、所要、止めた理由）だけを書き、観測の行を
    /// 書かない（I-2 (a)）。record は Player の runner が書き、Player の log の file はそのまま証拠に置く。
    /// test が assert するのは harness の成立だけ（全 round の process を起動して終わりを見届けた、record と log を集めた、後始末をした）で、
    /// 答えの向きは assert しない。判定の写像の出力は補助として verdict の file に書く（HANDOFF §2）。
    /// 待ち合わせは frame の進行と process の終了の signal で行い、Task.Delay・Thread.Sleep を使わない（常時契約）。
    /// </summary>
    public sealed class PlayerOverlayLaunch
    {
        // 1 起動あたりの runner の上限、終了行の後の猶予、launcher の上限の余裕（HANDOFF §4 の B 適応の既定。変えたら record に残る）。
        public const int RunnerLimitSeconds = 180, GraceAfterEndSeconds = 30, LauncherMarginSeconds = 60;
        public const string EntriesFile = "launch-entries.json";
        private const string ReservedPrefix = "SAMPLEGAME_";

        [UnityTest, Explicit, Timeout(7200000)]
        public IEnumerator SecondProcess()
        {
            var run = PovRun.Open("second");
            var first = JsonUtility.FromJson<FirstFacts>(File.ReadAllText(Path.Combine(run.Directory, I.FirstFactsFile)));
            var digests = I.Digests(run.Directory);
            var inputMatched = first.RunId == run.RunId && first.Head == run.Head && digests.SequenceEqual(first.Digests);
            var preconditions = V.Preconditions(first);
            run.Write("second-start", ("digests", digests), ("firstDigests", first.Digests), ("inputMatched", inputMatched), ("preconditions", preconditions),
                ("runnerLimitSeconds", RunnerLimitSeconds), ("graceAfterEndSeconds", GraceAfterEndSeconds), ("launcherLimitSeconds", RunnerLimitSeconds + LauncherMarginSeconds));
            // 第 1 process の出力が照合できない・前提が成り立たない時は、round を起動しない（AC1・AC2。未判定）。
            Assert.That(inputMatched && preconditions.Count == 0, Is.True, "first process output does not match or preconditions do not hold; rounds were not launched");

            var exe = Path.Combine(run.Directory, I.PlayerFolder, I.PlayerExeName);
            var entries = new List<LaunchEntry>();
            var started = new List<Process>();
            var failures = new List<string>();
            foreach (var round in V.AllRounds)
            for (var replicate = 1; replicate <= V.Replicates; replicate++)
            {
                var entry = new LaunchEntry { Round = round, Replicate = replicate, Label = round + "#" + replicate };
                entry.RecordFile = Path.Combine("records", round + "-" + replicate + ".jsonl");
                entry.LogFile = Path.Combine("logs", round + "-" + replicate + ".log");
                Directory.CreateDirectory(Path.Combine(run.Directory, "records"));
                Directory.CreateDirectory(Path.Combine(run.Directory, "logs"));
                Process? process = null;
                try { process = Start(run, exe, entry); }
                catch (Exception ex) { failures.Add(entry.Label + ": start failed: " + ex.Message); run.Write("launch-failed", ("label", entry.Label), ("error", ex.ToString())); }
                if (process == null) { entries.Add(entry); continue; }
                started.Add(process);
                // pid は起動の直後に record へ書く（launcher が落ちても、止めるべき process が分かる。I-3 (d)）。
                run.Write("launch", ("label", entry.Label), ("round", round), ("replicate", replicate), ("pid", entry.Pid), ("startedAt", entry.StartedAt),
                    ("environment", entry.Environment), ("removedVariables", entry.RemovedVariables), ("arguments", process.StartInfo.Arguments));
                var watch = Stopwatch.StartNew();
                var poll = Stopwatch.StartNew();
                double? endSeenAt = null;
                while (!process.HasExited)
                {
                    yield return null;
                    if (poll.ElapsedMilliseconds < 1000) continue;
                    poll.Restart();
                    if (endSeenAt == null && HasEndLine(Path.Combine(run.Directory, entry.RecordFile))) endSeenAt = watch.Elapsed.TotalSeconds;
                    if (endSeenAt != null && watch.Elapsed.TotalSeconds - endSeenAt > GraceAfterEndSeconds) { Stop(process, entry, "after-end"); break; }
                    if (watch.Elapsed.TotalSeconds > RunnerLimitSeconds + LauncherMarginSeconds) { Stop(process, entry, "launcher-limit"); break; }
                }
                process.WaitForExit(10000);
                entry.EndedAt = entry.EndedAt.Length != 0 ? entry.EndedAt : Now();
                entry.ExitedByItself = entry.StopReason.Length == 0;
                entry.ExitCode = process.HasExited ? process.ExitCode : int.MinValue;
                run.Write("exit", ("label", entry.Label), ("pid", entry.Pid), ("endedAt", entry.EndedAt), ("exitCode", entry.ExitCode), ("exitedByItself", entry.ExitedByItself),
                    ("stopReason", entry.StopReason), ("elapsedMs", watch.ElapsedMilliseconds), ("recordExists", File.Exists(Path.Combine(run.Directory, entry.RecordFile))),
                    ("logExists", File.Exists(Path.Combine(run.Directory, entry.LogFile))));
                entries.Add(entry);
            }

            // 自分が起動した process が全て終わっていること（他の process を名前で止めない。I-3 (d)、AC9 の (2) の材料）。
            var leftover = started.Where(x => !x.HasExited).Select(x => x.Id).ToList();
            run.Write("pids-after", ("pids", started.Select(x => x.Id)), ("alive", leftover));
            var summary = new LaunchSummary { InputMatched = inputMatched, Leftover = leftover.Count != 0, Entries = entries.ToArray() };
            File.WriteAllText(Path.Combine(run.Directory, EntriesFile), JsonUtility.ToJson(summary, true));
            WriteVerdict(run, "second", first, summary, Array.Empty<ProcessInput>());

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
            Assert.That(entries.Count, Is.EqualTo(V.AllRounds.Length * V.Replicates));
            Assert.That(leftover, Is.Empty, "launched Player processes remain");
            Assert.That(entries.All(x => File.Exists(Path.Combine(run.Directory, x.RecordFile)) && File.Exists(Path.Combine(run.Directory, x.LogFile))), Is.True, "a record or a Player log is missing");
        }

        // Player の起動。`SAMPLEGAME_` で始まる環境変数を除き（親の process の製品の設定を継承させない。HANDOFF §2 の (p)）、round の設定を
        // 環境変数で渡す。command line に足すのは -batchmode と -logFile だけ（-nographics は付けない。HANDOFF §2.5）。
        private static Process Start(PovRun run, string exe, LaunchEntry entry)
        {
            var info = new ProcessStartInfo(exe, "-batchmode -logFile \"" + Path.Combine(run.Directory, entry.LogFile) + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe)!,
            };
            var removed = info.Environment.Keys.Where(k => k.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var k in removed) info.Environment.Remove(k);
            var passed = RoundEnvironment(run, entry.Round, entry.Label, Path.Combine(run.Directory, entry.RecordFile), RunnerLimitSeconds, null);
            foreach (var kv in passed) info.Environment[kv.Key] = kv.Value;
            var process = Process.Start(info) ?? throw new InvalidOperationException("Player did not start: " + exe);
            entry.Pid = process.Id;
            entry.StartedAt = Now();
            entry.Environment = passed.Select(x => x.Key + "=" + x.Value).ToArray();
            entry.RemovedVariables = removed.ToArray();
            return process;
        }

        /// <summary>runner に渡す環境変数（名前は PlayerOverlayRunner の定数と揃える）。Editor の対照も同じ組を使う。</summary>
        internal static Dictionary<string, string> RoundEnvironment(PovRun run, string round, string label, string recordPath, int limitSeconds, bool? domainReloadDisabled)
        {
            var env = new Dictionary<string, string>
            {
                ["OSM_POV_ROUND"] = round, ["OSM_POV_LABEL"] = label, ["OSM_POV_ORDER"] = string.Join(",", V.RegisterOrder(round)),
                ["OSM_POV_DIR_BASE"] = Path.Combine(run.Directory, I.BaseFolder), ["OSM_POV_DIR_OVERLAY"] = Path.Combine(run.Directory, I.OverlayFolder),
                ["OSM_POV_FACTS"] = Path.Combine(run.Directory, I.FixtureFactsFile), ["OSM_POV_RECORD"] = recordPath, ["OSM_POV_RUN"] = run.RunId,
                ["OSM_POV_HEAD"] = run.Head, ["OSM_POV_LIMIT"] = limitSeconds.ToString(CultureInfo.InvariantCulture),
            };
            if (domainReloadDisabled != null) env["OSM_POV_DOMAIN_RELOAD_DISABLED"] = domainReloadDisabled.Value ? "true" : "false";
            return env;
        }

        // 自分が起動した process だけを止める。
        private static void Stop(Process process, LaunchEntry entry, string reason)
        {
            entry.StopReason = reason;
            entry.EndedAt = Now();
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { /* 止める前に終わった */ }
        }

        // runner は行ごとに開いて閉じるので、読む側は共有で開き、書き込み中の衝突は次の poll に回す。
        internal static bool HasEndLine(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Contains("\"type\":\"end\"");
            }
            catch (IOException) { return false; }
        }

        private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        /// <summary>launcher の起動の記録と record・Player の log から、判定の写像の入力を作る。</summary>
        internal static List<ProcessInput> Inputs(PovRun run, IEnumerable<LaunchEntry> entries) =>
            entries.Select(e => new ProcessInput
            {
                Launch = e,
                Record = I.ReadRecord(Path.Combine(run.Directory, e.RecordFile)),
                PlayerLog = File.Exists(Path.Combine(run.Directory, e.LogFile)) ? File.ReadAllLines(Path.Combine(run.Directory, e.LogFile)) : Array.Empty<string>(),
            }).ToList();

        /// <summary>補助の判定の file を書く（判定 C は record の行から独立に確かめる）。</summary>
        internal static void WriteVerdict(PovRun run, string name, FirstFacts first, LaunchSummary summary, IReadOnlyList<ProcessInput> control)
        {
            var verdict = V.Judge(first, summary.InputMatched, summary.Leftover, Inputs(run, summary.Entries), control);
            File.WriteAllText(Path.Combine(run.Directory, "verdict-" + name + ".json"), JsonUtility.ToJson(verdict, true));
            run.Write("verdict", ("file", "verdict-" + name + ".json"), ("outcome", verdict.Outcome), ("spike", verdict.Spike), ("editorControlRequired", verdict.EditorControlRequired));
        }
    }

    [Serializable] internal sealed class LaunchSummary
    {
        public bool InputMatched, Leftover;
        public LaunchEntry[] Entries = Array.Empty<LaunchEntry>();
    }
}
