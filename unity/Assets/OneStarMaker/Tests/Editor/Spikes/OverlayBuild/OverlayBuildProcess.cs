#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using S = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlaySpec;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>
    /// build の process（HANDOFF CONTENT_OVERLAY_BUILD §5 の判定必須 2）。fixture を v1 で作り、製品の coordinator で base（6 root、Full）を
    /// build して publish し、fixture を v2 にしてから、逆引きの結果で上書きを別の contentSet に build し、record を書き、fixture の source を消す。
    /// 前提 P1（観測点を source の root を通して読んだ値）と P2（source file の sha256）を、それぞれの build の直前に採る。
    /// 通常の回帰に混ぜないので [Explicit]。test が失敗するのは harness 自身の例外の時だけで、build の失敗や期待との違いは record の行と
    /// build-facts.json の値にする。判定は判定 C が record から読む（<see cref="OverlayBuildVerdict"/> は補助）。
    /// </summary>
    public sealed class OverlayBuildProcess
    {
        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator Build()
        {
            var run = OverlayRun.Create();
            var facts = new BuildFacts { RunId = run.RunId, Head = run.Head };
            var care = Aftercare.Begin(run);
            var scenes = SceneGuard.Begin();
            // build が出す Error の log を test の失敗にすると coroutine が途中で打ち切られ、finally（fixture の削除）が走らない（S2-1 の実測）。
            // 購読は try の直前に始め、finally の Stop で必ず戻す。
            var log = OverlayLog.Start(run);
            try
            {
                OverlayArtifacts.Reset();
                log.Phase("fixture-v1", false);
                new OverlayBuildFixture().CreateV1();
                var v1 = OverlayBuildFixture.SourceHashes();
                facts.BeforeBase = OverlayBuildFixture.ReadPoints();
                run.Write("p1-before-base", ("points", facts.BeforeBase.Select(Describe)), ("sources", v1.Select(x => x.Key + " " + x.Value)));

                var declaration = Declaration();
                var plan = Plan();
                var coordinator = new BuildContentCoordinator();
                log.Phase("build-base", false);
                var snapshot1 = Snapshot(run, "base", facts.Problems);
                var baseResult = coordinator.Build(new BuildContentRequest(plan, snapshot1, S.BaseSet, OverlayArtifacts.Root, declaration, null, BuildContentMode.Full));
                facts.BaseBuilt = baseResult.IsSuccess;
                WriteResult(run, "base", baseResult);
                yield return null;
                if (!facts.BaseBuilt) { facts.Problems.Add("base build failed: " + baseResult.Summary); yield break; }

                // base を publish し、transport manifest から base の識別を作る（呼び手が、登録する base から読む形）。
                var published = ContentTransportPublisher.Publish(baseResult, Path.Combine(OverlayArtifacts.Root, "transport", baseResult.Identity));
                facts.BasePublished = true;
                var manifestPath = Path.Combine(published, "transport.json");
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(manifestPath));
                facts.BaseDirectory = Path.Combine(published, "content");
                facts.BaseRevision = manifest.revision;
                facts.BaseManifestDigest = S.Sha256(File.ReadAllBytes(manifestPath));
                var baseIdentity = new ContentOverlayBaseIdentity(manifest.contentSet, manifest.revision, manifest.target, facts.BaseManifestDigest,
                    string.IsNullOrEmpty(manifest.declaration.name) ? null : manifest.declaration.name);
                run.Write("base-published", ("directory", published), ("contentSet", manifest.contentSet), ("revision", manifest.revision),
                    ("target", manifest.target), ("manifestSha256", facts.BaseManifestDigest), ("declaration", manifest.declaration.name));

                log.Phase("fixture-v2", false);
                new OverlayBuildFixture().ApplyV2();
                var v2 = OverlayBuildFixture.SourceHashes();
                facts.ChangedSources = v2.Where(x => v1[x.Key] != x.Value).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).ToList();
                facts.BeforeOverlay = OverlayBuildFixture.ReadPoints();
                run.Write("p1-before-overlay", ("points", facts.BeforeOverlay.Select(Describe)), ("sources", v2.Select(x => x.Key + " " + x.Value)),
                    ("changedSources", facts.ChangedSources));

                var snapshot2 = Snapshot(run, "overlay", facts.Problems);
                var selection = ContentOverlaySelection.Select(plan, snapshot2, S.Edits);
                facts.SelectedRoots = selection.Roots.Select(x => x.Root.StableKey).ToList();
                facts.RootReasons = selection.Roots.Select(x => x.Root.StableKey + "=" + string.Join(";", x.Edits)).ToList();
                facts.Unmatched = selection.Unmatched.Select(x => x.Path + "=" + x.Reason).ToList();
                facts.SharedAssets = selection.SharedAssets.ToList();
                run.Write("selection", ("required", selection.IsOverlayRequired), ("edits", selection.Edits), ("roots", facts.RootReasons),
                    ("unmatched", facts.Unmatched), ("shared", facts.SharedAssets));
                if (!selection.IsOverlayRequired) { facts.Problems.Add("selection needs no overlay"); yield break; }

                var baseFiles = BaseInventory(baseResult, published);
                run.Write("base-inventory", ("when", "before-overlay"), ("files", baseFiles));
                log.Phase("build-overlay", false);
                var overlayResult = coordinator.Build(new BuildContentRequest(selection, baseIdentity, S.OverlaySet, OverlayArtifacts.Root, declaration, null));
                facts.OverlayBuilt = overlayResult.IsSuccess;
                facts.OverlayDirectory = overlayResult.ContentPath ?? "";
                facts.OverlayIdentity = overlayResult.Identity;
                WriteResult(run, "overlay", overlayResult);
                ReadOverlayReports(facts, overlayResult);
                var after = BaseInventory(baseResult, published);
                facts.BaseUnchanged = after.SequenceEqual(baseFiles);
                run.Write("base-inventory", ("when", "after-overlay"), ("files", after), ("unchanged", facts.BaseUnchanged));
                yield return null;
                if (!facts.OverlayBuilt) { facts.Problems.Add("overlay build failed: " + overlayResult.Summary); yield break; }
                facts.Finished = true;
            }
            finally
            {
                // round は fixture の source が無い Editor で Play する（観測が directory を読むことを canary と合わせて確かめる）。
                log.Phase("aftercare", false);
                try { OverlayBuildFixture.Delete(); }
                catch (Exception ex) { facts.Problems.Add("fixture delete failed: " + ex.Message); }
                facts.FixtureDeleted = !OverlayBuildFixture.Exists;
                scenes.End(facts.Problems);
                Aftercare.End(run, care, facts.Problems);
                log.Stop();
                if (facts.Problems.Count != 0) facts.Finished = false;
                File.WriteAllText(Path.Combine(run.Directory, "build-facts.json"), JsonUtility.ToJson(facts, true));
                File.WriteAllText(OverlayRun.BuildPointer, run.RunId);
                run.Write("process-end", ("finished", facts.Finished), ("problems", facts.Problems), ("errorLogs", log.Errors));
            }
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        private static string Describe(PointValue p) => p.Id + " " + p.Path + " " + p.Value;

        // fixture と同じ合成の宣言（base と上書きの両方に付ける）。
        private static BuildPurposeDeclaration Declaration() =>
            new BuildPurposeDeclaration(S.DeclarationName, S.Roots, new BuildRequest(Array.Empty<BuildTag>()),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        // 6 root を selected に持つ plan。stable key と logical key は root の名前、physical key は root の固定 GUID。
        private static BuildPlan Plan() =>
            new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), Candidates(), Array.Empty<BuildContentExclusion>());

        private static BuildContentCandidate[] Candidates() => S.Roots.Select(r =>
            new BuildContentCandidate(r, r, OverlayBuildFixture.FixedGuid(S.RootPath(r)), new BuildProvenance("ovb-fixture", r))).ToArray();

        // 実 AssetDatabase から製品の AssetDependencySnapshotBuilder で閉包を作る（AC2。期待は fixture の作者が書いた root の名前）。
        private static BuildMaterializationSnapshot Snapshot(OverlayRun run, string when, List<string> problems)
        {
            var builder = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway());
            var issues = new List<BuildMaterializationIssue>();
            var closures = new List<BuildDependencySnapshot>();
            foreach (var c in Candidates())
            {
                var closure = builder.Build(c.PhysicalKey, c.StableKey, issues);
                if (closure != null) closures.Add(closure);
                run.Write("closure", ("when", when), ("root", c.StableKey), ("entries", closure?.Entries.Select(x => x.Path)));
            }
            if (issues.Count != 0) problems.Add("materialization issues (" + when + "): " + string.Join("; ", issues.Select(x => x.Code + " " + x.Path)));
            return new BuildMaterializationSnapshot(Candidates(), new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), closures);
        }

        private static void WriteResult(OverlayRun run, string name, BuildContentResult result)
        {
            var copy = Path.Combine(run.Directory, "reports", name);
            Directory.CreateDirectory(copy);
            if (File.Exists(result.PreflightPath)) File.Copy(result.PreflightPath, Path.Combine(copy, "preflight.json"), true);
            if (File.Exists(result.OutcomePath)) File.Copy(result.OutcomePath, Path.Combine(copy, "outcome.json"), true);
            run.Write("build-end", ("build", name), ("identity", result.Identity), ("success", result.IsSuccess), ("summary", result.Summary),
                ("content", result.ContentPath), ("issues", result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail)),
                ("preflight", File.Exists(result.PreflightPath) ? File.ReadAllText(result.PreflightPath) : null),
                ("outcome", File.Exists(result.OutcomePath) ? File.ReadAllText(result.OutcomePath) : null));
        }

        private static void ReadOverlayReports(BuildFacts facts, BuildContentResult result)
        {
            if (File.Exists(result.PreflightPath))
            {
                var p = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.PreflightPath));
                facts.PreflightOverlay = p.overlay;
                facts.PreflightBase = new List<string> { p.overlayBase.contentSet, p.overlayBase.revision, p.overlayBase.target,
                    p.overlayBase.manifestDigest, p.overlayBase.declarationName };
                facts.PreflightEdits = p.overlayEdits.ToList();
                facts.PreflightRoots = p.overlayRoots.Select(x => x.stableKey + "=" + string.Join(";", x.edits)).ToList();
                facts.PreflightUnmatched = p.overlayUnmatched.Select(x => x.path + "=" + x.reason).ToList();
                facts.PreflightShared = p.overlaySharedAssets.ToList();
            }
            if (File.Exists(result.OutcomePath))
                facts.OutcomeOverlay = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.OutcomePath)).overlay;
        }

        // base の content/<identity>、base の workspace、publish した base の全 file の名前と sha256（AC5、I-5）。
        private static List<string> BaseInventory(BuildContentResult baseResult, string published)
        {
            var workspace = Path.Combine(OverlayArtifacts.Root, "work", BuildContentCoordinator.TargetName, S.BaseSet);
            return new[] { ("content", baseResult.ContentPath!), ("workspace", workspace), ("published", published) }
                .SelectMany(x => !Directory.Exists(x.Item2) ? new[] { x.Item1 + " missing" }
                    : Directory.GetFiles(x.Item2, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
                        .Select(f => x.Item1 + "/" + Path.GetRelativePath(x.Item2, f).Replace('\\', '/') + " " + S.Sha256(File.ReadAllBytes(f))))
                .ToList();
        }
    }

    /// <summary>coordinator の出力先（`TestResults/bs3-fixture/ovb`、git 管理外）。marker を持つ時だけ、build の process の始めに消して作り直す。</summary>
    internal static class OverlayArtifacts
    {
        private const string MarkerName = "ovb-artifacts.marker";
        public static string Root => Path.Combine(OverlayRun.RepoRoot, "TestResults", "bs3-fixture", "ovb");

        public static void Reset()
        {
            if (Directory.Exists(Root))
            {
                var marker = Path.Combine(Root, MarkerName);
                if (!File.Exists(marker)) throw new InvalidOperationException(Root + " exists without the harness marker; not touching it.");
                Directory.Delete(Root, true);
            }
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, MarkerName), "ovb");
        }
    }

    /// <summary>
    /// 1 回の実行の record（JSONL）。root は `TestResults/ovb/&lt;実行 id&gt;`（git 管理外）。process ごとに別の file に書き、各行は
    /// header（実行 id、head、Unity の版、target、Enter Play Mode の設定、process の名前）と field を持つ。
    /// </summary>
    internal sealed class OverlayRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string RunRoot => Path.Combine(RepoRoot, "TestResults", "ovb");
        public static string BuildPointer => Path.Combine(RunRoot, "build-process.txt");
        private const string MarkerName = "ovb-run.marker";

        private OverlayRun(string runId, string process, string stamp)
        {
            RunId = runId; Process = process;
            Directory = Path.Combine(RunRoot, runId);
            Head = Git.Read("rev-parse HEAD", out var head) ? head.Trim() : head;
            RecordPath = Path.Combine(Directory, "record-" + process + "-" + stamp + ".jsonl");
        }

        public string RunId { get; }
        public string Process { get; }
        public string Directory { get; }
        public string Head { get; }
        public string RecordPath { get; }

        private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        public static OverlayRun Create()
        {
            var stamp = Stamp();
            var run = new OverlayRun(stamp, "build", stamp);
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            run.Write("process-start", ("scope", Scope));
            return run;
        }

        // round の process は、build の process が書いた実行 id の root を marker で確かめてから開く。
        public static OverlayRun Open(string process)
        {
            if (!File.Exists(BuildPointer)) throw new InvalidOperationException("The build process has not written its record.");
            var runId = File.ReadAllText(BuildPointer).Trim();
            var run = new OverlayRun(runId, process, Stamp());
            var marker = Path.Combine(run.Directory, MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != runId) throw new IOException("Run root has no marker: " + run.Directory);
            run.Write("process-start", ("scope", Scope));
            return run;
        }

        public const string Scope = "this PC, Unity 6000.6.0f1, StandaloneWindows64, Editor Play (Domain Reload disabled), synthetic fixture, base and overlay built in the same Library by the product coordinator";

        public void Write(string type, params (string key, object? value)[] fields)
        {
            var b = new StringBuilder("{");
            void Field(string key, object? value) { if (b.Length > 1) b.Append(','); b.Append(Json(key)).Append(':').Append(Json(value)); }
            Field("runId", RunId); Field("head", Head); Field("unity", Application.unityVersion);
            Field("target", EditorUserBuildSettings.activeBuildTarget + "/" + EditorUserBuildSettings.standaloneBuildSubtarget);
            Field("enterPlayMode", EditorSettings.enterPlayModeOptionsEnabled + "/" + EditorSettings.enterPlayModeOptions);
            Field("process", Process); Field("type", type); Field("at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            foreach (var (key, value) in fields) Field(key, value);
            File.AppendAllText(RecordPath, b.Append("}\n").ToString());
        }

        private static string Json(object? value) => value switch
        {
            null => "null",
            bool x => x ? "true" : "false",
            string x => "\"" + Escape(x) + "\"",
            int or long or ulong or double or float => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            IEnumerable x => "[" + string.Join(",", x.Cast<object?>().Select(Json)) + "]",
            _ => Json(value.ToString()),
        };

        private static string Escape(string s)
        {
            var b = new StringBuilder();
            foreach (var c in s)
                b.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                    _ => c < 0x20 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString() });
            return b.ToString();
        }
    }

    /// <summary>
    /// log の捕捉。Unity の log を test の失敗にせず（<see cref="LogAssert.ignoreFailingMessages"/>）、種類を問わず発生順に unity-log の行として
    /// record に書く。各行の phase は、その log を受けた時の区切りの名前。数える区切りの間の Error と Exception を <see cref="Errors"/> に数える。
    /// 購読と LogAssert は Stop で必ず元へ戻す。
    /// </summary>
    internal sealed class OverlayLog
    {
        private readonly OverlayRun _run;
        private readonly bool _ignoreBefore;
        private readonly object _lock = new object();
        private readonly List<(string phase, bool counting, string type, string message, bool main)> _pending = new List<(string, bool, string, string, bool)>();
        private readonly int _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private string _phase = "start";
        private bool _counting;
        private bool _stopped;

        private OverlayLog(OverlayRun run) { _run = run; _ignoreBefore = LogAssert.ignoreFailingMessages; }

        public int Errors { get; private set; }

        public static OverlayLog Start(OverlayRun run)
        {
            var log = new OverlayLog(run);
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded += log.OnLog;
            run.Write("log-capture", ("subscribed", true), ("ignoreFailingMessagesBefore", log._ignoreBefore));
            return log;
        }

        public void Phase(string phase, bool counting)
        {
            Flush();
            lock (_lock) { _phase = phase; _counting = counting; }
        }

        public void Flush()
        {
            (string phase, bool counting, string type, string message, bool main)[] logs;
            lock (_lock) { logs = _pending.ToArray(); _pending.Clear(); }
            foreach (var l in logs)
            {
                if (l.counting && (l.type == nameof(LogType.Error) || l.type == nameof(LogType.Exception))) Errors++;
                _run.Write("unity-log", ("phase", l.phase), ("counted", l.counting), ("logType", l.type), ("message", l.message), ("mainThread", l.main));
            }
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            Application.logMessageReceivedThreaded -= OnLog;
            Flush();
            LogAssert.ignoreFailingMessages = _ignoreBefore;
            _run.Write("log-capture", ("subscribed", false), ("ignoreFailingMessagesRestored", LogAssert.ignoreFailingMessages));
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            var main = System.Threading.Thread.CurrentThread.ManagedThreadId == _mainThread;
            lock (_lock) _pending.Add((_phase, _counting, type.ToString(), message, main));
        }
    }

    /// <summary>開いている Scene の構成を退避し、空の Scene で始め、終わりに戻す。未保存の Scene があれば始めない。</summary>
    internal sealed class SceneGuard
    {
        private SceneSetup[] _setup = Array.Empty<SceneSetup>();

        public static SceneGuard Begin()
        {
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; the harness does not replace it.");
            var guard = new SceneGuard { _setup = EditorSceneManager.GetSceneManagerSetup() };
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return guard;
        }

        public void End(List<string> problems)
        {
            if (EditorApplication.isPlaying) { problems.Add("finished while still in Play; the scene setup was not restored"); return; }
            if (_setup.Length != 0 && _setup.All(x => File.Exists(Path.Combine(OverlayRun.RepoRoot, "unity", x.path))) && _setup.Any(x => x.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(_setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    /// <summary>process の前後の比較（AC10）。git status・設定の前後が違えば、fixture と生成 folder が残れば problems に足す。</summary>
    internal sealed class Aftercare
    {
        private string _git = "", _settings = "";
        private bool _gitRead;

        public static Aftercare Begin(OverlayRun run)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("The harness requires the active StandaloneWindows64 Player target.");
            var care = new Aftercare { _settings = Settings() };
            care._gitRead = Git.Read("status --porcelain", out care._git);
            // marker の無い fixture の folder があれば RemoveLeftovers が投げ、何も消さずに止まる。
            var removed = OverlayBuildFixture.RemoveLeftovers();
            run.Write("process-aftercare-begin", ("leftoverFixtureRemoved", removed), ("settings", care._settings), ("gitStatus", care._git), ("gitRead", care._gitRead));
            return care;
        }

        public static void End(OverlayRun run, Aftercare care, List<string> problems)
        {
            var fixture = OverlayBuildFixture.Exists;
            var generated = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")) || File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated.meta"));
            var gitRead = Git.Read("status --porcelain", out var git);
            var settings = Settings();
            if (fixture || generated) problems.Add("fixture or generated folder remains");
            // 取得に失敗した git status は前後が同じ失敗の文字列になりうるので、一致として読まない。
            if (!care._gitRead || !gitRead) problems.Add("git status could not be read");
            else if (git != care._git) problems.Add("git status changed during the process");
            if (settings != care._settings) problems.Add("settings changed during the process");
            run.Write("process-aftercare", ("fixtureFolderExists", fixture), ("generatedFolderExists", generated), ("gitStatus", git),
                ("settings", settings), ("problems", problems));
        }

        // Build Settings、active target、Enter Play Mode の設定、Play の初期 Scene。
        private static string Settings()
        {
            var start = EditorSceneManager.playModeStartScene;
            return EditorUserBuildSettings.activeBuildTarget + "/" + EditorUserBuildSettings.standaloneBuildSubtarget
                   + " scenes=" + string.Join(";", EditorBuildSettings.scenes.Select(x => x.path + ":" + x.enabled))
                   + " epm=" + EditorSettings.enterPlayModeOptionsEnabled + "/" + EditorSettings.enterPlayModeOptions
                   + " startScene=" + (start != null ? AssetDatabase.GetAssetPath(start) : "");
        }
    }

    internal static class Git
    {
        // 失敗した時は false を返し、text に失敗の理由を入れる（record に残すため）。
        public static bool Read(string arguments, out string text)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("git", arguments)
                    { WorkingDirectory = OverlayRun.RepoRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                if (process == null) { text = "unknown (git did not start)"; return false; }
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                text = process.ExitCode == 0 ? output : "unknown (exit " + process.ExitCode + ")";
                return process.ExitCode == 0;
            }
            catch (Exception ex) { text = "unknown (" + ex.Message + ")"; return false; }
        }
    }
}
