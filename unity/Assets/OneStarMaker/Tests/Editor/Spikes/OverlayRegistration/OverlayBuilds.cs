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
using OneStarMaker.Runtime.BuildContent;
using Unity.Loading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayRegistration
{
    /// <summary>
    /// 第 1 process（HANDOFF §2.2 AC1・AC8）。fixture を v1 で作って base を build し、source を v2 に変えて上書きを build し、
    /// 両方の出力の全 file の名前・size・sha256 と ContentLayout.json の写しを残し、P1〜P3 の材料（<see cref="FirstFacts"/>）を
    /// record に書き、fixture を消して終わる。通常の回帰に混ぜないので [Explicit]。test が失敗するのは harness 自身の例外の時だけで、
    /// build の失敗や帰属の不一致は record の行（未判定の材料）にする。判定は判定 C が record から読む（HANDOFF §2）。
    /// 製品への依存は BuildContentRoot（無改変）だけで、build は BuildPipeline.BuildContentDirectory を直接呼ぶ（S2-0b と同じ）。
    /// </summary>
    public sealed class OverlayBuilds
    {
        public static readonly string[] Directories = { OverlayVerdict.Base, OverlayVerdict.Overlay };
        public static string BuildName(string directory) => "ovl-" + directory;

        // probe は UNITY_INCLUDE_TESTS を制約に持つ OneStarMaker.TestSupport にあるので、player の script の compile にこの define を渡す
        // （HANDOFF §4 の B 適応。harness が直接呼ぶ build の引数で、製品の build の経路は変えない）。
        private static readonly string[] ScriptingDefines = { "UNITY_INCLUDE_TESTS" };

        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator FirstProcess()
        {
            var run = SpikeRun.Create();
            run.WriteScope();
            var facts = new FirstFacts { RunId = run.RunId, Head = run.Head };
            var care = Aftercare.Begin(run);
            Exception? harness = null;
            try
            {
                var fixture = new OverlayFixture();
                fixture.CreateV1();
                var built = new Dictionary<string, (LayoutDocument layout, OutputFile[] files)>();
                var baseRoot = CreateRoot(fixture, OverlayVerdict.Base);
                var b = Build(run, OverlayVerdict.Base, baseRoot, facts.Problems);
                if (b != null) built[OverlayVerdict.Base] = b.Value;
                yield return null;
                fixture.ApplyV2();
                run.Write("source-v2", ("editedTexGuid", OverlayFixture.FixedGuid(OverlayFixture.EditedTex)), ("sceneEditGuid", OverlayFixture.FixedGuid(OverlayFixture.SceneEdit)));
                var overlayRoot = CreateRoot(fixture, OverlayVerdict.Overlay);
                var o = Build(run, OverlayVerdict.Overlay, overlayRoot, facts.Problems);
                if (o != null) built[OverlayVerdict.Overlay] = o.Value;
                yield return null;
                facts.BuildsSucceeded = built.Count == Directories.Length;
                if (facts.BuildsSucceeded) Attribute(run, facts, built);
                var p = OverlayVerdict.Preconditions(facts);
                run.Write("preconditions", ("hold", p.Count == 0), ("reasons", p));
            }
            finally
            {
                try { Aftercare.End(run, care, facts.Problems, true); }
                catch (Exception ex) { harness = ex; }
                File.WriteAllText(Path.Combine(run.Directory, "first-facts.json"), JsonUtility.ToJson(facts, true));
                File.WriteAllText(SpikeRun.FirstPointer, run.RunId);
                run.Write("process-end", ("facts", JsonUtility.ToJson(facts)));
            }
            Assert.That(harness, Is.Null, "first process aftercare failed: " + harness);
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        internal static string Output(SpikeRun run, string directory) => Path.Combine(run.Directory, "out", directory);

        // root は製品の BuildContentRoot を無改変で使う。Scene は LoadableSceneId（製品と同じく path から作る）、Object は Loadable<Object>。
        // logical key は物の名前で、両方の root で同じにする。base は 4 つ、上書きは SceneEdit と EditedTex だけ（HANDOFF §3）。
        private static string CreateRoot(OverlayFixture fixture, string d)
        {
            var entries = new List<BuildContentEntry>();
            if (d == OverlayVerdict.Base) entries.Add(SceneEntry(OverlayExpected.SceneKeep, OverlayFixture.SceneKeep));
            entries.Add(SceneEntry(OverlayExpected.SceneEdit, OverlayFixture.SceneEdit));
            if (d == OverlayVerdict.Base) entries.Add(ObjectEntry(OverlayExpected.SharedPrefab, OverlayFixture.SharedPrefab));
            entries.Add(ObjectEntry(OverlayExpected.EditedTex, OverlayFixture.EditedTex));
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(BuildName(d), "StandaloneWindows64-Player", entries);
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

        // build の失敗は record に残して null を返す（未判定の材料）。layout は BuildHistory の増分から BuildManifestHash で対応づける（S2-0b と同じ）。
        private static (LayoutDocument, OutputFile[])? Build(SpikeRun run, string d, string rootPath, List<string> problems)
        {
            var output = Output(run, d);
            Directory.CreateDirectory(output);
            var history = Path.Combine(SpikeRun.RepoRoot, "unity", "Library", "BuildHistory");
            var known = Directory.Exists(history) ? new HashSet<string>(Directory.GetDirectories(history)) : new HashSet<string>();
            var watch = Stopwatch.StartNew();
            var report = BuildPipeline.BuildContentDirectory(new BuildContentDirectoryParameters
            {
                outputPath = output, rootAssetPaths = new[] { rootPath }, name = BuildName(d),
                compression = BuildCompression.Uncompressed, options = BuildContentOptions.None, extraScriptingDefines = ScriptingDefines,
            });
            // BuildReport は UnityEngine.Object なので == null で判定する。
            var result = report == null ? "no report" : report.summary.result.ToString();
            run.Write("build-end", ("directory", d), ("result", result), ("elapsedMs", watch.ElapsedMilliseconds), ("options", "None, Uncompressed"),
                ("extraScriptingDefines", ScriptingDefines), ("root", rootPath));
            if (result != BuildResult.Succeeded.ToString()) { problems.Add("build " + d + " failed: " + result); return null; }

            var pointer = File.ReadAllText(Path.Combine(output, "BuildManifestHash.txt")).Trim();
            var added = Directory.GetDirectories(history).Where(x => !known.Contains(x)).Select(x => Path.Combine(x, "ContentLayout.json"))
                .Where(File.Exists).Where(x => JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(x)).BuildManifestHash == pointer).ToArray();
            if (added.Length != 1) { problems.Add("layout of " + d + " cannot be matched (" + added.Length + ")"); return null; }
            var copy = Path.Combine(run.Directory, "layouts", d);
            Directory.CreateDirectory(copy);
            File.Copy(added[0], Path.Combine(copy, "ContentLayout.json"));
            var files = Inventory(output);
            run.Write("inventory", ("directory", d), ("files", files.Select(x => x.Name + " " + x.Size + " " + x.Sha256)), ("digest", Digest(files)));
            return (JsonUtility.FromJson<LayoutDocument>(File.ReadAllText(added[0])), files);
        }

        internal static OutputFile[] Inventory(string output) =>
            Directory.GetFiles(output).OrderBy(x => x, StringComparer.Ordinal).Select(x => new OutputFile
                { Name = Path.GetFileName(x), Size = new FileInfo(x).Length, Sha256 = OverlayExpected.Sha256(File.ReadAllBytes(x)) })
                .Concat(Directory.GetDirectories(output).Select(x => new OutputFile { Name = "directory:" + Path.GetFileName(x) })).ToArray();

        // 棚卸しの digest: file の名前と sha256 の一覧の sha256（第 2 process の照合に使う。HANDOFF AC2）。
        internal static string Digest(OutputFile[] files) =>
            OverlayExpected.Sha256(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + " " + x.Sha256 + "\n"))));

        private static void Attribute(SpikeRun run, FirstFacts facts, Dictionary<string, (LayoutDocument layout, OutputFile[] files)> built)
        {
            facts.Digests = Directories.Select(d => d + "=" + Digest(built[d].files)).ToArray();
            // P1: MonoScript をまとめた file。BuildContentRoot の script を含む serialized file として引く（どの build も使う script。§1 の build 履歴の読み取り）。
            var probeRoot = ScriptableObject.CreateInstance<BuildContentRoot>();
            var rootScript = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(probeRoot));
            UnityEngine.Object.DestroyImmediate(probeRoot);
            var scriptFact = new FileFact { Item = "scripts" };
            foreach (var d in Directories)
            {
                var (layout, files) = built[d];
                var candidates = layout.SerializedFiles.Where(x => !x.IsBuiltIn && x.SourceAssets.Contains(rootScript)).ToArray();
                var file = candidates.Length == 1 ? files.FirstOrDefault(x => Stem(x.Name) == candidates[0].ContentHash) : null;
                var sources = new List<string>();
                if (file != null)
                {
                    Set(scriptFact, d, candidates[0].ID, file);
                    // 一覧の path は、probe の script の .meta の GUID から引いた path と照合する（file 名では引かない。HANDOFF §2 の P1）。
                    foreach (var probe in OverlayFixture.ProbeGuids)
                        if (candidates[0].SourceAssets.Contains(AssetDatabase.GUIDToAssetPath(probe.Value))) sources.Add(probe.Key);
                }
                if (d == OverlayVerdict.Base) facts.ScriptSourcesBase = sources; else facts.ScriptSourcesOverlay = sources;
                run.Write("script-file", ("directory", d), ("rootScript", rootScript), ("candidates", candidates.Length), ("id", candidates.FirstOrDefault()?.ID),
                    ("file", file?.Name), ("sha256", file?.Sha256), ("sourceAssets", candidates.FirstOrDefault()?.SourceAssets), ("probes", sources),
                    ("probePaths", OverlayFixture.ProbeGuids.Select(x => x.Key + "=" + AssetDatabase.GUIDToAssetPath(x.Value))));
            }
            facts.ScriptFile = scriptFact;

            // P2・P3: 帰属は固定 GUID で引く（Scene は LoadableSceneIds の GUID、他は GUID から path、root の entry は GUID と LFID も）。
            var items = new (string item, string path)[]
            {
                (OverlayExpected.SceneEdit, OverlayFixture.SceneEdit), (OverlayExpected.EditedTex, OverlayFixture.EditedTex),
                (OverlayExpected.SharedTex, OverlayFixture.SharedTex), (OverlayExpected.SharedMat, OverlayFixture.SharedMat),
                (OverlayExpected.SharedShader, OverlayFixture.SharedShader), (OverlayExpected.EditedMat, OverlayFixture.EditedMat),
            };
            foreach (var (item, path) in items)
            {
                var fact = new FileFact { Item = item };
                foreach (var d in Directories)
                {
                    var (layout, files) = built[d];
                    var sf = Attribute(layout, path, out var detail);
                    var file = sf == null ? null : files.FirstOrDefault(x => Stem(x.Name) == sf.ContentHash);
                    if (sf != null && file != null) Set(fact, d, sf.ID, file);
                    run.Write("attribution", ("directory", d), ("item", item), ("path", path), ("guid", OverlayFixture.FixedGuid(path)),
                        ("id", sf?.ID), ("file", file?.Name), ("sha256", file?.Sha256), ("detail", detail));
                }
                facts.Files.Add(fact);
            }
            // 観測（判定に使わない）: 両方にある同じ id で中身が違う file を全件、source asset 付きで。
            var la = built[OverlayVerdict.Base].layout;
            var lo = built[OverlayVerdict.Overlay].layout;
            foreach (var sa in la.SerializedFiles.Where(x => x.ContentHash.Length != 0))
            {
                var so = lo.SerializedFiles.FirstOrDefault(x => x.ID == sa.ID);
                if (so == null || so.ContentHash == sa.ContentHash) continue;
                run.Write("observe-differed-id", ("id", sa.ID), ("sourcesBase", sa.SourceAssets), ("sourcesOverlay", so.SourceAssets));
            }
        }

        private static void Set(FileFact fact, string d, string id, OutputFile file)
        {
            if (d == OverlayVerdict.Base) { fact.FoundBase = true; fact.IdBase = id; fact.NameBase = file.Name; fact.ShaBase = file.Sha256; }
            else { fact.FoundOverlay = true; fact.IdOverlay = id; fact.NameOverlay = file.Name; fact.ShaOverlay = file.Sha256; }
        }

        // 固定 GUID が指す source の serialized file。Scene は layout の LoadableSceneIds の GUID が指す file。他は GUID を path に戻して
        // SourceAssets から引き、LoadableObjectIds に載る物（root の entry）は GUID が指す file とも一致することを確かめる。候補が 0・複数なら null。
        private static LayoutSerializedFile? Attribute(LayoutDocument layout, string path, out string detail)
        {
            var guid = OverlayFixture.FixedGuid(path);
            if (AssetDatabase.GUIDToAssetPath(guid) != path) { detail = "guid does not resolve to the path"; return null; }
            if (path.EndsWith(".unity", StringComparison.Ordinal))
            {
                var scenes = layout.LoadableSceneIds.Where(x => x.GUID == guid).ToArray();
                var sceneFile = scenes.Length == 1 ? layout.SerializedFiles.FirstOrDefault(x => x.Index == scenes[0].SerializedFile) : null;
                detail = "loadable scene ids=" + scenes.Length + (sceneFile != null ? " index=" + sceneFile.Index : "");
                return sceneFile;
            }
            var candidates = layout.SerializedFiles.Where(x => !x.IsBuiltIn && x.SourceAssets.Contains(path)).ToArray();
            if (candidates.Length != 1) { detail = "serialized files: " + candidates.Length; return null; }
            var sf = candidates[0];
            var loadables = layout.LoadableObjectIds.Where(x => x.GUID == guid).ToArray();
            if (loadables.Any(x => x.SerializedFile != sf.Index)) { detail = "loadable id points to another serialized file"; return null; }
            detail = "index=" + sf.Index + " sources=" + string.Join(",", sf.SourceAssets) + " loadables=" + string.Join(",", loadables.Select(x => x.GUID + ":" + x.LFID));
            return sf;
        }

        private static string Stem(string name) => name.Contains('.') ? name.Substring(0, name.IndexOf('.')) : name;
    }

    // ContentLayout.json（Version 2）の、帰属に使う欄だけの写し（S2-0b の LayoutDocument に LoadableSceneIds を足した）。
    [Serializable] internal sealed class LayoutDocument
    {
        public int Version;
        public string BuildManifestHash = "";
        public LayoutSerializedFile[] SerializedFiles = Array.Empty<LayoutSerializedFile>();
        public LayoutObjectId[] LoadableObjectIds = Array.Empty<LayoutObjectId>();
        public LayoutSceneId[] LoadableSceneIds = Array.Empty<LayoutSceneId>();
    }

    [Serializable] internal sealed class LayoutSerializedFile
    {
        public int Index;
        public string ID = "";
        public bool IsBuiltIn;
        public string[] SourceAssets = Array.Empty<string>();
        public string ContentHash = "";
    }

    [Serializable] internal sealed class LayoutObjectId
    {
        public string GUID = "";
        public long LFID;
        public int SerializedFile = -1;
    }

    [Serializable] internal sealed class LayoutSceneId
    {
        public string GUID = "";
        public string Path = "";
        public int SerializedFile = -1;
    }

    [Serializable] internal sealed class OutputFile
    {
        public string Name = "";
        public long Size;
        public string Sha256 = "";
    }

    /// <summary>
    /// 1 回の実行の一時 root と JSONL の record。root は `TestResults/ovl/&lt;実行 id&gt;`（git 管理外）で、他の実行の root に触れない。
    /// record は process ごとの file に分ける（HANDOFF §2.2）。各行は header（実行 id、head、Unity の版、target、Enter Play Mode の設定、
    /// process の名前）と field を持つ。
    /// </summary>
    internal sealed class SpikeRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string SpikeRoot => Path.Combine(RepoRoot, "TestResults", "ovl");
        public static string FirstPointer => Path.Combine(SpikeRoot, "first-process.txt");
        private const string MarkerName = "ovl-run.marker";

        private SpikeRun(string runId, string process, string stamp)
        {
            RunId = runId; Process = process;
            Directory = Path.Combine(SpikeRoot, runId);
            Head = Git.Read("rev-parse HEAD", out var head) ? head.Trim() : head;
            // 同じ process の名前を再び実行しても、前の record に追記しない（process ごとに別の file）。
            RecordPath = Path.Combine(Directory, "record-" + process + "-" + stamp + ".jsonl");
            ObservationPath = Path.Combine(Directory, "observations-" + process + "-" + stamp + ".json");
        }

        public string RunId { get; }
        public string Process { get; }
        public string Directory { get; }
        public string Head { get; }
        public string RecordPath { get; }
        public string ObservationPath { get; }

        private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        // 実行 id は新しく作る。同じ id の root が既にあれば上書きせずに止める。
        public static SpikeRun Create()
        {
            var stamp = Stamp();
            var run = new SpikeRun(stamp, "first", stamp);
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            return run;
        }

        // 第 2 process は、第 1 process が書いた実行 id の root を marker で確かめてから開く。
        public static SpikeRun Open(string runId, string process)
        {
            var run = new SpikeRun(runId, process, Stamp());
            var marker = Path.Combine(run.Directory, MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != runId) throw new IOException("Run root has no marker: " + run.Directory);
            return run;
        }

        // 結論の範囲（HANDOFF §2 の「成果の範囲」）。各 process の record の先頭に書く。
        public const string Scope = "this PC, Unity 6000.6.0f1, StandaloneWindows64, Editor Play (Domain Reload disabled), loose files, synthetic fixture, base and overlay built in the same Library";

        public void WriteScope() => Write("scope", ("scope", Scope), ("unityVersion", Application.unityVersion));

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

    /// <summary>process の前後の比較と fixture の後始末（AC8）。git status・設定の前後が違えば problems に足す。</summary>
    internal sealed class Aftercare
    {
        private string _git = "", _settings = "";
        private bool _gitRead;

        public static Aftercare Begin(SpikeRun run)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("The spike requires the active StandaloneWindows64 Player target.");
            var care = new Aftercare { _settings = Settings() };
            care._gitRead = Git.Read("status --porcelain", out care._git);
            // marker の無い fixture の folder があれば RemoveLeftovers が投げ、何も消さずに止まる（AC8）。
            var removed = OverlayFixture.RemoveLeftovers();
            run.Write("process-start", ("leftoverFixtureRemoved", removed), ("settings", care._settings), ("gitStatus", care._git), ("gitRead", care._gitRead));
            return care;
        }

        public static void End(SpikeRun run, Aftercare care, List<string> problems, bool deleteFixture)
        {
            if (deleteFixture)
            {
                try { OverlayFixture.Delete(); }
                catch (Exception ex) { problems.Add("fixture delete failed: " + ex.Message); }
            }
            var fixture = OverlayFixture.Exists;
            var generated = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")) || File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated.meta"));
            var gitRead = Git.Read("status --porcelain", out var git);
            var settings = Settings();
            if (fixture || generated) problems.Add("fixture or generated folder remains");
            // 取得に失敗した git status は前後が同じ失敗の文字列になりうるので、一致として読まない（S2-0b と同じ）。
            if (!care._gitRead || !gitRead) problems.Add("git status could not be read");
            else if (git != care._git) problems.Add("git status changed during the process");
            if (settings != care._settings) problems.Add("settings changed during the process");
            run.Write("process-aftercare", ("fixtureFolderExists", fixture), ("generatedFolderExists", generated), ("gitStatus", git),
                ("settings", settings), ("problems", problems));
        }

        // Build Settings、active target、Enter Play Mode の設定、Play の初期 Scene（AC8）。
        private static string Settings()
        {
            var start = UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene;
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
                    { WorkingDirectory = SpikeRun.RepoRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
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
