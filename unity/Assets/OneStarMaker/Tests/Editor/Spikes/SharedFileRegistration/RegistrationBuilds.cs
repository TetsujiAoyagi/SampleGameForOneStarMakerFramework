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

namespace OneStarMaker.Tests.Editor.Spikes.SharedFileRegistration
{
    /// <summary>
    /// 第 1 process（HANDOFF §2.2 AC1・AC5）。fixture を作り、directory A と B を build し、出力の全 file の名前・size・sha256 と、
    /// ContentLayout.json による中核の 3 種・SharedShader・SharedTex の pixel の所在・正の対照の帰属を record に書き、fixture を消して終わる。
    /// 通常の回帰に混ぜないので [Explicit]。test が失敗するのは harness 自身の例外の時だけで、build の失敗や帰属の不一致は
    /// record の行（未判定の材料）にする。判定は判定 C が record から読む（HANDOFF §2 末尾）。
    /// 製品への依存は BuildContentRoot（無改変）だけで、build は BuildPipeline.BuildContentDirectory を直接呼ぶ（S2-0 と同じ）。
    /// </summary>
    public sealed class RegistrationBuilds
    {
        public static readonly string[] Directories = { "A", "B" };
        public static string BuildName(string directory) => "sfr-" + directory;

        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator FirstProcess()
        {
            var run = SpikeRun.Create("first");
            var state = new FirstState { RunId = run.RunId, Head = run.Head };
            var care = Aftercare.Begin(run);
            Exception? harness = null;
            try
            {
                var fixture = new RegistrationFixture();
                fixture.Create();
                var rawBytes = SourceRawBytes(run);
                yield return null;
                var layouts = new Dictionary<string, (LayoutDocument layout, OutputFile[] files)>();
                foreach (var d in Directories)
                {
                    var root = CreateRoot(fixture, d);
                    var built = Build(run, d, root, state.Problems);
                    if (built != null) layouts[d] = built.Value;
                    yield return null;
                }
                state.BuildsSucceeded = layouts.Count == Directories.Length;
                if (state.BuildsSucceeded) Attribute(run, state, layouts, rawBytes);
            }
            finally
            {
                try { Aftercare.End(run, care, state.Problems, true); }
                catch (Exception ex) { harness = ex; }
                File.WriteAllText(Path.Combine(run.Directory, "first-state.json"), JsonUtility.ToJson(state, true));
                File.WriteAllText(SpikeRun.FirstPointer, run.RunId);
                run.Write("process-end", ("state", JsonUtility.ToJson(state)));
            }
            Assert.That(harness, Is.Null, "first process aftercare failed: " + harness);
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        /// <summary>第 2 process が照合に使う第 1 process の結果（AC1 の G-09）。</summary>
        [Serializable] internal sealed class FirstState
        {
            public string RunId = "", Head = "";
            public bool BuildsSucceeded, TwinFilesDiffer;
            public string[] Digests = Array.Empty<string>();
            public List<string> NotSameFile = new List<string>();
            public List<string> Problems = new List<string>();
        }

        internal static string Output(SpikeRun run, string directory) => Path.Combine(run.Directory, "out", directory);

        // root は製品の BuildContentRoot を無改変で使う。Scene は LoadableSceneId、Object は Loadable<Object>（HANDOFF §3）。
        // logical key は両 directory で同じ名前にし、Play 中は root の entry をこの key で引く。
        private static string CreateRoot(RegistrationFixture fixture, string d)
        {
            var scene = d == "A" ? RegistrationFixture.SceneA : RegistrationFixture.SceneB;
            var entries = new List<BuildContentEntry>
            {
                new BuildContentEntry("Scene", scene, "", LoadableSceneIdEditorUtility.CreateLoadableSceneId(scene)),
                ObjectEntry(RegistrationFixture.SharedPrefabName, RegistrationFixture.SharedPrefab),
                ObjectEntry(RegistrationFixture.TwinName, RegistrationFixture.Twin(d)),
            };
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            root.Initialize(BuildName(d), "StandaloneWindows64-Player", entries);
            return fixture.AddRoot(d, root);
        }

        private static BuildContentEntry ObjectEntry(string key, string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null) throw new InvalidOperationException("missing " + path);
            return new BuildContentEntry(key, path, "", new Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)));
        }

        // build の失敗は record に残して null を返す（未判定の材料）。layout は BuildHistory の増分から BuildManifestHash で対応づける（S2-0 と同じ）。
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
                compression = BuildCompression.Uncompressed, options = BuildContentOptions.None, extraScriptingDefines = Array.Empty<string>(),
            });
            // BuildReport は UnityEngine.Object なので == null で判定する。
            var result = report == null ? "no report" : report.summary.result.ToString();
            run.Write("build-end", ("directory", d), ("result", result), ("elapsedMs", watch.ElapsedMilliseconds), ("options", "None, Uncompressed"));
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
                { Name = Path.GetFileName(x), Size = new FileInfo(x).Length, Sha256 = RegistrationFixture.Sha256(File.ReadAllBytes(x)) })
                .Concat(Directory.GetDirectories(output).Select(x => new OutputFile { Name = "directory:" + Path.GetFileName(x) })).ToArray();

        // 棚卸しの digest: file の名前と sha256 の一覧の sha256（AC1 の G-09）。
        internal static string Digest(OutputFile[] files) =>
            RegistrationFixture.Sha256(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + " " + x.Sha256 + "\n"))));

        // AC1 (ii) の raw の byte 数。build の前に、読める source の imported texture の mip 0 の raw data の長さから求め、
        // width・height・format・mip の数と一緒に record に書く。format を仮定した計算はしない（HANDOFF §4、A2 R-4。
        // J-1 の build では .cf の size との余裕が 2,220 bytes しかない）。求められなければ 0 を返し、(ii) が成り立たず未判定になる。
        private static long SourceRawBytes(SpikeRun run)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(RegistrationFixture.SharedTex);
            long raw = 0;
            string detail;
            // Texture2D は UnityEngine.Object なので == null で判定する。
            if (tex == null) detail = "source texture not found";
            else if (!tex.isReadable) detail = "source texture is not readable";
            else
            {
                raw = tex.GetPixelData<byte>(0).Length;
                detail = "mip 0 raw data";
            }
            run.Write("source-texture", ("path", RegistrationFixture.SharedTex), ("width", tex != null ? tex.width : 0), ("height", tex != null ? tex.height : 0),
                ("format", tex != null ? tex.format.ToString() : null), ("mipmapCount", tex != null ? tex.mipmapCount : 0), ("rawBytes", raw), ("detail", detail));
            return raw;
        }

        private static void Attribute(SpikeRun run, FirstState state, Dictionary<string, (LayoutDocument layout, OutputFile[] files)> built, long rawBytes)
        {
            state.Digests = Directories.Select(d => d + "=" + Digest(built[d].files)).ToArray();
            var items = new (string item, string path)[]
            {
                ("texture.cf", RegistrationFixture.SharedTex), ("material", RegistrationFixture.SharedMat),
                ("prefab", RegistrationFixture.SharedPrefab), ("shader", RegistrationFixture.SharedShader),
            };
            var found = new Dictionary<(string d, string item), OutputFile?>();
            var pixels = new Dictionary<(string d, string item), PixelEvidence>();
            foreach (var d in Directories)
            {
                var (layout, files) = built[d];
                foreach (var (item, path) in items.Append(("twin.cf", RegistrationFixture.Twin(d))))
                {
                    var cf = ContentFile(layout, files, path, out var detail);
                    found[(d, item)] = cf;
                    run.Write("attribution", ("directory", d), ("item", item), ("path", path), ("guid", RegistrationFixture.FixedGuid(path)),
                        ("file", cf?.Name), ("sha256", cf?.Sha256), ("detail", detail));
                    if (item != "texture.cf" && item != "twin.cf") continue;
                    // 正の対照には (ii) を求めないので raw の byte 数を渡さない（HANDOFF AC1 の正の対照）。
                    var pixelItem = item == "texture.cf" ? "texture.pixels" : "twin.pixels";
                    var e = Pixels(layout, files, cf, item == "texture.cf" ? rawBytes : 0);
                    pixels[(d, pixelItem)] = e;
                    run.Write("pixel-evidence", ("directory", d), ("item", pixelItem), ("cf", e.CfName), ("cfSize", e.CfSize),
                        ("contentFileArtifacts", e.ContentFileArtifacts), ("textureReferences", e.TextureReferences),
                        ("resS", e.ResSName), ("resSSha256", e.ResSSha256), ("unreferencedTextures", e.UnreferencedTextures), ("rawBytes", e.RawBytes));
                }
            }
            foreach (var item in new[] { "texture.cf", "material", "prefab", "shader" })
            {
                var a = found[("A", item)];
                var b = found[("B", item)];
                var same = a != null && b != null && a.Name == b.Name && a.Sha256 == b.Sha256;
                if (!same) state.NotSameFile.Add(item);
                run.Write("same-file", ("item", item), ("same", same), ("a", a?.Name), ("b", b?.Name));
            }
            // pixel の所在と同じ file かは pure の RegistrationVerdict で決め、ここでは分岐しない（HANDOFF §3、A2 R-1・R-2）。
            var location = RegistrationVerdict.LocatePixels(pixels[("A", "texture.pixels")], pixels[("B", "texture.pixels")]);
            if (location.NotSameFileItem != null) state.NotSameFile.Add(location.NotSameFileItem);
            run.Write("same-file", ("item", "texture.pixels"), ("same", location.SameFile), ("location", location.Location), ("reasons", location.Reasons));
            state.TwinFilesDiffer = RegistrationVerdict.ControlFilesDiffer(pixels[("A", "twin.pixels")], pixels[("B", "twin.pixels")], out var controlReason);
            run.Write("positive-control-files", ("differ", state.TwinFilesDiffer), ("reason", controlReason));
            // 観測（判定に使わない）: 両方にある同じ id で sha256 が違う file を全件、source asset 付きで（HANDOFF §1 対象外、G24）。
            var la = built["A"].layout;
            var lb = built["B"].layout;
            foreach (var sa in la.SerializedFiles.Where(x => x.ContentHash.Length != 0))
            {
                var sb = lb.SerializedFiles.FirstOrDefault(x => x.ID == sa.ID);
                if (sb == null || sb.ContentHash == sa.ContentHash) continue;
                run.Write("observe-differed-id", ("id", sa.ID), ("sourcesA", sa.SourceAssets), ("sourcesB", sb.SourceAssets));
            }
        }

        // 帰属: 固定 GUID が指す source の path を、layout の SerializedFiles の SourceAssets から引く（名前では対応づけない。A2 G-06）。
        // layout の SourceAssets は path なので GUID は AssetDatabase で path に戻して照合する。LoadableObjectIds に載る物（root の
        // entry）は GUID と LFID が指す SerializedFile も一致することを確かめる。候補が 0・複数、file が出力に無い時は null。
        private static OutputFile? ContentFile(LayoutDocument layout, OutputFile[] files, string path, out string detail)
        {
            var guid = RegistrationFixture.FixedGuid(path);
            if (AssetDatabase.GUIDToAssetPath(guid) != path) { detail = "guid does not resolve to the path"; return null; }
            var candidates = layout.SerializedFiles.Where(x => !x.IsBuiltIn && x.SourceAssets.Contains(path)).ToArray();
            if (candidates.Length != 1) { detail = "serialized files: " + candidates.Length; return null; }
            var sf = candidates[0];
            var loadables = layout.LoadableObjectIds.Where(x => x.GUID == guid).ToArray();
            if (loadables.Any(x => x.SerializedFile != sf.Index)) { detail = "loadable id points to another serialized file"; return null; }
            var file = files.Where(x => Stem(x.Name) == sf.ContentHash).ToArray();
            detail = "index=" + sf.Index + " id=" + sf.ID + " sources=" + string.Join(",", sf.SourceAssets)
                     + " loadables=" + string.Join(",", loadables.Select(x => x.GUID + ":" + x.LFID)) + " files=" + file.Length;
            return file.Length == 1 ? file[0] : null;
        }

        // pixel の所在の材料を layout から集める（HANDOFF AC1 の (1)〜(4b)。S2-0 の FileInventory と同じ向き）。値を集めるだけで、
        // 所在と同じ file かの判断は RegistrationVerdict.LocatePixels / ControlFilesDiffer が行う。.cf を帰属できなければ
        // (2) が 0 件のまま返り、未判定（対照なら不成立）になる。
        private static PixelEvidence Pixels(LayoutDocument layout, OutputFile[] files, OutputFile? cf, long rawBytes)
        {
            var e = new PixelEvidence { RawBytes = rawBytes };
            var referenced = new HashSet<int>(layout.BinaryArtifacts.Where(x => x.Category == "contentfile").SelectMany(x => x.ArtifactReferences));
            e.UnreferencedTextures = layout.BinaryArtifacts.Count(x => x.Category == "texture" && !referenced.Contains(x.Index));
            if (cf == null) return e;
            e.CfName = cf.Name; e.CfSha256 = cf.Sha256; e.CfSize = cf.Size;
            var owners = layout.BinaryArtifacts.Where(x => x.Category == "contentfile" && x.ContentHash == Stem(cf.Name)).ToArray();
            e.ContentFileArtifacts = owners.Length;
            if (owners.Length != 1) return e;
            var textures = layout.BinaryArtifacts.Where(x => owners[0].ArtifactReferences.Contains(x.Index) && x.Category == "texture").ToArray();
            e.TextureReferences = textures.Length;
            if (textures.Length != 1) return e;
            var file = files.Where(x => Stem(x.Name) == textures[0].ContentHash).ToArray();
            if (file.Length == 1) { e.ResSName = file[0].Name; e.ResSSha256 = file[0].Sha256; }
            return e;
        }

        private static string Stem(string name) => name.Contains('.') ? name.Substring(0, name.IndexOf('.')) : name;
    }

    // ContentLayout.json（Version 2）の、帰属に使う欄だけの写し（S2-0 の FileInventory から縮めた）。
    [Serializable] internal sealed class LayoutDocument
    {
        public int Version;
        public string BuildManifestHash = "";
        public LayoutSerializedFile[] SerializedFiles = Array.Empty<LayoutSerializedFile>();
        public LayoutObjectId[] LoadableObjectIds = Array.Empty<LayoutObjectId>();
        public LayoutBinaryArtifact[] BinaryArtifacts = Array.Empty<LayoutBinaryArtifact>();
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

    [Serializable] internal sealed class LayoutBinaryArtifact
    {
        public int Index;
        public string ContentHash = "";
        public string Category = "";
        public int[] ArtifactReferences = Array.Empty<int>();
    }

    [Serializable] internal sealed class OutputFile
    {
        public string Name = "";
        public long Size;
        public string Sha256 = "";
    }

    /// <summary>
    /// 1 回の実行の一時 root と JSONL の record。root は `TestResults/sfr/&lt;実行 id&gt;`（git 管理外）で、他の実行の root に触れない（AC5）。
    /// 各行は header（実行 id、head、Unity の版、target、Enter Play Mode の設定、process の別）と field を持つ。
    /// </summary>
    internal sealed class SpikeRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string SpikeRoot => Path.Combine(RepoRoot, "TestResults", "sfr");
        public static string FirstPointer => Path.Combine(SpikeRoot, "first-process.txt");
        private const string MarkerName = "sfr-run.marker";

        private SpikeRun(string runId, string process)
        {
            RunId = runId; Process = process;
            Directory = Path.Combine(SpikeRoot, runId);
            Head = Git.Read("rev-parse HEAD", out var head) ? head.Trim() : head;
        }

        public string RunId { get; }
        public string Process { get; }
        public string Directory { get; }
        public string Head { get; }
        public string RecordPath => Path.Combine(Directory, "record.jsonl");

        // 実行 id は新しく作る。同じ id の root が既にあれば上書きせずに止める。
        public static SpikeRun Create(string process)
        {
            var run = new SpikeRun(DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture), process);
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            return run;
        }

        // 第 2 process は、第 1 process が書いた実行 id の root を marker で確かめてから開く。
        public static SpikeRun Open(string runId, string process)
        {
            var run = new SpikeRun(runId, process);
            var marker = Path.Combine(run.Directory, MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != runId) throw new IOException("Run root has no marker: " + run.Directory);
            return run;
        }

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

    /// <summary>process の前後の比較と fixture の後始末（AC5）。git status・設定の前後が違えば problems に足す。</summary>
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
            // marker の無い fixture の folder があれば RemoveLeftovers が投げ、何も消さずに止まる（AC5）。
            var removed = RegistrationFixture.RemoveLeftovers();
            run.Write("process-start", ("leftoverFixtureRemoved", removed), ("settings", care._settings), ("gitStatus", care._git), ("gitRead", care._gitRead));
            return care;
        }

        public static void End(SpikeRun run, Aftercare care, List<string> problems, bool deleteFixture)
        {
            if (deleteFixture)
            {
                try { RegistrationFixture.Delete(); }
                catch (Exception ex) { problems.Add("fixture delete failed: " + ex.Message); }
            }
            var fixture = RegistrationFixture.Exists;
            var generated = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated")) || File.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated.meta"));
            var gitRead = Git.Read("status --porcelain", out var git);
            var settings = Settings();
            if (fixture || generated) problems.Add("fixture or generated folder remains");
            // 取得に失敗した git status は前後が同じ失敗の文字列になりうるので、一致として読まない（S2-0 と同じ）。
            if (!care._gitRead || !gitRead) problems.Add("git status could not be read");
            else if (git != care._git) problems.Add("git status changed during the process");
            if (settings != care._settings) problems.Add("settings changed during the process");
            run.Write("process-aftercare", ("fixtureFolderExists", fixture), ("generatedFolderExists", generated), ("gitStatus", git),
                ("settings", settings), ("problems", problems));
        }

        // Build Settings、active target、Enter Play Mode の設定、Play の初期 Scene（AC5）。
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
