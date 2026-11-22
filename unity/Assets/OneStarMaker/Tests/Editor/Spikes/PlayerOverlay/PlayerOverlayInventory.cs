#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using E = OneStarMaker.Tests.Editor.Spikes.PlayerOverlay.PlayerOverlayExpected;

namespace OneStarMaker.Tests.Editor.Spikes.PlayerOverlay
{
    /// <summary>
    /// build の出力の棚卸しと digest、ContentLayout.json からの帰属の読み取り、fixture の事実の file と record の読み書き
    /// （HANDOFF §3）。第 1 process（作る）と launcher・Editor の対照（照合する）が使う。Unity の build には依らない。
    /// </summary>
    internal static class PlayerOverlayInventory
    {
        public const string BaseFolder = "base", OverlayFolder = "ovl", PlayerFolder = "ply", PlayerExeName = "Pov.exe";
        public const string FirstFactsFile = "first-facts.json", FixtureFactsFile = "fixture-facts.json";

        public static string Folder(string directory) => directory == E.BaseDirectory ? BaseFolder : directory == E.OverlayDirectory ? OverlayFolder : PlayerFolder;

        // IL2CPP が Player の横に作る、出荷しない folder（backup と debug 情報）。棚卸しから除く（HANDOFF AC1）。
        private static bool IsNotShipped(string name) => name.IndexOf("DontShip", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>directory の下の全 file（出荷しない folder を除く）の、相対 path・size・sha256。並びは path の順。</summary>
        public static OutputFile[] Inventory(string root) =>
            Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(x => Relative(root, x))
                .Where(x => !x.Split('/').Any(IsNotShipped))
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => new OutputFile { Name = x, Size = new FileInfo(Path.Combine(root, x)).Length, Sha256 = E.Sha256(File.ReadAllBytes(Path.Combine(root, x))) })
                .ToArray();

        public static List<string> NotShippedFolders(string root) =>
            Directory.Exists(root) ? Directory.GetDirectories(root).Select(Path.GetFileName).Where(x => x != null && IsNotShipped(x)).Select(x => x!).ToList() : new List<string>();

        private static string Relative(string root, string path) => path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');

