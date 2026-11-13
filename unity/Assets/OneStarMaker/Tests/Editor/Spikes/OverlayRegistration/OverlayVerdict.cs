#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

// 取得した build への上書きの登録のスパイク（docs/handoff/CONTENT_OVERLAY_REGISTRATION_SPIKE.md）の、fixture の定義と判定の写像。
// 入力は文字列・数値・真偽だけで、Unity の型も製品の型も受け取らない。語と判定定義は HANDOFF §2 で実測の前に固定したもので、
// ここで意味を変えない。判定 C は record の観測の行から独立に確かめ、この出力は補助である（HANDOFF §2 の「test の合否と判定は別」）。
// 実験 code であり、Phase D で git 履歴へ退避して削除する。
namespace OneStarMaker.Tests.Editor.Spikes.OverlayRegistration
{
    /// <summary>probe の component 1 つの期待（fixture が付けた順の 1 位置）。</summary>
    internal sealed class ProbeExpectation
    {
        public ProbeExpectation(string fullName, string tag, int value) { FullName = fullName; Tag = tag; Value = value; }
        public string FullName { get; }
        public string Tag { get; }
        public int Value { get; }
    }

    /// <summary>
    /// fixture の定義（HANDOFF §3）。期待値の出所はここだけで、単独の基準（AC2）や測る対象の Play から採らない（HANDOFF §2 の (e)）。
    /// OverlayFixture はこの値で asset を作り、OverlayRounds と OverlayVerdict はこの値と照合する。
    /// </summary>
    internal static class OverlayExpected
    {
        public const string ProbeNamespace = "OneStarMaker.Tests.Spikes.OverlayProbes";
        public const string BaseOnly = ProbeNamespace + ".ProbeBaseOnly", OverlayOnly = ProbeNamespace + ".ProbeOverlayOnly", Both = ProbeNamespace + ".ProbeBoth";
        public static readonly string[] ProbeTypes = { BaseOnly, OverlayOnly, Both };

        // 物の名前（root の logical key と、Play 中に列挙する Object.name）。shader の Object.name は shader の source の名前。
        public const string SceneKeep = "SceneKeep", SceneEdit = "SceneEdit", SharedPrefab = "SharedPrefab", EditedTex = "EditedTex",
            SharedTex = "SharedTex", SharedMat = "SharedMat", SharedShader = "SharedShader", EditedMat = "EditedMat", KeepTex = "KeepTex", KeepMat = "KeepMat";
        public const string ShaderObjectName = "OsmSpikeOvl/SharedShader";
        public const string Keeper = "Keeper", MarkerV1 = "Marker_v1", MarkerV2 = "Marker_v2";
        public const string V1 = "v1", V2 = "v2", UnknownVersion = "unknown";

        // Q3 の対象（HANDOFF §2 の Q3）。両方の directory にある物は 2 以下、上書きに無い物は 1。
        public static readonly string[] CountedAtMostTwo = { SharedMat, SharedTex, SharedShader, EditedMat, EditedTex };
        public static readonly string[] CountedOne = { SharedPrefab, KeepTex };
        public static IEnumerable<string> Counted => CountedAtMostTwo.Concat(CountedOne);

        // EditedTex は v1 と v2 で違う単色の、読める小さな texture（HANDOFF §2 の「版」）。
        public const int EditedTexSize = 8;
        public static byte[] EditedColor(string version) => version == V1 ? new byte[] { 200, 40, 40, 255 } : new byte[] { 40, 40, 200, 255 };

