#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Spikes.PrefabStamp
{
    /// <summary>
    /// 実 build の harness の仕様（HANDOFF CONTENT_PREFAB_STAMP §5 の fixture と build の列）。期待値は fixture の source の値から導き、
    /// 実装の出力から作らない。Phase D で harness ごと削除する。
    /// </summary>
    internal static class PstSpec
    {
        public const string Folder = "Assets/OsmPst";
        public const string Inner = "Inner", Outer = "Outer", Shared = "Shared";
        public const string SOuter = "SOuter", SShared1 = "SShared1", SShared2 = "SShared2", SPlain = "SPlain";
        public const string V1 = "v1", V2 = "v2";
        public const string ContentSet = "pst", OverlaySet = "pst_ovl";
        public static readonly string[] Prefabs = { Inner, Outer, Shared };
        public static readonly string[] Scenes = { SOuter, SShared1, SShared2, SPlain };

        public static string Prefab(string name) => Folder + "/" + name + ".prefab";
        public static string Scene(string name) => Folder + "/" + name + ".unity";
        public static string Child(string prefab) => prefab + "Child";

        // 子の位置。prefab ごとに v1 / v2 で重ならない float の列にする。
        public static float[] Position(string prefab, string version) => (prefab, version) switch
        {
            (Inner, V1) => new[] { 1f, 2f, 3f }, (Inner, V2) => new[] { 4f, 5f, 6f },
            (Outer, V1) => new[] { 7f, 8f, 9f }, (Outer, V2) => new[] { 10f, 11f, 12f },
            (Shared, V1) => new[] { 13f, 14f, 15f }, (Shared, V2) => new[] { 16f, 17f, 18f },
            _ => throw new ArgumentException(prefab + "/" + version),
        };

        // Scene ごとに焼き込む prefab（入れ子を含む）。SOuter は Outer の instance で、Outer は Inner を nest する。
        public static string[] BakedPrefabs(string scene) => scene switch
        {
            SOuter => new[] { Inner, Outer },
            SShared1 or SShared2 => new[] { Shared },
            _ => Array.Empty<string>(),
        };

        internal sealed class Step
        {
            public Step(string name, string inner, string outer, string shared, bool full, bool overlay = false, bool cleanCache = false)
            {
                Name = name; Full = full; Overlay = overlay; CleanCache = cleanCache;
                Versions = new Dictionary<string, string> { [Inner] = inner, [Outer] = outer, [Shared] = shared };
            }
            public string Name { get; }
            public IReadOnlyDictionary<string, string> Versions { get; }
            public bool Full { get; }
            public bool Overlay { get; }
            public bool CleanCache { get; }
        }

        // B6 は製品の上書きのコンストラクタで build する（そのコンストラクタは mode を Full に固定する）。B6 は AC3 の数えに入らない。
        public static readonly Step[] Steps =
        {
            new Step("B1", V1, V1, V1, full: true),
            new Step("B2", V2, V1, V1, full: false),
            new Step("B3", V2, V1, V1, full: false),
            new Step("B4", V1, V1, V1, full: false),
            new Step("B5", V2, V2, V2, full: true),
            new Step("B6", V2, V1, V2, full: true, overlay: true),
            new Step("B5p", V2, V2, V2, full: true, cleanCache: true),
        };

        // B6 の上書きで逆引きに渡す編集（B5 から B6 で戻した prefab）。
        public static readonly string[] OverlayEdits = { Prefab(Outer) };

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }

        // float32 little-endian の 3 つを並べたバイト列（.cf の中で子の位置を探す形。S2-2 と S2-2b-0 と同じ観測）。
        public static byte[] FloatBytes(float[] xyz) => xyz.SelectMany(BitConverter.GetBytes).ToArray();
    }

    /// <summary>fixture（`Assets/OsmPst`）。marker で所有を示し、marker の無い folder には触れない。build の process の終わりに必ず消す。</summary>
    internal static class PstFixture
    {
        private const string MarkerName = "osm-pst.marker";
        private const string MarkerText = "osm-pst fixture; deleted at the end of the build process";

        public static string Disk(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, assetPath);
        public static bool Exists => Directory.Exists(Disk(PstSpec.Folder)) || File.Exists(Disk(PstSpec.Folder) + ".meta");

        public static bool RemoveLeftovers()
        {
            if (!Exists) return false;
            var marker = Path.Combine(Disk(PstSpec.Folder), MarkerName);
            if (!File.Exists(marker) || File.ReadAllText(marker) != MarkerText)
                throw new InvalidOperationException(PstSpec.Folder + " exists without the harness marker; not touching it.");
            Delete();
            return true;
        }

        public static void Delete()
        {
            AssetDatabase.DeleteAsset(PstSpec.Folder);
            if (Directory.Exists(Disk(PstSpec.Folder))) Directory.Delete(Disk(PstSpec.Folder), true);
            if (File.Exists(Disk(PstSpec.Folder) + ".meta")) File.Delete(Disk(PstSpec.Folder) + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>全て v1 の fixture を作る。Scene の作成は開いている Scene を置き換えるので、呼び手が先に構成を退避する。</summary>
        public static void Create()
        {
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(PstSpec.Folder));
            File.WriteAllText(Path.Combine(Disk(PstSpec.Folder), MarkerName), MarkerText);
            // marker は File で書くので Unity はまだ import しておらず `.meta` が無い。ここで import しないと、
            // 最初の build の製品の Refresh が marker の `.meta` を足し、build の前後の inventory の差（AC5）に出る。
            var markerAsset = PstSpec.Folder + "/" + MarkerName;
            AssetDatabase.ImportAsset(markerAsset, ImportAssetOptions.ForceSynchronousImport);
            if (!File.Exists(Disk(markerAsset) + ".meta"))
                throw new InvalidOperationException("The fixture marker was not imported: " + markerAsset);
            SavePrefab(PstSpec.Inner, WithChild(PstSpec.Inner));
            var outer = WithChild(PstSpec.Outer);
            Instance(PstSpec.Inner).transform.SetParent(outer.transform, false);
            SavePrefab(PstSpec.Outer, outer);
            SavePrefab(PstSpec.Shared, WithChild(PstSpec.Shared));
            SaveScene(PstSpec.SOuter, () => Instance(PstSpec.Outer));
            SaveScene(PstSpec.SShared1, () => Instance(PstSpec.Shared));
            SaveScene(PstSpec.SShared2, () => Instance(PstSpec.Shared));
            SaveScene(PstSpec.SPlain, () => new GameObject("Plain"));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>prefab の子の位置を、その step の版にする（今の版と違う prefab だけを保存し直す）。</summary>
        public static List<string> Apply(IReadOnlyDictionary<string, string> versions)
        {
            var changed = new List<string>();
            foreach (var prefab in PstSpec.Prefabs)
            {
                var path = PstSpec.Prefab(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var child = root.transform.Find(PstSpec.Child(prefab));
                    if (child == null) throw new InvalidOperationException(path + " has no " + PstSpec.Child(prefab));
                    var p = PstSpec.Position(prefab, versions[prefab]);
                    var target = new Vector3(p[0], p[1], p[2]);
                    if (child.localPosition == target) continue;
                    child.localPosition = target;
                    PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                    if (!saved) throw new InvalidOperationException("Prefab save failed: " + path);
                    changed.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return changed;
        }

        private static GameObject WithChild(string prefab)
        {
            var root = new GameObject(prefab);
            var child = new GameObject(PstSpec.Child(prefab));
            child.transform.SetParent(root.transform, false);
            var p = PstSpec.Position(prefab, PstSpec.V1);
            child.transform.localPosition = new Vector3(p[0], p[1], p[2]);
            return root;
        }

        private static GameObject Instance(string prefab) =>
            (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PstSpec.Prefab(prefab)));

        private static void SavePrefab(string name, GameObject root)
        {
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, PstSpec.Prefab(name), out var saved);
                if (!saved) throw new InvalidOperationException("Prefab save failed: " + name);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void SaveScene(string name, Func<GameObject> build)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            build();
            if (!EditorSceneManager.SaveScene(scene, PstSpec.Scene(name))) throw new InvalidOperationException("Scene save failed: " + name);
        }
    }

    // ───── build の process が書く構造化された事実（build-facts.json）。判定の process が読む。 ─────

    [Serializable]
    internal sealed class PstSceneState
    {
        public string scene = "";
        public string fileSha256 = "";
        public long fileTicks;
        public string metaSha256 = "";
        public long metaTicks;
        public string userData = "";
    }

    [Serializable]
    internal sealed class PstStepFacts
    {
        public string step = "";
        public string mode = "";
        public bool overlay;
        public List<string> changedPrefabs = new List<string>();
        public List<string> roots = new List<string>();
        public string identity = "";
        public bool success;
        public string summary = "";
        public string contentPath = "";
        public string historyCopy = "";
        public string historyName = "";
        public List<PstSceneState> before = new List<PstSceneState>();
        public List<PstSceneState> after = new List<PstSceneState>();
        public string inventoryBefore = "";
        public string inventoryAfter = "";
        public List<string> inventoryDiff = new List<string>();
        public bool generatedFolderAfter;
    }

    [Serializable]
    internal sealed class PstBuildFacts
    {
        public string runId = "";
        public string head = "";
        public List<PstStepFacts> steps = new List<PstStepFacts>();
        public List<string> problems = new List<string>();
        public bool fixtureDeleted;
        public bool finished;
    }

    // ContentLayout.json の読む部分（JsonUtility で読める形）。
    [Serializable] internal sealed class PstLayout { public PstSerializedFile[] SerializedFiles = Array.Empty<PstSerializedFile>(); public PstSceneId[] LoadableSceneIds = Array.Empty<PstSceneId>(); }
    [Serializable] internal sealed class PstSerializedFile { public int Index; public string ID = ""; public string ContentHash = ""; public string[] SourceAssets = Array.Empty<string>(); }
    [Serializable] internal sealed class PstSceneId { public string GUID = ""; public string Path = ""; public int SerializedFile; }
    [Serializable] internal sealed class PstSummary { public string BuildName = ""; }

    /// <summary>Unity の build 履歴（`Library/BuildHistory/&lt;build&gt;`）の読み取り。</summary>
    internal static class PstHistory
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
                if (File.Exists(summary) && JsonUtility.FromJson<PstSummary>(File.ReadAllText(summary)).BuildName == identity) return directory;
            }
            return null;
        }

        // Scene の serialized file の id（.cfid）を書いた event（`BuildPipeline::WriteSerializedFile`）の id の集合。
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
    internal static class PstInventory
    {
        public static Dictionary<string, string> Take()
        {
            var project = Path.GetDirectoryName(Application.dataPath)!;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var top in new[] { "Assets", "ProjectSettings" })
                foreach (var file in Directory.GetFiles(Path.Combine(project, top), "*", SearchOption.AllDirectories))
                    result[Path.GetRelativePath(project, file).Replace('\\', '/')] = PstSpec.Sha256(File.ReadAllBytes(file));
            return result;
        }

        public static string Write(Dictionary<string, string> inventory, string path)
        {
            var text = string.Join("\n", inventory.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value + " " + x.Key)) + "\n";
            File.WriteAllText(path, text);
            return PstSpec.Sha256(Encoding.UTF8.GetBytes(text));
        }

        public static List<string> Diff(Dictionary<string, string> before, Dictionary<string, string> after) =>
            before.Keys.Union(after.Keys).OrderBy(x => x, StringComparer.Ordinal)
                .Where(k => !before.TryGetValue(k, out var a) || !after.TryGetValue(k, out var b) || a != b)
                .Select(k => (before.ContainsKey(k) ? (after.ContainsKey(k) ? "changed " : "removed ") : "added ") + k).ToList();
    }

    /// <summary>
    /// 1 回の実行の record（JSONL）。root は `TestResults/pst/&lt;実行 id&gt;`（git 管理外）。process ごとに別の file に書く。
    /// 判定の process は、build の process が書いた実行 id の root を marker で確かめてから開く。
    /// </summary>
    internal sealed class PstRun
    {
        public static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string RunRoot => Path.Combine(RepoRoot, "TestResults", "pst");
        public static string BuildPointer => Path.Combine(RunRoot, "build-process.txt");
        public static string ArtifactsRoot => Path.Combine(RepoRoot, "TestResults", "bs3-fixture", "pst");
        private const string MarkerName = "pst-run.marker";

        private PstRun(string runId, string process, string stamp)
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

        public static PstRun Create()
        {
            var stamp = Stamp();
            var run = new PstRun(stamp, "build", stamp);
            if (System.IO.Directory.Exists(run.Directory)) throw new IOException("Run root already exists: " + run.Directory);
            System.IO.Directory.CreateDirectory(run.Directory);
            File.WriteAllText(Path.Combine(run.Directory, MarkerName), run.RunId);
            run.Write("process-start");
            return run;
        }

        public static PstRun Open(string process)
        {
            if (!File.Exists(BuildPointer)) throw new InvalidOperationException("The build process has not written its record.");
            var runId = File.ReadAllText(BuildPointer).Trim();
            var run = new PstRun(runId, process, Stamp());
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
