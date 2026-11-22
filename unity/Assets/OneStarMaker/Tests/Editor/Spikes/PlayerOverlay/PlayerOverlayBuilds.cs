#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;
using I = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayInventory;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// 第 1 process（HANDOFF AC1・AC9 の (1)）。fixture を v1 で作って base を build し、runner だけの Scene で Player を build し、source を
    /// v2 に変えて上書きを build する（順は base → Player → v2 → 上書きに固定。I-1 (f)）。3 つの出力の棚卸し、layout の写し、Player の
    /// build の設定と結果、P1〜P3 の材料、fixture の事実の file を書き、fixture を消して終わる。
    /// coroutine にしない [Test] で、設定の scope（開始から復元まで）を 1 つの同期の呼び出しの中に置く。通常の回帰に混ぜないので [Explicit]。
    /// test が失敗するのは harness 自身の不成立の時だけ（後始末の失敗、設定を戻せない）で、build の失敗や帰属の不一致は record の行（未判定の材料）
    /// にする。判定は判定 C が record から読む（HANDOFF §2）。製品への依存は BuildContentRoot（無改変）だけで、build は Unity の API を直接呼ぶ。
    /// </summary>
    public sealed class PlayerOverlayBuilds
    {
        public static readonly string[] Directories = { E.BaseDirectory, E.OverlayDirectory };

        [Test, Explicit, Timeout(7200000)]
        public void FirstProcess()
        {
            var run = PovRun.Create();
            var facts = new FirstFacts { RunId = run.RunId, Head = run.Head, ProjectVersion = PovRun.ProjectVersion(), EditorVersion = Application.unityVersion };
            var log = PovLog.Start(run);
            Exception? harness = null;
            PlayerOverlayProjectScope? scope = null;
            Aftercare? care = null;
            try
            {
                facts.TargetOk = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 &&
                                 EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Player;
                run.Write("target", ("activeBuildTarget", EditorUserBuildSettings.activeBuildTarget.ToString()), ("subtarget", EditorUserBuildSettings.standaloneBuildSubtarget.ToString()), ("ok", facts.TargetOk));
                // Active Build Target が違えば何も作らずに止まる（AC1。未判定）。
                if (!facts.TargetOk) return;
                care = Aftercare.Begin(run);
                facts.LibraryHadSpikeBuild = LibraryHistoryHasSpikeBuild(out var spikeBuilds);
                run.Write("library-history", ("hasSpikeBuild", facts.LibraryHadSpikeBuild), ("builds", spikeBuilds));

                var fixture = new PlayerOverlayFixture();
                fixture.CreateV1();
                log.Flush("fixture-v1");
                var built = new Dictionary<string, Built>();
                var b = BuildContent(run, E.BaseDirectory, CreateRoot(fixture, E.BaseDirectory), facts.Problems);
                if (b != null) built[E.BaseDirectory] = b;
                facts.BuildOrder.Add(E.BaseDirectory);
                log.Flush("build-base");
                var playerOk = false;
                if (b != null)
                {
                    playerOk = BuildPlayer(run, b.HistoryDirectory, facts, out scope);
                    facts.BuildOrder.Add("player");
                }
                log.Flush("build-player");
                fixture.ApplyV2();
                facts.BuildOrder.Add("v2");
                run.Write("source-v2", ("editedTexGuid", PlayerOverlayFixture.FixedGuid(PlayerOverlayFixture.EditedTex)), ("sceneEditGuid", PlayerOverlayFixture.FixedGuid(PlayerOverlayFixture.SceneEdit)));
                var o = BuildContent(run, E.OverlayDirectory, CreateRoot(fixture, E.OverlayDirectory), facts.Problems);
                if (o != null) built[E.OverlayDirectory] = o;
                facts.BuildOrder.Add(E.OverlayDirectory);
                log.Flush("build-overlay");
                facts.BuildsSucceeded = built.Count == Directories.Length && playerOk;
                if (built.Count == Directories.Length) Attribute(run, facts, built);
                facts.Digests = I.Digests(run.Directory);
                run.Write("digests", ("digests", facts.Digests));
                var p = PlayerOverlayVerdict.Preconditions(facts);
                run.Write("preconditions", ("hold", p.Count == 0), ("reasons", p));
            }
            catch (Exception ex) { harness = ex; run.Write("harness-exception", ("error", ex.ToString())); }
            finally
            {
                try
                {
                    if (care != null) Aftercare.End(run, care, facts.Problems);
                    if (scope != null && !scope.Restored) facts.Problems.Add("project settings were not restored");
                }
                catch (Exception ex) { harness ??= ex; }
                log.Stop();
                File.WriteAllText(Path.Combine(run.Directory, I.FixtureFactsFile), JsonUtility.ToJson(I.FixtureFacts(), true));
                File.WriteAllText(Path.Combine(run.Directory, I.FirstFactsFile), JsonUtility.ToJson(facts, true));
                File.WriteAllText(PovRun.FirstPointer, run.RunId);
                run.Write("process-end", ("facts", JsonUtility.ToJson(facts)));
            }
            Assert.That(harness, Is.Null, "first process harness failed: " + harness);
            Assert.That(scope == null || scope.Restored, Is.True, "project settings were not restored; see the record");
            Assert.That(facts.Problems.Where(x => x.StartsWith("aftercare", StringComparison.Ordinal)), Is.Empty, "aftercare failed; see the record");
        }

        // root は製品の BuildContentRoot を無改変で使う。base は 4 つ、上書きは SceneEdit と EditedTex だけ（HANDOFF §3）。
        private static string CreateRoot(PlayerOverlayFixture fixture, string d)
        {
            var entries = new List<BuildContentEntry>();
            if (d == E.BaseDirectory) entries.Add(SceneEntry(E.SceneKeep, PlayerOverlayFixture.SceneKeep));
            entries.Add(SceneEntry(E.SceneEdit, PlayerOverlayFixture.SceneEdit));
            if (d == E.BaseDirectory) entries.Add(ObjectEntry(E.SharedPrefab, PlayerOverlayFixture.SharedPrefab));
            entries.Add(ObjectEntry(E.EditedTex, PlayerOverlayFixture.EditedTex));
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(E.BuildName(d), "StandaloneWindows64-Player", entries);
            return fixture.AddRoot(d, root);
        }

        private static BuildContentEntry SceneEntry(string key, string path) =>
            new BuildContentEntry(key, path, "", LoadableSceneIdEditorUtility.CreateLoadableSceneId(path));

        private static BuildContentEntry ObjectEntry(string key, string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) throw new InvalidOperationException("missing " + path);
            return new BuildContentEntry(key, path, "", new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)));
        }

        private sealed class Built
        {
            public LayoutDocument Layout = new LayoutDocument();
            public OutputFile[] Files = Array.Empty<OutputFile>();
            public string HistoryDirectory = "";
        }

        // content の build（製品の UnityContentDirectoryAdapter と同じ引数）。失敗は record に残して null を返す（未判定の材料）。
        // layout と build report の directory は、BuildHistory の増分から BuildManifestHash で対応づける（S2-0b・S2-1 と同じ）。
        private static Built? BuildContent(PovRun run, string d, string rootPath, List<string> problems)
        {
            var output = Path.Combine(run.Directory, I.Folder(d));
            Directory.CreateDirectory(output);
            var history = PovRun.HistoryRoot;
            var known = Directory.Exists(history) ? new HashSet<string>(Directory.GetDirectories(history)) : new HashSet<string>();
            var watch = Stopwatch.StartNew();
            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                outputPath = output, rootAssetPaths = new[] { rootPath }, name = E.BuildName(d),
                compression = BuildCompression.Uncompressed, options = BuildContentOptions.None, extraScriptingDefines = Array.Empty<string>(),
            });
            // BuildReport は UnityEngine.Object なので == null で判定する。
            var result = report == null ? "no report" : report.summary.result.ToString();
            run.Write("build-end", ("directory", d), ("result", result), ("elapsedMs", watch.ElapsedMilliseconds), ("options", "None, Uncompressed"), ("root", rootPath), ("output", output));
            if (result != BuildResult.Succeeded.ToString()) { problems.Add("build " + d + " failed: " + result); return null; }
            var pointer = File.ReadAllText(Path.Combine(output, "BuildManifestHash.txt")).Trim();
            var added = Directory.GetDirectories(history).Where(x => !known.Contains(x))
                .Where(x => File.Exists(Path.Combine(x, "ContentLayout.json")) && JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(Path.Combine(x, "ContentLayout.json"))).BuildManifestHash == pointer).ToArray();
            if (added.Length != 1) { problems.Add("layout of " + d + " cannot be matched (" + added.Length + ")"); return null; }
            var copy = Path.Combine(run.Directory, "layouts", d);
            Directory.CreateDirectory(copy);
            File.Copy(Path.Combine(added[0], "ContentLayout.json"), Path.Combine(copy, "ContentLayout.json"));
            var files = I.Inventory(output);
            run.Write("inventory", ("directory", d), ("files", I.Lines(files)), ("digest", I.Digest(files)), ("buildReportDirectory", added[0]),
                ("scriptsOnlyCache", File.Exists(Path.Combine(added[0], "ScriptsOnlyCache.yaml"))));
            return new Built { Layout = JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(Path.Combine(added[0], "ContentLayout.json"))), Files = files, HistoryDirectory = added[0] };
        }

        // Player の build（製品の UnityPlayerBuildAdapter と同じ引数。製品の code は呼ばない）。Scene は runner の Scene だけ、
        // previousBuildReportDirectories は base の build report の directory（BuildHistory の、ScriptsOnlyCache.yaml を持つ folder）だけ。
        // 設定の scope は、この同期の呼び出しの中で開いて閉じる（HANDOFF I-3 (b)）。
        private static bool BuildPlayer(PovRun run, string baseReportDirectory, FirstFacts facts, out PlayerOverlayProjectScope? scope)
        {
            var playerDirectory = Path.Combine(run.Directory, I.PlayerFolder);
            var exe = Path.Combine(playerDirectory, I.PlayerExeName);
            // IL2CPP の backup の folder が深いので、build の前に出力の path の長さの見積もりを残す（HANDOFF §3 の build の設定）。
            var deepest = Path.Combine(playerDirectory, "Pov_BackUpThisFolder_ButDontShipItWithYourGame", "il2cppOutput", "cpp", new string('x', 64) + ".cpp");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { PlayerOverlayFixture.BootScene }, locationPathName = exe, target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player, options = BuildOptions.DetailedBuildReport, extraScriptingDefines = Array.Empty<string>(),
            };
            object boxed = options;
            var property = typeof(BuildPlayerOptions).GetProperty("previousBuildReportDirectories", BindingFlags.Public | BindingFlags.Instance);
            var field = typeof(BuildPlayerOptions).GetField("previousBuildReportDirectories", BindingFlags.Public | BindingFlags.Instance);
            if (property != null) property.SetValue(boxed, new[] { baseReportDirectory });
            else if (field != null) field.SetValue(boxed, new[] { baseReportDirectory });
            else throw new MissingMemberException("BuildPlayerOptions.previousBuildReportDirectories is unavailable.");
            options = (BuildPlayerOptions)boxed;
            run.Write("player-build-start", ("target", options.target.ToString()), ("subtarget", "Player"), ("options", options.options.ToString()), ("scenes", options.scenes),
                ("previousBuildReportDirectories", new[] { baseReportDirectory }), ("extraScriptingDefines", options.extraScriptingDefines), ("exe", exe),
                ("exePathLength", exe.Length), ("deepestPathEstimate", deepest.Length));
            var watch = Stopwatch.StartNew();
            BuildReport? report;
            scope = new PlayerOverlayProjectScope();
            try { report = BuildPipeline.BuildPlayer(options); }
            finally { scope.Dispose(); }
            run.Write("project-scope", ("before", scope.Before), ("during", scope.During), ("after", scope.After), ("projectSettingsHashBefore", scope.ProjectHashBefore),
                ("projectSettingsHashAfter", scope.ProjectHashAfter), ("addressablesSettings", scope.AddressablesPath), ("addressablesHashBefore", scope.AddressablesHashBefore),
                ("addressablesHashAfter", scope.AddressablesHashAfter), ("restored", scope.Restored));
            var result = report == null ? "no report" : report.summary.result.ToString();
            run.Write("player-build-end", ("result", result), ("elapsedMs", watch.ElapsedMilliseconds), ("guid", report == null ? "" : report.summary.guid.ToString()),
                ("totalErrors", report == null ? -1 : report.summary.totalErrors));
            if (report == null || report.summary.result != BuildResult.Succeeded) { facts.Problems.Add("player build failed: " + result); return false; }
            facts.PlayerBuildGuid = report.summary.guid.ToString();

            // Player 本体に fixture の asset が入っていないこと（runner の Scene を除く。HANDOFF §2 の (d)、AC1）。
            var fixtureGuids = PlayerOverlayFixture.FixtureAssetGuids();
            var packs = report.packedAssets ?? Array.Empty<PackedAssets>();
            facts.PackedAssetsRead = packs.Length != 0;
            var packed = new HashSet<string>(packs.SelectMany(x => x.contents).Select(x => x.sourceAssetGUID.ToString()), StringComparer.OrdinalIgnoreCase);
            facts.PackedFixtureAssets = fixtureGuids.Where(x => packed.Contains(x.Key)).Select(x => x.Key + " " + x.Value).ToList();
            var dataDirectory = Path.Combine(playerDirectory, Path.GetFileNameWithoutExtension(I.PlayerExeName) + "_Data");
            facts.AddressablesDataInPlayer = Directory.Exists(Path.Combine(dataDirectory, "StreamingAssets", "aa"));
            facts.ScriptingAssemblies = ScriptingAssemblies(dataDirectory, report);
            facts.ProbeAssemblyInPlayer = facts.ScriptingAssemblies.Any(x => x.IndexOf(E.ProbeAssembly, StringComparison.Ordinal) >= 0);
            var files = I.Inventory(playerDirectory);
            run.Write("player-result", ("packs", packs.Select(x => x.shortPath)), ("packedFixtureAssets", facts.PackedFixtureAssets), ("fixtureAssets", fixtureGuids.Select(x => x.Key + " " + x.Value)),
                ("addressablesDataInPlayer", facts.AddressablesDataInPlayer), ("scriptingAssemblies", facts.ScriptingAssemblies), ("probeAssemblyInPlayer", facts.ProbeAssemblyInPlayer),
                ("reportFiles", report.GetFiles().Select(x => x.role + " " + x.path)), ("notShipped", I.NotShippedFolders(playerDirectory)));
            run.Write("inventory", ("directory", "player"), ("files", I.Lines(files)), ("digest", I.Digest(files)));
            return true;
        }

        // Player の scripting assembly の一覧。ScriptingAssemblies.json（Player の data の folder）があればその名前、無ければ build report の
        // managed の file。読み方は HANDOFF §4 の B 適応の範囲。
        private static List<string> ScriptingAssemblies(string dataDirectory, BuildReport report)
        {
            var json = Path.Combine(dataDirectory, "ScriptingAssemblies.json");
            if (File.Exists(json))
            {
                var names = Regex.Match(File.ReadAllText(json), "\"names\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
                if (names.Success) return Regex.Matches(names.Groups[1].Value, "\"([^\"]+)\"").Cast<Match>().Select(x => "ScriptingAssemblies.json: " + x.Groups[1].Value).ToList();
            }
            return report.GetFiles().Where(x => x.role.IndexOf("Managed", StringComparison.OrdinalIgnoreCase) >= 0).Select(x => "report: " + Path.GetFileName(x.path)).ToList();
        }

        // 第 1 process の build の前に、Library の build の履歴に spike の名前の build があるか（HANDOFF I-1 (i)。読むかどうかは未確認で、P4 に添える）。
        private static bool LibraryHistoryHasSpikeBuild(out List<string> builds)
        {
            builds = new List<string>();
            if (!Directory.Exists(PovRun.HistoryRoot)) return false;
            foreach (var d in Directory.GetDirectories(PovRun.HistoryRoot))
            {
                var summary = Path.Combine(d, "BuildReportSummary.json");
                if (!File.Exists(summary)) continue;
                var name = Regex.Match(File.ReadAllText(summary), "\"BuildName\"\\s*:\\s*\"([^\"]*)\"");
                if (name.Success && name.Groups[1].Value.StartsWith(E.BuildName(""), StringComparison.Ordinal)) builds.Add(Path.GetFileName(d) + " " + name.Groups[1].Value);
            }
            return builds.Count != 0;
        }

        // P1〜P3 の材料（S2-1 と同じ。帰属は固定 GUID で引き、名前で対応づけない）。
        private static void Attribute(PovRun run, FirstFacts facts, Dictionary<string, Built> built)
        {
            // P1: MonoScript をまとめた file。BuildContentRoot の script を含む serialized file として引く（どの build も使う script）。
            var probeRoot = ScriptableObject.CreateInstance<BuildContentRoot>();
            var rootScript = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(probeRoot));
            UnityEngine.Object.DestroyImmediate(probeRoot);
            var scriptFact = new FileFact { Item = "scripts" };
            foreach (var d in Directories)
            {
                var x = built[d];
                var candidates = x.Layout.SerializedFiles.Where(y => !y.IsBuiltIn && y.SourceAssets.Contains(rootScript)).ToArray();
                var file = candidates.Length == 1 ? I.FileOf(candidates[0], x.Files) : null;
                var sources = new List<string>();
                if (file != null)
                {
                    Set(scriptFact, d, candidates[0].ID, file);
                    // 一覧の path は、probe の script の .meta の GUID から引いた path と照合する（file 名では引かない。HANDOFF §2 の P1）。
                    foreach (var probe in PlayerOverlayFixture.ProbeGuids)
                        if (candidates[0].SourceAssets.Contains(AssetDatabase.GUIDToAssetPath(probe.Value))) sources.Add(probe.Key);
                }
                if (d == E.BaseDirectory) facts.ScriptSourcesBase = sources; else facts.ScriptSourcesOverlay = sources;
                run.Write("script-file", ("directory", d), ("rootScript", rootScript), ("candidates", candidates.Length), ("id", candidates.FirstOrDefault()?.ID),
                    ("file", file?.Name), ("sha256", file?.Sha256), ("sourceAssets", candidates.FirstOrDefault()?.SourceAssets), ("probes", sources),
                    ("probePaths", PlayerOverlayFixture.ProbeGuids.Select(y => y.Key + "=" + AssetDatabase.GUIDToAssetPath(y.Value))));
            }
            facts.ScriptFile = scriptFact;
            var items = new (string item, string path)[]
            {
                (E.SceneEdit, PlayerOverlayFixture.SceneEdit), (E.EditedTex, PlayerOverlayFixture.EditedTex), (E.SharedTex, PlayerOverlayFixture.SharedTex),
                (E.SharedMat, PlayerOverlayFixture.SharedMat), (E.SharedShader, PlayerOverlayFixture.SharedShader), (E.EditedMat, PlayerOverlayFixture.EditedMat),
            };
            foreach (var (item, path) in items)
            {
                var fact = new FileFact { Item = item };
                var guid = PlayerOverlayFixture.FixedGuid(path);
                foreach (var d in Directories)
                {
                    var x = built[d];
                    var detail = "guid does not resolve to the path";
                    var sf = AssetDatabase.GUIDToAssetPath(guid) == path ? I.Attribute(x.Layout, path, guid, out detail) : null;
                    var file = sf == null ? null : I.FileOf(sf, x.Files);
                    if (sf != null && file != null) Set(fact, d, sf.ID, file);
                    run.Write("attribution", ("directory", d), ("item", item), ("path", path), ("guid", guid), ("id", sf?.ID), ("file", file?.Name), ("sha256", file?.Sha256), ("detail", detail));
                }
                facts.Files.Add(fact);
            }
        }

        private static void Set(FileFact fact, string d, string id, OutputFile file)
        {
            if (d == E.BaseDirectory) { fact.FoundBase = true; fact.IdBase = id; fact.NameBase = file.Name; fact.ShaBase = file.Sha256; }
            else { fact.FoundOverlay = true; fact.IdOverlay = id; fact.NameOverlay = file.Name; fact.ShaOverlay = file.Sha256; }
        }
    }

    /// <summary>
    /// 1 回の実行の一時 root と、Editor の process が書く JSONL の record。root は `TestResults/pov/&lt;実行 id&gt;`（git 管理外）で、
    /// 他の実行の root に触れない。record は process ごとの file に分ける。Player の round の record は runner が書く（ここでは書かない）。
    /// </summary>
    internal sealed class PovRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string SpikeRoot => Path.Combine(RepoRoot, "TestResults", "pov");
        public static string FirstPointer => Path.Combine(SpikeRoot, "first-process.txt");
        public static string HistoryRoot => Path.Combine(RepoRoot, "unity", "Library", "BuildHistory");
        private const string MarkerName = "pov-run.marker";

        private PovRun(string runId, string process)
        {
            RunId = runId; Process = process;
            Directory = Path.Combine(SpikeRoot, runId);
            Head = Git.Read("rev-parse HEAD", out var head) ? head.Trim() : head;
            // 同じ process の名前を再び実行しても、前の record に追記しない（process ごとに別の file）。
            RecordPath = Path.Combine(Directory, "editor-" + process + "-" + DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture) + ".jsonl");
        }

        public string RunId { get; }
        public string Process { get; }
        public string Directory { get; }
        public string Head { get; }
        public string RecordPath { get; }

        // 実行 id は短くする（IL2CPP の backup の folder が深いので、path の長さを抑える。HANDOFF §3）。同じ id の root があれば止める。
        public static PovRun Create()
        {
            var run = new PovRun(DateTime.UtcNow.ToString("MMddHHmmss", CultureInfo.InvariantCulture), "first");
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            run.Write("scope", ("unityVersion", Application.unityVersion), ("projectVersion", ProjectVersion()));
            return run;
        }

        // 第 2 process と Editor の対照は、第 1 process が書いた実行 id の root を marker で確かめてから開く。
        public static PovRun Open(string process)
        {
            if (!File.Exists(FirstPointer)) throw new InvalidOperationException("FirstProcess has not written its pointer.");
            var runId = File.ReadAllText(FirstPointer).Trim();
            var run = new PovRun(runId, process);
            var marker = Path.Combine(run.Directory, MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != runId) throw new IOException("Run root has no marker: " + run.Directory);
            return run;
        }

        public static string ProjectVersion()
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot, "unity", "ProjectSettings", "ProjectVersion.txt"));
            var m = Regex.Match(text, @"m_EditorVersion:\s*(\S+)");
            return m.Success ? m.Groups[1].Value : "";
        }

        public void Write(string type, params (string key, object? value)[] fields)
        {
            var b = new StringBuilder("{");
            void Field(string key, object? value) { if (b.Length > 1) b.Append(','); b.Append(Json(key)).Append(':').Append(Json(value)); }
            Field("runId", RunId); Field("head", Head); Field("unity", Application.unityVersion);
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
                b.Append(c switch
                {
                    '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                    _ => c < 0x20 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString(),
                });
            return b.ToString();
        }
    }

    /// <summary>
    /// Editor の process の log の捕捉。Unity の log を test の失敗にせず（build が出す Error で test が打ち切られると後始末が走らない。
    /// S2-1 の判定 C 1 回目の実測）、種類を問わず発生順に unity-log の行として record に書く。購読と LogAssert は Stop で必ず戻す。
    /// </summary>
    internal sealed class PovLog
    {
        private readonly PovRun _run;
        private readonly bool _ignoreBefore;
        private readonly object _lock = new object();
        private readonly List<(string type, string message)> _pending = new List<(string, string)>();
        private bool _stopped;

        private PovLog(PovRun run) { _run = run; _ignoreBefore = LogAssert.ignoreFailingMessages; }

        public static PovLog Start(PovRun run)
        {
            var log = new PovLog(run);
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceivedThreaded += log.OnLog;
            return log;
        }

        public void Flush(string phase)
        {
            (string type, string message)[] logs;
            lock (_lock) { logs = _pending.ToArray(); _pending.Clear(); }
            foreach (var l in logs) _run.Write("unity-log", ("phase", phase), ("logType", l.type), ("message", l.message));
        }

        public void Stop()
        {
            if (_stopped) return;
            _stopped = true;
            Application.logMessageReceivedThreaded -= OnLog;
            Flush("end");
            LogAssert.ignoreFailingMessages = _ignoreBefore;
        }

        private void OnLog(string message, string stackTrace, LogType type) { lock (_lock) _pending.Add((type.ToString(), message)); }
    }

    /// <summary>process の前後の比較と fixture の後始末（AC9 の (1)）。git status が変われば problems に足す。</summary>
    internal sealed class Aftercare
    {
        private string _git = "";
        private bool _gitRead;

        public static Aftercare Begin(PovRun run)
        {
            var care = new Aftercare();
            care._gitRead = Git.Read("status --porcelain", out care._git);
            // marker の無い fixture の folder があれば RemoveLeftovers が投げ、何も消さずに止まる（I-3 (f)）。
            var removed = PlayerOverlayFixture.RemoveLeftovers();
            run.Write("process-start", ("leftoverFixtureRemoved", removed), ("gitStatus", care._git), ("gitRead", care._gitRead));
            return care;
        }

        public static void End(PovRun run, Aftercare care, List<string> problems)
        {
            try { PlayerOverlayFixture.Delete(); }
            catch (Exception ex) { problems.Add("aftercare: fixture delete failed: " + ex.Message); }
            var fixture = PlayerOverlayFixture.Exists;
            var generated = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")) || File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated.meta"));
            var gitRead = Git.Read("status --porcelain", out var git);
            if (fixture || generated) problems.Add("aftercare: fixture or generated folder remains");
            // 取得に失敗した git status は前後が同じ失敗の文字列になりうるので、一致として読まない（S2-0b と同じ）。
            if (!care._gitRead || !gitRead) problems.Add("aftercare: git status could not be read");
            else if (git != care._git) problems.Add("aftercare: git status changed during the process");
            run.Write("process-aftercare", ("fixtureFolderExists", fixture), ("generatedFolderExists", generated), ("gitStatus", git), ("problems", problems));
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
                    { WorkingDirectory = PovRun.RepoRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
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
