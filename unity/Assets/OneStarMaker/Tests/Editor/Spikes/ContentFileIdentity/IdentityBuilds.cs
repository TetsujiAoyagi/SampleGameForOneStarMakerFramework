#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.AssetManagement.Components;
using OneStarMaker.Runtime.BuildContent;
using OneStarMaker.Runtime.BuildContent.Distribution;
using OneStarMaker.Runtime.Rendering.Vfx;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Spikes.ContentFileIdentity
{
    /// <summary>
    /// build の行列（HANDOFF §3）を実行し、JSONL の record を書く実験 test。通常の回帰に混ぜないので [Explicit]。
    /// Phase C が FirstProcess と SecondProcess を別の Unity process で順に実行する。判定の結論（GO / NO-GO 等）は
    /// record の verdict 行に書き、test の合否にしない。test が失敗するのは harness が record を書けなかった時だけ。
    /// 製品への依存はこの file だけで（root の BuildContentRoot、fixture の prefab に付ける Runtime の component を含む）、
    /// BuildContentCoordinator と ContentTransportPublisher は無改変で呼ぶ。
    /// </summary>
    public sealed class IdentityBuilds
    {
        private const string Unloaded = "unknown";
        private static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string SpikeRoot => Path.Combine(RepoRoot, "TestResults", "cfi");
        private static string FirstPointer => Path.Combine(SpikeRoot, "first-process.txt");
        // coordinator は出力の root を artifacts/bs2b か TestResults/bs3-fixture の下に限る（AC2）。
        private static string CoordinatorRoot => Path.Combine(RepoRoot, "TestResults", "bs3-fixture", "cfi");

        [Serializable] private sealed class ProcessState
        {
            public string RunId = "";
            public string[] Undecided = Array.Empty<string>();
            public SourceRecord[] Sources = Array.Empty<SourceRecord>();
            public string[] Builds = Array.Empty<string>();
            public string TransportCheck = "";
        }

        [Serializable] private sealed class BuildEnd
        {
            public string Build = "";
            public bool Succeeded;
            public string Options = "";
            public long ElapsedMs;
            public string UnityTotalTimeMs = "";
            public string OutputPath = "";
            public string LayoutCopy = "";
            public string LayoutMatch = "";
            public bool SourcesStable;
            public string[] CacheLogLines = Array.Empty<string>();
        }

        [Serializable] private sealed class Aftercare
        {
            public bool LeftoverRemoved;
            public bool FixtureFolderExists;
            public bool GeneratedFolderExists;
            public string GitStatusBefore = "";
            public string GitStatusAfter = "";
            public bool GitStatusBeforeRead;
            public bool GitStatusAfterRead;
            public string SettingsBefore = "";
            public string SettingsAfter = "";
        }

        [Serializable] private sealed class Numbers
        {
            public string[] BuildBytes = Array.Empty<string>();
            public long SumAB, UnionAB, SavedByHashAB, OneBuildAB, OneBuildMinusUnion;
            public string[] DifferedByKind = Array.Empty<string>();
            public string[] RedistributedByRound = Array.Empty<string>();
            public string TransportCheck = "";
            public string Note = "Bytes are fixture ratios, not representative of a real project.";
        }

        [Serializable] private sealed class Header
        {
            public string runId = "", head = "", unity = "", target = "", process = "", type = "", at = "";
        }

        [Serializable] private sealed class TextList { public string[] Items = Array.Empty<string>(); }

        private sealed class Run
        {
            public Run(string runId, string process)
            {
                RunId = runId; Process = process;
                Directory = Path.Combine(SpikeRoot, runId);
                System.IO.Directory.CreateDirectory(Path.Combine(Directory, "inventories"));
                Head = Git("rev-parse HEAD").Trim();
            }
            public string RunId { get; }
            public string Process { get; }
            public string Directory { get; }
            public string Head { get; }
            public List<string> Undecided { get; } = new List<string>();
            // build ごとの、BuildLog から抜き出した cache の行の数（AC2 の CleanBuildCache の材料）。
            public Dictionary<string, int> CacheLines { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
            // component は SharedPrefab と OnlyB で別の既存の Runtime 型。どちらも直列化する値を持たない（HANDOFF §6 の B 適応 2）。
            public IdentityFixture Fixture { get; } = new IdentityFixture(typeof(AssetReleaseOnDestroy), typeof(VfxOwnerDestroyNotifier));

            // 1 行 = header と JsonUtility の payload。process の別と head を全行に持たせる。
            public void Write(string type, object payload)
            {
                var header = JsonUtility.ToJson(new Header { runId = RunId, head = Head, unity = Application.unityVersion,
                    target = EditorUserBuildSettings.activeBuildTarget + "/" + EditorUserBuildSettings.standaloneBuildSubtarget,
                    process = Process, type = type, at = DateTime.UtcNow.ToString("o") });
                File.AppendAllText(Path.Combine(Directory, "record.jsonl"),
                    "{\"header\":" + header + ",\"payload\":" + JsonUtility.ToJson(payload) + "}\n");
            }
        }

        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator FirstProcess()
        {
            var run = new Run(DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"), "first");
            var care = Begin(run);
            var state = new ProcessState { RunId = run.RunId };
            var builds = new Dictionary<string, BuildInventory>(StringComparer.Ordinal);
            try
            {
                var f = run.Fixture;
                f.Create();
                run.Write("import-hashes", new TextList { Items = f.ImportHashes() });
                var sources = f.CaptureSources();
                var a = new[] { f.SceneA, f.SharedPrefab };
                var b = new[] { f.SceneB, f.SharedPrefab, f.OnlyB };
                var roots = new Dictionary<string, string>
                {
                    ["S"] = CreateRoot(f, "S", "cfi-S", new[] { f.SharedPrefab }),
                    ["A"] = CreateRoot(f, "A", "cfi-A", a),
                    ["A-name"] = CreateRoot(f, "A-name", "cfi-A-name", a),
                    ["B"] = CreateRoot(f, "B", "cfi-B", b),
                    ["AB"] = CreateRoot(f, "AB", "cfi-AB", a.Concat(b).Distinct()),
                };
                // 第 2 process はこの一覧（root を含む）と照合する（AC2）。
                state.Sources = f.CaptureSources();
                run.Write("sources", new TextList { Items = state.Sources.Select(x => x.Path + " " + x.Sha256 + " " + x.Guid).ToArray() });
                foreach (var (build, root, name) in new[] { ("S", "S", "cfi-S"), ("A", "A", "cfi-A"), ("A-same", "A", "cfi-A"),
                             ("A-name", "A-name", "cfi-A-name"), ("B", "B", "cfi-B"), ("AB", "AB", "cfi-AB") })
                {
                    builds[build] = Direct(run, build, roots[root], name, BuildContentOptions.None);
                    yield return null;
                }
                var p1 = Coordinated(run, "P1", a, builds);
                yield return null;
                var p2 = Coordinated(run, "P2", a, builds);
                state.TransportCheck = TransportCheck(run, p1, p2, builds);
                yield return null;

                // AC5: 各 round は最初の A（B-tex は B）と比べる。変更の後に必ず作成直後の bytes へ戻す。
                var tex = f.ChangeTexture();
                builds["A-tex"] = Direct(run, "A-tex", roots["A"], "cfi-A", BuildContentOptions.None);
                builds["B-tex"] = Direct(run, "B-tex", roots["B"], "cfi-B", BuildContentOptions.None);
                f.Restore(tex);
                yield return null;
                var mat = f.ChangeMaterial();
                builds["A-mat"] = Direct(run, "A-mat", roots["A"], "cfi-A", BuildContentOptions.None);
                f.Restore(mat);
                yield return null;
                var prefab = f.ChangePrefab();
                builds["A-prefab"] = Direct(run, "A-prefab", roots["A"], "cfi-A", BuildContentOptions.None);
                f.Restore(prefab);
                var restored = f.CaptureSources().Where(x => sources.Any(y => y.Path == x.Path)).ToArray();
                if (!Same(restored, sources)) run.Undecided.Add("AC5: sources after restore differ from the first A");
                builds["A-back"] = Direct(run, "A-back", roots["A"], "cfi-A", BuildContentOptions.None);
            }
            finally
            {
                End(run, care);
                state.Undecided = run.Undecided.ToArray();
                state.Builds = builds.Keys.ToArray();
                File.WriteAllText(Path.Combine(run.Directory, "first-state.json"), JsonUtility.ToJson(state, true));
                File.WriteAllText(FirstPointer, run.RunId);
                run.Write("process-end", state);
            }
        }

        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator SecondProcess()
        {
            if (!File.Exists(FirstPointer)) throw new InvalidOperationException("FirstProcess has not written its record.");
            var runId = File.ReadAllText(FirstPointer).Trim();
            var run = new Run(runId, "second");
            var first = JsonUtility.FromJson<ProcessState>(File.ReadAllText(Path.Combine(run.Directory, "first-state.json")));
            var care = Begin(run);
            var builds = first.Builds.ToDictionary(x => x, x => JsonUtility.FromJson<BuildInventory>(
                File.ReadAllText(Path.Combine(run.Directory, "inventories", x + ".json"))), StringComparer.Ordinal);
            try
            {
                var f = run.Fixture;
                f.Create();
                run.Write("import-hashes", new TextList { Items = f.ImportHashes() });
                var rootA = CreateRoot(f, "A", "cfi-A", new[] { f.SceneA, f.SharedPrefab });
                var rootB = CreateRoot(f, "B", "cfi-B", new[] { f.SceneB, f.SharedPrefab, f.OnlyB });
                // 第 2 process は root を A と B の分だけ作る。作った全 file が第 1 process と同じ bytes と GUID で、
                // 第 1 process の root 以外の file が全てあることを確かめる。
                var sources = f.CaptureSources();
                run.Write("sources", new TextList { Items = sources.Select(x => x.Path + " " + x.Sha256 + " " + x.Guid).ToArray() });
                var mismatched = sources.Where(x => !first.Sources.Any(y => y.Path == x.Path && y.Sha256 == x.Sha256 && y.Guid == x.Guid))
                    .Select(x => x.Path)
                    .Concat(first.Sources.Where(x => !x.Path.Contains("/Root_") && sources.All(y => y.Path != x.Path)).Select(x => "missing " + x.Path))
                    .ToArray();
                if (mismatched.Length != 0)
                    run.Undecided.Add("AC2: second-process sources differ from the first process: " + string.Join(", ", mismatched));
                builds["A-clean"] = Direct(run, "A-clean", rootA, "cfi-A", BuildContentOptions.CleanBuildCache);
                yield return null;
                builds["B-clean"] = Direct(run, "B-clean", rootB, "cfi-B", BuildContentOptions.CleanBuildCache);
            }
            finally
            {
                End(run, care);
                Conclude(run, first, builds);
            }
        }

        private static void Conclude(Run run, ProcessState first, Dictionary<string, BuildInventory> builds)
        {
            var classifier = run.Fixture.Classifier;
            var e = new Evidence();
            foreach (var pair in builds) e.Builds[pair.Key] = pair.Value;
            e.HarnessUndecided.AddRange(first.Undecided);
            e.HarnessUndecided.AddRange(run.Undecided);
            foreach (var x in new[] { ("A", "A-same", false), ("A", "A-name", false), ("A", "A-clean", true), ("B", "B-clean", true), ("P1", "P2", false) })
                e.SameSourcePairs.Add(x);
            foreach (var x in new[] { ("S", "A"), ("S", "B"), ("S", "AB"), ("A", "B"), ("A", "AB"), ("B", "AB") }) e.RootSetPairs.Add(x);
            e.Rounds.Add(new Round("A", "A-tex", new[] { classifier.SharedTex }, false));
            e.Rounds.Add(new Round("B", "B-tex", new[] { classifier.SharedTex }, false));
            e.Rounds.Add(new Round("A", "A-mat", new[] { classifier.SharedMat }, false));
            e.Rounds.Add(new Round("A", "A-prefab", new[] { classifier.SharedPrefab }, false));
            e.Rounds.Add(new Round("A", "A-back", Array.Empty<string>(), true));

            // 全ての名指しの対の 2 軸を record に残す（判定 C は棚卸しの行から独立に確かめる）。観測の対も同じ形で書く。
            void Pair(string kind, string a, string b, string[]? changed = null)
            {
                if (builds.ContainsKey(a) && builds.ContainsKey(b)) run.Write(kind, FileInventory.Compare(builds[a], builds[b], changed));
            }
            foreach (var (a, b, _) in e.SameSourcePairs) Pair("compare-ac2", a, b);
            foreach (var (a, b) in e.RootSetPairs) Pair("compare-ac3", a, b);
            foreach (var r in e.Rounds) Pair("compare-ac5", r.Baseline, r.Build, r.ChangedSources);
            Pair("observe-ac2-P1-A", "P1", "A");
            Pair("observe-ac3-clean", "A-clean", "B-clean");
            if (builds.ContainsKey("S"))
                foreach (var other in new[] { "A", "B", "AB" }.Where(builds.ContainsKey))
                    run.Write("absent-S-" + other, new TextList { Items = FileInventory.Absent(builds["S"], builds[other]) });
            run.Write("observe-ac1-name-sha", new TextList { Items = FileInventory.NameShaBreaks(builds.Values) });

            if (builds.TryGetValue("S", out var s) && !s.Files.Any(x => x.Name.EndsWith(".resS", StringComparison.Ordinal)))
                e.HarnessUndecided.Add("AC1: build S has no .resS");
            var numbers = Count(builds, e, first.TransportCheck);
            if (numbers == null) e.HarnessUndecided.Add("AC6: numbers are missing");
            else run.Write("numbers", numbers);

            var verdict = Judgement.Judge(e, classifier);
            var notes = verdict.Notes.ToList();
            notes.Add("Scope: this PC, Unity " + Application.unityVersion + ", StandaloneWindows64, loose files, fixture only.");
            if (FileInventory.NameShaBreaks(builds.Values).Length != 0) notes.Add("Files can be merged by sha256 only, not by name.");
            if (new[] { "A-clean", "B-clean" }.Any(x => !run.CacheLines.TryGetValue(x, out var count) || count == 0))
                notes.Add("Incremental cache involvement is unknown (no cache line in the CleanBuildCache build logs).");
            if (new[] { "A", "B", "A-clean", "B-clean" }.All(builds.ContainsKey))
            {
                static string Findings(PairResult c) => string.Join(";", c.Differed.Select(x => x.KindLabel + ":" + x.Id).Concat(c.Moved.Select(x => x.Kind + ":" + x.SourceAsset)).OrderBy(x => x));
                var warm = Findings(FileInventory.Compare(builds["A"], builds["B"]));
                var clean = Findings(FileInventory.Compare(builds["A-clean"], builds["B-clean"]));
                if (warm != clean) notes.Add("A-B and A-clean-B-clean disagree: [" + warm + "] vs [" + clean + "]");
            }
            notes.Add("Bytes are fixture ratios, not representative of a real project.");
            verdict.Notes = notes.ToArray();
            run.Write("verdict", verdict);
        }

        private static Numbers? Count(Dictionary<string, BuildInventory> builds, Evidence e, string transport)
        {
            if (!new[] { "A", "B", "AB" }.All(builds.ContainsKey)) return null;
            static IEnumerable<(string, long)> Bytes(BuildInventory b) => b.Files.Select(x => (x.Sha256, x.Size));
            var n = new Numbers
            {
                BuildBytes = builds.Values.OrderBy(x => x.Build, StringComparer.Ordinal).Select(x => x.Build + "=" + x.TotalBytes).ToArray(),
                SumAB = builds["A"].TotalBytes + builds["B"].TotalBytes,
                UnionAB = FileInventory.DistinctBytes(Bytes(builds["A"]).Concat(Bytes(builds["B"]))),
                OneBuildAB = builds["AB"].TotalBytes,
                TransportCheck = transport,
            };
            n.SavedByHashAB = n.SumAB - n.UnionAB;
            n.OneBuildMinusUnion = n.OneBuildAB - n.UnionAB;
            n.DifferedByKind = e.RootSetPairs.Where(x => builds.ContainsKey(x.a) && builds.ContainsKey(x.b))
                .SelectMany(x => FileInventory.Compare(builds[x.a], builds[x.b]).Differed)
                .GroupBy(x => x.KindLabel).Select(g => g.Key + ": count=" + g.Count() + " bytes=" + g.Sum(x => x.SizeB)).ToArray();
            n.RedistributedByRound = e.Rounds.Where(r => builds.ContainsKey(r.Baseline) && builds.ContainsKey(r.Build))
                .Select(r => r.Build + "=" + FileInventory.RedistributedBytes(builds[r.Baseline], builds[r.Build])).ToArray();
            return n;
        }

        private static Aftercare Begin(Run run)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("The spike requires the active StandaloneWindows64 Player target.");
            var care = new Aftercare { SettingsBefore = Settings() };
            care.GitStatusBeforeRead = TryGit("status --porcelain", out care.GitStatusBefore);
            care.LeftoverRemoved = IdentityFixture.RemoveLeftovers();
            run.Write("process-start", care);
            return care;
        }

        // AC7: process の終わりに fixture を消し、git とは別に file system を直接見る。設定と git status を開始時と比べる。
        private static void End(Run run, Aftercare care)
        {
            try { IdentityFixture.Delete(); }
            catch (Exception ex) { run.Undecided.Add("AC7: fixture delete failed: " + ex.Message); }
            care.FixtureFolderExists = IdentityFixture.Exists;
            care.GeneratedFolderExists = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated"))
                                         || File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated.meta"));
            care.GitStatusAfterRead = TryGit("status --porcelain", out care.GitStatusAfter);
            care.SettingsAfter = Settings();
            if (care.FixtureFolderExists || care.GeneratedFolderExists) run.Undecided.Add("AC7: generated folders remain");
            // 取得に失敗した git status は前後が同じ失敗の文字列になりうるので、一致として読まずに未判定にする。
            if (!care.GitStatusBeforeRead || !care.GitStatusAfterRead) run.Undecided.Add("AC7: git status could not be read");
            else if (care.GitStatusAfter != care.GitStatusBefore) run.Undecided.Add("AC7: git status changed during the process");
            if (care.SettingsAfter != care.SettingsBefore) run.Undecided.Add("AC7: build settings, target or graphics settings changed");
            run.Write("process-aftercare", care);
        }

        private static string Settings()
        {
            string Hash(string relative)
            {
                var path = Path.Combine(RepoRoot, "unity", "ProjectSettings", relative);
                return File.Exists(path) ? IdentityFixture.Sha256(File.ReadAllBytes(path)) : "missing";
            }
            return EditorUserBuildSettings.activeBuildTarget + "/" + EditorUserBuildSettings.standaloneBuildSubtarget
                   + " scenes=" + string.Join(";", EditorBuildSettings.scenes.Select(x => x.path + ":" + x.enabled))
                   + " EditorBuildSettings=" + Hash("EditorBuildSettings.asset") + " GraphicsSettings=" + Hash("GraphicsSettings.asset");
        }

        // build を直接呼ぶ経路。root は fixture の BuildContentRoot、出力先は build ごとに別。
        private static BuildInventory Direct(Run run, string build, string rootPath, string name, BuildContentOptions options)
        {
            var output = Path.Combine(run.Directory, "out", build);
            if (Directory.Exists(output)) throw new IOException("Output already exists: " + output);
            Directory.CreateDirectory(output);
            return Measure(run, build, options.ToString(), () =>
            {
                var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
                {
                    outputPath = output, rootAssetPaths = new[] { rootPath }, name = name,
                    compression = BuildCompression.Uncompressed, options = options, extraScriptingDefines = Array.Empty<string>(),
                });
                // BuildReport は UnityEngine.Object なので、null の判定は == null で行う（?. は破棄済みを見落とす）。
                if (report == null)
                    throw new InvalidOperationException("Content build " + build + " failed: no build report");
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Content build " + build + " failed: " + report.summary.result);
                return output;
            });
        }

        // 製品の公開の入口（無改変）。既存の BuildContentDirectoryIntegrationTests と同じく plan と snapshot を合成する。
        // root は製品の BuildContentRoot を無改変で使う。Scene は LoadableSceneId、Object は Loadable<Object> で持つ（HANDOFF §3）。
        // fixture は作った root を他の asset と同じく固定 GUID で置く。
        private static string CreateRoot(IdentityFixture fixture, string build, string identity, IEnumerable<string> members)
        {
            var entries = new List<BuildContentEntry>();
            foreach (var path in members)
            {
                if (path.EndsWith(".unity", StringComparison.Ordinal))
                    entries.Add(new BuildContentEntry(path, path, "", LoadableSceneIdEditorUtility.CreateLoadableSceneId(path)));
                else
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null) throw new InvalidOperationException("missing " + path);
                    entries.Add(new BuildContentEntry(path, path, "",
                        new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset))));
                }
            }
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(identity, "StandaloneWindows64-Player", entries);
            return fixture.AddRoot(build, root);
        }

        private static BuildContentResult Coordinated(Run run, string build, string[] members, Dictionary<string, BuildInventory> builds)
        {
            BuildContentResult? result = null;
            builds[build] = Measure(run, build, "coordinator Full", () =>
            {
                var candidates = members.Select((p, i) => new BuildContentCandidate("cfi-" + i, "cfi-logical-" + i,
                    AssetDatabase.AssetPathToGUID(p), new BuildProvenance("cfi", p))).ToArray();
                var closures = members.Select((p, i) => new BuildDependencySnapshot(candidates[i].PhysicalKey, p,
                    AssetDatabase.GetDependencies(p).Where(x => x.StartsWith("Assets/", StringComparison.Ordinal))
                        .Select(x => new BuildDependencyEntry(AssetDatabase.AssetPathToGUID(x), x)))).ToArray();
                var snapshot = new BuildMaterializationSnapshot(candidates, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                    Array.Empty<BuildContentRequirement>(), closures);
                var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates, Array.Empty<BuildContentExclusion>());
                result = new BuildContentCoordinator().Build(new BuildContentRequest(plan, snapshot, "cfi", CoordinatorRoot, null, null, BuildContentMode.Full));
                if (!result.IsSuccess) throw new InvalidOperationException("Coordinator build " + build + " failed: " + result.Summary);
                return result.ContentPath!;
            });
            return result!;
        }

        // AC6: 製品の transport manifest の files の sha256 だけで P1・P2 の和集合を数え、棚卸しの和集合と比べる（合否にしない）。
        private static string TransportCheck(Run run, BuildContentResult p1, BuildContentResult p2, Dictionary<string, BuildInventory> builds)
        {
            try
            {
                var files = new[] { p1, p2 }.SelectMany(p =>
                {
                    var published = ContentTransportPublisher.Publish(p, Path.Combine(CoordinatorRoot, "pub", run.RunId, p == p1 ? "P1" : "P2"));
                    var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(published, "transport.json")));
                    return manifest.files.Select(x => (x.sha256, x.size));
                }).ToArray();
                var viaManifest = FileInventory.DistinctBytes(files);
                var viaInventory = FileInventory.DistinctBytes(builds["P1"].Files.Concat(builds["P2"].Files).Select(x => (x.Sha256, x.Size)));
                var check = "transport union=" + viaManifest + " inventory union=" + viaInventory + (viaManifest == viaInventory ? " (same)" : " (differs)");
                run.Write("transport", new TextList { Items = new[] { check } });
                return check;
            }
            catch (Exception ex)
            {
                var check = "product manifest check not performed: " + ex.Message;
                run.Write("transport", new TextList { Items = new[] { check } });
                return check;
            }
        }

        // build 1 件の共通の手順: source を前後で採り、BuildHistory の増分から layout を対応づけて写し、棚卸しを作る。
        // 終了行（build-end）は成功した時だけ書く。行が無い build は未判定（HANDOFF §2.2）。
        private static BuildInventory Measure(Run run, string build, string options, Func<string> execute)
        {
            var before = run.Fixture.CaptureSources();
            var history = Path.Combine(RepoRoot, "unity", "Library", "BuildHistory");
            var known = Directory.Exists(history) ? new HashSet<string>(Directory.GetDirectories(history)) : new HashSet<string>();
            var watch = Stopwatch.StartNew();
            var output = execute();
            watch.Stop();
            var after = run.Fixture.CaptureSources();
            var end = new BuildEnd { Build = build, Succeeded = true, Options = options, ElapsedMs = watch.ElapsedMilliseconds,
                OutputPath = output, SourcesStable = Same(before, after) };
            if (!end.SourcesStable) run.Undecided.Add("AC1: sources moved during build " + build);
            // AC1 の「直前と直後に採り、両者が一致する」を record から確かめられるよう、両方の一覧を書く。
            run.Write("sources-before-" + build, new TextList { Items = before.Select(x => x.Path + " " + x.Sha256 + " " + x.Guid).ToArray() });
            run.Write("sources-after-" + build, new TextList { Items = after.Select(x => x.Path + " " + x.Sha256 + " " + x.Guid).ToArray() });

            var pointer = File.ReadAllText(Path.Combine(output, FileInventory.PointerName)).Trim();
            var added = Directory.GetDirectories(history).Where(x => !known.Contains(x)).ToArray();
            var candidates = (added.Length == 1 ? added : Directory.GetDirectories(history))
                .Select(x => Path.Combine(x, "ContentLayout.json")).Where(File.Exists)
                .Where(x => JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(x)).BuildManifestHash == pointer).ToArray();
            LayoutDocument? layout = null;
            if (candidates.Length == 0 || candidates.Select(x => IdentityFixture.Sha256(File.ReadAllBytes(x))).Distinct().Count() != 1)
            {
                end.LayoutMatch = "unmatched (added=" + added.Length + ", candidates=" + candidates.Length + ")";
                run.Undecided.Add("AC4: layout of build " + build + " cannot be matched: " + end.LayoutMatch);
            }
            else
            {
                end.LayoutMatch = added.Length == 1 ? "one added entry" : "matched by hash among " + candidates.Length + " identical layouts";
                var source = Path.GetDirectoryName(candidates[0])!;
                var copy = Path.Combine(run.Directory, "layouts", build);
                Directory.CreateDirectory(copy);
                foreach (var file in new[] { "ContentLayout.json", "BuildLog.jsonl", "BuildReportSummary.json" }.Where(x => File.Exists(Path.Combine(source, x))))
                    File.Copy(Path.Combine(source, file), Path.Combine(copy, file));
                end.LayoutCopy = copy;
                layout = JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(Path.Combine(copy, "ContentLayout.json")));
                var summary = Path.Combine(copy, "BuildReportSummary.json");
                if (File.Exists(summary)) end.UnityTotalTimeMs = JsonUtility.FromJson<Summary>(File.ReadAllText(summary)).TotalTimeMs.ToString();
                // 増分 cache の関与の材料。log の形は未確認なので抜き出すだけで合否にしない。
                var log = Path.Combine(copy, "BuildLog.jsonl");
                if (File.Exists(log))
                    end.CacheLogLines = File.ReadLines(log).Where(x => x.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0).Take(50).ToArray();
                run.CacheLines[build] = end.CacheLogLines.Length;
            }

            // 棚卸しの対象は出力 directory の直下の全 file。下位 directory は未知の file として名前を残す。
            var files = Directory.GetFiles(output).Select(x => new OutputFile { Name = Path.GetFileName(x), Size = new FileInfo(x).Length,
                    Sha256 = IdentityFixture.Sha256(File.ReadAllBytes(x)) })
                .Concat(Directory.GetDirectories(output).Select(x => new OutputFile { Name = "directory:" + Path.GetFileName(x), Sha256 = "" }))
                .ToArray();
            var inventory = FileInventory.Read(build, layout, files, pointer, run.Fixture.Classifier);
            File.WriteAllText(Path.Combine(run.Directory, "inventories", build + ".json"), JsonUtility.ToJson(inventory, true));
            run.Write("inventory", inventory);
            run.Write("build-end", end);
            return inventory;
        }

        [Serializable] private sealed class Summary { public long TotalTimeMs; }

        private static bool Same(SourceRecord[] a, SourceRecord[] b) =>
            a.Length == b.Length && a.Zip(b, (x, y) => x.Path == y.Path && x.Sha256 == y.Sha256 && x.Guid == y.Guid).All(x => x);

        private static string Git(string arguments)
        {
            TryGit(arguments, out var text);
            return text;
        }

        // 失敗した時は false を返し、text に失敗の理由を入れる（record に残すため）。
        private static bool TryGit(string arguments, out string text)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo("git", arguments)
                    { WorkingDirectory = RepoRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                if (process == null) { text = Unloaded + " (git did not start)"; return false; }
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0) { text = output; return true; }
                text = Unloaded + " (exit " + process.ExitCode + ")";
                return false;
            }
            catch (Exception ex) { text = Unloaded + " (" + ex.Message + ")"; return false; }
        }
    }
}