        // probe の値は fixture の全ての component で互いに違える（同じ GameObject の 2 つも違う。HANDOFF §2 の「正しい class」）。
        // 並びは fixture が付けた順（BaseOnly か OverlayOnly が先、Both が後）。
        public static IReadOnlyList<ProbeExpectation> Probes(string item, string version)
        {
            switch (item)
            {
                case SceneKeep: return new[] { new ProbeExpectation(BaseOnly, "keep.baseOnly", 101), new ProbeExpectation(Both, "keep.both", 102) };
                case SharedPrefab: return new[] { new ProbeExpectation(BaseOnly, "prefab.baseOnly", 401), new ProbeExpectation(Both, "prefab.both", 402) };
                case SceneEdit when version == V1: return new[] { new ProbeExpectation(BaseOnly, "edit.v1.baseOnly", 201), new ProbeExpectation(Both, "edit.v1.both", 202) };
                case SceneEdit when version == V2: return new[] { new ProbeExpectation(OverlayOnly, "edit.v2.overlayOnly", 301), new ProbeExpectation(Both, "edit.v2.both", 302) };
                default: throw new ArgumentException("No probes are defined for " + item + " " + version);
            }
        }

        public static string MarkerName(string version) => version == V1 ? MarkerV1 : MarkerV2;
        public static string VersionOfMarker(string objectName) => objectName == MarkerV1 ? V1 : objectName == MarkerV2 ? V2 : UnknownVersion;

        /// <summary>EditedTex の全 pixel を RGBA の順に並べた bytes の sha256（版の判定の基準）。</summary>
        public static string PixelSha(string version)
        {
            var color = EditedColor(version);
            var bytes = new byte[EditedTexSize * EditedTexSize * 4];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = color[i % 4];
            return Sha256(bytes);
        }

        public static string VersionOfPixels(string sha) => sha == PixelSha(V1) ? V1 : sha == PixelSha(V2) ? V2 : UnknownVersion;