        /// <summary>棚卸しの digest: path と sha256 の一覧の sha256（launcher と Editor の対照の照合に使う。HANDOFF §2 の (e)）。</summary>
        public static string Digest(IEnumerable<OutputFile> files) =>
            E.Sha256(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Name + " " + x.Sha256 + "\n"))));

        public static IEnumerable<string> Lines(IEnumerable<OutputFile> files) => files.Select(x => x.Name + " " + x.Size + " " + x.Sha256);

        /// <summary>3 つの出力（base、上書き、Player）の digest を、第 1 process と同じ形（"base=…"）で取り直す。</summary>
        public static string[] Digests(string runDirectory) =>
            new[] { E.BaseDirectory, E.OverlayDirectory, "player" }
                .Select(d => d + "=" + (Directory.Exists(Path.Combine(runDirectory, Folder(d))) ? Digest(Inventory(Path.Combine(runDirectory, Folder(d)))) : "missing"))
                .ToArray();

        // ───── ContentLayout.json からの帰属（S2-1 の帰属を写した） ─────

        /// <summary>
        /// 固定 GUID が指す source の serialized file。Scene は layout の LoadableSceneIds の GUID が指す file。他は path を SourceAssets から
        /// 引き、LoadableObjectIds に載る物（root の entry）は GUID が指す file とも一致することを確かめる。候補が 0・複数なら null。
        /// </summary>
        public static LayoutSerializedFile? Attribute(LayoutDocument layout, string path, string guid, out string detail)
        {
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

        /// <summary>serialized file の出力 file（名前の stem が ContentHash の file）。</summary>
        public static OutputFile? FileOf(LayoutSerializedFile sf, IEnumerable<OutputFile> files) =>
            files.FirstOrDefault(x => Stem(Path.GetFileName(x.Name)) == sf.ContentHash);

        private static string Stem(string name) => name.Contains('.') ? name.Substring(0, name.IndexOf('.')) : name;

        // ───── fixture の事実の file（runner が読む key と名前。期待値は入れない） ─────

        public static PovFixtureFacts FixtureFacts() => new PovFixtureFacts
        {
            sceneKeep = E.SceneKeep, sceneEdit = E.SceneEdit, sharedPrefab = E.SharedPrefab, editedTex = E.EditedTex,
            keeper = E.Keeper, markerPrefix = E.MarkerPrefix, bootScene = E.BootScene, buildNamePrefix = E.BuildName(""),
            fixtureObjectNames = E.FixtureObjectNames,
            counted = E.Counted, countKinds = E.Counted.Select(E.KindOf).ToArray(), countObjectNames = E.Counted.Select(E.ObjectNameOf).ToArray(),
            slots = E.Slots.Select(x => x.Key + "=" + string.Join(",", x.Value)).ToArray(),
            textureOf = E.TextureOf.Select(x => x.Key + "=" + x.Value).ToArray(),
            sharedShader = E.SharedShader, keepEditedSlot = E.KeepEditedSlot,
            probeTypes = E.ProbeTypes.Select(x => x + ", " + E.ProbeAssembly).ToArray(),
        };

        // ───── record の読み取り ─────

        /// <summary>
        /// JSONL の record を行ごとに読む。途中で切れた最後の行（落ちた process）は読めた所まで残し、読めない行は捨てずに
        /// type を "unreadable" にする（判定は最後の行の位置を見るので、行の数を変えない）。
        /// </summary>
        public static List<PovLine> ReadRecord(string path)
        {
            var lines = new List<PovLine>();
            if (!File.Exists(path)) return lines;
            foreach (var text in File.ReadAllLines(path))
            {
                if (text.Length == 0) continue;
                var line = new PovLine();
                try { JsonUtility.FromJsonOverwrite(text, line); }
                catch (ArgumentException) { line = new PovLine { type = "unreadable", message = text }; }
                lines.Add(line);
            }
            return lines;
        }

        public static void AppendJson(string path, object value)
        {
            File.AppendAllText(path, JsonUtility.ToJson(value) + "\n");
        }
    }

    // ───── ContentLayout.json（Version 2）の、帰属に使う欄だけの写し ─────

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

    [Serializable] internal sealed class LayoutObjectId { public string GUID = ""; public long LFID; public int SerializedFile = -1; }
    [Serializable] internal sealed class LayoutSceneId { public string GUID = ""; public string Path = ""; public int SerializedFile = -1; }
    [Serializable] internal sealed class OutputFile { public string Name = ""; public long Size; public string Sha256 = ""; }

    /// <summary>
    /// runner が読む fixture の事実（PlayerOverlayRunner の同名の型と同じ欄。名前は JSON の key なので、片方だけを変えない）。
    /// key と名前だけで、版・pixel・probe の期待の列は入れない（HANDOFF §2 の「生の値と判定の分担」）。
    /// </summary>
    [Serializable] internal sealed class PovFixtureFacts
    {
        public string sceneKeep = "", sceneEdit = "", sharedPrefab = "", editedTex = "", keeper = "", markerPrefix = "", bootScene = "", buildNamePrefix = "";
        public string[] fixtureObjectNames = Array.Empty<string>();
        public string[] counted = Array.Empty<string>(), countKinds = Array.Empty<string>(), countObjectNames = Array.Empty<string>();
        public string[] slots = Array.Empty<string>(), textureOf = Array.Empty<string>();
        public string sharedShader = "";
        public int keepEditedSlot;
        public string[] probeTypes = Array.Empty<string>();
    }

    /// <summary>両 directory の、ある source の serialized file（layout の帰属と出力の棚卸し）。見つからなければ Found が偽。</summary>
    [Serializable] internal sealed class FileFact
    {
        public string Item = "";
        public bool FoundBase, FoundOverlay;
        public string IdBase = "", IdOverlay = "", NameBase = "", NameOverlay = "", ShaBase = "", ShaOverlay = "";
    }

    /// <summary>第 1 process の事実（HANDOFF AC1）。判定の写像の入力で、launcher と Editor の対照が照合に使う。</summary>
    [Serializable] internal sealed class FirstFacts
    {
        public string RunId = "", Head = "", ProjectVersion = "", EditorVersion = "";
        public bool TargetOk, BuildsSucceeded, PackedAssetsRead, ProbeAssemblyInPlayer, AddressablesDataInPlayer, LibraryHadSpikeBuild;
        public string PlayerBuildGuid = "";
        public string[] Digests = Array.Empty<string>();
        public FileFact ScriptFile = new FileFact();
        public List<string> ScriptSourcesBase = new List<string>(), ScriptSourcesOverlay = new List<string>();
        public List<FileFact> Files = new List<FileFact>();
        public List<string> PackedFixtureAssets = new List<string>();
        public List<string> ScriptingAssemblies = new List<string>();
        public List<string> BuildOrder = new List<string>();
        public List<string> Problems = new List<string>();
    }

    /// <summary>launcher が書く、1 つの Player の process の起動と終わり方（観測ではない。HANDOFF §2 の record）。</summary>
    [Serializable] internal sealed class LaunchEntry
    {
        public string Label = "", Round = "", StartedAt = "", EndedAt = "", StopReason = "", RecordFile = "", LogFile = "";
        public int Replicate, Pid, ExitCode;
        public bool ExitedByItself;
        public string[] Environment = Array.Empty<string>(), RemovedVariables = Array.Empty<string>();
    }

    /// <summary>判定の写像に渡す 1 つの process（Player か Editor の対照）。</summary>
    internal sealed class ProcessInput
    {
        public LaunchEntry Launch = new LaunchEntry();
        public List<PovLine> Record = new List<PovLine>();
        public string[] PlayerLog = Array.Empty<string>();
    }

    /// <summary>
    /// runner が書く record の 1 行。runner は行の種類ごとに必要な欄だけを書き、ここは全ての種類の欄の和（JSON の key なので、
    /// PlayerOverlayRecord が書く名前と揃える）。欄の意味は PlayerOverlayRunner の各行の書き手のコメントにある。
    /// </summary>
    [Serializable] internal sealed class PovLine
    {
        public string at = "", type = "", stage = "", round = "", label = "";
        public int seq;
        // header
        public string runId = "", head = "", platform = "", backend = "", unityVersion = "", buildGuid = "", graphics = "";
        public bool isEditor, firstPlay, domainReloadDisabled;
        public int pid, limitSeconds;
        public string[] order = Array.Empty<string>();
        // p0・p4
        public int directories = -1, fixtureObjects = -1;
        public string[] scenes = Array.Empty<string>(), fixtureCounts = Array.Empty<string>(), roots = Array.Empty<string>(), probeTypes = Array.Empty<string>();
        // op-start・op-result
        public string op = "", dir = "", key = "", failure = "", exceptionType = "", message = "", rootIdentity = "", entityId = "";
        public bool ok, handleValid;
        public string[] listed = Array.Empty<string>();
        // 観測
        public bool sceneIdsEqual, loadableIdsEqual;
        public string[] sceneIds = Array.Empty<string>(), loadableIds = Array.Empty<string>();
        public string marker = "", pixels = "", item = "", objectName = "", point = "", objectId = "";
        public string[] components = Array.Empty<string>(), reached = Array.Empty<string>(), enumerated = Array.Empty<string>();
        public string[] materials = Array.Empty<string>(), textures = Array.Empty<string>();
        public int generation, count = -1;
        public bool renderer;
        // log・end
        public string logType = "", abortedAt = "";
        public bool mainThread, completed;
    }
}
