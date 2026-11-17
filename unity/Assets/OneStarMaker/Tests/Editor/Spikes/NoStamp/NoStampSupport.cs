#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.NoStamp
{
    // ContentLayout.json の読む部分（JsonUtility で読める形）。
    [Serializable] internal sealed class NstLayout { public NstSerializedFile[] SerializedFiles = Array.Empty<NstSerializedFile>(); public NstSceneId[] LoadableSceneIds = Array.Empty<NstSceneId>(); }
    [Serializable] internal sealed class NstSerializedFile { public int Index; public string ID = ""; public string ContentHash = ""; public string[] SourceAssets = Array.Empty<string>(); }
    [Serializable] internal sealed class NstSceneId { public string GUID = ""; public string Path = ""; public int SerializedFile; }
    [Serializable] internal sealed class NstSummary { public string BuildName = ""; }

    /// <summary>Unity の build 履歴（`Library/BuildHistory/&lt;build&gt;`）の読み取り。</summary>
    internal static class NstHistory
    {
        public static readonly string[] Files = { "BuildContentTEP.json", "ContentLayout.json", "BuildReportSummary.json" };
        public static string Root => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Library", "BuildHistory");

        // BuildReportSummary.json の BuildName は adapter が渡す build identity。
        public static string? Find(string identity)
        {
            if (!Directory.Exists(Root)) return null;
            foreach (var directory in Directory.GetDirectories(Root))
            {
                var summary = Path.Combine(directory, "BuildReportSummary.json");
                if (File.Exists(summary) && JsonUtility.FromJson<NstSummary>(File.ReadAllText(summary)).BuildName == identity) return directory;
            }
            return null;
        }

        // serialized file の id（.cfid）を書いた event（`BuildPipeline::WriteSerializedFile`）の id の集合。
        // 集合が空なら、event 名か書式が 6000.6.9f1 で変わって何も数えられていない（判定の process が harness の欠陥として扱う。H1）。
        public static HashSet<string> WrittenFileIds(string tepText)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            const string name = "\"name\": \"BuildPipeline::WriteSerializedFile\"";
            const string key = "\"filePath\": \"mem:/Temp/";
            var position = 0;
            while ((position = tepText.IndexOf(name, position, StringComparison.Ordinal)) >= 0)
            {
                var lineEnd = tepText.IndexOf('\n', position);
                var line = tepText.Substring(position, (lineEnd < 0 ? tepText.Length : lineEnd) - position);
                var at = line.IndexOf(key, StringComparison.Ordinal);
                if (at >= 0)
                {
                    var start = at + key.Length;
                    var end = line.IndexOf('"', start);
                    if (end > start) ids.Add(line.Substring(start, end - start));
                }
                position += name.Length;
            }
            return ids;
        }
    }

    /// <summary>`Assets/` と `ProjectSettings/` の全 file の sha256（AC5）。一覧を file に書き、要約と差を返す。</summary>
    internal static class NstInventory
    {
        public static Dictionary<string, string> Take()
        {
            var project = Path.GetDirectoryName(Application.dataPath)!;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var top in new[] { "Assets", "ProjectSettings" })
                foreach (var file in Directory.GetFiles(Path.Combine(project, top), "*", SearchOption.AllDirectories))
                    result[Path.GetRelativePath(project, file).Replace('\\', '/')] = NstSpec.Sha256(File.ReadAllBytes(file));
            return result;
        }

        public static string Write(Dictionary<string, string> inventory, string path)
        {
            var text = string.Join("\n", inventory.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value + " " + x.Key)) + "\n";
            File.WriteAllText(path, text);
            return NstSpec.Sha256(Encoding.UTF8.GetBytes(text));
        }

        public static List<string> Diff(Dictionary<string, string> before, Dictionary<string, string> after) =>
            before.Keys.Union(after.Keys).OrderBy(x => x, StringComparer.Ordinal)
                .Where(k => !before.TryGetValue(k, out var a) || !after.TryGetValue(k, out var b) || a != b)
                .Select(k => (before.ContainsKey(k) ? (after.ContainsKey(k) ? "changed " : "removed ") : "added ") + k).ToList();

        // 撤去した印の記録の置き場（`Library/OneStarMaker/ScenePrefabStamp/`）の状態。adapter が何も書かないので、run の始めと同じでなければならない。
        public static string StampDirectoryState()
        {
            var directory = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Library", "OneStarMaker", "ScenePrefabStamp");
            if (!Directory.Exists(directory)) return "absent";
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(directory, f).Replace('\\', '/') + ":" + NstSpec.Sha256(File.ReadAllBytes(f)));
            return "present:" + string.Join(";", files);
        }

        public static NstAssetState Asset(string path)
        {
            var disk = NstFixture.Disk(path);
            return new NstAssetState
            {
                path = path,
                sha256 = File.Exists(disk) ? NstSpec.Sha256(File.ReadAllBytes(disk)) : "missing",
                dependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
            };
        }
    }

    /// <summary>
    /// 1 回の実行の record（JSONL）。root は `TestResults/nst/&lt;実行 id&gt;`（git 管理外）。process ごとに別の file に書く。
    /// 判定の process は、build の process が書いた実行 id の root を marker で確かめてから開く。
    /// </summary>
    internal sealed class NstRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string RunRoot => Path.Combine(RepoRoot, "TestResults", "nst");
        public static string BuildPointer => Path.Combine(RunRoot, "build-process.txt");
        public static string ArtifactsRoot => Path.Combine(RepoRoot, "TestResults", "bs3-fixture", "nst");
        private const string MarkerName = "nst-run.marker";

        private NstRun(string runId, string process, string stamp)
        {
            RunId = runId; Process = process;
            Directory = Path.Combine(RunRoot, runId);
            Head = Git("rev-parse HEAD");
            RecordPath = Path.Combine(Directory, "record-" + process + "-" + stamp + ".jsonl");
        }

        public string RunId { get; }
        public string Process { get; }
        public string Directory { get; }
        public string Head { get; }
        public string RecordPath { get; }
        public string FactsPath => Path.Combine(Directory, "build-facts.json");

        private static string Stamp() => DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

        public static NstRun Create()
        {
            var stamp = Stamp();
            var run = new NstRun(stamp, "build", stamp);
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            run.Write("process-start");
            return run;
        }

        public static NstRun Open(string process)
        {
            if (!File.Exists(BuildPointer)) throw new InvalidOperationException("The build process has not written its record.");
            var runId = File.ReadAllText(BuildPointer).Trim();
            var run = new NstRun(runId, process, Stamp());
            var marker = Path.Combine(run.Directory, MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != runId) throw new IOException("Run root has no marker: " + run.Directory);
            run.Write("process-start");
            return run;
        }

        public void Write(string type, params (string key, object? value)[] fields)
        {
            var b = new StringBuilder("{");
            void Field(string key, object? value) { if (b.Length > 1) b.Append(','); b.Append(Json(key)).Append(':').Append(Json(value)); }
            Field("runId", RunId); Field("head", Head); Field("unity", Application.unityVersion);
            Field("target", EditorUserBuildSettings.activeBuildTarget + "/" + EditorUserBuildSettings.standaloneBuildSubtarget);
            Field("process", Process); Field("type", type); Field("at", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            foreach (var (key, value) in fields) Field(key, value);
            File.AppendAllText(RecordPath, b.Append("}\n").ToString());
        }

        private static string Json(object? value) => value switch
        {
            null => "null",
            bool x => x ? "true" : "false",
            string x => "\"" + Escape(x) + "\"",
            int or long or double or float => Convert.ToString(value, CultureInfo.InvariantCulture)!,
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

        private static string Git(string arguments)
        {
            try
            {
                using var process = System.Diagnostics.Process.Start(new ProcessStartInfo("git", arguments)
                    { WorkingDirectory = RepoRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
                if (process == null) return "unknown (git did not start)";
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? output.Trim() : "unknown (exit " + process.ExitCode + ")";
            }
            catch (Exception ex) { return "unknown (" + ex.Message + ")"; }
        }
    }
}