        public static string Sha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(x => x.ToString("x2")));
        }
    }

    // ───── 第 1 process の事実（AC1） ─────

    /// <summary>両 directory の、ある source の serialized file（layout の帰属と出力の棚卸し）。見つからなければ Found が偽。</summary>
    [Serializable] internal sealed class FileFact
    {
        public string Item = "";
        public bool FoundBase, FoundOverlay;
        public string IdBase = "", IdOverlay = "", NameBase = "", NameOverlay = "", ShaBase = "", ShaOverlay = "";
    }

    [Serializable] internal sealed class FirstFacts
    {
        public string RunId = "", Head = "";
        public bool BuildsSucceeded;
        public string[] Digests = Array.Empty<string>();
        // MonoScript をまとめた file。Sources は、その file の layout の SourceAssets に、.meta の GUID から引いた path が載った probe の完全名。
        public FileFact ScriptFile = new FileFact();
        public List<string> ScriptSourcesBase = new List<string>();
        public List<string> ScriptSourcesOverlay = new List<string>();
        public List<FileFact> Files = new List<FileFact>();
        public List<string> Problems = new List<string>();
    }

    // ───── 第 2 process の観測（AC2〜AC7） ─────

    [Serializable] internal sealed class ComponentObservation
    {
        // GetComponents<Component>() の 1 要素。== null（Missing）なら FullName と Json は空。
        public bool Missing;
        public string FullName = "";
        // MonoBehaviour だけ JsonUtility.ToJson の値。それ以外は空。
        public string Json = "";
    }

    [Serializable] internal sealed class ProbeObservation
    {
        public string Item = "";
        public bool Found;
        public string ObjectName = "";
        public List<ComponentObservation> Components = new List<ComponentObservation>();
    }

    /// <summary>登録か load の 1 回の呼び出し。Call は "base:register"・"overlay:SceneEdit" の形（directory:物）。</summary>
    [Serializable] internal sealed class CallObservation
    {
        public string Call = "";
        public bool Succeeded;
        // SceneEdit は root の GameObject の名前から、EditedTex は pixel の sha256 から読んだ版。読まない呼び出しは空。
        public string Version = "";
    }

    [Serializable] internal sealed class CountObservation
    {
        public string Item = "";
        public List<string> LoadIds = new List<string>();
        public List<string> EnumeratedIds = new List<string>();
    }

    [Serializable] internal sealed class CheckpointObservation
    {
        public string Step = "";
        public List<string> Broken = new List<string>();
    }

    [Serializable] internal sealed class RoundObservation
    {
        public string Round = "";
        // process の中で何回目の Play か（0 がその process の最初の Play）。
        public int PlayIndex = -1;
        public bool Ended, CleanedUp, SourceInAssetDatabase, RootsMatched;
        public int DirectoriesAtStart = -1, LeftoverAtStart = -1;
        public List<string> RegisterOrder = new List<string>();
        public List<CallObservation> Calls = new List<CallObservation>();
        public List<ProbeObservation> Probes = new List<ProbeObservation>();
        public List<CountObservation> Counts = new List<CountObservation>();
        // AC3 と AC6-read: 両方の root の SceneEdit の LoadableSceneId と、EditedTex の LoadableObjectId が等しいか。
        public bool SceneIdsEqual, LoadableIdsEqual;
        // AC4 と AC6-class の観測（答えには使わない）: SceneKeep の EditedMat の mainTexture の版。
        public string KeepEditedTexVersion = "";
        // AC5: (1)〜(5) の確認点、(6) の解除の後の登録の数、Error・Exception の log の数（(1)〜(5) と (6) を分ける）。
        public List<CheckpointObservation> Checkpoints = new List<CheckpointObservation>();
        public int DirectoriesAfterUnregister = -1;
        public int ErrorLogs, ErrorLogsInRelease;
    }

    [Serializable] internal sealed class ProcessObservation
    {
        public string Process = "", RunId = "", Head = "";
        public bool InputMatched, DomainReloadDisabled, TargetOk, Finished;
        public List<string> HarnessProblems = new List<string>();
        public List<RoundObservation> Rounds = new List<RoundObservation>();
    }

    internal sealed class Verdict
    {
        public string Outcome = "";
        public List<string> Reasons = new List<string>();
        public List<string> Answers = new List<string>();
        public string Spike = "";
    }

    internal static class OverlayVerdict
    {
        public const string Go = "GO", Undecided = "UNDECIDED";
        public const string Base = "base", Overlay = "overlay";
        public const string Strong = "strong", Weak = "weak", NotHeld = "not held";
        public const string Correct = "correct", Wrong = "wrong", Fits = "fits", Exceeds = "exceeds", Intact = "intact", Broken = "broken";
        public const string Yes = "YES", ConditionalYes = "CONDITIONAL YES", No = "NO";
        public const string CannotPrefix = "cannot: ";

        // round（HANDOFF §2.2 の前文）。
        public const string Ac4 = "AC4", Ac5 = "AC5", Ac3 = "AC3", Ac6Class = "AC6-class", Ac6Read = "AC6-read", Ac2Base = "AC2-base", Ac2Overlay = "AC2-overlay";
        public static readonly string[] MainRounds = { Ac4, Ac5, Ac3, Ac6Class, Ac6Read };
        public const string ClassFirst = "ClassFirst", LifetimeFirst = "LifetimeFirst", ReadFirst = "ReadFirst",
            ControlClassFirst = "ControlClassFirst", ControlReadFirst = "ControlReadFirst", BaseAloneFirst = "BaseAloneFirst", OverlayAloneFirst = "OverlayAloneFirst";
        public static readonly string[] RequiredProcesses = { ClassFirst, LifetimeFirst, ReadFirst, ControlClassFirst, ControlReadFirst };
        public const string StepRelease = "(6)";
        public static readonly string[] Ac5Steps = { "(1)", "(2)", "(3)", "(4)", "(5)" };

        private static readonly string[] NonProbeTypes = { "UnityEngine.Transform", "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer" };

        /// <summary>process の round の並び。名前の round を最初に、残りを決めた順に、AC2 を最後に（HANDOFF §2.2 の前文）。</summary>
        public static string[] Plan(string process)
        {
            if (process == BaseAloneFirst) return new[] { Ac2Base };
            if (process == OverlayAloneFirst) return new[] { Ac2Overlay };
            var index = Array.IndexOf(RequiredProcesses, process);
            if (index < 0) throw new ArgumentException("Unknown process " + process);
            var first = MainRounds[index];
            return new[] { first }.Concat(MainRounds.Where(x => x != first)).Concat(new[] { Ac2Base, Ac2Overlay }).ToArray();
        }

        /// <summary>round の `Register` の順。主の順は base → 上書き、対照の順は上書き → base。</summary>
        public static string[] RegisterOrder(string round) => round switch
        {
            Ac2Base => new[] { Base },
            Ac2Overlay => new[] { Overlay },
            Ac6Class or Ac6Read => new[] { Overlay, Base },
            _ => new[] { Base, Overlay },
        };

        /// <summary>P1〜P3 と build の成否（HANDOFF §2 の「危険な条件の前提」）。成り立たない理由の列で、空なら成り立つ。</summary>
        public static List<string> Preconditions(FirstFacts f)
        {
            var reasons = new List<string>();
            if (!f.BuildsSucceeded) reasons.Add("AC1: builds did not succeed");
            reasons.AddRange(f.Problems.Select(x => "AC1: " + x));
            // P1 の根拠は、MonoScript をまとめた file の layout の SourceAssets の一覧だけ。sha256 の差だけでは満たさない（外部レビュー F-1）。
            var s = f.ScriptFile;
            if (!s.FoundBase || !s.FoundOverlay) reasons.Add("P1: script file is not in both directories");
            else
            {
                if (s.IdBase != s.IdOverlay) reasons.Add("P1: script file ids differ");
                if (s.ShaBase == s.ShaOverlay) reasons.Add("P1: script file sha256 is the same");
            }
            void Sources(string dir, List<string> list, string present, string absent)
            {
                foreach (var t in new[] { present, OverlayExpected.Both })
                    if (!list.Contains(t)) reasons.Add("P1: " + dir + " script list lacks " + t);
                if (list.Contains(absent)) reasons.Add("P1: " + dir + " script list has " + absent);
            }
            Sources(Base, f.ScriptSourcesBase, OverlayExpected.BaseOnly, OverlayExpected.OverlayOnly);
            Sources(Overlay, f.ScriptSourcesOverlay, OverlayExpected.OverlayOnly, OverlayExpected.BaseOnly);

            foreach (var item in new[] { OverlayExpected.SceneEdit, OverlayExpected.EditedTex })
            {
                var x = f.Files.FirstOrDefault(y => y.Item == item);
                if (x == null || !x.FoundBase || !x.FoundOverlay) reasons.Add("P2: " + item + " is not in both directories");
                else if (x.IdBase != x.IdOverlay || x.ShaBase == x.ShaOverlay) reasons.Add("P2: " + item + " is not the same id with different sha256");
            }
            foreach (var item in new[] { OverlayExpected.SharedTex, OverlayExpected.SharedMat, OverlayExpected.SharedShader, OverlayExpected.EditedMat })
            {
                var x = f.Files.FirstOrDefault(y => y.Item == item);
                if (x == null || !x.FoundBase || !x.FoundOverlay) reasons.Add("P3: " + item + " is not in both directories");
                else if (x.IdBase != x.IdOverlay || x.NameBase != x.NameOverlay || x.ShaBase != x.ShaOverlay) reasons.Add("P3: " + item + " is not the same file");
            }
            return reasons;
        }

        /// <summary>
        /// HANDOFF §2 の「正しい class」。Missing が無く、probe でない物を除いた列が期待の列と位置ごとに一致する
        /// （完全名と、JsonUtility の値の `_tag`・`_value`）。型の集合が同じでも位置が入れ替われば正しくない。
        /// </summary>
        public static bool ClassesCorrect(IReadOnlyList<ComponentObservation> observed, IReadOnlyList<ProbeExpectation> expected, out string why)
        {
            var missing = observed.Count(x => x.Missing);
            if (missing != 0) { why = "missing " + missing; return false; }
            var probes = observed.Where(x => !NonProbeTypes.Contains(x.FullName)).ToArray();
            if (probes.Length != expected.Count) { why = "probe count " + probes.Length + " / " + expected.Count; return false; }
            for (var i = 0; i < probes.Length; i++)
            {
                var e = expected[i];
                if (probes[i].FullName != e.FullName) { why = "position " + i + " is " + probes[i].FullName + ", expected " + e.FullName; return false; }
                if (!ReadValue(probes[i].Json, out var tag, out var value) || tag != e.Tag || value != e.Value)
                { why = "position " + i + " value " + probes[i].Json + ", expected " + e.Tag + "/" + e.Value; return false; }
            }
            why = "";
            return true;
        }

        private static bool ReadValue(string json, out string tag, out int value)
        {
            var t = Regex.Match(json, "\"_tag\":\"((?:[^\"\\\\]|\\\\.)*)\"");
            var v = Regex.Match(json, "\"_value\":(-?\\d+)");
            tag = t.Success ? Regex.Unescape(t.Groups[1].Value) : "";
            value = v.Success ? int.Parse(v.Groups[1].Value) : 0;
            return t.Success && v.Success;
        }

        /// <summary>HANDOFF §2 の「実体の数」の整合の規則。load の結果の相異なる id の数で、列挙と集合が一致する時だけ。答えにできない時は null。</summary>
        public static int? Count(CountObservation? o)
        {
            if (o == null || o.LoadIds.Count == 0 || o.LoadIds.Contains("null")) return null;
            var loaded = new HashSet<string>(o.LoadIds, StringComparer.Ordinal);
            return loaded.SetEquals(o.EnumeratedIds) ? loaded.Count : (int?)null;
        }

        /// <summary>HANDOFF §2 の判定定義。上から未判定の理由を集め、無ければ各問とスパイクの答えを返す。</summary>
        public static Verdict Judge(FirstFacts first, IReadOnlyList<ProcessObservation> processes)
        {
            var reasons = Preconditions(first);
            foreach (var name in RequiredProcesses)
                if (!processes.Any(x => x.Process == name && x.Finished)) reasons.Add(name + ": no finished record");
            // 答えは round ごとに、全ての process の答えを集める（照合は全件、答えは最初の Play の回から）。
            var answers = new Dictionary<string, List<(ProcessObservation p, RoundObservation r, Dictionary<string, string> a)>>();
            foreach (var p in processes)
            {
                var label = p.Process;
                if (p.RunId != first.RunId || p.Head != first.Head || !p.InputMatched) reasons.Add(label + ": input is not the first process output of the same run");
                if (!p.DomainReloadDisabled) reasons.Add(label + ": Enter Play Mode is not Domain Reload disabled");
                if (!p.TargetOk) reasons.Add(label + ": target is not StandaloneWindows64");
                if (!p.Finished) reasons.Add(label + ": process did not finish");
                reasons.AddRange(p.HarnessProblems.Select(x => label + ": harness: " + x));
                string[] plan;
                try { plan = Plan(p.Process); }
                catch (ArgumentException) { reasons.Add(label + ": unknown process"); continue; }
                var observed = p.Rounds.OrderBy(x => x.PlayIndex).Select(x => x.Round).ToArray();
                if (!observed.SequenceEqual(plan) || p.Rounds.Select(x => x.PlayIndex).Distinct().Count() != p.Rounds.Count)
                    reasons.Add(label + ": rounds " + string.Join(",", observed) + " differ from the plan " + string.Join(",", plan));
                foreach (var r in p.Rounds)
                {
                    var a = RoundAnswer(r, processes, reasons, label + " " + r.Round);
                    if (a == null) continue;
                    if (!answers.TryGetValue(r.Round, out var list)) answers[r.Round] = list = new List<(ProcessObservation, RoundObservation, Dictionary<string, string>)>();
                    list.Add((p, r, a));
                }
            }
            foreach (var round in MainRounds)
            {
                if (!answers.TryGetValue(round, out var list)) { reasons.Add(round + ": no answer"); continue; }
                if (!list.Any(x => x.r.PlayIndex == 0)) reasons.Add(round + ": no process where it is the first Play");
                var distinct = list.Select(x => Key(x.a)).Distinct().ToArray();
                if (distinct.Length > 1)
                    reasons.Add(round + ": answers differ across processes (" + string.Join(" / ", list.Select(x => x.p.Process + "#" + x.r.PlayIndex + "=" + Key(x.a))) + ")");
            }
            if (reasons.Count != 0) return new Verdict { Outcome = Undecided, Reasons = reasons };

            Dictionary<string, string> From(string round) => answers[round].First(x => x.r.PlayIndex == 0).a;
            var q1 = From(Ac3)["Q1"];
            var q2 = From(Ac4)["Q2"];
            var q3a = From(Ac4)["Q3"];
            var q3b = From(Ac5)["Q3"];
            var q3 = q3a == Fits && q3b == Fits ? Fits : q3a == Fits ? q3b : q3a;
            var lifetime = answers[Ac5].First(x => x.r.PlayIndex == 0).r;
            var q4 = From(Ac5)["Q4"];
            if (q4 == Intact && lifetime.ErrorLogs + lifetime.ErrorLogsInRelease != 0) q4 = Intact + " (log)";
            var verdict = new Verdict { Outcome = Go };
            verdict.Answers.Add("Q1: " + q1);
            verdict.Answers.Add("Q2: " + q2);
            verdict.Answers.Add("Q3: " + q3 + " (AC4 " + q3a + ", AC5 (1) " + q3b + ")");
            verdict.Answers.Add("Q4: " + q4);
            var rest = q2 == Correct && q3 == Fits && q4.StartsWith(Intact, StringComparison.Ordinal);
            verdict.Spike = rest && q1 == Strong ? Yes : rest && q1 == Weak ? ConditionalYes : No;
            // 対照の順（答えには使わない。HANDOFF AC6）。
            var c1 = From(Ac6Read)["Q1"];
            var c2 = From(Ac6Class)["Q2"];
            var c3 = From(Ac6Class)["Q3"];
            verdict.Answers.Add("control Q1: " + c1 + (c1 == q1 ? " (same as main)" : " (differs from main)"));
            verdict.Answers.Add("control Q2: " + c2 + (c2 == q2 ? " (same as main)" : " (differs from main)"));
            verdict.Answers.Add("control Q3 (AC4 counts): " + c3 + (c3 == q3a ? " (same as main)" : " (differs from main)"));
            verdict.Answers.Add("ids equal: scene " + answers[Ac3].First(x => x.r.PlayIndex == 0).r.SceneIdsEqual
                                + ", loadable " + answers[Ac3].First(x => x.r.PlayIndex == 0).r.LoadableIdsEqual);
            return verdict;
        }

        private static string Key(Dictionary<string, string> a) => string.Join(";", a.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + "=" + x.Value));

        /// <summary>1 つの round の答え（問ごと）。round が答えに使えなければ reasons に足して null。AC2 は基準の確認だけで、空の答えを返す。</summary>
        internal static Dictionary<string, string>? RoundAnswer(RoundObservation r, IReadOnlyList<ProcessObservation> processes, List<string> reasons, string label)
        {
            var before = reasons.Count;
            if (!r.Ended) reasons.Add(label + ": no end line");
            if (!r.CleanedUp) reasons.Add(label + ": round cleanup did not finish");
            if (r.DirectoriesAtStart != 0) reasons.Add(label + ": directories registered at round start");
            if (r.LeftoverAtStart != 0) reasons.Add(label + ": objects with fixture names at round start");
            if (r.SourceInAssetDatabase) reasons.Add(label + ": fixture source in AssetDatabase");
            if (!r.RegisterOrder.SequenceEqual(RegisterOrder(r.Round))) reasons.Add(label + ": Register order " + string.Join(">", r.RegisterOrder) + " is not the round definition");
            if (!r.RootsMatched) reasons.Add(label + ": a handle's root BuildIdentity does not match its directory");
            if (reasons.Count != before) return null;

            if (r.Round == Ac2Base || r.Round == Ac2Overlay) return Baseline(r, reasons, label) ? new Dictionary<string, string>() : null;

            // 2 つの登録の下での登録・load の失敗は、同じ呼び出しが単独の登録の最初の Play で成功した時だけ答えにする（HANDOFF §2 の GO、AC7 の補足）。
            var failed = r.Calls.Where(x => !x.Succeeded).Select(x => x.Call).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (failed.Length != 0)
            {
                foreach (var call in failed)
                    if (!SucceededAloneFirst(call, processes)) reasons.Add(label + ": " + call + " failed and has no success in a single-registration first Play");
                if (reasons.Count != before) return null;
                var cannot = CannotPrefix + string.Join(",", failed);
                return Questions(r.Round).ToDictionary(x => x, _ => cannot);
            }

            switch (r.Round)
            {
                case Ac3:
                case Ac6Read:
                    var q1 = new[] { Q1Item(r, OverlayExpected.SceneEdit, r.SceneIdsEqual, reasons, label), Q1Item(r, OverlayExpected.EditedTex, r.LoadableIdsEqual, reasons, label) };
                    // AC7: 両方の root から load して保持した EditedTex の数え方が整合する。
                    if (Count(r.Counts.FirstOrDefault(x => x.Item == OverlayExpected.EditedTex)) == null) reasons.Add(label + ": EditedTex count does not follow the consistency rule");
                    if (reasons.Count != before) return null;
                    return new Dictionary<string, string> { ["Q1"] = q1.Contains(NotHeld) ? NotHeld : q1.Contains(Weak) ? Weak : Strong };
                case Ac4:
                case Ac6Class:
                    var q2 = Q2(r, reasons, label);
                    var q3 = Q3(r, reasons, label);
                    return reasons.Count != before ? null : new Dictionary<string, string> { ["Q2"] = q2!, ["Q3"] = q3! };
                case Ac5:
                    var q3l = Q3(r, reasons, label);
                    if (r.Checkpoints.Select(x => x.Step).SequenceEqual(Ac5Steps) == false) reasons.Add(label + ": checkpoints " + string.Join(",", r.Checkpoints.Select(x => x.Step)));
                    if (reasons.Count != before) return null;
                    var broken = r.Checkpoints.Where(x => x.Broken.Count != 0).Select(x => x.Step + " " + string.Join(",", x.Broken)).ToList();
                    if (r.DirectoriesAfterUnregister != 0) broken.Add(StepRelease + " directories after unregister " + r.DirectoriesAfterUnregister);
                    return new Dictionary<string, string> { ["Q4"] = broken.Count == 0 ? Intact : Broken + " (" + string.Join("; ", broken) + ")", ["Q3"] = q3l! };
                default:
                    reasons.Add(label + ": unknown round");
                    return null;
            }
        }

        private static IEnumerable<string> Questions(string round) => round switch
        {
            Ac3 or Ac6Read => new[] { "Q1" },
            Ac4 or Ac6Class => new[] { "Q2", "Q3" },
            _ => new[] { "Q4", "Q3" },
        };

        private static bool SucceededAloneFirst(string call, IReadOnlyList<ProcessObservation> processes)
        {
            var dir = call.Split(':')[0];
            var (process, round) = dir == Base ? (BaseAloneFirst, Ac2Base) : (OverlayAloneFirst, Ac2Overlay);
            var alone = processes.Where(p => p.Process == process && p.Finished).SelectMany(p => p.Rounds).Where(x => x.Round == round && x.PlayIndex == 0).ToArray();
            return alone.Length != 0 && alone.All(x => x.Ended && x.Calls.Any(c => c.Call == call && c.Succeeded));
        }

        // Q1 の 1 つの物の答え。両方の root の id が等しい時、2 つの要求は 1 つなので「弱」は語として読めない（Phase A へ）。
        private static string Q1Item(RoundObservation r, string item, bool idsEqual, List<string> reasons, string label)
        {
            var b = r.Calls.FirstOrDefault(x => x.Call == Base + ":" + item);
            var o = r.Calls.FirstOrDefault(x => x.Call == Overlay + ":" + item);
            if (b == null || o == null) { reasons.Add(label + ": " + item + " is not requested from both roots"); return ""; }
            if (o.Version == OverlayExpected.V1) return NotHeld;
            if (o.Version == OverlayExpected.V2 && b.Version == OverlayExpected.V2) return Strong;
            if (o.Version == OverlayExpected.V2 && b.Version == OverlayExpected.V1)
            {
                if (idsEqual) reasons.Add(label + ": " + item + " is weak although both ids are equal");
                return Weak;
            }
            reasons.Add(label + ": " + item + " versions base=" + b.Version + " overlay=" + o.Version + " are not in the defined terms");
            return "";
        }

        // Q2: SceneKeep、上書き版の SceneEdit、base の root の SharedPrefab の全 probe が正しい class。
        // AC4 の SceneEdit が上書き版でない時は「上書き版の SceneEdit」を観測できないので、語で読めない（Phase A へ）。
        private static string? Q2(RoundObservation r, List<string> reasons, string label)
        {
            var wrong = new List<string>();
            foreach (var item in new[] { OverlayExpected.SceneKeep, OverlayExpected.SceneEdit, OverlayExpected.SharedPrefab })
            {
                var p = r.Probes.FirstOrDefault(x => x.Item == item);
                if (p == null || !p.Found) { reasons.Add(label + ": " + item + " probe object was not found"); continue; }
                var version = item == OverlayExpected.SceneEdit ? OverlayExpected.VersionOfMarker(p.ObjectName) : "";
                if (item == OverlayExpected.SceneEdit && version != OverlayExpected.V2) { reasons.Add(label + ": SceneEdit is " + p.ObjectName + ", not the overlay version"); continue; }
                if (!ClassesCorrect(p.Components, OverlayExpected.Probes(item, version), out var why)) wrong.Add(item + " " + why);
            }
            return wrong.Count == 0 ? Correct : Wrong + " (" + string.Join("; ", wrong) + ")";
        }

        private static string? Q3(RoundObservation r, List<string> reasons, string label)
        {
            var over = new List<string>();
            foreach (var item in OverlayExpected.Counted)
            {
                var n = Count(r.Counts.FirstOrDefault(x => x.Item == item));
                if (n == null) { reasons.Add(label + ": " + item + " count does not follow the consistency rule"); continue; }
                var limit = OverlayExpected.CountedOne.Contains(item) ? 1 : 2;
                if (n > limit || (limit == 1 && n != 1)) over.Add(item + "=" + n);
            }
            return over.Count == 0 ? Fits : Exceeds + " (" + string.Join(",", over) + ")";
        }

        // AC2: 単独で登録した時の版・class・値が fixture の定義と一致する。一致しなければ未判定（HANDOFF AC2）。
        private static bool Baseline(RoundObservation r, List<string> reasons, string label)
        {
            var before = reasons.Count;
            var baseRound = r.Round == Ac2Base;
            var dir = baseRound ? Base : Overlay;
            var version = baseRound ? OverlayExpected.V1 : OverlayExpected.V2;
            foreach (var c in r.Calls.Where(x => !x.Succeeded)) reasons.Add(label + ": " + c.Call + " failed alone");
            var items = baseRound ? new[] { OverlayExpected.SceneKeep, OverlayExpected.SceneEdit, OverlayExpected.SharedPrefab } : new[] { OverlayExpected.SceneEdit };
            foreach (var item in items)
            {
                var p = r.Probes.FirstOrDefault(x => x.Item == item);
                var v = item == OverlayExpected.SceneEdit ? version : "";
                if (p == null || !p.Found) reasons.Add(label + ": " + item + " probe object was not found");
                else if (item == OverlayExpected.SceneEdit && p.ObjectName != OverlayExpected.MarkerName(version)) reasons.Add(label + ": SceneEdit is " + p.ObjectName);
                else if (!ClassesCorrect(p.Components, OverlayExpected.Probes(item, v), out var why)) reasons.Add(label + ": " + item + " " + why);
            }
            foreach (var item in new[] { OverlayExpected.SceneEdit, OverlayExpected.EditedTex })
            {
                var c = r.Calls.FirstOrDefault(x => x.Call == dir + ":" + item);
                if (c == null || c.Version != version) reasons.Add(label + ": " + item + " version " + (c?.Version ?? "none") + ", expected " + version);
            }
            return reasons.Count == before;
        }
    }
}
